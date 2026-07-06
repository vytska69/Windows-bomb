# Legacy UEFI boot: what's actually possible

Users often want Windows 7/Vista/XP/8.1 install media to boot on a modern UEFI-only PC (no CSM/legacy
BIOS mode). Whether that's possible depends entirely on what boot files Microsoft actually put on that
media — this tool detects the real situation rather than promising one blanket fix.

`LegacyUefiBootInjector.Assess()` inspects the extracted media and returns one of three states:

## 1. `NativeAlready` — Windows 8, 8.1, 10, 11

These already ship `\EFI\Boot\bootx64.efi`, the fallback loader UEFI firmware looks for when there's no
existing NVRAM boot entry (i.e. booting fresh off a USB stick with no prior install). Nothing to do.

## 2. `FixableFallbackBootloaderMissing` — Windows 7 x64, Windows Vista SP1+ x64

These do ship a Microsoft EFI bootloader, at `\EFI\Microsoft\Boot\bootmgfw.efi` — but not the
`\EFI\Boot\bootx64.efi` fallback copy. Firmware without an existing boot entry for this media won't
find it. The fix is exactly one file copy:

```
EFI\Microsoft\Boot\bootmgfw.efi  -->  EFI\Boot\bootx64.efi
```

This is a well-documented community/OEM technique, not something invented for this tool. Three caveats
that are real and worth stating up front:

- **Secure Boot must be off in firmware.** `bootmgfw.efi` from Windows 7/Vista predates Secure Boot;
  it isn't in the Microsoft-signed allow list modern firmware checks against.
- **32-bit media is not covered.** Virtually no consumer UEFI firmware on real hardware executes
  32-bit (IA32) EFI binaries, so this fix only targets x64 media, which is what
  `LegacyUefiBootInjector` assumes (`bootx64.efi`).
- **This only gets the bootloader *found* — not necessarily a finished boot**, on hardware with no CSM
  at all. See the next section.

### The deeper problem this fix does *not* solve: "UEFI Class 3" hardware and Windows 7's Int10h dependency

"Finding the bootloader" and "actually finishing the boot" are two separate problems for Windows 7.
Firmware is commonly grouped into UEFI classes by how much legacy BIOS behavior it still emulates via
CSM (Compatibility Support Module):

- **Class 2**: UEFI with CSM available (most PCs sold roughly 2012–2020). Legacy BIOS interrupts still
  work even when booting in native UEFI mode. The fallback-bootloader fix above is sufficient by itself.
- **Class 3**: UEFI only, no CSM at all — the norm on hardware from about the last several years, as
  Intel/AMD reference designs phased CSM out. Windows 7's early boot/graphics initialization calls the
  legacy BIOS **Int10h video interrupt**, which simply does not exist on Class 3 firmware. The result:
  Setup or Windows itself can freeze at "Starting Windows" or fail with error `0xc000000d`, *even with
  the fallback bootloader in place and correctly found*.

[**UefiSeven**](https://github.com/manatails/uefiseven) is the reference open-source fix for exactly
this: a small chainloading `.efi` that installs a minimal Int10h handler in memory before handing
control to the real Windows bootloader (it doesn't patch Windows itself — it's inserted ahead of it in
the boot chain, the same "chainload" shape as the fallback fix above, just solving a different layer of
the problem). Its own instructions cover two separate swaps: one on the install USB
(`\EFI\Boot\bootx64.efi`, which fixes Setup/WinPE booting) and, separately, one on the installed
machine's hard drive after first reboot (`\EFI\Microsoft\Boot\bootmgfw.efi`, which this tool cannot
reach — it only ever touches install media, never a machine's already-installed OS drive).

**This tool does not bundle, mirror, or redistribute a copy of UefiSeven itself.** The repository ships
no LICENSE file, so there are no stated terms for this project to redistribute its compiled binary. What
it does instead, entirely opt-in and behind an explicit confirmation prompt (Source tab):

- `UefiSevenReleaseFetcher` queries GitHub's public API for manatails/uefiseven's latest release —
  the same request a browser makes, not something this project caches or re-hosts.
- `UefiSevenDownloadService` downloads the asset it finds (unpacking it first if it's a `.zip`) and
  returns the path to the `.efi` file inside — the download goes straight from GitHub to the user's own
  machine.
- `LegacyUefiBootInjector.ApplyUefiSevenChainload()` then follows the project's own README exactly:
  the fallback bootloader that `ApplyFallbackBootloaderFix()` put at `\EFI\Boot\bootx64.efi` is renamed
  to `\EFI\Boot\bootx64.original.efi` (UefiSeven's own naming convention), and UefiSeven's binary takes
  its place — so UefiSeven runs first and chainloads to the real bootloader afterwards. No Windows file
  is patched or replaced; the swap is confined to files this tool already writes.

This still doesn't remove the second reason for caution: UefiSeven patches the boot chain at the
firmware level, which is exactly the kind of change this tool can't verify the outcome of in an
automated test (a wrong patch there doesn't throw an exception, it just leaves a machine unable to
boot). That's why the download step requires an explicit confirmation, why the GUI also always offers a
plain link to the upstream project for manual review, and why this tool never applies the chainload
without the user first opting in per build.

If your target hardware is Class 3 (no CSM/legacy option in firmware at all) and you're installing
Windows 7 or Vista, expect to need UefiSeven (or an equivalent Int10h shim) in addition to, not instead
of, this tool's fallback-bootloader fix.

## 3. `Unsupported` — Windows XP (any edition), Windows Vista RTM, any 32-bit media

There is no Microsoft EFI bootloader anywhere on this media, full stop. Windows XP predates Microsoft
UEFI boot support entirely (it was introduced in Vista SP1); Vista RTM shipped before SP1 added it.

**This tool will not fabricate support here.** The only way to get this media "UEFI bootable" is a
third-party shim bootloader (e.g. Clover) that chainloads the legacy BIOS/MBR/NTLDR boot path from
UEFI — a fundamentally different mechanism (emulating a BIOS boot environment under UEFI, not making
XP/Vista's own bootloader UEFI-native), much less reliable, and out of scope for this tool. If you
need XP/Vista on UEFI-only modern hardware, boot it via that firmware's CSM/legacy mode instead, or
look at a dedicated multi-boot shim project designed for that specific use case.
