# MicDJ

<img src="src/MicDJ/Assets/brand.png" alt="MicDJ logo" width="20%">

A mixer for Windows 10/11: combine your microphone and a selected WAV file into one input.

> [!IMPORTANT]
> Requires [VB-CABLE](https://vb-audio.com/Cable/) installed.

## Build

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build MicDJ.sln -c Release
dotnet run --project src/MicDJ
dotnet run --project tests/MicDJ.Checks -c Release
```
