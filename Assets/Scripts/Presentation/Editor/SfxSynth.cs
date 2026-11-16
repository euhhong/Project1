using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Project1.Presentation.EditorTools
{
    /// <summary>
    /// 효과음을 코드로 합성해 WAV로 만든다(FE) — 메뉴 Tools/FE/효과음 만들기. 외부 파일 없이 사인파·잡음·필터·감쇠만으로 만든다.
    /// 결과: Assets/Audio/Resources/Sfx/{이름}.wav (SfxPlayer가 Resources.Load로 읽는다). 다시 실행하면 덮어쓴다(같은 씨앗 = 같은 소리).
    /// </summary>
    public static class SfxSynth
    {
        private const int Rate = 44100;
        private const string Folder = "Assets/Audio/Resources/Sfx";

        [MenuItem("Tools/FE/효과음 만들기")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Write("swing", Swing());
            Write("hit_light", Hit(0.13f, 150f, 70f, 0.55f, 1));
            Write("hit_heavy", Hit(0.32f, 105f, 42f, 0.9f, 2));
            Write("block", Clang());
            Write("wall", Wall());
            Write("shockwave", Boom());
            Write("jump", Puff(0.09f, 2600f, 0.35f, 3));
            Write("land", Thud(0.12f, 95f, 0.55f, 4));
            Write("stun", Dizzy());
            Write("skill", Rise());
            Write("pallet", Crash());
            Write("mirror_in", Shimmer(0.85f, true));
            Write("mirror_out", Shimmer(0.6f, false));
            AssetDatabase.Refresh();
            Debug.Log($"[FE] 효과음 13개를 {Folder}에 만들었습니다.");
        }

        // ───────────── 소리 ─────────────

        /// <summary>휘두르기: 띠 잡음이 높은 데서 낮은 데로 쓸려 내려간다.</summary>
        private static float[] Swing()
        {
            float len = 0.2f; var s = Buf(len); var r = new System.Random(10);
            var bp = new Biquad();
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, k = t / len;
                bp.SetBand(Mathf.Lerp(2800f, 700f, k), 1.6f);
                s[i] = bp.Run(Noise(r)) * Env(k, 0.25f, 2.2f) * 1.4f;
            }
            return s;
        }

        /// <summary>맞음: 낮은 쿵(사인 음 높이가 빠르게 떨어짐) + 짧은 딱 소리 잡음.</summary>
        private static float[] Hit(float len, float f0, float f1, float noiseAmt, int seed)
        {
            var s = Buf(len); var r = new System.Random(seed); var lp = new Biquad(); lp.SetLow(3200f, 0.8f);
            double ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, k = t / len;
                float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(k));
                ph += 2 * Math.PI * f / Rate;
                float body = (float)Math.Sin(ph) * Mathf.Exp(-k * 4.5f);
                float click = lp.Run(Noise(r)) * Mathf.Exp(-t * 60f) * noiseAmt;
                s[i] = Soft((body * 1.1f + click) * 1.3f);
            }
            return s;
        }

        /// <summary>막기·패링: 쇠붙이가 부딪는 챙 — 어긋난 배음 여러 개가 길게 울리고, 처음에 딱.</summary>
        private static float[] Clang()
        {
            float len = 0.6f; var s = Buf(len); var r = new System.Random(5);
            float[] fr = { 620f, 1490f, 2380f, 3310f, 4570f }, am = { 0.5f, 0.35f, 0.25f, 0.18f, 0.1f }, dc = { 5f, 7f, 9f, 12f, 16f };
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int h = 0; h < fr.Length; h++) v += Mathf.Sin(2f * Mathf.PI * fr[h] * t) * am[h] * Mathf.Exp(-t * dc[h]);
                v += Noise(r) * Mathf.Exp(-t * 90f) * 0.6f;
                s[i] = Soft(v * 1.1f);
            }
            return s;
        }

        /// <summary>벽에 박음: 묵직한 쿵 + 낮게 우르르.</summary>
        private static float[] Wall()
        {
            float len = 0.4f; var s = Buf(len); var r = new System.Random(6); var lp = new Biquad(); lp.SetLow(500f, 0.7f);
            double ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, k = t / len;
                ph += 2 * Math.PI * Mathf.Lerp(80f, 45f, k) / Rate;
                float v = (float)Math.Sin(ph) * Mathf.Exp(-k * 5f) + lp.Run(Noise(r)) * Mathf.Exp(-k * 6f) * 1.2f;
                s[i] = Soft(v * 1.4f);
            }
            return s;
        }

        /// <summary>충격파: 아주 낮은 붐 + 길게 퍼지는 바람 소리.</summary>
        private static float[] Boom()
        {
            float len = 0.9f; var s = Buf(len); var r = new System.Random(7); var lp = new Biquad();
            double ph = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, k = t / len;
                ph += 2 * Math.PI * Mathf.Lerp(60f, 28f, k) / Rate;
                lp.SetLow(Mathf.Lerp(1800f, 250f, k), 0.7f);
                float v = (float)Math.Sin(ph) * Mathf.Exp(-k * 3.5f) * 1.2f + lp.Run(Noise(r)) * Env(k, 0.04f, 3f) * 0.9f;
                s[i] = Soft(v * 1.2f);
            }
            return s;
        }

        /// <summary>짧은 바람 훅(점프).</summary>
        private static float[] Puff(float len, float freq, float amp, int seed)
        {
            var s = Buf(len); var r = new System.Random(seed); var bp = new Biquad(); bp.SetBand(freq, 0.9f);
            for (int i = 0; i < s.Length; i++) { float k = i / (float)s.Length; s[i] = bp.Run(Noise(r)) * Env(k, 0.2f, 3f) * amp * 2f; }
            return s;
        }

        /// <summary>낮고 부드러운 쿵(착지).</summary>
        private static float[] Thud(float len, float f, float amp, int seed)
        {
            var s = Buf(len); var r = new System.Random(seed); var lp = new Biquad(); lp.SetLow(900f, 0.7f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, k = t / len;
                s[i] = (Mathf.Sin(2f * Mathf.PI * f * (1f - 0.3f * k) * t) * 0.8f + lp.Run(Noise(r)) * 0.5f) * Mathf.Exp(-k * 6f) * amp;
            }
            return s;
        }

        /// <summary>기절: 맑은 종소리 셋이 빙글 내려간다(띠링띠링).</summary>
        private static float[] Dizzy()
        {
            float len = 0.75f; var s = Buf(len);
            float[] notes = { 1568f, 1319f, 1175f, 1319f };
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * 0.14f * Rate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float vib = 1f + 0.01f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                    s[i] += (Mathf.Sin(2f * Mathf.PI * notes[n] * vib * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[n] * 2.76f * t)) * Mathf.Exp(-t * 7f) * 0.35f;
                }
            }
            for (int i = 0; i < s.Length; i++) s[i] = Soft(s[i]);
            return s;
        }

        /// <summary>스킬: 띠 잡음이 위로 쓸려 올라가며 차오른다.</summary>
        private static float[] Rise()
        {
            float len = 0.45f; var s = Buf(len); var r = new System.Random(9); var bp = new Biquad();
            for (int i = 0; i < s.Length; i++)
            {
                float k = i / (float)s.Length;
                bp.SetBand(Mathf.Lerp(400f, 3600f, k * k), 2f);
                s[i] = bp.Run(Noise(r)) * Env(k, 0.7f, 6f) * 1.6f;
            }
            return s;
        }

        /// <summary>판자 쓰러짐: 나무가 넘어가 쾅 — 나무 공명(낮은 띠) 딱딱 몇 번 + 쿵.</summary>
        private static float[] Crash()
        {
            float len = 0.7f; var s = Buf(len); var r = new System.Random(11);
            float[] at = { 0f, 0.035f, 0.07f, 0.16f }, gain = { 1f, 0.7f, 0.5f, 0.35f };
            for (int n = 0; n < at.Length; n++)
            {
                var bp1 = new Biquad(); bp1.SetBand(320f + n * 60f, 3f);
                var bp2 = new Biquad(); bp2.SetBand(780f + n * 90f, 4f);
                int start = (int)(at[n] * Rate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (i - start) / (float)Rate, e = Mathf.Exp(-t * (n == 0 ? 9f : 22f));
                    float x = Noise(r) * e;
                    s[i] += (bp1.Run(x) * 2.2f + bp2.Run(x) * 1.4f) * gain[n];
                }
            }
            double ph = 0;
            for (int i = 0; i < s.Length; i++) { float t = i / (float)Rate; ph += 2 * Math.PI * Mathf.Lerp(95f, 55f, t / len) / Rate; s[i] = Soft(s[i] + (float)Math.Sin(ph) * Mathf.Exp(-t * 7f) * 0.7f); }
            return s;
        }

        /// <summary>거울: 반짝이는 유리 울림 — 맑은 사인 여러 개가 위로(들어감)·아래로(나옴) 미끄러지며 떨린다.</summary>
        private static float[] Shimmer(float len, bool up)
        {
            var s = Buf(len); var r = new System.Random(up ? 12 : 13); var hp = new Biquad(); hp.SetHigh(3000f, 0.7f);
            float[] baseF = { 880f, 1320f, 1760f, 2640f };
            var ph = new double[baseF.Length];
            for (int i = 0; i < s.Length; i++)
            {
                float k = i / (float)s.Length, v = 0f;
                float bend = up ? Mathf.Lerp(0.8f, 1.6f, k) : Mathf.Lerp(1.5f, 0.85f, k);
                for (int h = 0; h < baseF.Length; h++)
                {
                    ph[h] += 2 * Math.PI * baseF[h] * bend * (1f + 0.004f * h) / Rate;
                    v += (float)Math.Sin(ph[h]) * (0.3f / (h + 1));
                }
                float trem = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 18f * k * len);
                v = v * trem + hp.Run(Noise(r)) * 0.25f;
                s[i] = Soft(v * Env(k, up ? 0.5f : 0.08f, up ? 5f : 3.5f) * 1.2f);
            }
            return s;
        }

        // ───────────── 도구 ─────────────

        private static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        private static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);
        private static float Soft(float x) => (float)Math.Tanh(x); // 부드럽게 눌러 깨짐 없게

        /// <summary>올라감(attack 비율)까지 선형으로 오르고 그 뒤 지수로 줄어드는 봉우리(k = 0~1).</summary>
        private static float Env(float k, float attack, float decay) =>
            k < attack ? k / Mathf.Max(attack, 1e-4f) : Mathf.Exp(-(k - attack) * decay);

        private sealed class Biquad
        {
            private float b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public void SetLow(float f, float q) => Set(f, q, 0);
            public void SetHigh(float f, float q) => Set(f, q, 1);
            public void SetBand(float f, float q) => Set(f, q, 2);
            private void Set(float f, float q, int type)
            {
                float w = 2f * Mathf.PI * Mathf.Clamp(f, 20f, Rate * 0.45f) / Rate, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q), a0 = 1f + al;
                if (type == 0) { b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; }
                else if (type == 1) { b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; }
                else { b0 = al; b1 = 0; b2 = -al; }
                b0 /= a0; b1 /= a0; b2 /= a0; a1 = -2 * cs / a0; a2 = (1 - al) / a0;
            }
            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>16비트 모노 WAV로 쓴다(최댓값을 0.9로 맞춤).</summary>
        private static void Write(string name, float[] s)
        {
            // 끝이 뚝 끊겨 딸깍 소리가 나지 않게: 처음 2ms 올리고, 끝 40ms(짧으면 1/4)는 부드럽게 0으로
            int fin = Mathf.Min(s.Length / 8, Rate / 500), fout = Mathf.Min(s.Length / 4, Rate / 25);
            for (int i = 0; i < fin; i++) s[i] *= i / (float)fin;
            for (int i = 0; i < fout; i++) { float k = i / (float)fout; s[s.Length - 1 - i] *= k * k; }
            float peak = 1e-4f;
            foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = 0.9f / peak;
            using var fs = new FileStream($"{Folder}/{name}.wav", FileMode.Create);
            using var w = new BinaryWriter(fs);
            int bytes = s.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (var v in s) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * g * 32767f), -32768, 32767));
        }
    }
}
