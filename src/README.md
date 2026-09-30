# HIKI Notifier public source

Public source snapshot: v1.20C

The latest binary release may contain newer code not yet published in this repository.

This is a separate copy of the archived v1.20C source. No v1.25C features are included.

## Build

Requires Windows, Visual Studio MSBuild with .NET Framework 4.8 targeting pack and C# 7.3 support. In a Visual Studio Developer PowerShell:

```powershell
MSBuild "HIKI Notifier.sln" /t:Rebuild /p:Configuration=Release /p:Platform=x64
```

## Assets

HIKI application icon and Korean, English and Japanese application logos are retained. Original character artwork and platform/test artwork are excluded and replaced with simple neutral placeholder PNGs at the existing resource paths. These placeholders preserve provider resource loading and are not the artwork used by the distributed application.

The third-party Mixkit WAV is not included; its EmbeddedResource entries are removed. The existing sound code handles a missing built-in sound resource without crashing. The built-in default sound is unavailable in this snapshot until a properly licensed replacement is supplied. Custom WAV and silent modes retain their original code.

Development outputs, user settings/profiles, signing material and environment-specific build scripts are excluded. C# implementation files are unchanged from the archived v1.20C source.

Copyright © Eltax. All rights reserved.
