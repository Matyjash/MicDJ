using MicDJ.Core;
using NAudio.Wave;

int passed = 0;
void Check(string name, Action test)
{
    test();
    Console.WriteLine($"PASS: {name}");
    passed++;
}
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
string directory = Path.Combine(Path.GetTempPath(), "MicDJ-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    Check("Device choices survive a new session including monitor-only output", () =>
    {
        string path = Path.Combine(directory, "devices.json");
        Assert(DeviceSelections.Load(path).Microphone == null, "Missing settings must use defaults");
        new DeviceSelections { Microphone = "mic-id", Destination = "", VirtualInput = "cable-id", Headphones = "phones-id" }.Save(path);
        var loaded = DeviceSelections.Load(path);
        Assert(loaded.Microphone == "mic-id" && loaded.Destination == "" && loaded.VirtualInput == "cable-id" && loaded.Headphones == "phones-id", "Device selections lost");
        loaded.Destination = "output-id";
        loaded.Save(path);
        Assert(DeviceSelections.Load(path).Destination == "output-id", "New choice not persisted");
    });
    string journal = Path.Combine(directory, "session.json");
    var devices = new FakeInputs();
    var session = new DefaultInputSession(devices, journal);
    Check("Start/Stop restores all three independent roles", () =>
    {
        session.Begin("cable");
        Assert(devices.Current.All(x => x == "cable"), "Not all roles switched");
        session.Restore();
        Assert(devices.Current.SequenceEqual(new[] { "mic0", "mic1", "mic2" }), "Original roles lost");
        session.Restore();
    });
    Check("A partial Start failure rolls back", () =>
    {
        devices.FailOnRole = 1;
        try { session.Begin("cable"); throw new Exception("Expected switch failure"); }
        catch (InvalidOperationException) { }
        Assert(devices.Current.SequenceEqual(new[] { "mic0", "mic1", "mic2" }), "Rollback failed");
        Assert(!session.HasPendingRestore, "Journal left behind after successful rollback");
    });
    Check("New process recovers journal; manual user changes are preserved", () =>
    {
        session.Begin("cable");
        devices.Current[1] = "user-choice";
        new DefaultInputSession(devices, journal).Restore();
        Assert(devices.Current.SequenceEqual(new[] { "mic0", "user-choice", "mic2" }), "Recovery overwrote user choice");
    });
    Check("Failed restore retains journal and can be retried", () =>
    {
        session.Begin("cable");
        devices.FailOnRole = 0;
        try { session.Restore(); throw new Exception("Expected restore failure"); }
        catch (AggregateException) { }
        Assert(session.HasPendingRestore, "Recovery journal was deleted");
        session.Restore();
        Assert(!session.HasPendingRestore && devices.Current[0] == "mic0", "Retry failed");
    });
    Check("Mic mute leaves WAV intact and respects buffer offset", () =>
    {
        var mix = new MixProvider(new Tone(.4f)) { MicrophoneEnabled = false };
        mix.Play([.5f, -.5f, .25f, -.25f]);
        float[] buffer = Enumerable.Repeat(99f, 8).ToArray();
        mix.Read(buffer, 2, 4);
        Assert(buffer[0] == 99 && buffer[6] == 99, "Buffer boundary overwritten");
        Assert(Math.Abs(buffer[2] - .35f) < .0001f && Math.Abs(buffer[3] + .35f) < .0001f, "Sound muted or changed");
        mix.Read(buffer, 0, 8);
        Assert(buffer.All(x => x == 0), "Ended clip repeated or muted microphone leaked");
    });
    Check("Gate suppresses quiet noise but passes speech-level signal", () =>
    {
        float[] buffer = new float[48000];
        new MixProvider(new Tone(.001f)).Read(buffer, 0, buffer.Length);
        Assert(buffer.All(x => x == 0), "Quiet noise passed gate");
        new MixProvider(new Tone(.2f)).Read(buffer, 0, buffer.Length);
        Assert(buffer.Max(x => Math.Abs(x)) > .1f, "Speech signal suppressed");
    });
    Check("Mixer limits clipping and replay replaces current sound", () =>
    {
        var mix = new MixProvider(new Tone(.9f));
        mix.Play(Enumerable.Repeat(4f, 2000).ToArray());
        float[] buffer = new float[2000];
        mix.Read(buffer, 0, buffer.Length);
        Assert(buffer.All(x => float.IsFinite(x) && Math.Abs(x) <= 1), "Out of range audio");
        mix.MicrophoneEnabled = false;
        mix.Play([.5f, .5f]);
        mix.Play([.25f, .25f]);
        mix.Read(buffer, 0, 2);
        Assert(Math.Abs(buffer[0] - .175f) < .0001f, "Replay did not replace sound");
    });
    Check("Key mappings reject collisions without losing existing assignments", () =>
    {
        var mappings = new KeyMappings();
        mappings.Assign("sound:one.wav", Keys.F1);
        mappings.Assign("sound:two.wav", Keys.Control | Keys.D2);
        try { mappings.Assign("sound:two.wav", Keys.F1); throw new Exception("Expected collision"); }
        catch (InvalidOperationException) { }
        Assert(mappings.FindSound(Keys.F1) == "sound:one.wav", "Collision replaced original sound");
        Assert(mappings.FindSound(Keys.Control | Keys.D2) == "sound:two.wav", "Collision removed old binding");
        mappings.Assign("SOUND:ONE.WAV", Keys.F3);
        Assert(mappings.FindSound(Keys.F1) == null && mappings.Bindings.Count == 2, "Reassignment left stale binding");
    });
    Check("Mapping drafts isolate cancel and save changes atomically", () =>
    {
        string path = Path.Combine(directory, "keys.json");
        var original = new KeyMappings();
        original.Assign("builtin:welcome", Keys.F1);
        original.Save(path);
        var draft = original.Clone();
        draft.Remove("builtin:welcome");
        draft.Assign("sound:intro.wav", Keys.Control | Keys.Shift | Keys.D1);
        Assert(original.FindSound(Keys.F1) == "builtin:welcome", "Draft changed active mapping");
        Assert(KeyMappings.Load(path).FindSound(Keys.F1) == "builtin:welcome", "Unsaved changes leaked to disk");
        draft.Save(path);
        var loaded = KeyMappings.Load(path);
        Assert(loaded.Bindings.Count == 1 && loaded.FindSound(Keys.Control | Keys.Shift | Keys.D1) == "sound:intro.wav", "Saved mapping did not round-trip");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Temporary save file leaked");
    });
    Check("Shortcuts preserve navigation keys and format combinations clearly", () =>
    {
        Assert(!KeyMappings.IsValid(Keys.Tab) && !KeyMappings.IsValid(Keys.Escape) && !KeyMappings.IsValid(Keys.Alt | Keys.F4), "Reserved keys accepted");
        Assert(!KeyMappings.IsValid(Keys.A) && !KeyMappings.IsValid(Keys.ControlKey), "Incomplete shortcut accepted");
        Assert(KeyMappings.IsValid(Keys.F2) && KeyMappings.IsValid(Keys.Control | Keys.D2), "Valid shortcut rejected");
        Assert(KeyMappings.Format(Keys.Control | Keys.Shift | Keys.D2) == "Ctrl + Shift + 2", "Wrong shortcut display");
    });
    Check("Invalid saved mappings fail without overwriting the file", () =>
    {
        string path = Path.Combine(directory, "invalid-keys.json");
        string content = "{\"one\":112,\"two\":112}";
        File.WriteAllText(path, content);
        try { KeyMappings.Load(path); throw new Exception("Expected collision in stored mappings"); }
        catch (InvalidOperationException) { }
        Assert(File.ReadAllText(path) == content, "Invalid mappings were overwritten");
    });
    Console.WriteLine($"{passed} checks passed.");
}
finally
{
    foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
    Directory.Delete(directory);
}

sealed class FakeInputs : IDefaultInputs
{
    public string[] Current = ["mic0", "mic1", "mic2"];
    public int FailOnRole = -1;
    public string Get(int role) => Current[role];
    public void Set(string id, int role)
    {
        if (FailOnRole == role) { FailOnRole = -1; throw new InvalidOperationException("Simulated missing endpoint"); }
        Current[role] = id;
    }
}

sealed class Tone(float amplitude) : ISampleProvider
{
    private int frame;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    public int Read(float[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i += 2)
        {
            float value = amplitude * MathF.Sin(2 * MathF.PI * 440 * frame++ / 48000);
            buffer[offset + i] = buffer[offset + i + 1] = value;
        }
        return count;
    }
}
