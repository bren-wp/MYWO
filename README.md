# MYWO Windows App v0.9.0

MYWO je Windows desktop aplikacija za centralizirano upravljanje proizvodima, uslugama i cjenicima, generiranje strukturiranih XML/CSV datoteka te lokalnu integraciju putem REST API-ja.

Ova verzija prenosi odobreni MYWO vizualni smjer iz referentnih ekrana izravno u stvarni WPF projekt. UI više nije generički prototip: glavni prozor, navigacija, dashboard, tablice, publikacija, API, arhiva, analitika cijena i administracija imaju isti dizajnerski sustav, gustoću, raspored, stanja i komponentni jezik kao referentni prikazi.

## Tehnologija

- Windows 10/11
- .NET 10 LTS + WPF
- SQLite (`Microsoft.Data.Sqlite` 10.0.12)
- ASP.NET Core Kestrel za localhost REST API
- podaci: `%APPDATA%\MYWO\mywo.db`
- log: `%APPDATA%\MYWO\logs\mywo.log`
- bez vanjskog UI frameworka i bez web-runtime ovisnosti

## Što je novo u v0.9

### Reference-locked UI i Windows release

- glavni prozor je ponovno kalibriran prema odobrenim referentnim slikama: 1380×950 početna veličina, 214 px sidebar, 42 px title strip i 68 px app toolbar
- title strip je vizualno prazan kao na referencama; MYWO znak ostaje u sidebaru
- design tokeni su dodatno kalibrirani prema referencama (`#071321`, `#0D1D30`, `#23415F`, `#0BA6FF → #1B4DE8`, `#22C55E`, `#F59E0B`, `#EF4444`)
- Windows 11 DWM dark mode i rounded-corner preference primjenjuju se kada ih OS podržava
- layout koristi rounding/snapping kako bi rubovi, tablice i 1 px separatori ostali oštri na 100–200% DPI skaliranju
- uklonjen je vidljivi legacy status-bar kako bi vertikalna geometrija odgovarala referentnim ekranima
- Proizvodi i Usluge dobili su dvorazinske filter/action alatne trake i kalibrirane desne edit drawere (422/420 px)
- Kategorije i brendovi sada imaju lijevi hijerarhijski pregled i odvojene desne tablice, kao u referentnom rasporedu
- API ključevi sada prate referentni raspored: KPI kartice, tablica ključeva, brze akcije i endpoint panel
- Administracija/Postavke preuređena je u referentni raspored s karticama tvrtki, sistemskim postavkama i audit pregledom
- uveden je nativno stiliziran toggle switch za sve prekidače koji su na referencama prikazani kao Windows 11 switch kontrole
- dodan je `docs/PIXEL-PARITY.md` i stroži source validator koji zaključava ključne geometrijske tokene
- dodana je prava portable detekcija: `MYWO-Portable.exe` sprema bazu i logove u lokalni `MYWO-Data` uz izvršnu datoteku
- instalirana verzija i dalje koristi korisnički `%APPDATA%\MYWO`

### Setup, update i integrirani uninstall

Release sada proizvodi točno tri glavna Windows izvršna artefakta:

- `MYWO-Setup.exe` – per-user setup bez administratorskog prompta
- `MYWO-Portable.exe` – jedan self-contained portable executable
- `MYWO-Update.exe` – updater i integrirani uninstaller

Setup registrira MYWO u Windows *Installed apps* i za uninstall koristi `MYWO-Update.exe --uninstall`. Instalacija i nadogradnja koriste validirani staging i rollback-safe zamjenu instalacijske mape. Ne isporučuje se zaseban `uninstall.exe`/`unins000.exe`. Updater podržava SHA-256 provjeru update paketa, privremeni staging, zamjenu datoteka i opcionalno uklanjanje korisničkih podataka pri deinstalaciji.

## Postojeće produkcijske funkcije

- više tvrtki s izolacijom preko `company_id`
- proizvodi, usluge, kategorije i brendovi
- povijest MPC i sidrenih cijena
- audit log
- CSV import s upsert pravilima
- XML/CSV objava i verzionirane datoteke
- stabilni `cjenik.xml` i `cjenik.csv`
- SHA-256 svake objave
- lokalni REST API na `127.0.0.1`
- API ključevi pohranjeni kao hash
- backup/restore baze
- WAL checkpoint i SQLite integrity check
- lokalni rotirajući error log

## Lokalni REST API

- `GET /health`
- `GET /api/v1/company`
- `GET /api/v1/products`
- `GET /api/v1/services`
- `GET /api/v1/categories`
- `GET /api/v1/brands`
- `GET /api/v1/catalog`

Sve `/api/v1/*` rute zahtijevaju API ključ putem `X-API-Key` ili `Authorization: Bearer`. Ključevi imaju granularne read dozvole, a API je vezan isključivo na `127.0.0.1`.

Detalji: `docs/API.md`.

Vizualna matrica referentnih ekrana: `docs/REFERENCE-MATRIX.md`.

## Build

Potreban je .NET 10 SDK na Windowsu.

```powershell
dotnet restore .\src\MYWO.Desktop\MYWO.Desktop.csproj
dotnet build .\src\MYWO.Desktop\MYWO.Desktop.csproj -c Release
```

Automatizirani release:

```powershell
.\scripts\build-release.ps1 -Version 0.9.0
```

Skripta izrađuje `MYWO-Setup.exe`, `MYWO-Portable.exe`, `MYWO-Update.exe`, update payload, `update.json`, source ZIP i `SHA256SUMS.txt`. GitHub Actions nakon uspješnog compile/smoke-test prolaza automatski objavljuje release `v0.9.0`. Inno Setup više nije potreban jer MYWO ima vlastiti setup/update/uninstall sloj.

## Nadogradnja s v0.1/v0.2

Baza se ne resetira. v0.9 koristi postojeću `user_version=5` shemu i zadržava postojeću v0.1–v0.5 bazu bez brisanja postojećih podataka. Prije prve produkcijske nadogradnje ipak napravite sigurnosnu kopiju `%APPDATA%\MYWO\mywo.db`.

## Regulatorna napomena

XML/CSV je i dalje MYWO aplikacijski format. Ova verzija ne tvrdi da je određena službena hrvatska regulatorna XML shema formalno certificirana. Prije označavanja proizvoda kao regulatorno certificiranog format treba mapirati i validirati prema tada važećoj službenoj specifikaciji.

## Validacija u ovom paketu

Windows release se gradi kroz `windows-latest` CI workflow. Lokalna Linux radna okolina i dalje ne pokreće WPF runtime, pa runtime validaciju treba temeljiti na CI build rezultatu i stvarnom Windows testu. Napravljene su statičke provjere XAML-a, svih UI event handlera, `x:Name` veza i strukture paketa. `scripts/build-release.ps1` ostaje obavezna završna build provjera na Windows računalu.
