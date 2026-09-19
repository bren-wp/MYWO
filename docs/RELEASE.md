# MYWO Windows release procedure – v0.9

## Build requirements

- Windows 10/11 or GitHub Actions `windows-latest`
- .NET 10 SDK
- PowerShell 7/Windows PowerShell

No Inno Setup, NSIS or external installer compiler is required.

## Build

```powershell
.\scripts\build-release.ps1 -Version 0.9.2
```

The script must produce in `dist/`:

- `MYWO-Setup.exe`
- `MYWO-Portable.exe`
- `MYWO-Update.exe`
- `MYWO-Update-Payload-0.9.2.zip`
- `MYWO-Installed-Files-0.9.2.zip`
- `update.json`
- `SHA256SUMS.txt`

## Installation model

`MYWO-Setup.exe` installs per-user into `%LOCALAPPDATA%\Programs\MYWO`. It creates Start Menu and Desktop shortcuts and writes the Windows Installed Apps entry under HKCU.

The uninstall command is:

```text
MYWO-Update.exe --uninstall
```

There is intentionally no `uninstall.exe`, `unins000.exe` or other dedicated uninstaller. The updater copies itself to a temporary location before update/uninstall so it can safely replace or remove the installed application directory.

## Portable model

`MYWO-Portable.exe` is self-contained. When the executable name contains `Portable`, MYWO stores its database and logs under `MYWO-Data` next to the executable instead of `%APPDATA%`.

## Update model

`MYWO-Update.exe` reads `https://mywo.hr/download/update.json` by default. The manifest contains `version`, `url` and `sha256`. The update package is downloaded to a temporary staging directory and its SHA-256 is verified before any installed file is replaced.

## Required validation

Test on a clean Windows 11 x64 VM/user profile: setup, first launch, shortcuts, Installed Apps entry, update, uninstall with preserved data, uninstall with removed data, portable mode, database migration, XML/CSV publishing, API startup, CSV round-trip, backup/restore and 100/125/150/200% display scaling.


## Responsive UI QA

Before publishing, test the screen-size matrix in `RESPONSIVE-UI.md` in addition to the automated Windows smoke tests.
