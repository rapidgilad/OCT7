using System;
using Godot;

namespace OCT7.Game.Audio
{
    /// <summary>
    /// Procedural sound effects, synthesized once at startup (no audio files needed). Each sound is a mix of
    /// filtered noise bursts, pitch-dropping low sines and a short slap-back echo, then normalized and soft-clipped.
    /// Variants use different noise seeds and slightly different parameters so repeated shots don't sound identical.
    /// Real recordings placed in res://assets/audio/ replace these (see <see cref="SoundBank"/>).
    /// </summary>
    public static class SoundSynth
    {
        public const int MixRate = 44100;
        private const float Tau = Mathf.Tau;

        public static AudioStreamWav Create(SoundId id, int variant)
        {
            switch (id)
            {
                case SoundId.Rifle: return Gunshot(variant, 0.55f, crack: 0.9f, crackTau: 0.004f, bodyTau: 0.035f, thumpFrom: 165f, thumpTo: 75f, tailTau: 0.15f, echo: 0.26f);
                case SoundId.MachineGun: return Gunshot(variant + 10, 0.45f, crack: 0.8f, crackTau: 0.004f, bodyTau: 0.045f, thumpFrom: 130f, thumpTo: 60f, tailTau: 0.11f, echo: 0.2f);
                case SoundId.Sniper: return Gunshot(variant + 20, 1.2f, crack: 1.1f, crackTau: 0.006f, bodyTau: 0.05f, thumpFrom: 140f, thumpTo: 55f, tailTau: 0.35f, echo: 0.38f);
                case SoundId.Cannon: return Cannon(variant);
                case SoundId.RocketLaunch: return RocketLaunch(variant);
                case SoundId.Explosion: return Explosion(variant);
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        // ------------------------------------------------------------------ sounds

        private static AudioStreamWav Gunshot(int seed, float seconds, float crack, float crackTau, float bodyTau, float thumpFrom, float thumpTo, float tailTau, float echo)
        {
            var rng = new Noise(seed);
            float v = 1f + (rng.Next() * 0.08f); // per-variant spread
            var buffer = new float[(int)(seconds * MixRate)];
            var crackHp = new OnePole(1800f);
            var body = new OnePole(2400f * v);
            var tail = new OnePole(850f * v);
            double phase = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / MixRate;
                float n = rng.Next();
                float crackPart = crackHp.HighPass(n) * crack * Exp(t, crackTau * v);
                float bodyPart = body.LowPass(n) * 0.85f * Exp(t, bodyTau * v);
                float f = thumpTo + (thumpFrom - thumpTo) * Exp(t, 0.02f);
                phase += Tau * f / MixRate;
                float thump = (float)Math.Sin(phase) * 0.75f * Exp(t, 0.03f * v);
                float tailPart = tail.LowPass(n) * 0.22f * Exp(t, tailTau * v) * Rise(t, 0.004f);
                buffer[i] = (crackPart + bodyPart + thump + tailPart) * Rise(t, 0.0005f);
            }

            AddEcho(buffer, 0.085f + 0.05f * Math.Abs(rng.Next()), echo, 1400f);
            return ToStream(buffer, drive: 2.2f);
        }

        private static AudioStreamWav Cannon(int variant)
        {
            var rng = new Noise(100 + variant);
            var buffer = new float[(int)(1.9f * MixRate)];
            var crackHp = new OnePole(1200f);
            var body = new OnePole(700f);
            var rumble = new OnePole(220f);
            var rumble2 = new OnePole(220f);
            double phase = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / MixRate;
                float n = rng.Next();
                float f = 34f + 46f * Exp(t, 0.06f);
                phase += Tau * f / MixRate;
                float boom = (float)Math.Sin(phase) * 1.1f * Exp(t, 0.17f);
                float crackPart = crackHp.HighPass(n) * 0.9f * Exp(t, 0.007f);
                float bodyPart = body.LowPass(n) * 0.9f * Exp(t, 0.16f);
                float rumblePart = rumble2.Smooth(rumble.LowPass(n)) * 0.45f * Exp(t, 0.55f) * Rise(t, 0.02f);
                buffer[i] = (boom + crackPart + bodyPart + rumblePart) * Rise(t, 0.0008f);
            }

            AddEcho(buffer, 0.24f + 0.04f * variant, 0.3f, 600f);
            return ToStream(buffer, drive: 1.8f);
        }

        private static AudioStreamWav RocketLaunch(int variant)
        {
            var rng = new Noise(200 + variant);
            var buffer = new float[(int)(1.0f * MixRate)];
            var pop = new OnePole(3200f);
            var hissHigh = new OnePole(3400f);
            var hissLow = new OnePole(500f);
            double phase = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / MixRate;
                float n = rng.Next();
                float popPart = pop.LowPass(n) * 1.0f * Exp(t, 0.012f);
                float band = hissHigh.LowPass(n) - hissLow.LowPass(n) * 0.9f; // band-pass: 500 Hz – 3.4 kHz
                float whoosh = band * 0.85f * Rise(t, 0.03f) * Exp(t, 0.28f);
                float f = 95f - 40f * Math.Min(1f, t * 3f);
                phase += Tau * f / MixRate;
                float thump = (float)Math.Sin(phase) * 0.6f * Exp(t, 0.04f);
                buffer[i] = (popPart + whoosh + thump) * Rise(t, 0.0006f);
            }

            AddEcho(buffer, 0.12f, 0.2f, 1200f);
            return ToStream(buffer, drive: 2f);
        }

