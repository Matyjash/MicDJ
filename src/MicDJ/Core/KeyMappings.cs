using System.Text.Json;

namespace MicDJ.Core;

public sealed record SoundChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed class KeyMappings
{
    private readonly Dictionary<string, Keys> bindings = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, Keys> Bindings => bindings;

    public KeyMappings Clone()
    {
        var result = new KeyMappings();
        foreach (var pair in bindings) result.bindings.Add(pair.Key, pair.Value);
        return result;
    }

    public string? FindSound(Keys shortcut) => bindings.FirstOrDefault(pair => pair.Value == shortcut).Key;
    public void Remove(string soundId) => bindings.Remove(soundId);

    public void Assign(string soundId, Keys shortcut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(soundId);
        if (!IsValid(shortcut)) throw new ArgumentException("Use a function key, or Ctrl/Alt with a letter or number.");
        string? conflict = FindSound(shortcut);
        if (conflict != null && !StringComparer.OrdinalIgnoreCase.Equals(conflict, soundId))
            throw new InvalidOperationException("This shortcut is already assigned to another sound.");
        bindings[soundId] = shortcut;
    }

    public static bool IsValid(Keys shortcut)
    {
        Keys key = shortcut & Keys.KeyCode;
        Keys modifiers = shortcut & Keys.Modifiers;
        if ((modifiers & ~(Keys.Control | Keys.Alt | Keys.Shift)) != 0) return false;
        if (key >= Keys.F1 && key <= Keys.F24) return !(key == Keys.F4 && modifiers.HasFlag(Keys.Alt));
        bool character = key >= Keys.A && key <= Keys.Z || key >= Keys.D0 && key <= Keys.D9 || key >= Keys.NumPad0 && key <= Keys.NumPad9;
        return character && (modifiers & (Keys.Control | Keys.Alt)) != 0;
    }

    public static string Format(Keys shortcut)
    {
        var parts = new List<string>();
        if (shortcut.HasFlag(Keys.Control)) parts.Add("Ctrl");
        if (shortcut.HasFlag(Keys.Alt)) parts.Add("Alt");
        if (shortcut.HasFlag(Keys.Shift)) parts.Add("Shift");
        Keys key = shortcut & Keys.KeyCode;
        parts.Add(key >= Keys.D0 && key <= Keys.D9 ? ((int)key - (int)Keys.D0).ToString() : key.ToString());
        return string.Join(" + ", parts);
    }

    public static KeyMappings Load(string path)
    {
        var result = new KeyMappings();
        if (!File.Exists(path)) return result;
        var saved = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The key mappings file is empty.");
        foreach (var pair in saved) result.Assign(pair.Key, (Keys)pair.Value);
        return result;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(bindings.ToDictionary(pair => pair.Key, pair => (int)pair.Value), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
