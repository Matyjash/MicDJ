using MicDJ.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MicDJ.Audio;

internal sealed record DeviceChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

internal sealed class AudioEngine : IDisposable
{
    private WasapiCapture? capture;
    private WasapiOut? output;
    private WasapiOut? monitor;
    private readonly List<MMDevice> devices = [];
    private MixProvider? mixer;
    private TapProvider? tap;
    private volatile bool stopping;
    public event Action<string>? Failed;
    public bool MicrophoneEnabled { set { if (mixer != null) mixer.MicrophoneEnabled = value; } }
    public bool MonitorEnabled { set { if (tap != null) tap.Enabled = value; } }

    public static List<DeviceChoice> ListDevices(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        List<DeviceChoice> result = [];
        foreach (var device in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (device) result.Add(new(device.ID, device.FriendlyName));
        }
        return result;
    }

    public static string? DefaultDevice(DataFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        try { using var device = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia); return device.ID; }
        catch (System.Runtime.InteropServices.COMException) { return null; }
    }

    public void Start(string microphone, string? destination, string headphones, bool micEnabled, bool monitorEnabled)
    {
        stopping = false;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice Open(string id) { var device = enumerator.GetDevice(id); devices.Add(device); return device; }
            capture = new WasapiCapture(Open(microphone));
            var microphoneBuffer = new BufferedWaveProvider(capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(300), DiscardOnBufferOverflow = true, ReadFully = true
            };
            capture.DataAvailable += (_, e) => microphoneBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
            capture.RecordingStopped += (_, e) => ReportFailure(e.Exception, "Microphone capture was interrupted.");
            mixer = new MixProvider(Normalize(microphoneBuffer.ToSampleProvider())) { MicrophoneEnabled = micEnabled };
            var monitorBuffer = new BufferedWaveProvider(mixer.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(200), DiscardOnBufferOverflow = true, ReadFully = true
            };
            tap = new TapProvider(mixer, monitorBuffer, destination == null) { Enabled = monitorEnabled };
            output = new WasapiOut(Open(destination ?? headphones), AudioClientShareMode.Shared, true, 60);
            output.Init(tap);
            output.PlaybackStopped += (_, e) => ReportFailure(e.Exception, "Audio output stopped.");
            if (destination != null)
            {
                monitor = new WasapiOut(Open(headphones), AudioClientShareMode.Shared, true, 80);
                monitor.Init(monitorBuffer);
                monitor.PlaybackStopped += (_, e) => ReportFailure(e.Exception, "Audio monitoring stopped.");
                monitor.Play();
            }
            capture.StartRecording();
            output.Play();
        }
        catch (Exception startError)
        {
            try { Dispose(); }
            catch (Exception disposeError) { throw new AggregateException(startError, disposeError); }
            throw;
        }
    }

    private void ReportFailure(Exception? exception, string message)
    {
        if (!stopping) Failed?.Invoke(exception?.Message ?? message);
    }

    public static ISampleProvider Normalize(ISampleProvider source)
    {
        if (source.WaveFormat.Channels == 1) source = new MonoToStereoSampleProvider(source);
        else if (source.WaveFormat.Channels != 2)
            throw new NotSupportedException("Only mono or stereo devices and files are supported.");
        if (source.WaveFormat.SampleRate != 48000) source = new WdlResamplingSampleProvider(source, 48000);
        return source;
    }

    public static float[] LoadSound(string path)
    {
        using var reader = new WaveFileReader(path);
        if (reader.TotalTime > TimeSpan.FromMinutes(2))
            throw new InvalidDataException("Sounds must be no longer than 2 minutes.");
        var source = Normalize(reader.ToSampleProvider());
        var samples = new List<float>();
        var chunk = new float[8192];
        int read;
        while ((read = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (samples.Count + read > 48000 * 2 * 120)
                throw new InvalidDataException("The sound exceeds the 2-minute limit.");
            for (int i = 0; i < read; i++)
            {
                if (!float.IsFinite(chunk[i])) throw new InvalidDataException("The file contains invalid audio samples.");
                samples.Add(chunk[i]);
            }
        }
        if (samples.Count == 0) throw new InvalidDataException("The WAV file is empty.");
        return samples.ToArray();
    }

    public void Play(float[] samples) => mixer?.Play(samples);

    public void Dispose()
    {
        stopping = true;
        // Continue releasing the remaining endpoints even if a removed device fails.
        List<Exception> errors = [];
        void Release(Action action) { try { action(); } catch (Exception ex) { errors.Add(ex); } }
        if (capture != null) { Release(capture.StopRecording); Release(capture.Dispose); capture = null; }
        if (output != null) { Release(output.Stop); Release(output.Dispose); output = null; }
        if (monitor != null) { Release(monitor.Stop); Release(monitor.Dispose); monitor = null; }
        foreach (var device in devices) Release(device.Dispose);
        devices.Clear();
        mixer = null;
        tap = null;
        if (errors.Count > 0) throw new AggregateException("Could not release an audio device.", errors);
    }

    private sealed class TapProvider(ISampleProvider source, BufferedWaveProvider monitor, bool previewOnly) : IWaveProvider
    {
        private float[] samples = [];
        private volatile bool enabled;
        public bool Enabled { set { enabled = value; } }
        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int sampleCount = count / sizeof(float);
            if (samples.Length < sampleCount) samples = new float[sampleCount];
            source.Read(samples, 0, sampleCount);
            Buffer.BlockCopy(samples, 0, buffer, offset, count);
            bool listen = enabled;
            if (previewOnly)
            {
                if (!listen) Array.Clear(buffer, offset, count);
            }
            else if (listen) monitor.AddSamples(buffer, offset, count);
            else monitor.ClearBuffer();
            return count;
        }
    }
}
