# Changelog

## 0.9.1

- added adaptive full / compact / narrow desktop layouts
- lowered the practical main-window minimum to 880×600 DIP
- fixed product/service toolbar and footer overlap with wrapping action layouts
- changed brittle fixed filter widths to proportional columns
- added compact sidebar, top toolbar and editor drawer behavior
- made all application windows respect the available Windows work area
- enabled resize grips for management dialogs that could otherwise be trapped off-screen
- added high-quality WPF bitmap scaling and lower-memory product preview decoding
- added responsive UI/UX documentation and a screen-size QA matrix
- rewrote README as a marketing/product landing page with current downloads, brand imagery and badges
- removed bin/obj build outputs from generated source archives
- updated Desktop, Setup, Updater and Windows CI release pipeline to 0.9.1

## 0.9.0

- upgraded the application icon and sidebar brand asset with a multi-resolution MYWO visual
- hardened setup replacement with payload validation, rollback-safe install-tree replacement and non-fatal shortcut creation
- hardened updater downloads with HTTPS-only transport, package-size limits, SHA-256 validation and rollback-safe replacement
- strengthened integrated uninstall with retry-based directory removal while preserving optional user-data retention
- moved the default updater manifest to GitHub Releases and generated release payload URLs from the repository release tag
- added automatic GitHub release publishing from Windows CI after compile and smoke tests pass
- added source-package generation to the release pipeline and expanded source validation
- finalized the reference-locked 1380×950 shell and 214/42/68 DIP global geometry
- retained native WPF controls for every reference screen; no screenshot backgrounds are used
- hardened product/service right-side editor drawers and full module navigation behavior
- removed hard-coded visible release labels from the shell in favor of runtime assembly version reporting
- synchronized Desktop, Setup and Updater package versions to 0.9.0
- added stricter static XAML/event-handler/name validation and release artifact acceptance checks
- kept portable data isolation in `MYWO-Data` and installed data isolation in `%APPDATA%\MYWO`
- kept uninstall integrated into `MYWO-Update.exe --uninstall` with no standalone uninstaller executable
- refreshed pixel-parity and Windows release documentation

## 0.7.0

- recalibrated the WPF shell to the approved reference geometry and spacing
- recalibrated product and service toolbars to the two-row reference composition
- widened the product/service editor drawers to 422/420 DIP and refined drawer surfaces
- reworked Categories & Brands into a reference-matched split layout
- reworked API Keys into KPI/table/quick-actions/endpoints composition
- reworked Administration settings into company/system/audit composition
- added native-style toggle switches for reference-matched settings and editor controls
- removed the visible legacy status bar from the reference shell
- added `docs/PIXEL-PARITY.md` and stronger screenshot-geometry validation
- aligned core semantic colors with the approved MYWO design-system palette
- removed title-strip branding that was not present in the reference screens
- enabled layout rounding and device-pixel snapping for sharper 1 px geometry
- added Windows 11 dark/rounded DWM integration with safe fallback
- added portable-mode path isolation through `MYWO-Data` beside `MYWO-Portable.exe`
- added dedicated self-contained `MYWO-Setup.exe` project
- added dedicated `MYWO-Update.exe` project with SHA-256 verified update packages
- integrated uninstall into `MYWO-Update.exe --uninstall`; no standalone uninstaller binary is shipped
- added Windows Installed Apps registration, Start Menu/Desktop shortcuts and per-user installation
- added update controls to the Settings screen
- replaced the Inno Setup dependency with MYWO's own setup/update pipeline
- release script now outputs Setup, Portable, Update, update manifest, payload and SHA256 sums


## 0.5.0

- added real multi-select bulk price changes for products and services
- bulk pricing supports percentage changes, fixed adjustments and exact values
- bulk pricing can target MPC, anchor price or both and supports configurable rounding
- added scheduled future price changes with per-item auditability
- added pending price schedule panel and cancellation workflow in Price Changes
- due scheduled prices are applied automatically while MYWO is running
- added safe one-click undo for a selected price-history entry, guarded against overwriting newer edits
- extended price history with batch ID, source and note metadata
- added `price_schedules` table and non-destructive database migration to `user_version=5`
- expanded audit and in-app notifications for bulk, scheduled and reverted price changes
- added a dedicated MYWO bulk-price dialog with immediate/scheduled execution modes
- updated release, installer and documentation to 0.5.0

## 0.4.0

- added product descriptions, local product images, image preview and product-table thumbnails
- added service notes
- added hierarchical categories with parent relations, descriptions and cycle protection
- added brand descriptions
- added scoped API keys and redesigned API key creation dialog
- added X-API-Key and Bearer token authentication support
- added API request telemetry (status, route, duration, key usage) and real usage statistics
- added products/services API pagination
- added in-app notification center with unread badge and per-company preference
- added scheduled daily publication while the desktop application is running
- added next-publication status to the publication center
- added XML/CSV/API live preview modes
- added archive snapshot restore with SHA-256 verification
- expanded XML/CSV export and CSV import with descriptions/notes
- fixed publication settings Save action so it persists the controls shown on the Publication screen
- migrated database schema non-destructively to user_version 4
- updated installer, release script and project version to 0.4.0

## 0.3.0

- transferred the approved MYWO dark visual language directly into the WPF application
- added custom Windows title bar, app toolbar and active navigation state
- rebuilt dashboard with KPI cards, recent publications, quick actions and real price-history chart
- rebuilt Products screen to match reference layout and data density
- rebuilt Services screen and editing workflow
- rebuilt Categories/Brands management presentation
- rebuilt Publication center with status cards, XML preview, generated file list and integrity action
- rebuilt API key screen with endpoint reference and one-time key success dialog
- rebuilt Archive with search/filter, selected publication details and file actions
- rebuilt Price Changes with real summary calculations and responsive chart rendering
- visually unified Audit, Companies and Settings into the Administration module
- added global search propagation and Ctrl+K shortcut
- added products CSV export
- added services CSV export
- added latest-publication SHA-256 verification action
- added catalog endpoint copy action
- restyled Product, Service, Category/Brand, Company and Prompt dialogs
- updated versioning and release defaults to 0.3.0

## 0.2.0

- migrated target from .NET 8 to .NET 10 LTS
- updated Microsoft.Data.Sqlite to 10.0.12
- replaced HttpListener with localhost-only ASP.NET Core Kestrel API
- added non-destructive database schema migration support
- added company settings (publish folder, active-only publish, API port, currency)
- added price history and audit log
- added advanced product/service filters
- added bulk activate/deactivate
- added duplicate product/service actions
- added CSV import with upsert behavior and per-row error reporting
- added category/brand auto-creation during import
- added product code/barcode uniqueness checks
- added stronger OIB checksum validation and website validation
- added publication file paths and SHA-256 verification from UI
- added stable-file replacement through temporary files
- added database backup/restore and SQLite integrity check
- added application error logging and log rotation
- added generic local API error responses without leaking internal exception details
- hardened backup restore by removing stale SQLite WAL/SHM sidecars
- added keyboard shortcuts and redesigned navigation
- added self-contained x64/ARM64 publish profiles and Windows release script
- added Inno Setup configuration for installer/uninstall
- expanded API, CSV, architecture and release documentation
