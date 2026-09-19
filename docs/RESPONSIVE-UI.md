# MYWO responsive UI / UX

MYWO v0.9.3 keeps the approved 1380×950 reference composition as the full desktop layout, while adapting safely to smaller and larger Windows work areas.

## Adaptive modes

- **Full — 1180 DIP and wider:** 214 DIP navigation, full company selector, full search and full publish action.
- **Compact — 1000–1179 DIP:** icon navigation, narrower company/search controls, icon publish action and tighter content margins.
- **Narrow — below 1000 DIP:** 68 DIP icon navigation, 350 DIP editor drawers, hidden secondary header context and compact page spacing.

The practical main-window minimum is now **880×600 DIP**. Windows and dialogs are also clamped to the available Windows work area so controls are not opened outside the visible desktop.

## Overlap prevention

Product and service action rows use wrapping layouts instead of brittle fixed horizontal stacks. Filter rows use proportional columns. Footer actions grow automatically and wrap when needed. Data grids retain their own scrolling behavior.

## Dialogs

Management dialogs that were previously fixed-size now support resize grips. Existing ScrollViewer regions keep longer forms usable at reduced work-area heights.

## Images

WPF image rendering uses high-quality bitmap scaling and device-pixel snapping. Product drawer previews decode to an 800 px preview width to reduce memory usage while keeping the preview sharp.

## Reference parity

The supplied reference screenshots remain the design authority for the full desktop breakpoint. Responsive modes intentionally compact or reflow controls only when retaining the exact full-layout geometry would cause clipping or overlap.

## QA matrix

| Window / scale | Acceptance target |
| --- | --- |
| 880×600 | compact navigation; primary actions remain reachable |
| 1024×768 | products, services and settings remain usable |
| 1366×768 | reference-like density without collisions |
| 1920×1080 | full navigation and toolbar |
| 2560×1440 | full composition remains crisp |
| 3840×2160 at 150–200% | layout rounding and images remain clean |

Windows CI additionally compiles the solution, starts Portable, verifies portable data creation, installs Setup and verifies integrated uninstall through `MYWO-Update.exe --uninstall`.


## Dense modules

At the narrow breakpoint Dashboard, Publication, API and Price Changes reflow KPI/action groups into additional rows. API, Archive and Price Changes also use vertical fallback scrolling, so short 600–768 px laptop work areas do not compress the bottom sections into unusable space.


## Per-monitor DPI

MYWO v0.9.3 declares **PerMonitorV2** awareness. Window fitting uses the work area of the actual monitor that owns the WPF window and converts native monitor coordinates to WPF device-independent pixels using that monitor's DPI scale.

This matters for mixed-DPI systems such as a 150% laptop display combined with a 100% or 200% external monitor.

## Short-window mode

When the available MYWO window height falls below 760 DIP, the shell reduces non-essential vertical chrome:

- sidebar logo region becomes shorter;
- toolbar and page-header rows become shorter;
- the promotional sidebar card is hidden;
- primary content receives more usable vertical space.

## Administration reflow

Below 1000 DIP width, the company-management card and system-settings card are no longer forced side by side. They are placed one below the other inside the existing settings ScrollViewer, preserving usable input widths without relying on horizontal scrolling.

## Data-heavy screens

All DataGrid controls explicitly use row/column virtualization with recycling and automatic scrollbars. This reduces visual-tree pressure when catalogs contain hundreds or thousands of products/services.
