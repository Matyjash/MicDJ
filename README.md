# MicDJ

<img src="src/MicDJ/Assets/brand.png" alt="MicDJ logo" width="20%">

A mixer for Windows 10/11: combine your microphone and a selected WAV file into one input.

> [!IMPORTANT]
> Requires [VB-CABLE](https://vb-audio.com/Cable/) installed.

## Keyboard shortcuts

Open **Key mappings** from the main window. Search for a sound by name, select it,
click the shortcut field, press a function key or a Ctrl/Alt combination, then click
**Assign**. The scrollable list supports editing and clearing assignments.
**Save mappings** applies and remembers changes; **Cancel** discards them.

Shortcuts play sounds globally while the mixer is running, including when MicDJ is
minimized or another application is focused. Holding a shortcut does not repeat it.
They are released when the mixer stops or while editing mappings.
If Windows or another application has reserved a shortcut, MicDJ reports the conflict;
choose another combination. F12 is reserved by Windows and cannot be activated.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build MicDJ.sln -c Release
dotnet run --project src/MicDJ
dotnet run --project tests/MicDJ.Checks -c Release
```
