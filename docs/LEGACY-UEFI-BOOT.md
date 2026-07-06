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

This is a well-documented community/OEM technique, not something invented for this tool. Two caveats
that are real and worth stating up front:

- **Secure Boot must be off in firmware.** `bootmgfw.efi` from Windows 7/Vista predates Secure Boot;
  it isn't in the Microsoft-signed allow list modern firmware checks against.
- **32-bit media is not covered.** Virtually no consumer UEFI firmware on real hardware executes
  32-bit (IA32) EFI binaries, so this fix only targets x64 media, which is what
  `LegacyUefiBootInjector` assumes (`bootx64.efi`).

## 3. `Unsupported` — Windows XP (any edition), Windows Vista RTM, any 32-bit media

There is no Microsoft EFI bootloader anywhere on this media, full stop. Windows XP predates Microsoft
UEFI boot support entirely (it was introduced in Vista SP1); Vista RTM shipped before SP1 added it.

**This tool will not fabricate support here.** The only way to get this media "UEFI bootable" is a
third-party shim bootloader (e.g. Clover) that chainloads the legacy BIOS/MBR/NTLDR boot path from
UEFI — a fundamentally different mechanism (emulating a BIOS boot environment under UEFI, not making
XP/Vista's own bootloader UEFI-native), much less reliable, and out of scope for this tool. If you
need XP/Vista on UEFI-only modern hardware, boot it via that firmware's CSM/legacy mode instead, or
look at a dedicated multi-boot shim project designed for that specific use case.
