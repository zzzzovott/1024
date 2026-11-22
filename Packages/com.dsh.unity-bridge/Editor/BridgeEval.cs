using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dsh.UnityBridge
{
    /// <summary>
    /// Compiles C# snippets against the running editor's assemblies with the editor's own Roslyn
    /// into &lt;project&gt;/Temp/DSHUnityBridge so that loading the resulting assembly never triggers
    /// an asset import or a domain reload.
    ///
    /// The compiler layout differs per editor generation: Unity 2021.2+ ships a Roslyn
    /// <c>csc.dll</c> next to a private .NET runtime (DotNetSdkRoslyn + NetCoreRuntime), while
    /// Unity 2019/2020 ships a self-contained Roslyn apphost at Tools/Roslyn/csc.exe whose .NET
    /// runtime sits beside it. The BCL reference assemblies move as well (UnityReferenceAssemblies
    /// on 2021.2+, the bundled Mono 4.7.1-api on 2019/2020), and the accepted C# language version
    /// follows the bundled Roslyn (C# 8 on 2020.3, C# 9 on 2022.3).
    /// </summary>
    internal static class EvalCompiler
    {
        const int CompileTimeoutMs = 120000;
        static readonly TimeSpan Retention = TimeSpan.FromHours(12);

        /// <summary>A compiler invocation: the process to start plus fixed arguments before "@rsp".</summary>
        sealed class CompilerTool
        {
            internal string Executable;
            internal string ArgumentsPrefix;
        }

        internal static string DataPath
        {
            get { return EditorApplication.applicationContentsPath; }
        }

        public static string Compile(string code, string mode, out string diagnostics)
        {
            diagnostics = null;
            if (string.IsNullOrEmpty(code)) throw new BridgeException("missing_param", "Parameter 'code' is required.");

            string projectRoot = BridgeDiscovery.ProjectRoot;
            string root = Path.Combine(projectRoot, "Temp", "DSHUnityBridge");
            string workDir = Path.Combine(root, "eval-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(workDir);
            Cleanup(root, workDir);

            string sourcePath = Path.Combine(workDir, "Program.cs");
            string assemblyPath = Path.Combine(workDir, "eval.dll");
            File.WriteAllText(sourcePath, BuildSource(code, mode), new UTF8Encoding(false));

            string rspPath = Path.Combine(workDir, "csc.rsp");
            File.WriteAllText(rspPath, BuildArguments(projectRoot, sourcePath, assemblyPath), new UTF8Encoding(false));

            int exitCode;
            string output;
            string error;
            CompilerTool compiler = ResolveCompiler(out error);
            if (compiler == null)
            {
                diagnostics = error;
                throw new BridgeException("compiler_missing", error);
            }

            RunCompiler(compiler, rspPath, out exitCode, out output, out error);

            if (exitCode != 0 || !File.Exists(assemblyPath))
            {
                diagnostics = string.IsNullOrEmpty(error) ? output : output + "\n" + error;
                if (string.IsNullOrEmpty(diagnostics)) diagnostics = "C# compilation failed with exit code " + exitCode + ".";
                throw new BridgeException("compile_error", diagnostics.Trim());
            }

            diagnostics = output.Trim();
            return assemblyPath;
        }

        static void Cleanup(string root, string current)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                DateTime cutoff = DateTime.UtcNow - Retention;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    if (string.Equals(dir, current, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        if (Directory.GetCreationTimeUtc(dir) < cutoff) Directory.Delete(dir, true);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        static CompilerTool ResolveCompiler(out string error)
        {
            error = null;
            string data = DataPath;

            // Unity 2021.2+ / 2022.3: Roslyn as csc.dll, run by the editor's private .NET runtime.
            string runtimeRoot = Path.Combine(data, "NetCoreRuntime");
            string dotnet = Path.Combine(runtimeRoot, "dotnet.exe");
            if (!File.Exists(dotnet)) dotnet = Path.Combine(runtimeRoot, "dotnet");
            string sdkCsc = Path.Combine(data, "DotNetSdkRoslyn", "csc.dll");
            if (File.Exists(dotnet) && File.Exists(sdkCsc))
                return new CompilerTool { Executable = dotnet, ArgumentsPrefix = Quote(sdkCsc) + " " };

            // Unity 2019/2020: a self-contained Roslyn apphost that needs no external runtime.
            string toolsRoslyn = Path.Combine(data, "Tools", "Roslyn");
            string apphost = Path.Combine(toolsRoslyn, "csc.exe");
            if (!File.Exists(apphost)) apphost = Path.Combine(toolsRoslyn, "csc");
            if (File.Exists(apphost))
                return new CompilerTool { Executable = apphost, ArgumentsPrefix = string.Empty };

            // Last resort: some 2019/2020 packages ship only csc.dll, to be run by bundled Mono.
            string legacyDll = Path.Combine(toolsRoslyn, "csc.dll");
            string mono = Path.Combine(data, "MonoBleedingEdge", "bin", "mono.exe");
            if (!File.Exists(mono)) mono = Path.Combine(data, "MonoBleedingEdge", "bin", "mono");
            if (File.Exists(legacyDll) && File.Exists(mono))
                return new CompilerTool { Executable = mono, ArgumentsPrefix = Quote(legacyDll) + " " };

            error = "The editor's C# compiler is missing. Expected '" + sdkCsc + "' together with '" + dotnet +
                    "', or '" + apphost + "'.";
            return null;
        }

        static void RunCompiler(CompilerTool compiler, string rspPath, out int exitCode, out string output, out string error)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = compiler.Executable;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.WorkingDirectory = Path.GetDirectoryName(rspPath);
            psi.Arguments = compiler.ArgumentsPrefix + "@" + Quote(rspPath);

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                using (var stdoutDone = new System.Threading.ManualResetEventSlim(false))
                using (var stderrDone = new System.Threading.ManualResetEventSlim(false))
                {
                    process.OutputDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) stdoutDone.Set();
                        else lock (stdout) stdout.AppendLine(e.Data);
                    };
                    process.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e)
                    {
                        if (e.Data == null) stderrDone.Set();
                        else lock (stderr) stderr.AppendLine(e.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(CompileTimeoutMs))
                    {
                        try { process.Kill(); } catch (Exception) { }
                        output = stdout.ToString();
                        error = "C# compilation timed out after " + (CompileTimeoutMs / 1000) + "s.";
                        exitCode = -1;
                        return;
                    }

                    stdoutDone.Wait(5000);
                    stderrDone.Wait(5000);
                    exitCode = process.ExitCode;
                    output = stdout.ToString();
                    error = stderr.ToString();
                }
            }
        }

        static string Quote(string value)
        {
            return "\"" + value + "\"";
        }

        static string BuildArguments(string projectRoot, string sourcePath, string assemblyPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-nologo");
            sb.AppendLine("-target:library");
            sb.AppendLine("-langversion:" + LanguageVersion());
            sb.AppendLine("-nostdlib+");
            sb.AppendLine("-nowarn:1701,1702,0618");
            sb.AppendLine("-optimize-");
            sb.AppendLine("-debug-");
            sb.AppendLine("-define:" + string.Join(";", Defines()));
            sb.AppendLine("-out:" + Quote(assemblyPath));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string reference in References(projectRoot))
            {
                string file = Path.GetFileName(reference);
                if (!seen.Add(file)) continue;
                sb.AppendLine("-r:" + Quote(reference));
            }

            sb.AppendLine(Quote(sourcePath));
            return sb.ToString();
        }

        /// <summary>
        /// The language version the bundled Roslyn accepts. Unity 2020.3 ships Roslyn 3.5, which
        /// rejects "9" with CS1617 ("invalid option '9'"), so asking for C# 9 there breaks every
        /// eval; Unity 2022.3 ships Roslyn 4.x and does support C# 9.
        /// </summary>
        static string LanguageVersion()
        {
            Version version = BridgeDiscovery.UnityVersion;
            if (version.Major >= 2022) return "9";
            if (version.Major >= 2020) return "8.0";
            return "7.3";
        }

        static IEnumerable<string> Defines()
        {
            var defines = new List<string>();
            defines.Add("UNITY_EDITOR");
            defines.Add("CSHARP_7_3_OR_NEWER");

            Version version = BridgeDiscovery.UnityVersion;
            int major = version.Major > 0 ? version.Major : 2020;
            int minor = version.Major > 0 ? version.Minor : 3;
            int patch = version.Build > 0 ? version.Build : 0;

            // Mirror the "*_OR_NEWER" chain Unity defines for the running editor. Hardcoding the
            // 2022 symbols made a 2020 editor compile the wrong branch of a snippet guarded by
            // "#if UNITY_2020_1_OR_NEWER", and made UNITY_2022_3_OR_NEWER guards (which reference
            // API that does not exist in 2020) compile against 2020 assemblies and fail.
            defines.Add("UNITY_5_3_OR_NEWER");
            defines.Add("UNITY_5_4_OR_NEWER");
            defines.Add("UNITY_5_5_OR_NEWER");
            defines.Add("UNITY_5_6_OR_NEWER");
            for (int year = 2017; year <= major; year++)
            {
                int lastMinor = year == major ? minor : 4;
                for (int m = 1; m <= lastMinor; m++)
                    defines.Add("UNITY_" + year + "_" + m + "_OR_NEWER");
            }
            defines.Add("UNITY_" + major + "_" + minor + "_" + patch);

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                defines.Add("UNITY_EDITOR_WIN");
                defines.Add("UNITY_STANDALONE_WIN");
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                defines.Add("UNITY_EDITOR_OSX");
                defines.Add("UNITY_STANDALONE_OSX");
            }
            else
            {
                defines.Add("UNITY_EDITOR_LINUX");
                defines.Add("UNITY_STANDALONE_LINUX");
            }
            return defines;
        }

        static List<string> References(string projectRoot)
        {
            var refs = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string data = DataPath;
            string managed = Path.Combine(data, "Managed");

            // Mirror what Unity itself feeds the compiler for Assembly-CSharp-Editor (see the
            // generated Library/Bee/**/Assembly-CSharp-Editor.rsp): mscorlib from the 4.8 reference
            // assemblies plus the *facade* netstandard.dll that sits next to it in Facades/, which
            // only type-forwards into mscorlib. Using NetStandard/ref/2.1.0/netstandard.dll instead
            // would give the snippet a second corlib and Roslyn reports CS0518 ("predefined type
            // 'System.Void' is not defined or imported"); leaving mscorlib out makes every call into
            // a project assembly fail with CS0012 because those are compiled against 4.8 mscorlib.
            //
            // Unity 2021.2+ keeps those assemblies in Data/UnityReferenceAssemblies/unity-4.8-api.
            // Unity 2019/2020 has no such folder: the same .NET 4.x reference assemblies live in the
            // bundled Mono install at Data/MonoBleedingEdge/lib/mono/4.7.1-api, with the facades in
            // its Facades/ subfolder.
            string refAssemblies = Path.Combine(data, "UnityReferenceAssemblies", "unity-4.8-api");
            if (Directory.Exists(refAssemblies))
            {
                AddAll(refs, seen, refAssemblies);
                AddAll(refs, seen, Path.Combine(refAssemblies, "Facades"));
            }
            else
            {
                string monoApi = Path.Combine(data, "MonoBleedingEdge", "lib", "mono", "4.7.1-api");
                AddAll(refs, seen, monoApi);
                AddAll(refs, seen, Path.Combine(monoApi, "Facades"));
            }

            // The engine API lives in the per-module assemblies; Managed/UnityEngine also carries
            // the small UnityEngine.dll/UnityEditor.dll facades that the modules forward from.
            // Managed/UnityEngine.dll at the top of Managed/ is the full legacy assembly: adding it
            // next to the modules makes the compiler report CS0433 ("Object is defined in
            // UnityEngine.CoreModule and UnityEngine") for every type that appears in both.
            //
            // On 2020 the editor modules live in Managed/UnityEngine alongside the engine modules,
            // so the same two folders serve both generations (a missing folder is skipped).
            AddAll(refs, seen, Path.Combine(managed, "UnityEngine"));
            AddAll(refs, seen, Path.Combine(managed, "UnityEditor"));

            // Project scripts and package assemblies, so snippets can touch user code.
            AddAll(refs, seen, Path.Combine(projectRoot, "Library", "ScriptAssemblies"));

            return refs;
        }

        static void AddAll(List<string> into, HashSet<string> seen, string directory, string exclude = null)
        {
            try
            {
                if (!Directory.Exists(directory)) return;
                foreach (string file in Directory.GetFiles(directory, "*.dll"))
                {
                    if (exclude != null && string.Equals(Path.GetFileName(file), exclude, StringComparison.OrdinalIgnoreCase)) continue;
                    AddIfExists(into, seen, file);
                }
            }
            catch (Exception) { }
        }

        static void AddIfExists(List<string> into, HashSet<string> seen, string file)
        {
            if (!File.Exists(file)) return;
            // Two directories can carry the same assembly name; the compiler rejects that.
            if (!seen.Add(Path.GetFileName(file))) return;
            into.Add(file);
        }

        // -------------------------------------------------------------- source

        static string BuildSource(string code, string mode)
        {
            var hoisted = new List<string>();
            string body = code;

            // Hoist leading "using X;" lines so snippets can be pasted verbatim.
            while (true)
            {
                string trimmed = body.TrimStart();
                if (!trimmed.StartsWith("using ", StringComparison.Ordinal)) break;
                int newline = trimmed.IndexOf('\n');
                string candidate = newline < 0 ? trimmed : trimmed.Substring(0, newline);
                string line = candidate.Trim();
                if (!line.EndsWith(";", StringComparison.Ordinal) || line.Contains("=")) break;
                if (line.Contains("(")) break;
                hoisted.Add(line);
                if (newline < 0) { body = string.Empty; break; }
                body = trimmed.Substring(newline + 1);
            }

            string trimmedBody = body.Trim();
            bool expression = string.Equals(mode, "expression", StringComparison.OrdinalIgnoreCase);
            if (!expression && (string.IsNullOrEmpty(mode) || string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase)))
                expression = LooksLikeExpression(trimmedBody);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> DSH Unity Bridge eval snippet </auto-generated>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Reflection;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEditor.SceneManagement;");
            sb.AppendLine("using UnityEngine.SceneManagement;");
            sb.AppendLine("using Object = UnityEngine.Object;");
            foreach (string directive in hoisted)
                sb.AppendLine(directive);
            sb.AppendLine();
            sb.AppendLine("namespace __DshEval");
            sb.AppendLine("{");
            sb.AppendLine("    public static class Program");
            sb.AppendLine("    {");
            sb.AppendLine("        public static object Run()");
            sb.AppendLine("        {");
            if (expression)
            {
                sb.AppendLine("            return (object)(" + trimmedBody + ");");
            }
            else
            {
                sb.AppendLine(body);
                // A snippet that returns on every path must not get an unreachable return after it.
                bool returns = System.Text.RegularExpressions.Regex.IsMatch(body, @"\breturn\b");
                if (!returns && trimmedBody.Length > 0 && !trimmedBody.EndsWith("}", StringComparison.Ordinal))
                    sb.AppendLine("            return null;");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static bool LooksLikeExpression(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            if (code.IndexOf(';') >= 0) return false;
            if (code.IndexOf('\n') >= 0) return false;
            if (code.Contains("return ")) return false;
            return true;
        }
    }

    /// <summary>Loads a compiled snippet assembly and invokes its entry point on the main thread.</summary>
    internal static class EvalRunner
    {
        public static object Run(string assemblyPath)
        {
            Assembly assembly;
            try
            {
                // Load the bytes rather than the path: every snippet compiles to the same
                // assembly identity (eval.dll), and Assembly.LoadFrom caches by identity, so
                // the second eval in one editor session would silently re-run the first one.
                assembly = Assembly.Load(File.ReadAllBytes(assemblyPath));
            }
            catch (Exception e)
            {
                throw new BridgeException("eval_load_failed", "Could not load compiled snippet: " + e.Message);
            }

            Type type = assembly.GetType("__DshEval.Program", false);
            if (type == null) throw new BridgeException("eval_failed", "Compiled snippet has no __DshEval.Program type.");
            MethodInfo method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new BridgeException("eval_failed", "Compiled snippet has no public static object Run() method.");

            try
            {
                return method.Invoke(null, null);
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null) ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
