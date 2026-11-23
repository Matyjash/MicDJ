using System.Text.Json;

namespace MicDJ.Core;

public interface IDefaultInputs
{
    string Get(int role);
    void Set(string id, int role);
}

public sealed record InputSnapshot(string Target, string[] Previous);

// Persist before touching Windows. A new process can recover after a crash.
public sealed class DefaultInputSession(IDefaultInputs devices, string journalPath)
{
    public bool HasPendingRestore => File.Exists(journalPath);

    public void Begin(string target)
    {
        Restore();
        var snapshot = new InputSnapshot(target, Enumerable.Range(0, 3).Select(devices.Get).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        string temp = journalPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(snapshot));
        File.Move(temp, journalPath, true);
        try
        {
            for (int role = 0; role < 3; role++) devices.Set(target, role);
        }
        catch (Exception startError)
        {
            try { Restore(); }
            catch (Exception restoreError) { throw new AggregateException(startError, restoreError); }
            throw;
        }
    }

    public void Restore()
    {
        if (!File.Exists(journalPath)) return;
        var snapshot = JsonSerializer.Deserialize<InputSnapshot>(File.ReadAllText(journalPath))
            ?? throw new InvalidDataException("The microphone settings backup is invalid.");
        if (snapshot.Previous.Length != 3) throw new InvalidDataException("The microphone settings backup is incomplete.");
        List<Exception> errors = [];
        for (int role = 0; role < 3; role++)
        {
            try
            {
                // Respect changes made by the user while MicDJ was running.
                if (devices.Get(role) == snapshot.Target) devices.Set(snapshot.Previous[role], role);
            }
            catch (Exception ex) { errors.Add(ex); }
        }
        if (errors.Count != 0)
            throw new AggregateException("Could not restore the microphone. Reconnect the previous device and click Stop again.", errors);
        File.Delete(journalPath);
    }
}
