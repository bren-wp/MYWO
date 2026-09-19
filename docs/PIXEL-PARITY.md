# MYWO screenshot parity specification — v0.9.0

The supplied 1448×1086 reference captures are the visual source of truth. The application is not allowed to use those captures as a background; the UI must be constructed from native WPF controls and vector/icon glyphs.

## Reference shell geometry

At 100% Windows display scale the calibrated application shell is 1380×950 DIP with a 42 DIP custom title strip, a 214 DIP navigation rail, a 68 DIP command/search bar and no visible legacy status bar. The content area uses a 20 DIP horizontal inset. The product edit drawer is 422 DIP wide and the service edit drawer is 420 DIP wide.

The shell uses Windows 11 rounded DWM corners when available. `UseLayoutRounding` and `SnapsToDevicePixels` remain enabled so borders, separators and table rows land on whole device pixels at the reference scale.

## Visual tokens

Primary background: `#071321`. Sidebar: `#07131F`. Main toolbar: `#0A1828`. Card surface: `#0D1D30`. Input surface: `#10243A`. Primary border: `#23415F`. Primary text: `#F4F8FD`. Secondary text: `#9AAAC1`. Accent starts at `#0BA6FF` and ends at `#1B4DE8`.

Buttons use 8 DIP radii and 40+ DIP interaction height. Cards use 10 DIP radii. Active navigation uses the cyan-to-blue MYWO gradient. Toggle controls use a native-styled 38×22 switch rather than the default WPF checkbox on settings/drawer surfaces.

## Page mapping

- **Pregled:** six summary cards, price-change visualization, publication history, quick facts/actions and the MYWO value card.
- **Proizvodi:** first row filters, second row actions, selection strip, dense product table and a 422 DIP right-side editor drawer.
- **Usluge:** first row search/filter controls, second row actions/count, dense service table and a 420 DIP right-side editor drawer.
- **Kategorije i brendovi:** category navigation/summary column plus category and brand tables stacked on the right, with native dialogs for category/brand editing.
- **Objava:** status cards, explicit XML/CSV/API publication actions, generated-content preview, publication settings and generated files.
- **API ključevi:** three metric cards, key table, quick actions and endpoint/reference panel. Generated secrets remain one-time-visible.
- **Arhiva:** filter row, publication table, details/file/action cards.
- **Promjene cijena:** four metric cards, chart/planned changes and detailed price history.
- **Administracija:** three-section Audit/Tvrtke/Postavke navigation, company cards, system settings, backup/notifications/update controls and recent audit activity.

## Responsive behavior

The 1380×950 layout is the pixel-parity baseline. At smaller usable desktop sizes WPF layout panels, scroll viewers and proportional columns preserve functionality rather than clipping controls. Drawers remain anchored to the right edge. Large data regions scroll instead of allowing the window to collapse into unusable geometry.

## Release acceptance

A Windows release is acceptable only after the CI build validates XAML, compiles all three projects, launches the portable executable, confirms creation of the portable `MYWO-Data` directory, installs via `MYWO-Setup.exe`, verifies `MYWO-Update.exe`, confirms there is no separate `uninstall.exe`/`unins*.exe`, and successfully performs integrated uninstall through `MYWO-Update.exe --uninstall --quiet`.