        private static AudioStreamWav Explosion(int variant)
        {
            var rng = new Noise(300 + variant);
            float v = 1f + rng.Next() * 0.1f;
            var buffer = new float[(int)(2.8f * MixRate)];
            var sweep = new OnePole(3000f);
            var rumble = new OnePole(130f);
            var rumble2 = new OnePole(130f);
            var debrisHp = new OnePole(2500f);
            double phase = 0;
            float debris = 0f;
            float debrisDecay = (float)Math.Exp(-1.0 / (MixRate * 0.003));
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = (float)i / MixRate;
                float n = rng.Next();
                float transient = n * 1.4f * Exp(t, 0.012f);
                sweep.SetCutoff(140f + 2900f * Exp(t, 0.11f * v));
                float body = sweep.LowPass(n) * 1.2f * Exp(t, 0.2f * v) * Rise(t, 0.002f);
                float f = 26f + 30f * Exp(t, 0.12f);
                phase += Tau * f / MixRate;
                float sub = (float)Math.Sin(phase) * 1.0f * Exp(t, 0.28f * v);
                float rumblePart = rumble2.Smooth(rumble.LowPass(n)) * 0.55f * Exp(t, 0.6f * v) * Rise(t, 0.05f);

                // Falling debris: sparse short clicks in the tail.
                if (t > 0.12f && Math.Abs(rng.Next()) < 0.0035f * Exp(t, 0.8f))
                {
                    debris = 0.25f + 0.3f * Math.Abs(rng.Next());
                }

                debris *= debrisDecay;
                float debrisPart = debrisHp.HighPass(n) * debris;
                buffer[i] = (transient + body + sub + rumblePart + debrisPart) * Rise(t, 0.0005f);
            }

            AddEcho(buffer, 0.3f + 0.05f * variant, 0.25f, 500f);
            return ToStream(buffer, drive: 1.7f);
        }

        // ------------------------------------------------------------------ helpers

        private static float Exp(float t, float tau) => (float)Math.Exp(-t / tau);

        /// <summary>Linear fade-in over <paramref name="seconds"/> (avoids clicks at sound start).</summary>
        private static float Rise(float t, float seconds) => t >= seconds ? 1f : t / seconds;

        /// <summary>Single low-passed slap-back echo (sound bouncing off buildings).</summary>
        private static void AddEcho(float[] buffer, float delaySeconds, float gain, float cutoff)
        {
            int delay = (int)(delaySeconds * MixRate);
            var lp = new OnePole(cutoff);
            for (int i = buffer.Length - 1; i >= delay; i--)
            {
                buffer[i] += buffer[i - delay] * gain;
            }

            // Soften the whole tail a little so the echo sits behind the dry sound.
            for (int i = delay; i < buffer.Length; i++)
            {
                float wet = lp.LowPass(buffer[i]);
                buffer[i] = buffer[i] * 0.7f + wet * 0.3f;
            }
        }

        /// <summary>Remove DC, normalize, soft-clip (tanh) for punch, fade the last 20 ms, convert to 16-bit mono PCM.</summary>
        private static AudioStreamWav ToStream(float[] buffer, float drive)
        {
            // DC blocker (one-pole high-pass at ~15 Hz): sub-bass sines that stop mid-cycle leave an offset.
            var dc = new OnePole(15f);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = dc.HighPass(buffer[i]);
            }

            float peak = 1e-6f;
            for (int i = 0; i < buffer.Length; i++)
            {
                peak = Math.Max(peak, Math.Abs(buffer[i]));
            }

            float norm = (float)Math.Tanh(drive);
            int fade = MixRate / 50;
            var bytes = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                float x = (float)Math.Tanh(drive * buffer[i] / peak) / norm * 0.9f;
                int fromEnd = buffer.Length - 1 - i;
                if (fromEnd < fade)
                {
                    x *= (float)fromEnd / fade;
                }

                short s = (short)Math.Round(Math.Clamp(x, -1f, 1f) * 32767f);
                bytes[i * 2] = (byte)(s & 0xff);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xff);
            }

            return new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = MixRate,
                Stereo = false,
                Data = bytes,
            };
        }

        /// <summary>Seeded xorshift white noise in [-1, 1] (presentation only; never used by the sim).</summary>
        private struct Noise
        {
            private uint _state;

            public Noise(int seed)
            {
                _state = (uint)(seed * 747796405 + 2891336453);
                if (_state == 0)
                {
                    _state = 1;
                }
            }

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state / (float)uint.MaxValue) * 2f - 1f;
            }
        }

        /// <summary>
        /// One-pole low-pass with loudness compensation: white noise keeps roughly the same RMS after filtering,
        /// so mix levels stay intuitive whatever the cutoff. High-pass = input minus low-pass.
        /// </summary>
        private struct OnePole
        {
            private float _a;
            private float _gain;
            private float _y;

            public OnePole(float cutoff)
            {
                _a = 0f;
                _gain = 1f;
                _y = 0f;
                SetCutoff(cutoff);
            }

            public void SetCutoff(float cutoff)
            {
                _a = 1f - (float)Math.Exp(-Tau * cutoff / MixRate);
                _gain = 1f / (float)Math.Sqrt(_a / (2f - _a));
            }

            public float LowPass(float x)
            {
                _y += _a * (x - _y);
                return _y * _gain;
            }

            /// <summary>Plain low-pass without loudness compensation, for a second stage on already-filtered input.</summary>
            public float Smooth(float x)
            {
                _y += _a * (x - _y);
                return _y;
            }

            public float HighPass(float x)
            {
                _y += _a * (x - _y);
                return x - _y;
            }
        }
    }
}
