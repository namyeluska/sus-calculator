# Sus Calculator

![Sus Calculator Logo](github_assets/logo.png)

Sus Calculator is a Windows desktop calculator with a hidden VM trigger. It behaves like a standard calculator, but when a secret expression is entered, it launches a QEMU virtual machine using settings from a JSON config file. A second secret expression opens a modern configuration GUI.

**What Is This Program?**
- A WinForms calculator with a hidden VM launcher.
- A configuration UI that lets you manage QEMU settings without editing JSON manually.
- A lightweight launcher that starts QEMU with persistent storage.

**Default Operations (What It Does)**
- Standard arithmetic: `+`, `-`, `*`, `/`, `%`
- Utility functions: `sqrt`, `x^2`, `1/x`, `+/-`, `CE`, `C`, `Back`, `=`
- Keyboard support for digits and operators
- Dark, modern UI

**Configuration Paths**
- Default config file: `vm-config.json`
- Default QEMU binaries: `calculator_files\qemu-system-x86_64.exe` and `calculator_files\qemu-img.exe`
- Default ISO path: `calculator_files\your-os.iso`
- Default disk: `vm\sus.qcow2`
- Default log: `log.txt`

`vm-config.json` is loaded from the current working directory first. If not found, it falls back to the executable directory.

**Which VM Platform Does It Use?**
- QEMU (Windows binaries).
- This project does not embed QEMU source code; it uses the QEMU executables you provide.

**Key Features**
- Hidden VM trigger via `secretTrigger.expression`
- Hidden Config Editor trigger via `configEditorTrigger.expression`
- Persistent disk (`qcow2`) created on first run
- QEMU stdout/stderr logging to `log.txt`
- Optional QEMU debug logging via `DebugFlags`
- Modern, dark configuration GUI with file pickers

**How To Run From Source**
1. Install the .NET SDK 8.0 (Windows).
2. Ensure QEMU binaries are available under `calculator_files\`.
3. Run the app:
   ```bash
   dotnet run
   ```

**How The Program Works**
1. The calculator UI collects input and evaluates operations.
2. When you press `=`, the engine checks the result against hidden trigger expressions.
3. If the VM trigger matches, QEMU is launched using `vm-config.json`.
4. If the config trigger matches, the configuration GUI opens.
5. Logs are written to `log.txt`, and a persistent disk is used for the VM.

**Default Hidden Triggers**
- `404 + 404 =` opens the configuration GUI.
- `69 + 69 =` launches the VM.

**Configuration Example**
```json
{
  "SecretTrigger": { "Expression": "1337+1" },
  "ConfigEditorTrigger": { "Expression": "404+404" },
  "Qemu": {
    "QemuPath": "calculator_files\\qemu-system-x86_64.exe",
    "QemuImgPath": "calculator_files\\qemu-img.exe",
    "IsoPath": "calculator_files\\your-os.iso",
    "DiskPath": "vm\\sus.qcow2",
    "DiskSizeGB": 40,
    "MemoryMB": 4096,
    "Cpus": 2,
    "BootOrder": "cd",
    "Accelerator": "",
    "LogPath": "log.txt",
    "DebugFlags": "",
    "ExtraArgs": [
      "-device",
      "virtio-net-pci,netdev=net0",
      "-netdev",
      "user,id=net0"
    ]
  }
}
```

**Publishing (Windows Only)**
```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The published output will include:
- `SusCalculator.exe`
- `vm-config.json`
- `calculator_files\` (with QEMU binaries and optional ISO)

**How Other Developers Can Extend It**
- Add new operations in `CalculatorEngine.cs`
- Add new buttons in `CalculatorForm.cs`
- Extend QEMU arguments in `VmLauncher.cs`
- Expand the configuration UI in `ConfigEditorForm.cs`
- Add validation or presets in `ConfigLoader.cs`

**Notes**
- This is a Windows-only application because it uses WinForms.
- The VM disk is persistent; remove the ISO after OS installation to boot from disk.
- For best performance on Windows, enable Hyper-V and set `Accelerator` to `whpx` in the config.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/M4M41TRW1T)
