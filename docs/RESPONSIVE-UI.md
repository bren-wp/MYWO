# MYWO responsive UI / UX

MYWO v0.9.5 keeps the approved 1380×950 reference composition as the full desktop layout, while adapting safely to smaller and larger Windows work areas.

## Adaptive modes

- **Full — 1180 DIP and wider:** 214 DIP navigation, full company selector, full search and full publish action.
- **Compact — 1000–1179 DIP:** icon navigation, narrower company/search controls, icon publish action, tighter content margins and two-row Product/Service filter layouts.
- **Narrow — below 1000 DIP:** 68 DIP icon navigation, narrower editor drawers, hidden secondary header context, vertically stacked Price Changes overview and Administration cards.
- **Ultra-narrow — below 760 DIP when the Windows work area forces it:** 58 DIP icon navigation, collapsed company selector, tighter page margins, additional Dashboard/API/Archive stacking, single-column Administration options and a Categories action fallback.

The normal requested minimum remains **880×600 DIP**. `App.FitWindowToCurrentWorkArea` may lower the effective minimum when the current monitor work area is smaller in device-independent pixels, which is why the ultra-narrow fallback exists.

## Overlap prevention

Product and Service filters move from one proportional row to two balanced rows in compact mode. Action/footer regions use wrapping layouts and allow the action group to consume the remaining row width rather than measuring it as an unbounded right-docked strip. Data grids keep automatic horizontal/vertical scrolling and virtualization.

## Dense module reflow

- **Dashboard:** KPI cards reflow at narrow widths; chart/history and the three lower summary cards stack further in ultra-narrow mode.
- **Publication:** KPI/action groups reflow into additional rows while the preview and generated-file regions retain scrolling.
- **API:** KPI cards reflow on narrow layouts; quick actions and endpoint documentation stack vertically in ultra-narrow mode.
- **Archive:** search controls tighten on narrow layouts; selected-publication details, file metadata and actions stack vertically in ultra-narrow mode.
- **Price Changes:** KPI cards reflow and the trend chart / scheduled-change panel stack vertically below the narrow breakpoint.
- **Categories:** the normal tree/detail split is preserved where it is usable; ultra-narrow mode hides the tree and exposes the essential New/Edit/Delete actions in the detail header.
- **Administration:** Companies/System stack below the narrow breakpoint. The settings option surface uses 3 columns on full, 2 on narrow and 1 on ultra-narrow layouts.

Long dense screens remain inside existing `ScrollViewer` regions with explicit minimum content heights so reduced-height laptop windows do not force cards into unusably small rows.

## Editor drawers and popups

Product and Service drawers preserve their reference widths on full layouts, reduce to 390 DIP in compact mode and clamp against the actual available content width on narrow/ultra-narrow work areas. Notification popup width is also constrained by the available main-content width.

## Dialogs

Management dialogs support resize grips where needed and are clamped to the available monitor work area. Existing ScrollViewer regions keep longer forms usable when the work area is short.

## Data-heavy screens

All DataGrid controls explicitly use row/column virtualization with recycling and automatic scrollbars. This reduces visual-tree pressure when catalogs contain hundreds or thousands of products/services.

## Images

WPF image rendering uses high-quality bitmap scaling and device-pixel snapping. Product drawer previews decode to an 800 px preview width to reduce memory usage while keeping the preview sharp.

## Per-monitor DPI

MYWO v0.9.5 declares **PerMonitorV2** awareness. Window fitting uses the work area of the monitor that owns the WPF window and converts native monitor coordinates to WPF device-independent pixels using that monitor's DPI scale.

DPI and display-setting changes trigger a fresh work-area fit and responsive reflow. Main-window geometry is persisted in `window-state.json` under the MYWO data root and validated before restoration.

## Short-window mode

When the available MYWO window height falls below 760 DIP, non-essential vertical chrome is reduced:

- sidebar logo region becomes shorter;
- toolbar and page-header rows become shorter;
- the promotional sidebar card is hidden;
- primary content receives more usable vertical space.

Below 650 DIP, the shell compacts further and the secondary page subtitle is hidden.

## Reference parity

The supplied reference screenshots remain the design authority for the full desktop breakpoint. Responsive modes intentionally compact or reflow controls when preserving exact reference geometry would cause clipping or overlap. Pixel-perfect parity is not claimed without measured screenshot/diff validation.

## QA matrix

| Runtime window size | Automated resize smoke target |
| --- | --- |
| 880×600 | compact/narrow layout remains alive and primary actions stay reachable through scroll/reflow |
| 1024×768 | compact filters and management screens remain stable |
| 1280×720 | short-window behavior remains stable |
| 1366×768 | reference-like density without runtime failure |
| 1440×900 | full layout transition remains stable |
| 1600×900 | full desktop composition remains stable |
| 1920×1080 | full navigation and toolbar remain stable |
| 2560×1440 | large desktop scaling remains stable |
| 3840×2160 | 4K window sizing remains stable |

Windows CI launches the real Portable EXE and applies all nine window sizes with Win32 `SetWindowPos`. The release fails if the main window cannot be acquired or the application exits after any resize.

## DPI QA boundary

The runtime resize test verifies **window-size stability on the GitHub Windows runner**. It does not emulate every Windows scaling factor. Manual/physical Windows QA is still required at **100%, 125%, 150%, 175% and 200%**, especially when moving MYWO between monitors with different DPI values.

Windows CI separately compiles the solution, verifies portable database creation, installs Setup and validates integrated uninstall through `MYWO-Update.exe --uninstall`.
