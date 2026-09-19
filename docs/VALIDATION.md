# Validation – MYWO v0.9.3

## Static source validation

The release validator checks:

- every WPF `.xaml` file parses as XML;
- reference desktop geometry tokens remain present;
- responsive shell tokens exist for compact/short layouts;
- Product and Service drawers retain their reference widths;
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
- silent Setup install;
- installed `MYWO.exe` and `MYWO-Update.exe` existence checks;
- verification that no standalone `uninstall.exe` or `unins*.exe` is shipped;
- verification that Installed Apps points uninstall to `MYWO-Update.exe --uninstall`;
- quiet integrated uninstall and registry/install-directory cleanup;
- required release artifact and non-empty-file checks;
- GitHub Release publication.

## v0.9.3 responsive checks

The source contract now additionally covers:

- Per-Monitor V2 DPI manifest;
- active-monitor work-area sizing/placement;
- compact navigation tooltips;
- short-window shell chrome;
- adaptive Categories split width;
- stacked Administration settings below the narrow breakpoint;
- resize-safe dialog minimum sizes and scroll fallback;
- DataGrid row/column virtualization and auto scrollbars.

## What automated CI does not prove

CI compile/runtime smoke tests do **not** establish measured pixel-for-pixel equivalence with a screenshot. Visual QA is still required on representative resolutions and DPI scales, especially mixed-DPI multi-monitor setups.

The approved reference screenshots remain the visual authority for the full desktop breakpoint. Responsive breakpoints intentionally reflow controls where keeping the reference geometry would create clipping or overlap.

## Data/regulatory note

The current XML/CSV representation is a MYWO application format. Validation of the application does not mean that the file format is an officially certified Croatian regulatory schema. Any such claim requires a separate mapping against current authoritative requirements.
