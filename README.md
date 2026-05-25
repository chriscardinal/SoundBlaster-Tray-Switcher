# SBQuickSwitch

A tiny Windows system-tray app that flips a Sound Blaster AE-7 between **speakers** and **headphones** in one click — no need to open the Sound Blaster Command app every time.

The AE-7's speaker/headphone toggle is a physical relay on the card (you can hear it click); it isn't a separate Windows audio device. The vendor's app exposes it as a single switch buried inside the GUI, with no system-tray shortcut. This app gives you that shortcut.

![tray icon screenshot — left-click to toggle, right-click for menu](docs/screenshot.png)

## Features

- **Left-click** the tray icon → flips the relay and updates the icon. Blue headphone icon = headphones, green speaker icon = speakers.
- **Right-click** menu: current mode, manual Toggle, Refresh state, "Start with Windows" toggle, Exit.
- **CLI mode** (`SBQuickSwitchCli.exe`) for scripting / AutoHotKey: `--read`, `--toggle`, `--set-headphones`, `--set-speakers`, `--list`, `--diagnose`.
- No dependencies beyond the Creative AE-Series driver (which you already have if you own an AE-7).
- ~17 KB single executable, x86, .NET Framework 4.x.

## Requirements

- Windows 10 / 11
- Creative **Sound Blaster AE-7** with the official AE-Series driver installed
  - The companion **Sound Blaster Command** app isn't required at runtime, only the driver.
- .NET Framework 4.x (preinstalled on modern Windows)

The same approach probably works for other AE-series cards (AE-5, AE-9, AE-9 PE — they all use the same Malcolm device-control interface) but only the AE-7 has been tested.

## Install

1. Grab `SBQuickSwitch.exe` from the [latest release](../../releases) and drop it anywhere — e.g. `C:\Tools\SBQuickSwitch\`.
2. Double-click to run. The tray icon appears immediately and reads the current state.
3. (Optional) Right-click → **Start with Windows** to auto-launch at logon.

There is no installer and no registry footprint beyond the optional shortcut in your Startup folder.

## How it works

Reverse-engineered from `Interop.CtSndCr.dll` shipped with Sound Blaster Command:

1. The vendor's relay toggle is a call to a registered COM class — `CtHdaMgr` (CLSID `{3C0E7BA7-F9C3-460F-BCBE-FC91A06EF3F3}`) implementing `ISoundCore` (IID `{6111E7C4-3EA4-47ED-B074-C638875282C4}`). The COM server is `CtxHdC32.dll`, registered at install time by the AE-Series driver and hosted by the `CtxSvc32` service.
2. The specific parameter the switch writes is `eParamMalcolmDeviceControl_MultiplexOutput` (= 15) under feature `eFeature_System_MalcolmDeviceControl` (= 0x01000001).
3. Values: `0 = FrontPanel_Headphone` (the ACM's headphone amp), `1 = BackPanel_CenterLFE` (back-panel multi-channel speaker outputs).
4. This app calls `IMMDeviceEnumerator` (Windows Core Audio) to locate the AE-7 render endpoint, then `ISoundCore.BindHardware` → `SetParamValue` to flip the value. The relay click happens inside the card.

Because the COM server is 32-bit-only (registered exclusively under `HKLM\SOFTWARE\Classes\WOW6432Node\CLSID`), this app is compiled as x86.

## Building from source

Requirements: a Roslyn `csc.exe` (any recent Visual Studio Build Tools install) **or** the framework-bundled `csc.exe` at `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` (C# 5 syntax only — works with the current code).

```powershell
.\build.ps1          # outputs bin\SBQuickSwitch.exe and bin\SBQuickSwitchCli.exe
.\build.ps1 -Run     # build and launch the tray
```

Source layout:

- `Native.cs` — `[ComImport]` declarations for `ISoundCore`, `IMMDevice*`, `IPropertyStore`, plus the parameter / enum constants pulled from the vendor's interop assembly.
- `AE7.cs` — high-level controller: locate endpoint, bind, get/set/toggle MultiplexOutput.
- `Program.cs` — `Main`, CLI dispatch, `TrayContext` (NotifyIcon, glyph icon drawing, menu).
- `Startup.cs` — manages the per-user Startup-folder shortcut via WScript.Shell.

## Caveats / known limitations

- The Startup shortcut hardcodes the exe path; if you move the exe, toggle Start with Windows off then on to repoint it.
- No live event subscription — if you flip the toggle inside Sound Blaster Command, this app's icon won't update until you Refresh.
- Single global hotkey is not built in. Use the CLI with [AutoHotKey](https://www.autohotkey.com/) or PowerToys if you want one:
  ```ahk
  ^!h::Run, "C:\Tools\SBQuickSwitch\SBQuickSwitchCli.exe" --toggle
  ```

## License

MIT — see [LICENSE](LICENSE).

## Acknowledgements

This is a third-party tool, not affiliated with or endorsed by Creative Technology Ltd. "Sound Blaster" is a trademark of Creative.
