using System;
using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 程序化音频总控。
    ///
    /// · 所有音效运行时用代码合成（AudioClip.Create + 波形），不依赖任何素材；
    /// · **真实音频优先**：如果 Resources/Audio/ 下有同名 .wav（策划把文件丢进 Assets/Audio/ 后
    ///   会由编辑器自动镜像过去），就优先播真实音频，没有才用合成音；
    /// · 擦桌子 / 电视雪花是循环音源，由交互脚本开关。
    ///
    /// 资源 id 一览（放真实音频就按这些名字命名）：
    ///   sfx_click / sfx_tick / sfx_type / sfx_pickup / sfx_store
    ///   sfx_box_drop / sfx_tape_rip / sfx_dust / sfx_wipe
    ///   sfx_tv_click / sfx_tv_snow / sfx_metal / sfx_tinnitus
    ///   hum_ambient / hum_tape
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Header("音量")]
        [Range(0f, 1f)] public float sfxVolume = 0.7f;
        [Range(0f, 1f)] public float ambienceVolume = 0.25f;

        [Header("真实音频替换")]
        [Tooltip("勾上时优先播放 Resources/Audio 下的同名音频，找不到再用合成音")]
        public bool preferRealClips = true;
        public string resourceFolder = "Audio/";

        // ---- 资源 id ----
        public const string IdClick = "sfx_click";
        public const string IdTick = "sfx_tick";
        public const string IdType = "sfx_type";
        public const string IdPickup = "sfx_pickup";
        public const string IdStore = "sfx_store";
        public const string IdBoxDrop = "sfx_box_drop";
        public const string IdTapeRip = "sfx_tape_rip";
        public const string IdDust = "sfx_dust";
        public const string IdWipe = "sfx_wipe";
        public const string IdTvClick = "sfx_tv_click";
        public const string IdTvSnow = "sfx_tv_snow";
        public const string IdMetal = "sfx_metal";
        public const string IdTinnitus = "sfx_tinnitus";
        public const string IdHumAmbient = "hum_ambient";
        public const string IdHumTape = "hum_tape";
        public const string IdBgm = "bgm_room";

        AudioSource _sfx;
        AudioSource _amb;
        AudioSource _humSource;
        AudioSource _wipeSource;
        AudioSource _snowSource;
        AudioSource _musicSource;

        [Header("背景音乐")]
        [Range(0f, 1f)] public float musicVolume = 0.22f;
        [Tooltip("暂停/聚焦时压低到多少（0~1 倍率）")]
        [Range(0f, 1f)] public float musicDuck = 0.35f;
        float _musicTarget;

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);

        /// <summary>磁带哼唱专用循环源（频谱可视化要读它）。</summary>
        public AudioSource HumSource { get { return _humSource; } }

        void Awake()
        {
            Instance = this;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.spatialBlend = 0f;

            _amb = gameObject.AddComponent<AudioSource>();
            _amb.playOnAwake = false;
            _amb.loop = true;
            _amb.spatialBlend = 0f;
            _amb.volume = 0f;

            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.playOnAwake = false;
            _humSource.loop = false;
            _humSource.spatialBlend = 0f;
            _humSource.volume = 0.55f;

            _wipeSource = CreateLoopSource();
            _snowSource = CreateLoopSource();
            _musicSource = CreateLoopSource();

            LoadAll();

            // 怀旧千禧年底噪 BGM，进游戏就有，菜单里也一直在
            if (_musicSource != null && _musicSource.clip != null)
            {
                _musicSource.volume = 0f;
                _musicSource.Play();
                _musicTarget = musicVolume;
            }
        }

        void Update()
        {
            if (_musicSource == null) return;
            _musicSource.volume = Mathf.MoveTowards(_musicSource.volume, _musicTarget, Time.deltaTime * 0.2f);
        }

        /// <summary>压低 / 恢复 BGM（暂停、记忆闪回时用）。</summary>
        public void SetMusicDucked(bool ducked)
        {
            _musicTarget = musicVolume * (ducked ? musicDuck : 1f);
        }

        public void StopMusic()
        {
            _musicTarget = 0f;
            if (_musicSource != null) _musicSource.Stop();
        }

        AudioSource CreateLoopSource()
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        /// <summary>注册所有音效：真实音频优先，没有就现合成。</summary>
        void LoadAll()
        {
            Register(IdClick, BuildClick);
            Register(IdTick, BuildTick);
            Register(IdType, BuildType);
            Register(IdPickup, BuildPickup);
            Register(IdStore, BuildStore);
            Register(IdBoxDrop, BuildBoxDrop);
            Register(IdTapeRip, BuildTapeRip);
            Register(IdDust, BuildDust);
            Register(IdWipe, BuildWipe);
            Register(IdTvClick, BuildTvClick);
            Register(IdTvSnow, BuildTvSnow);
            Register(IdMetal, BuildMetal);
            Register(IdTinnitus, BuildTinnitus);
            Register(IdHumAmbient, BuildHum);
            Register(IdHumTape, BuildHumming);
            Register(IdBgm, BuildBgmRoom);

            _amb.clip = Get(IdHumAmbient);
            _humSource.clip = Get(IdHumTape);
            if (_musicSource != null) _musicSource.clip = Get(IdBgm);
        }

        void Register(string id, Func<AudioClip> fallback)
        {
            AudioClip real = preferRealClips ? Resources.Load<AudioClip>(resourceFolder + id) : null;
            _clips[id] = real != null ? real : fallback();
        }

        AudioClip Get(string id)
        {
            AudioClip clip;
            return _clips.TryGetValue(id, out clip) ? clip : null;
        }

        void PlaySfx(string id, float volumeScale = 1f)
        {
            AudioClip clip = Get(id);
            if (_sfx == null || clip == null) return;
            _sfx.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        // ---------------------------------------------------------------- 对外接口

        public void PlayClick() { PlaySfx(IdClick); }

        /// <summary>拾取东西（杂物 / 抹布 / 旧物）。</summary>
        public void PlayPickup() { PlaySfx(IdPickup); }

        /// <summary>把东西丢进纸箱。</summary>
        public void PlayStore() { PlaySfx(IdStore); }

        /// <summary>搬家纸箱落地的沉闷碰撞。</summary>
        public void PlayBoxDrop() { PlaySfx(IdBoxDrop, 1.1f); }

        public void PlayTick() { PlaySfx(IdTick, 0.6f); }

        /// <summary>打字机的"哒"。</summary>
        public void PlayType() { PlaySfx(IdType, 0.35f); }

        /// <summary>记忆穿梭的高频耳鸣。</summary>
        public void PlayTinnitus() { PlaySfx(IdTinnitus, 0.55f); }

        /// <summary>撕封箱胶带。</summary>
        public void PlayTapeRip() { PlaySfx(IdTapeRip, 0.8f); }

        /// <summary>灰尘被擦干净后"消散"的一口气。</summary>
        public void PlayDust() { PlaySfx(IdDust, 0.7f); }

        /// <summary>老电视拍打（哐哐）。</summary>
        public void PlayTvClick() { PlaySfx(IdTvClick, 0.7f); }

        /// <summary>兼容旧调用名。</summary>
        public void PlayKnock() { PlayTvClick(); }

        /// <summary>铁盒金属碰撞。</summary>
        public void PlayMetal() { PlaySfx(IdMetal, 0.85f); }

        /// <summary>擦桌子的沙沙声（循环，由 DustErase 每帧开关）。</summary>
        public void SetWipe(bool active)
        {
            FadeLoop(_wipeSource, IdWipe, active, 0.45f);
        }

        /// <summary>电视雪花底噪（循环，灯亮时开）。</summary>
        public void SetTvSnow(bool active)
        {
            FadeLoop(_snowSource, IdTvSnow, active, 0.35f);
        }

        void FadeLoop(AudioSource source, string id, bool active, float targetScale)
        {
            if (source == null) return;

            if (active)
            {
                if (source.clip == null)
                {
                    source.clip = Get(id);
                    if (source.clip == null) return;
                }
                if (!source.isPlaying) source.Play();
                source.volume = Mathf.MoveTowards(source.volume, sfxVolume * targetScale, Time.deltaTime * 2.5f);
            }
            else
            {
                source.volume = Mathf.MoveTowards(source.volume, 0f, Time.deltaTime * 4f);
                if (source.isPlaying && source.volume <= 0.01f) source.Stop();
            }
        }

        public void PlayHumming()
        {
            if (_humSource == null || _humSource.clip == null) return;
            _humSource.Stop();
            _humSource.Play();
        }

        public void StopHumming()
        {
            if (_humSource != null) _humSource.Stop();
        }

        public void AmbienceOn(float target = -1f)
        {
            if (_amb == null) return;
            if (!_amb.isPlaying) _amb.Play();
            _amb.volume = target < 0f ? ambienceVolume : target;
        }

        public void AmbienceOff()
        {
            if (_amb != null) _amb.volume = 0f;
        }

        // ---------------------------------------------------------------- 合成

        const int Rate = 44100;

        static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>短促的"嗒"（点击 / 拾取骨架）。</summary>
        static AudioClip BuildClick()
        {
            int n = (int)(Rate * 0.09f);
            float[] d = new float[n];
            System.Random rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 55f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;
                float body = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f;
                d[i] = (noise + body) * env * 0.9f;
            }
            return MakeClip("sfx_click", d);
        }

        static AudioClip BuildTick()
        {
            int n = (int)(Rate * 0.05f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 90f);
                d[i] = Mathf.Sin(2f * Mathf.PI * 1760f * t) * env * 0.35f;
            }
            return MakeClip("sfx_tick", d);
        }

        static AudioClip BuildType()
        {
            int n = (int)(Rate * 0.035f);
            float[] d = new float[n];
            System.Random rng = new System.Random(23);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 140f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float body = Mathf.Sin(2f * Mathf.PI * 620f * t);
                d[i] = (noise * 0.45f + body * 0.35f) * env;
            }
            return MakeClip("sfx_type", d);
        }

        /// <summary>拾取：塑料/布料被拿起来的"啵"。</summary>
        static AudioClip BuildPickup()
        {
            int n = (int)(Rate * 0.12f);
            float[] d = new float[n];
            System.Random rng = new System.Random(41);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 34f);
                float sweep = Mathf.Sin(2f * Mathf.PI * (420f - 160f * t) * t) * 0.45f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f;
                d[i] = (sweep + noise) * env;
            }
            return MakeClip("sfx_pickup", d);
        }

        /// <summary>丢进纸箱：软的"噗"。</summary>
        static AudioClip BuildStore()
        {
            int n = (int)(Rate * 0.2f);
            float[] d = new float[n];
            System.Random rng = new System.Random(43);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 22f);
                float body = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.6f
                           + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.2f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f * Mathf.Exp(-t * 60f);
                d[i] = (body + noise) * env * 0.8f;
            }
            return MakeClip("sfx_store", d);
        }

        /// <summary>搬家纸箱落地：低沉的一声闷响 + 纸板摩擦。</summary>
        static AudioClip BuildBoxDrop()
        {
            int n = (int)(Rate * 0.42f);
            float[] d = new float[n];
            System.Random rng = new System.Random(47);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 9f);
                float thud = Mathf.Sin(2f * Mathf.PI * (70f - 20f * t) * t) * 0.85f;
                float body = Mathf.Sin(2f * Mathf.PI * 130f * t) * 0.25f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f * Mathf.Exp(-t * 18f);
                d[i] = (thud + body + noise) * env;
            }
            return MakeClip("sfx_box_drop", d);
        }

        static AudioClip BuildTapeRip()
        {
            int n = (int)(Rate * 0.55f);
            float[] d = new float[n];
            System.Random rng = new System.Random(31);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 3.2f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.55f);
                float crackle = (rng.NextDouble() > 0.94) ? noise * 1.6f : 0f;
                d[i] = (lp * 0.7f + crackle) * env * 0.8f;
            }
            return MakeClip("sfx_tape_rip", d);
        }

        static AudioClip BuildDust()
        {
            int n = (int)(Rate * 0.8f);
            float[] d = new float[n];
            System.Random rng = new System.Random(53);
            float lp = 0f;
            float hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 3.8f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.28f);
                hp = Mathf.Lerp(hp, lp, 0.02f);
                d[i] = ((lp - hp) * 1.6f + hp * 0.5f) * env * 0.55f;
            }
            return MakeClip("sfx_dust", d);
        }

        /// <summary>擦桌子的沙沙声：带通噪声 + 慢速起伏，做成可循环。</summary>
        static AudioClip BuildWipe()
        {
            int n = (int)(Rate * 0.7f);
            float[] d = new float[n];
            System.Random rng = new System.Random(59);
            float lp = 0f;
            float hp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                lp = Mathf.Lerp(lp, noise, 0.35f);    // 高频沙沙
                hp = Mathf.Lerp(hp, lp, 0.05f);       // 低频摩擦

                float scrub = (lp - hp);
                float wobble = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 3.2f * t);
                d[i] = scrub * wobble * 0.9f;
            }

            // 首尾各 30ms 交叉淡入淡出，循环时不会"咯噔"
            CrossfadeEdges(d, (int)(Rate * 0.03f));
            return MakeClip("sfx_wipe", d);
        }

        /// <summary>老电视拍打：塑料壳"哐" + 箱体共鸣。</summary>
        static AudioClip BuildTvClick()
        {
            int n = (int)(Rate * 0.24f);
            float[] d = new float[n];
            System.Random rng = new System.Random(79);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 26f);
                float body = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.75f
                           + Mathf.Sin(2f * Mathf.PI * 320f * t) * 0.35f;
                float shell = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 70f) * 0.35f;
                d[i] = (body + shell) * env;
            }
            return MakeClip("sfx_tv_click", d);
        }

        /// <summary>电视雪花底噪：细密的高频嘶声，可循环。</summary>
        static AudioClip BuildTvSnow()
        {
            int n = (int)(Rate * 1.0f);
            float[] d = new float[n];
            System.Random rng = new System.Random(61);
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.7f);
                float crackle = (rng.NextDouble() > 0.995) ? noise * 2.2f : 0f;
                float flicker = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                d[i] = (lp * 0.5f + crackle) * flicker * 0.55f;
            }

            CrossfadeEdges(d, (int)(Rate * 0.02f));
            return MakeClip("sfx_tv_snow", d);
        }

        /// <summary>铁盒金属碰撞：几个非谐波分音 + 高频瞬态。</summary>
        static AudioClip BuildMetal()
        {
            int n = (int)(Rate * 0.55f);
            float[] d = new float[n];
            System.Random rng = new System.Random(67);
            float[] partials = { 523f, 787f, 1175f, 1603f, 2210f };

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 9f);
                float tone = 0f;
                for (int p = 0; p < partials.Length; p++)
                    tone += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * (0.35f / (p + 1));

                float hit = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 120f) * 0.5f;
                d[i] = (tone + hit) * env;
            }
            return MakeClip("sfx_metal", d);
        }

        static AudioClip BuildTinnitus()
        {
            int n = (int)(Rate * 0.9f);
            float[] d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * 4200f * t) * 0.55f
                           + Mathf.Sin(2f * Mathf.PI * 6100f * t) * 0.25f;
                d[i] = tone * env * 0.5f;
            }
            return MakeClip("sfx_tinnitus", d);
        }

        /// <summary>房间底噪：低频电流 + 风扇。</summary>
        static AudioClip BuildHum()
        {
            int n = (int)(Rate * 2f);
            float[] d = new float[n];
            System.Random rng = new System.Random(71);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.02f);
                float fan = Mathf.Sin(2f * Mathf.PI * 47f * t) * 0.12f;
                d[i] = lp * 0.6f + fan;
            }
            CrossfadeEdges(d, Rate / 20);
            return MakeClip("hum_ambient", d);
        }

        /// <summary>磁带里那段女孩哼唱。</summary>
        static AudioClip BuildHumming()
        {
            int n = (int)(Rate * 7f);
            float[] d = new float[n];
            float[] notes = { 392f, 440f, 523f, 440f, 392f, 349f, 392f };
            int noteLen = n / notes.Length;
            System.Random rng = new System.Random(73);
            float hiss = 0f;

            for (int i = 0; i < n; i++)
            {
                int idx = Mathf.Min(notes.Length - 1, i / Mathf.Max(1, noteLen));
                float t = (float)i / Rate;
                float local = (float)(i % Mathf.Max(1, noteLen)) / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(local / 0.55f) * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * notes[idx] * t)
                           + 0.35f * Mathf.Sin(2f * Mathf.PI * notes[idx] * 2f * t);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hiss = Mathf.Lerp(hiss, noise, 0.05f);
                d[i] = tone * env * 0.22f + hiss * 0.06f;
            }

            CrossfadeEdges(d, Rate / 5);
            return MakeClip("hum_tape", d);
        }

        static float MidiToFreq(float midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }

        /// <summary>
        /// 怀旧千禧年 BGM：Am–F–C–G 慢速进行 + 温暖铺底和弦 + 稀疏铃音旋律 + 磁带底噪。
        /// 32 拍 / 72BPM 循环，首尾交叉淡化，能无缝循环。
        /// </summary>
        static AudioClip BuildBgmRoom()
        {
            const int musicRate = 22050;
            const float bpm = 72f;
            const int beats = 32;
            float beat = 60f / bpm;
            int n = Mathf.RoundToInt(musicRate * beat * beats);
            float[] d = new float[n];

            // Am - F - C - G，每段 8 拍
            int[][] chords =
            {
                new[] { 57, 60, 64, 69 },
                new[] { 53, 57, 60, 65 },
                new[] { 48, 52, 55, 60 },
                new[] { 55, 59, 62, 67 },
            };
            int[] roots = { 45, 41, 36, 43 };

            // 铺底和弦（轻微 detune + 缓慢 wow，像旧磁带）
            for (int c = 0; c < 4; c++)
            {
                float start = c * 8f * beat;
                float dur = 8f * beat;
                int s0 = Mathf.RoundToInt(start * musicRate);
                int len = Mathf.RoundToInt(dur * musicRate);

                for (int k = 0; k < chords[c].Length; k++)
                {
                    float freq = MidiToFreq(chords[c][k]);
                    float detune = (k % 2 == 0) ? 1.0025f : 0.9975f;

                    for (int i = 0; i < len && s0 + i < n; i++)
                    {
                        float lt = (float)i / musicRate;
                        float env = Mathf.Clamp01(lt / 0.9f) * Mathf.Clamp01((dur - lt) / 1.4f);
                        float wow = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 0.27f * (start + lt));
                        d[s0 + i] += Mathf.Sin(2f * Mathf.PI * freq * detune * wow * lt) * 0.095f * env;
                    }
                }

                // 低音
                float rootFreq = MidiToFreq(roots[c]);
                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float lt = (float)i / musicRate;
                    float env = Mathf.Clamp01(lt / 0.6f) * Mathf.Clamp01((dur - lt) / 1.0f);
                    d[s0 + i] += Mathf.Sin(2f * Mathf.PI * rootFreq * lt) * 0.16f * env;
                }
            }

            // 铃音旋律（稀疏、带泛音，像旧电子琴）
            float[][] bells =
            {
                new[] { 0f, 76f }, new[] { 2f, 72f }, new[] { 3.5f, 69f },
                new[] { 8f, 77f }, new[] { 10f, 72f }, new[] { 11.5f, 69f },
                new[] { 16f, 79f }, new[] { 18f, 76f }, new[] { 19.5f, 72f },
                new[] { 24f, 74f }, new[] { 26f, 71f }, new[] { 27.5f, 67f },
            };

            for (int e = 0; e < bells.Length; e++)
            {
                float start = bells[e][0] * beat;
                float freq = MidiToFreq(bells[e][1]);
                int s0 = Mathf.RoundToInt(start * musicRate);
                int len = Mathf.RoundToInt(2.2f * musicRate);

                for (int i = 0; i < len && s0 + i < n; i++)
                {
                    float lt = (float)i / musicRate;
                    float env = Mathf.Exp(-lt * 2.6f) * Mathf.Clamp01(lt / 0.01f);
                    float tone = Mathf.Sin(2f * Mathf.PI * freq * lt)
                               + 0.35f * Mathf.Sin(2f * Mathf.PI * freq * 2f * lt)
                               + 0.15f * Mathf.Sin(2f * Mathf.PI * freq * 3f * lt);
                    d[s0 + i] += tone * 0.075f * env;
                }
            }

            // 磁带底噪 + 偶发爆豆
            System.Random rng = new System.Random(97);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                double r = rng.NextDouble();
                float noise = (float)(r * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.22f);
                float crackle = r > 0.99975 ? noise * 2.5f : 0f;
                d[i] += lp * 0.016f + crackle * 0.25f;
            }

            // 归一化，避免削波
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
            float gain = 0.72f / peak;
            for (int i = 0; i < n; i++) d[i] *= gain;

            CrossfadeEdges(d, musicRate / 8);
            return MakeClipAt("bgm_room", d, musicRate);
        }

        static AudioClip MakeClipAt(string name, float[] data, int rate)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>循环音首尾交叉淡化，避免循环点出现"咔"。</summary>
        static void CrossfadeEdges(float[] data, int fade)
        {
            if (fade <= 0 || data.Length < fade * 2) return;

            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }
    }
}
