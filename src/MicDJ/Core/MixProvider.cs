using NAudio.Wave;

namespace MicDJ.Core;

// One consumer (the primary output) advances the mix; the monitor receives a copy.
public sealed class MixProvider : ISampleProvider
{
    private readonly ISampleProvider microphone;
    private readonly object sync = new();
    private float[] clip = [];
    private int position;
    private readonly float[] previousInput = new float[2];
    private readonly float[] previousOutput = new float[2];
    private float envelope;
    private float gain;
    private volatile bool microphoneEnabled = true;
    public bool MicrophoneEnabled { get => microphoneEnabled; set => microphoneEnabled = value; }
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    public MixProvider(ISampleProvider microphone) => this.microphone = microphone;

    public void Play(float[] samples)
    {
        lock (sync) { clip = samples; position = 0; }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = microphone.Read(buffer, offset, count);
        Array.Clear(buffer, offset + read, count - read);
        lock (sync)
        {
            for (int i = 0; i < count; i += 2)
            {
                float peak = 0;
                for (int c = 0; c < 2 && i + c < count; c++)
                {
                    int index = offset + i + c;
                    float input = buffer[index];
                    // First-order 80 Hz high-pass removes rumble and DC.
                    float filtered = .9896f * (previousOutput[c] + input - previousInput[c]);
                    previousInput[c] = input;
                    previousOutput[c] = filtered;
                    buffer[index] = filtered;
                    peak = Math.Max(peak, Math.Abs(filtered));
                }
                envelope = Math.Max(peak, envelope * .9995f);
                // Soft noise gate, about -43 dBFS; smooth attack/release.
                float target = envelope > .007f ? 1 : 0;
                gain += (target - gain) * (target > gain ? .015f : .001f);
                for (int c = 0; c < 2 && i + c < count; c++)
                {
                    int index = offset + i + c;
                    float sound = position < clip.Length ? clip[position++] : 0;
                    float mic = microphoneEnabled ? buffer[index] * gain : 0;
                    buffer[index] = Math.Clamp((mic + sound) * .7f, -1, 1);
                }
            }
            if (position >= clip.Length) { clip = []; position = 0; }
        }
        return count;
    }
}
