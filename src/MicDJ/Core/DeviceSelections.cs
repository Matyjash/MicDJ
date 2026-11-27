using System.Text.Json;

namespace MicDJ.Core;

public sealed class DeviceSelections
{
    public string? Microphone { get; set; }
    public string? Destination { get; set; }
    public string? VirtualInput { get; set; }
    public string? Headphones { get; set; }

    public static DeviceSelections Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<DeviceSelections>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The device settings file is empty.")
        : new();

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
