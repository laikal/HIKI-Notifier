# HIKI Notifier 1.16c — Provider modules

## Build and runtime layout

Open `HIKI Notifier.sln` in Visual Studio with the .NET Framework 4.8 targeting pack.
Run `./build.ps1 -Clean -Test` for Release x64 rebuild and offline regression checks.
The new output is `bin/Modular/Release/`; the pre-refactor release folder/ZIP is not replaced.

```text
HIKI Notifier.exe
HIKI.Notifier.Contracts.dll
HIKI.Notifier.Version.dll
Providers/
  HIKI.Provider.CHZZK.dll
  HIKI.Provider.YouTube.dll
  HIKI.Provider.RPLAY.dll
  HIKI.Provider.Twitch.dll
  HIKI.Provider.SOOP.dll
  HIKI.Provider.CIME.dll
lang/ko.ini, en.ini, ja.ini
```

Distribute this complete output, not just the EXE. Provider PNGs are embedded in their own
DLLs; main logos, About art, icon and sound remain embedded in the EXE. No external PNG/WAV
deployment is required. Existing unused artwork does not add a supported platform.

## Boundaries and where to work

| Project/path | Responsibility |
|---|---|
| `HIKI-Notifier.csproj`, UI, Tray, Services | Forms, rendering, localization, settings/migration, scheduling, notifications/sound, browser launch |
| `Contracts/` | Public provider interface, metadata, capabilities, channel/state DTOs; no HTTP or site parsers |
| `Version/` | Product version generation and assembly metadata |
| `Providers/CHZZK/` | CHZZK URL validation, API response parsing and live updates |
| `Providers/RPLAY/` | creatorOid mapping, livestream list, live/twitch activity semantics |
| `Providers/YouTube/` | Resolve, RSS, Videos/Shorts HTML fallback, baseline/history/replay |
| `Providers/Twitch/` | Channel-bound structured HTML parsing and session transitions |
| `Providers/SOOP/` | Existing player live API and BNO deduplication |
| `Providers/CIME/` | Target-scoped RUNE data, primary live.state and supplementary isLive |

For a platform fix, start with its `*Client.cs`, `*Provider.cs`, `*Metadata.cs` and project.
Do not start by reading other providers or changing Main. Metadata owns display name, URL
template, capability flags, logo, menu order, polling order/interval and persistence policy.
The optional `IContentCatalogProvider` supplies the existing built-in content picker.

Main has **no assembly reference** to provider DLLs. Main project references only establish
build ordering (`ReferenceOutputAssembly=false`); runtime discovery uses `Assembly.LoadFrom`
against `Providers/*.dll`. A public concrete implementation with a public parameterless
constructor is required. Load/type/constructor/metadata errors, duplicate string IDs or
numeric storage codes, and unrelated DLLs are isolated and traced. First valid registration
wins. Discovery happens once per process; restart after replacing modules. This is a trusted
local plugin boundary, not a sandbox for untrusted DLLs.

Polling keeps the existing 30-second base loop: CHZZK/RPLAY 30s, YouTube 180s,
Twitch/SOOP/CIME 60s. The original CHZZK → RPLAY → YouTube polling order is independent
of menu order. Twitch/SOOP/CIME retain independent, non-overlapping batches. Provider
exceptions are isolated; Unknown does not clear session/baseline history.

## Compatibility and intentionally retained policy

The user explicitly chose **actual v1.16c behavior**, including platform exceptions:
SOOP can report a new BNO on initial Live lookup; Twitch can recognize a new session after
Unknown. Their existing callback semantics, notification and AutoOpen effects are preserved.
They were not replaced by a universal startup/Unknown suppression rule. YouTube remains
new-content-only, with silent initial baseline and existing ID-based deduplication.

Built-in HIKI CHZZK + YouTube identity/URLs, protection, memo and legacy migration remain
host product policy in `Models/StreamerProfile.cs` and `Services/SettingsService.cs`.
These are intentional platform-specific references, not parsers. Historical numeric enum
values and ProviderState JSON property names remain to avoid changing persisted data.
The DTO previously called YouTubeEntry is now neutral `ContentEntry`; this transport DTO
is not the saved profile format.

