# MYWO Price Automation

## Masovne promjene

Na ekranima **Proizvodi** i **Usluge** moguće je označiti više redaka i pokrenuti **Promijeni cijene**.

Podržani načini:

- postotna promjena, npr. `+5%` ili `-10%`
- fiksna korekcija, npr. `+1,50 EUR` ili `-0,20 EUR`
- postavljanje točne vrijednosti

Promjena se može primijeniti na MPC, sidrenu cijenu ili oba polja. Rezultat se može zaokružiti na 0,01 €, 0,05 €, 0,10 € ili 1,00 €.

Svaka stvarna izmjena zapisuje se u `price_history` s `batch_id`, `source` i `note` podacima te u audit log.

## Planirane promjene

Masovna operacija može se spremiti kao buduća promjena. MYWO tada sprema konkretne buduće cijene u `price_schedules`.

Dok je desktop aplikacija pokrenuta, interni timer provjerava dospjele promjene i primjenjuje ih. Svaka uspješno izvršena planirana promjena:

1. ažurira proizvod ili uslugu,
2. zapisuje novu stavku u povijest cijena sa `source=scheduled`,
3. označava raspored kao `applied`,
4. dodaje audit zapis i lokalnu obavijest.

Planirana promjena može se otkazati dok je status `pending`.

## Siguran povrat cijene

Na ekranu **Promjene cijena** moguće je vratiti odabranu povijesnu promjenu. Povrat nije slijepo prepisivanje: MYWO prvo provjerava odgovara li trenutna cijena vrijednosti nastaloj tom povijesnom promjenom.

Ako je stavka nakon toga ponovno uređena, povrat se odbija kako se novija promjena ne bi izgubila. Uspješan povrat stvara novu povijesnu stavku sa `source=undo` i audit zapis.

## Baza podataka

`user_version=5` dodaje:

- `price_history.batch_id`
- `price_history.source`
- `price_history.note`
- tablicu `price_schedules`

Migracija ne briše postojeće podatke.
