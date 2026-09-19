# Validation – MYWO v0.9.5

## Static source validation

The release validator checks:

- every WPF `.xaml` file parses as XML;
- reference desktop geometry tokens remain present;
- responsive shell tokens exist for compact/short/ultra-narrow layouts;
- Product and Service drawers retain their full-reference widths;
- v0.9.5 module reflow tokens exist for Product, Service, Dashboard, Categories, API, Archive, Price Changes and Administration;
- the Windows workflow contains the complete nine-size runtime resize matrix;
- MYWO branding assets exist and are non-trivial files;
- Desktop, Setup and Updater project versions match the release version;
- `app.manifest` exists and declares `PerMonitorV2` plus long-path awareness;
- integrated setup/update/uninstall contract tokens remain present;
- production C#/XAML source does not contain TODO/FIXME/placeholder markers.

## Windows CI validation

A release is not accepted merely because the source looks correct. The Windows pipeline must complete all of these stages:

- source validation;
- solution build on `windows-latest` with .NET 10;
- self-contained x64 release generation;
- Portable launch smoke test;
- creation of `MYWO-Data\mywo.db` in portable mode;
- live-window resize smoke test at 880×600, 1024×768, 1280×720, 1366×768, 1440×900, 1600×900, 1920×1080, 2560×1440 and 3840×2160;
- silent Setup install;
- installed `MYWO.exe` and `MYWO-Update.exe` existence checks;
- verification that no standalone `uninstall.exe` or `unins*.exe` is shipped;
- verification that Installed Apps points uninstall to `MYWO-Update.exe --uninstall`;
- quiet integrated uninstall and registry/install-directory cleanup;
- required release artifact and non-empty-file checks;
- GitHub Release publication.

## v0.9.5 responsive contract

The source contract additionally covers:

- compact two-row Product and Service filter layouts;
- ultra-narrow Dashboard card stacking;
- ultra-narrow Categories action fallback;
- API quick-action / endpoint vertical stacking;
- Archive detail/file/action vertical stacking;
- Price Changes chart/schedule vertical stacking;
- 3/2/1-column Administration settings options;
- safe editor-drawer and notification-popup width clamping;
- Per-Monitor V2 DPI manifest and active-monitor work-area sizing;
- compact navigation tooltips and short-window shell chrome;
- DataGrid row/column virtualization and automatic scrollbars.

## What automated CI does not prove

CI compile/runtime smoke tests do **not** establish measured pixel-for-pixel equivalence with a screenshot. They also do not emulate every Windows DPI factor or a physical mixed-DPI multi-monitor transition.

Visual QA remains required on representative machines at 100/125/150/175/200% scaling. The approved reference screenshots remain the visual authority for the full desktop breakpoint; responsive breakpoints intentionally reflow controls where keeping the reference geometry would create clipping or overlap.

## Data/regulatory note

The current XML/CSV representation is a MYWO application format. Validation of the application does not mean that the file format is an officially certified Croatian regulatory schema. Any such claim requires a separate mapping against current authoritative requirements.
