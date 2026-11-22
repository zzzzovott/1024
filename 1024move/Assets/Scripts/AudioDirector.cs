using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 程序化音效：不依赖任何音频素材，运行时合成"咔哒""电子音""底噪"。
    /// 初稿阶段靠它撑住"有声音"，正式版换成陈婉婷录的素材即可（直接替换 AudioClip 引用）。
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        [Range(0f, 1f)] public float sfxVolume = 0.7f;
        [Range(0f, 1f)] public float ambienceVolume = 0.25f;

        AudioSource _sfx;
        AudioSource _amb;

        AudioClip _click;
        AudioClip _tick;
        AudioClip _hum;
        AudioClip _type;
        AudioClip _tinnitus;
        AudioClip _tape;
        AudioClip _humming;
        AudioClip _dust;
        AudioClip _knock;

        AudioSource _humSource;

        /// <summary>磁带哼唱专用的循环源，频谱可视化要读它。</summary>
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

            _click = BuildClick();
            _tick = BuildTick();
            _hum = BuildHum();
            _type = BuildType();
            _tinnitus = BuildTinnitus();
            _tape = BuildTapeRip();
            _dust = BuildDust();
            _knock = BuildKnock();

            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.playOnAwake = false;
            _humSource.loop = false;
            _humSource.spatialBlend = 0f;
            _humSource.volume = 0.55f;

            _amb.clip = _hum;

            _humming = BuildHumming();
            _humSource.clip = _humming;
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

        public void PlayClick()
        {
            if (_sfx != null && _click != null) _sfx.PlayOneShot(_click, sfxVolume);
        }

        public void PlayTick()
        {
            if (_sfx != null && _tick != null) _sfx.PlayOneShot(_tick, sfxVolume * 0.6f);
        }

        /// <summary>打字机的"哒"。</summary>
        public void PlayType()
        {
            if (_sfx != null && _type != null) _sfx.PlayOneShot(_type, sfxVolume * 0.35f);
        }

        /// <summary>记忆穿梭的高频耳鸣。</summary>
        public void PlayTinnitus()
        {
            if (_sfx != null && _tinnitus != null) _sfx.PlayOneShot(_tinnitus, sfxVolume * 0.55f);
        }

        /// <summary>撕封箱胶带。</summary>
        public void PlayTapeRip()
        {
            if (_sfx != null && _tape != null) _sfx.PlayOneShot(_tape, sfxVolume * 0.8f);
        }

        /// <summary>灰尘被擦干净后"消散"的一口气（模块 2）。</summary>
        public void PlayDust()
        {
            if (_sfx != null && _dust != null) _sfx.PlayOneShot(_dust, sfxVolume * 0.7f);
        }

        /// <summary>拍老电视的"哐"（模块 3）。</summary>
        public void PlayKnock()
        {
            if (_sfx != null && _knock != null) _sfx.PlayOneShot(_knock, sfxVolume * 0.6f);
        }

        public void AmbienceOn(float target = -1f)
        {
            if (_amb == null) return;
            if (!_amb.isPlaying) _amb.Play();
            _amb.volume = (target < 0f ? ambienceVolume : target);
        }

        public void AmbienceOff()
        {
            if (_amb != null) _amb.volume = 0f;
        }

        // ---------------- 合成 ----------------

        const int Rate = 44100;

        static AudioClip BuildClick()
        {
            int n = (int)(Rate * 0.09f);
            float[] data = new float[n];
            System.Random rng = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 55f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;
                float body = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f;
                data[i] = (noise + body) * env * 0.9f;
            }
            return MakeClip("sfx_click", data);
        }

        static AudioClip BuildTick()
        {
            int n = (int)(Rate * 0.05f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 90f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1760f * t) * env * 0.35f;
            }
            return MakeClip("sfx_tick", data);
        }

        /// <summary>打字机的一声"哒"：很短、很干、偏木头。</summary>
        static AudioClip BuildType()
        {
            int n = (int)(Rate * 0.035f);
            float[] data = new float[n];
            System.Random rng = new System.Random(23);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 140f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float body = Mathf.Sin(2f * Mathf.PI * 620f * t);
                data[i] = (noise * 0.45f + body * 0.35f) * env;
            }
            return MakeClip("sfx_type", data);
        }

        /// <summary>记忆穿梭的耳鸣：高频正弦 + 缓慢起伏，0.9 秒。</summary>
        static AudioClip BuildTinnitus()
        {
            int n = (int)(Rate * 0.9f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);   // 先起后落
                float tone = Mathf.Sin(2f * Mathf.PI * 4200f * t) * 0.55f
                           + Mathf.Sin(2f * Mathf.PI * 6100f * t) * 0.25f;
                data[i] = tone * env * 0.5f;
            }
            return MakeClip("sfx_tinnitus", data);
        }

        static AudioClip BuildHum()
        {
            int n = Rate * 3;
            float[] data = new float[n];
            System.Random rng = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = Mathf.Lerp(lp, noise, 0.02f);            // 低通，做出风扇底噪
                float fan = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.06f;
                float hiss = lp * 0.5f;
                data[i] = (fan + hiss) * 0.5f;
            }
            // 首尾淡入淡出，避免循环接缝咔一声
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[i] *= k;
                data[n - 1 - i] *= k;
            }
            return MakeClip("amb_hum", data);
        }

        /// <summary>撕胶带：一串不规则的短促噪声，带轻微音高下滑。</summary>
        static AudioClip BuildTapeRip()
        {
            int n = (int)(Rate * 1.1f);
            float[] data = new float[n];
            System.Random rng = new System.Random(31);
            float env = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                // 前 0.15 秒淡入，尾部 0.3 秒收掉
                float a = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1.1f - t) / 0.3f);
                float grit = (float)(rng.NextDouble() * 2.0 - 1.0);
                // 每 ~8ms 一次"粘住-弹开"的颗粒感
                float stick = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 62f * t));
                env = Mathf.Lerp(env, grit, 0.6f);
                data[i] = env * a * (0.35f + 0.65f * stick) * 0.55f;
            }
            return MakeClip("sfx_tape", data);
        }

        /// <summary>磁带里录的哼唱：一段五声音阶的小调子 + 磁带走带的抖晃 + 底噪。</summary>
        static AudioClip BuildHumming()
        {
            // A 小调五声：A C D E G
            float[] notes = { 440f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 440f, 392f, 440f };
            float noteLen = 0.62f;
            int total = (int)(Rate * (notes.Length * noteLen + 0.6f));
            float[] data = new float[total];
            System.Random rng = new System.Random(41);

            float hiss = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(Rate * n * noteLen);
                int len = (int)(Rate * noteLen);
                float f0 = notes[n];

                for (int i = 0; i < len && start + i < total; i++)
                {
                    float t = (float)i / Rate;
                    float u = (float)i / len;

                    float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1f - u) / 0.45f);

                    // 颤音 + 走带抖晃（wow/flutter），让它一听就是磁带
                    float vib = 1f + 0.007f * Mathf.Sin(2f * Mathf.PI * 5.2f * t);
                    float wow = 1f + 0.0035f * Mathf.Sin(2f * Mathf.PI * 0.7f * t);

                    float phase = 2f * Mathf.PI * f0 * t * vib * wow;
                    float tone = Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 2f) * 0.14f;

                    data[start + i] += tone * env * 0.42f;
                }
            }

            // 铺一层底噪
            for (int i = 0; i < total; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                hiss = Mathf.Lerp(hiss, noise, 0.05f);
                data[i] += hiss * 0.08f;
            }

            // 首尾淡入淡出
            int fade = Rate / 5;
            for (int i = 0; i < fade && i < total; i++)
            {
                float k = (float)i / fade;
                data[i] *= k;
                data[total - 1 - i] *= k;
            }

            return MakeClip("hum_tape", data);
        }

        /// <summary>灰尘消散：一层带通噪声，先"噗"一下再沙沙散开。0.8 秒。</summary>
        static AudioClip BuildDust()
        {
            int n = (int)(Rate * 0.8f);
            float[] data = new float[n];
            System.Random rng = new System.Random(53);
            float lp = 0f;
            float hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                // 前 0.05s 冲起来，之后慢慢散掉
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 3.8f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                lp = Mathf.Lerp(lp, noise, 0.28f);          // 高频沙沙
                hp = Mathf.Lerp(hp, lp, 0.02f);             // 低频"噗"
                float breath = lp - hp;

                data[i] = (breath * 1.6f + hp * 0.5f) * env * 0.55f;
            }
            return MakeClip("sfx_dust", data);
        }

        /// <summary>拍电视：低频撞击 + 一点塑料壳的空腔共振。0.18 秒。</summary>
        static AudioClip BuildKnock()
        {
            int n = (int)(Rate * 0.18f);
            float[] data = new float[n];
            System.Random rng = new System.Random(67);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Exp(-t * 38f);
                float thud = Mathf.Sin(2f * Mathf.PI * 96f * t) * 0.75f
                           + Mathf.Sin(2f * Mathf.PI * 168f * t) * 0.25f;
                float tick = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 120f);
                data[i] = (thud + tick * 0.5f) * env * 0.8f;
            }
            return MakeClip("sfx_knock", data);
        }

        static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}