# Validation – MYWO v0.5.0

## Provjere izvršene u ovom razvojnom okruženju

- svi WPF `.xaml` dokumenti prolaze XML parser: PASS
- svi event handleri navedeni u XAML-u postoje u pripadajućem code-behindu: PASS
- nema dupliciranih `x:Name` vrijednosti unutar XAML dokumenata: PASS
- svi `AppDb.*` pozivi iz ostatka projekta imaju odgovarajuću javnu metodu: PASS
- nova SQLite v0.5 shema izvršava se u SQLite engineu: PASS
- simulirana migracija sheme v0.4 → v0.5 zadržava postojeću povijest cijena i dodaje `price_schedules` te nova metadata polja: PASS
- provjereni novi stupci: category hierarchy/description, brand description, product description/image, service notes, API permissions, scheduled publish i notification settings: PASS
- provjerene nove tablice `api_request_log` i `notifications`: PASS
- projekt, release skripta i installer usklađeni na verziju `0.5.0`: PASS
- XML/CSV/API preview gumbi imaju stvarne event handlere: PASS
- publish-screen gumb `Spremi` sada sprema upravo publish kontrole, a ne zasebne settings kontrole: PASS

## Ograničenje okruženja

U ovom Linux razvojnom okruženju nije instaliran Windows .NET SDK/MSBuild/WPF toolchain. Zbog toga ovdje nije izvršen pravi Windows `dotnet build`, `dotnet publish` niti runtime screenshot test. To nije označeno kao PASS.

Završna obavezna provjera na Windows računalu:

```powershell
.\scripts\build-release.ps1 -Version 0.5.0
```

Nakon builda testirati najmanje: clean install, upgrade postojeće v0.1/v0.2/v0.3/v0.4 baze, CRUD svih entiteta, izbor/uklanjanje slike proizvoda, CSV round-trip, XML/CSV/API preview, ručnu i automatsku objavu, restore arhivske verzije, SHA-256, scoped API ključeve, Bearer i X-API-Key autentifikaciju, request statistiku, notification centar, backup/restore, port conflict te DPI 100/125/150/200%.

## v0.5 dodatne provjere

- XAML parsiranje uključuje novi `BulkPriceDialog` i nadograđeni Price Changes ekran.
- Provjeriti migraciju v4 → v5 i očuvanje postojećih zapisa.
- Provjeriti bulk +5%, bulk fiksnu korekciju i exact-set na više proizvoda/usluga.
- Provjeriti pending → applied tijek planirane cijene.
- Provjeriti otkazivanje pending rasporeda.
- Provjeriti da Undo odbija povrat ako je stavka nakon odabrane promjene ponovno uređena.
