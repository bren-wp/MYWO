# MYWO UI implementation map – v0.9.4

Ovaj dokument opisuje kako su referentni MYWO ekrani preneseni u stvarni WPF UI.

## Globalni shell

- tamna MYWO paleta, plavo-cijan gradijent, semantičke statusne boje
- vektorski MYWO znak u title baru i sidebaru
- vlastita title traka s minimize/maximize/close kontrolama
- lijevi sidebar: Pregled, Proizvodi, Usluge, Kategorije, Brendovi, Objava, API ključevi, Arhiva, Promjene cijena, Audit, Tvrtke, Postavke
- top bar: izbor tvrtke, globalna pretraga, obavijesti i `Objavi cjenik`
- aktivna navigacija je vizualno istaknuta identično kroz sve module

## Reference geometry lock

The v0.9 shell is intentionally calibrated to the supplied 1440×1080 reference captures rather than generic WPF defaults:

- initial application window: 1380×950 DIP
- sidebar: 214 DIP
- custom title strip: 42 DIP
- app toolbar: 68 DIP
- content horizontal margin: 20 DIP
- reference card radius: 9 DIP
- standard data row/header: 42 DIP
- product edit drawer: 422 DIP
- service edit drawer: 420 DIP

`UseLayoutRounding` and `SnapsToDevicePixels` are enabled to prevent fractional borders at Windows DPI scaling. On Windows 11 the window also requests native rounded corners and immersive dark chrome through DWM.

The title strip is deliberately empty except for Minimize/Maximize/Close because that is how the supplied reference screens are composed; the primary MYWO wordmark remains in the sidebar.

## Pregled

Referenca: dashboard s KPI karticama, grafom i poviješću objava.

Implementacija:
- proizvodi / aktivni proizvodi
- usluge / aktivne usluge
- kategorije
- brendovi
- aktivni API ključevi
- zadnja objava
- stvarni 30-dnevni graf iz `price_history`
- zadnjih pet objava
- brzi podaci i brze akcije

## Proizvodi

Referenca: filter toolbar, detaljna tablica i desni edit drawer.

Implementacija:
- filteri status / dostupnost / kategorija / brend
- pretraga
- tablica polja koja ulaze u MYWO katalog
- CSV import/export
- novi, edit, duplicate, bulk active/inactive, delete
- redizajnirani modal koristi isti raspored i design tokens kao referentni drawer

## Usluge

Referenca: servisna tablica s posebnim oblikom prodaje i edit drawerom.

Implementacija:
- pretraga, kategorija i status
- MPC, posebna prodaja, naziv posebne prodaje, sidrena cijena, status, zadnja izmjena
- CSV import/export
- redizajnirani edit prozor

## Kategorije i brendovi

Referenca: objedinjeni pogled kategorija i brendova.

Implementacija:
- kategorije kao glavni panel
- brendovi kao paralelni panel
- zasebna brend stranica ostavljena za puni pregled
- novi MYWO dijalozi za unos i uređivanje

## Objava

Referenca: statusne kartice, četiri glavne akcije, preview, postavke i generirane datoteke.

Implementacija:
- status objave
- aktivna tvrtka
- izlazna putanja
- API status
- SHA-256 integritet
- XML / CSV / sve / provjera integriteta
- stvarni pregled `cjenik.xml` nakon objave
- zadnje generirane datoteke
- publish folder i active-only opcija
- pokretanje i zaustavljanje localhost API-ja

## API ključevi

Referenca: statistike, lista ključeva, endpoint dokumentacija i one-time success modal.

Implementacija:
- aktivni ključevi
- ključni endpointi
- status lokalnog API-ja
- kreiranje i opoziv
- kopiranje endpointa
- one-time prikaz punog ključa u vlastitom MYWO dijalogu

## Arhiva

Referenca: filteri, arhivska tablica, detalji datoteke i akcije.

Implementacija:
- pretraga po nazivu/SHA-256
- filter format XML/CSV
- detalji odabrane objave
- putanja, format, broj stavki i hash
- provjera integriteta
- otvaranje datoteke / mape

## Promjene cijena

Referenca: 4 KPI kartice, trend graf i detaljna tablica.

Implementacija:
- broj povećanja
- broj smanjenja
- prosječna postotna promjena
- broj zahvaćenih stavki
- stvarni graf iz `price_history`
- detaljna MPC/sidrena povijest

## Administracija

Referenca: Audit / Tvrtke / Postavke kao jedinstveni administracijski kontekst.

Implementacija:
- tri vizualno povezana modula
- pregled i uređivanje tvrtki
- publish path, API port, valuta i publish behavior
- backup/restore
- SQLite integrity check
- audit zapis

## Dizajnerske konstante

Glavne boje nalaze se u `App.xaml`. Nema vanjskog UI frameworka; sve komponente su WPF-native kako bi se zadržala kontrola nad izgledom, performansama i distribucijom.
