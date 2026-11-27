using MicDJ.Core;
using MicDJ;
using System.Runtime.InteropServices;

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
    Check("Buffered library rows render names and shortcuts below the first row", () =>
    {
        using var library = new StudioLibrary { Size = new Size(480, 200) };
        library.Items.Add("First sound");
        library.Items.Add("Second sound.wav");
        library.ShortcutText = _ => "Ctrl + 2";
        var draw = typeof(StudioLibrary).GetMethod("OnDrawItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        foreach (bool playing in new[] { false, true })
        {
            library.UpdatePlayback(playing ? library.Items[1] : null);
            using var bitmap = new Bitmap(480, 200);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Black);
            draw.Invoke(library, [new DrawItemEventArgs(graphics, library.Font, new Rectangle(0, 52, 480, 52), 1,
                DrawItemState.Selected, library.ForeColor, library.BackColor)]);
            bool HasText(int left, int right)
            {
                for (int y = 55; y < 101; y++)
                    for (int x = left; x < right; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.R > 120 && pixel.G > 120 && pixel.B > 120) return true;
                    }
                return false;
            }
            Assert(HasText(50, 250), "Second row sound name is missing");
            Assert(HasText(350, 470), "Second row shortcut is missing");
        }
    });
    Check("Windows hotkeys detect conflicts, dispatch messages and release registrations", () =>
    {
        var mappings = new KeyMappings();
        mappings.Assign("sound:global.wav", Keys.Control | Keys.Alt | Keys.Shift | Keys.F24);
        using var first = new GlobalHotKeys();
        using var second = new GlobalHotKeys();
        Assert(first.Register(mappings).Count == 0, "Could not register integration-test shortcut");
        Assert(second.Register(mappings).Count == 1, "Windows shortcut conflict was not reported");
        string? received = null;
        first.Pressed += id => received = id;
        Assert(NativeMessages.PostMessage(first.Handle, 0x0312, new IntPtr(1), IntPtr.Zero), "Could not queue Windows hotkey message");
        Application.DoEvents();
        Assert(received == "sound:global.wav", "Windows message did not dispatch the sound");
        first.Clear();
        received = null;
        Assert(NativeMessages.PostMessage(first.Handle, 0x0312, new IntPtr(1), IntPtr.Zero), "Could not queue stale message");
        Application.DoEvents();
        Assert(received == null, "Released shortcut dispatched a stale message");
        Assert(second.Register(mappings).Count == 0, "Cleared shortcut remained reserved");
        second.Dispose();
        Assert(first.Register(mappings).Count == 0, "Disposed window did not release its shortcut");
        var replacement = new KeyMappings();
        replacement.Assign("sound:replacement.wav", Keys.Control | Keys.Alt | Keys.Shift | Keys.F23);
        Assert(first.Register(replacement).Count == 0, "Could not update registration");
        using var third = new GlobalHotKeys();
        Assert(third.Register(mappings).Count == 0, "Updating mappings did not release old combination");
    });
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

static class NativeMessages
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
