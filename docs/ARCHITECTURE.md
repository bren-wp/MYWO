# MYWO Desktop architecture — v0.5

## Runtime

- Windows desktop UI: WPF
- Runtime: .NET 10 LTS
- Local database: SQLite via Microsoft.Data.Sqlite
- Local REST API: ASP.NET Core Kestrel bound to `127.0.0.1`
- Data location: `%APPDATA%\MYWO\mywo.db`
- Product media: `%APPDATA%\MYWO\media\company-<id>\products\`
- Log: `%APPDATA%\MYWO\logs\mywo.log`

## Data isolation

All catalog entities use a `company_id` scope. CRUD queries include the selected company. Category and brand ownership is verified before linking an item, preventing cross-company relationships even if an invalid ID is supplied programmatically.

Categories can reference a parent category. Application validation blocks self-parenting and circular category graphs. Deleting a category detaches direct children before deletion, which also preserves correct behavior for databases upgraded from versions created before the `parent_id` column existed.

## Database upgrades

`AppDb.Initialize()` uses idempotent `CREATE TABLE IF NOT EXISTS` operations and additive `EnsureColumn` migrations. v0.5 sets SQLite `user_version=5` and upgrades existing v0.1–v0.4 databases without resetting user data.

v0.4 persistence includes product descriptions/images, service notes, category hierarchy/descriptions, brand descriptions, API scopes, scheduled-publication preferences, in-app notification preferences, `api_request_log`, and `notifications`.

## Auditability

`price_history` stores retail/anchor price changes. `audit_log` records major CRUD, publish, API-key, settings, scheduled-publication, restore and bulk-status operations. Publication records contain the file path, item count, timestamp and SHA-256 digest.

The local API records method, path, status code, duration and the authorizing API-key ID. Plaintext keys and Authorization header values are never written to request logs. Request telemetry is retained for 90 days.

## Publishing

MYWO writes timestamped XML and CSV files, then refreshes stable `cjenik.xml` and `cjenik.csv` names. Temporary files are used while versioned files are generated, reducing the chance of a partially written result after failure.

Archived XML/CSV versions can be restored as the active stable file only after SHA-256 verification succeeds. Daily scheduled publication runs while the desktop application is running and creates both XML and CSV outputs.

The current XML/CSV structure is a MYWO application format. It must not be treated as certification that a specific external legal or regulatory schema has been satisfied unless that schema has been formally mapped and validated.

## API security

API keys are generated from cryptographically secure random bytes. Only the SHA-256 hash and a short prefix are stored. Keys have read scopes and can be revoked immediately. Authentication accepts `X-API-Key` or `Authorization: Bearer`.

The API is localhost-only by default, CORS is not enabled, and unhandled exceptions are logged locally while clients receive a generic `internal_error` response.

## Automatizacija cijena (v0.5)

Masovne i planirane promjene cijena implementirane su u `AppDb` kao transakcijske operacije. `price_schedules` sprema konkretne buduće MPC/sidrene cijene, a desktop timer poziva `ApplyDuePriceSchedules`. Povrat cijene koristi optimističku sigurnosnu provjeru: trenutna cijena mora odgovarati `new_*` vrijednostima odabrane povijesne stavke prije nego se vrati `old_*` stanje.
