# Windows ISO Optimizer

An accessible Windows desktop tool for customizing Windows 10/11 install media before you install it:
strip telemetry/bloat, pick which in-box apps to remove, inject drivers from your current PC, and add
UEFI bootability to older install media where that's actually possible.

**You must supply your own Windows ISO.** This tool edits a copy of media you already legitimately
have (e.g. downloaded from microsoft.com) — it does not download, crack, or activate Windows.

## What it does today

- **Telemetry / privacy tweaks** — a curated set of registry policies (diagnostic data level,
  advertising ID, Cortana, consumer/"suggested apps" features, Copilot, feedback prompts), applied
  offline to the image's registry hives. See `src/WinIsoOptimizer.Core/Telemetry/TelemetryDebloatProfile.cs`
  for the exact list and the reasoning behind each entry.
- **Disables telemetry scheduled tasks** (Compatibility Appraiser, CEIP, Feedback, etc.) and the
  DiagTrack/dmwappushservice services.
- **Pick which in-box apps to remove** (Xbox, Bing Weather/News, Solitaire, Feedback Hub, Phone Link,
  Clipchamp, and more) from a checklist populated from the actual image — not a fixed guess.
- **Component-store cleanup**, optionally with `/ResetBase` to permanently shrink the image (opt-in;
  this is irreversible for that WIM).
- **Driver injection**: export every third-party driver from the Windows install you're currently
  running, and inject them into the target ISO's `install.wim` (and optionally `boot.wim`, for
  storage/NIC controllers Setup itself needs to see).
- **autounattend.xml generation**: local account instead of a forced Microsoft account, OOBE privacy
  screens skipped.
- **UEFI boot fix for Windows 7 x64 / Vista SP1+ x64 media**: adds the missing
  `\EFI\Boot\bootx64.efi` fallback loader so the media boots on UEFI firmware with no NVRAM entry
  needed (Secure Boot must be off in firmware — see [docs/LEGACY-UEFI-BOOT.md](docs/LEGACY-UEFI-BOOT.md)
  for exactly what this can and can't do, including why **Windows XP and Vista RTM cannot be made to
  boot via UEFI at all** — Microsoft never shipped the components for it).

## What it deliberately does not do

- **No Microsoft Edge/WebView2 removal.** Current Windows builds have Settings and other in-box
  components depending on the Edge runtime; deleting it offline is unsupported and reliably leaves a
  broken image. Not included, rather than included as a half-working, image-breaking option.
- **No third-party shim bootloader for XP/Vista UEFI boot.** See above.

## Requirements to build/run

This tool is fundamentally Windows-only: it shells out to `dism.exe`, `reg.exe`, `robocopy.exe`, and
PowerShell's `Mount-DiskImage`/`Dismount-DiskImage`, none of which exist outside Windows.

- **Windows 10/11**, run as **Administrator** (DISM and offline registry editing require it — the
  app's manifest already requests elevation).
- **.NET 8 SDK** (or just the Desktop Runtime to run a published build).
- **Windows ADK — "Deployment Tools" component** for `oscdimg.exe`, used to author the final bootable
  ISO. This is the one piece Windows doesn't ship in-box; download the ADK from Microsoft and install
  just that component, then point the app at `oscdimg.exe` (Kūrimas/Build tab) if it's not at the
  default path.

```powershell
dotnet build .\WinIsoOptimizer.sln
dotnet run --project .\src\WinIsoOptimizer.App\WinIsoOptimizer.App.csproj
```

### What builds where

`WinIsoOptimizer.Core` and `WinIsoOptimizer.Core.Tests` are plain `net8.0` class libraries with no
Windows-specific dependencies (all Windows-only behavior is behind `IProcessRunner`, so the argument
construction and text-parsing logic is unit tested with a fake process runner) — they build and run
their tests on any OS with the .NET 8 SDK, including this repo's Linux CI/dev sandbox.

`WinIsoOptimizer.App` targets `net8.0-windows` with WPF (`UseWPF`) and Windows Forms' folder picker
(`UseWindowsForms`), both of which only build on Windows — the WPF build tasks themselves require the
Windows desktop SDK pack. Build and run that project on an actual Windows machine.

## Architecture

- `Imaging/` — `DismService` (mount/unmount, list/remove apps, add/export drivers, cleanup),
  `OfflineRegistryService` (loads/edits/unloads the mounted image's registry hives), `IsoService`
  (extract via Mount-DiskImage + robocopy, rebuild via oscdimg), `ImageInspectionService` (read-only
  lookups for the GUI to populate its edition/app lists).
- `Telemetry/` — the debloat catalogue, the service that applies it, and the unattend.xml generator.
- `Drivers/` — export-from-running-system / inject-into-mounted-image.
- `LegacyBoot/` — the Win7/Vista-SP1+ x64 UEFI fallback-bootloader fix, with an honest assessment of
  what is and isn't fixable this way.
- `Jobs/IsoOptimizationJob` — orchestrates the full pipeline end to end, reporting progress through
  one `IProgress<OptimizationProgress>` callback so a GUI can drive a progress bar and a
  screen-reader-announced status line from the same stream.
- `WinIsoOptimizer.App` — WPF GUI (MVVM, no external MVVM library), see
  [docs/ACCESSIBILITY.md](docs/ACCESSIBILITY.md) for the screen-reader-specific design choices.

## Testing

```
dotnet test src/WinIsoOptimizer.Core.Tests/WinIsoOptimizer.Core.Tests.csproj
```

All 38 tests run and pass without a Windows host or dism/oscdimg installed — they exercise argument
construction, dism-output parsing, and error/cleanup ordering (e.g. "a registry hive is always
unloaded even if a tweak fails, or dism unmount always runs even if servicing throws") against a fake
process runner. The GUI project has no automated tests — it needs manual verification on Windows with
a screen reader, see docs/ACCESSIBILITY.md.
