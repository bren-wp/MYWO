# MYWO Windows release procedure – v0.9.4

## Build requirements

- Windows 10/11 or GitHub Actions `windows-latest`
- .NET 10 SDK
- PowerShell 7 / Windows PowerShell

No Inno Setup, NSIS or external installer compiler is required.

## Build

```powershell
.\scripts\build-release.ps1 -Version 0.9.4
```

The script produces in `dist/`:

- `MYWO-Setup.exe`
- `MYWO-Portable.exe`
- `MYWO-Update.exe`
- `MYWO-Update-Payload-0.9.4.zip`
- `MYWO-Installed-Files-0.9.4.zip`
- `MYWO-0.9.4-source.zip`
- `update.json`
- `SHA256SUMS.txt`

## Installation model

`MYWO-Setup.exe` installs per-user into `%LOCALAPPDATA%\Programs\MYWO`. It creates Start Menu/Desktop shortcuts and writes the Windows Installed Apps entry under HKCU.

The uninstall command is:

```text
MYWO-Update.exe --uninstall
```

There is intentionally no `uninstall.exe`, `unins000.exe` or other dedicated uninstaller. The updater copies itself to a temporary directory before update/uninstall so it can safely replace or remove the installed application tree.

## Portable model

`MYWO-Portable.exe` is self-contained. When the executable name contains `Portable`, MYWO stores its database and logs under `MYWO-Data` next to the executable instead of `%APPDATA%`.

## Update model

`MYWO-Update.exe` uses the latest GitHub Release manifest by default:

```text
https://github.com/bren-wp/MYWO/releases/latest/download/update.json
```

The manifest contains `version`, `url` and `sha256`. The update package must use HTTPS, is downloaded into temporary staging, is bounded by the configured package-size limit and must pass SHA-256 verification before any installed file is replaced.

## DPI / display contract

The WPF desktop project includes an application manifest with `PerMonitorV2` DPI awareness. Window fitting uses the actual monitor work area rather than assuming the primary display. Test mixed-DPI monitor movement and launch behavior at 100/125/150/175/200% scaling.

## Required automated validation

The GitHub Actions Windows release pipeline must pass:

1. static XAML/XML and responsive-token validation;
2. .NET restore/build;
3. self-contained publish;
4. Portable runtime smoke test and portable database creation;
5. Setup installation smoke test;
6. Installed Apps/uninstall-command validation;
7. integrated `MYWO-Update.exe --uninstall --quiet` smoke test;
8. release artifact existence/non-empty validation;
9. GitHub Release upload.

## Required visual QA

Automated CI cannot prove visual pixel parity. Before calling a build visually final, inspect at minimum:

- 880×600
- 1024×768
- 1366×768
- 1920×1080
- 2560×1440
- 3840×2160 with 150–200% scale
- mixed-DPI multi-monitor launch/move scenarios

Use the matrix in `RESPONSIVE-UI.md` and compare the full desktop breakpoint against the approved reference screenshots.

## Release signing

Current CI can produce deterministic/tested binaries without a code-signing certificate. For commercial distribution, Authenticode signing should be added when a valid Windows code-signing certificate and secure signing channel are available; do not embed private signing keys in the repository.


## Responsive window resize smoke test

The Windows release workflow starts `MYWO-Portable.exe`, obtains the real main-window handle and resizes it through the supported QA matrix. This runtime check is performed before Setup/install/uninstall validation and before publishing the release.
