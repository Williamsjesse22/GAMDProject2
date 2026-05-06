using UnityEngine;

namespace TicTacToe
{
    /// <summary>
    /// Tiny utility to synthesize <see cref="AudioClip"/>s at runtime so the project
    /// doesn't need shipped audio assets. Sine-wave envelope-shaped tones are good
    /// enough for click / win / loss cues.
    /// </summary>
    public static class SoundSynth
    {
        private const int SampleRate = 44100;

        /// <summary>Create a single sine-tone clip with a small attack/decay envelope.</summary>
        public static AudioClip Beep(string name, float frequency, float duration, float amplitude = 0.35f)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            FillTone(data, 0, samples, frequency, amplitude);

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Create an arpeggio clip — each frequency in <paramref name="frequencies"/>
        /// plays for an equal slice of <paramref name="totalDuration"/>.
        /// </summary>
        public static AudioClip Arp(string name, float[] frequencies, float totalDuration, float amplitude = 0.35f)
        {
            int totalSamples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * totalDuration));
            int perNote = Mathf.Max(1, totalSamples / frequencies.Length);
            var data = new float[totalSamples];

            for (int n = 0; n < frequencies.Length; n++)
            {
                int start = n * perNote;
                int end = Mathf.Min(start + perNote, totalSamples);
                FillTone(data, start, end - start, frequencies[n], amplitude);
            }

            var clip = AudioClip.Create(name, totalSamples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Fill `data[offset..offset+length]` with a sine wave at `frequency`,
        // shaped by a quick attack/decay envelope to avoid clicks.
        private static void FillTone(float[] data, int offset, int length, float frequency, float amplitude)
        {
            float twoPiF = 2f * Mathf.PI * frequency;
            float duration = (float)length / SampleRate;
            float attack = Mathf.Min(0.006f, duration * 0.2f);
            float decayStart = Mathf.Max(0f, duration - 0.04f);

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float env;
                if (t < attack) env = t / attack;
                else if (t > decayStart && duration > decayStart) env = Mathf.Max(0f, (duration - t) / (duration - decayStart));
                else env = 1f;

                data[offset + i] = Mathf.Sin(twoPiF * t) * amplitude * env;
            }
        }
    }
}
