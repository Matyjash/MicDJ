using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MicDJ.Core;

[assembly: InternalsVisibleTo("MicDJ.Checks")]

namespace MicDJ;

internal sealed class GlobalHotKeys : NativeWindow, IDisposable
{
    private readonly Dictionary<int, string> sounds = [];
    private int nextId;
    public event Action<string>? Pressed;

    public GlobalHotKeys() => CreateHandle(new CreateParams { Parent = new IntPtr(-3) });

    public IReadOnlyList<string> Register(KeyMappings mappings)
    {
        Clear();
        List<string> failures = [];
        foreach (var (sound, shortcut) in mappings.Bindings)
        {
            if ((shortcut & Keys.KeyCode) == Keys.F12)
            {
                failures.Add($"{KeyMappings.Format(shortcut)}: F12 is reserved by Windows. Choose another shortcut.");
                continue;
            }
            uint modifiers = 0x4000; // MOD_NOREPEAT prevents held keys from restarting sounds.
            if (shortcut.HasFlag(Keys.Alt)) modifiers |= 1;
            if (shortcut.HasFlag(Keys.Control)) modifiers |= 2;
            if (shortcut.HasFlag(Keys.Shift)) modifiers |= 4;
            int id = ++nextId;
            if (id > 0xBFFF) throw new InvalidOperationException("Restart MicDJ to register more shortcuts.");
            if (RegisterHotKey(Handle, id, modifiers, (uint)(shortcut & Keys.KeyCode)))
                sounds.Add(id, sound);
            else
                failures.Add($"{KeyMappings.Format(shortcut)}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }
        return failures;
    }

    public void Clear()
    {
        foreach (int id in sounds.Keys) UnregisterHotKey(Handle, id);
        sounds.Clear();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312)
        {
            if (sounds.TryGetValue(message.WParam.ToInt32(), out string? sound)) Pressed?.Invoke(sound);
            return;
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        Clear();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