Actual v1.16c uses **JSON, not settings.ini**:
`%LocalAppData%/HIKI Notifier/settings.json` and `profiles/{ID}.json`.
Existing EXE-adjacent/AppData legacy JSON migration and backups remain unchanged. Missing
DLLs do not delete profiles or reset saved baseline/session/AutoOpen values. Those channels
are not polled until their DLL is restored. Their URLs can still be opened explicitly.

Release loader/error messages use `System.Diagnostics.Trace`; existing detailed debug
diagnostics remain debug-only. Successful polls do not produce a new verbose Release log.
The baseline has no file logger or rollover policy; this refactor does not invent one.
Use a Trace listener/debugger to collect diagnostics; no exception/HTML dump is added to UI.

## Version

Edit only `Version/ReleaseVersion.txt`. The Version build generates `VersionInfo.g.cs`;
all assemblies compile the shared `AssemblyMetadata.cs`. UI accesses Version DLL at runtime.
`1.16c` maps to numeric file/assembly version `1.16.2.0` (a=0, b=1, c=2), matching the
existing release. ProductVersion/About display `1.16c`. Deploy the matching full assembly set.

## Adding a future Kick provider (not implemented)

1. Add `Providers/Kick/HIKI.Provider.Kick.csproj`, `KickClient.cs`, `KickProvider.cs`,
   `KickMetadata.cs`, embedded logo and platform fixtures/tests.
2. Reference Contracts and Version only. Implement `IPlatformProvider`; metadata declares
   a unique stable string ID and unused nonnegative numeric storage code, e.g. cast the
   reserved code to PlatformType. Do not renumber existing codes 0–5. Main does not require
   a new enum member. Coordinate codes between modules; collisions are rejected.
3. Own URL parsing, HTTP/parsing, state/session logic, replay policy, normalization and
   capabilities in that project. Use cancellation/timeouts. AutoOpen requires the metadata
   AutoOpen flag as well as live transition support. Return existing DTOs/callback semantics.
4. Add the project to the solution and optionally Main's build-only references for the
   standard full build, plus its tests to the test harness. Copy its DLL into Providers.
5. No MainForm/ProfileEditForm/Tray/polling switch or existing provider change is needed.
   Menu, URL template, logo and scheduler discover metadata automatically.

Contracts changes are needed only for a genuinely new contract capability, not for another
platform implementing existing live/content behavior. Keep existing platform rules local.

## Validation

`Tests/ModuleChecks.cs` builds a synthetic test-only provider (not Kick) and checks discovery,
bad DLL, unrelated DLL, throwing constructor, invalid metadata, duplicates, ko/en/ja menu
generation, new numeric platform persistence, missing-plugin state preservation and poll
failure isolation. Separate child processes construct Main/Tray in missing/corrupt-plugin
folders using isolated settings paths; verify built-in rows, close-to-tray and clean bootstrap.
This uses the same MainForm source, without touching the user's running instance/settings.

The existing suite covers six platform fixtures/transitions, YouTube Videos/Shorts baseline
and deduplication, replay/AutoOpen, built-in and JSON migration, localization, notification
opacity/sound/resources and existing drawing contracts. `TwitchChecks.cs` and `CimeChecks.cs`
contain their scoped fixtures; remaining legacy checks live in `TestProgram.cs`.
These are deterministic/offline checks, not a new live broadcast end-to-end certification.
Manual screen appearance and all external platform uptime are not certified by fixture tests.

### Final local result (2026-09-26)

- `build.ps1 -Clean -Test`: Release x64 rebuild successful, **PASS 335**.
- Includes the original 306 checks plus 29 module/compatibility/startup checks.
- Binary assembly-reference inspection: Main references Contracts/Version and framework
  libraries, no `HIKI.Provider.*` assembly. Source scan found no platform Client/HTML/API
  implementation references in Main UI, Services, Tray or Contracts.
- EXE and Version DLL: FileVersion `1.16.2.0`, ProductVersion `1.16c`.
- Eight library versions (six Providers, Contracts, Version) checked by the harness.
- Output contains EXE, two shared DLLs, six Provider DLLs and three language INIs.
- New output only; existing release ZIP/folder and GitHub were not modified/published.
- No fresh online broadcast/upload test was run during this structural refactor.
