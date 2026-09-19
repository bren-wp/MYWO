# MYWO CSV format — v0.5

MYWO exports UTF-8 CSV with a semicolon (`;`) separator. The exported file can be imported again. Import also falls back to Windows-1250 for legacy Croatian Excel/CSV files that are not valid UTF-8.

Core columns:

`vrsta;naziv;sifra;marka;kategorija;jedinica_mjere;cijena_po_jedinici;maloprodajna_cijena;valuta;poseban_oblik_prodaje;naziv_posebnog_oblika_prodaje;sidrena_cijena;barkod;dostupnost;opis_napomena;aktivno;zadnja_izmjena`

`opis_napomena` contains the product description for `proizvod` rows and the service note for `usluga` rows.

For import, `naziv` and `maloprodajna_cijena` are required. If `vrsta` is omitted, MYWO treats the row as a product.

Accepted `vrsta` values: `proizvod`, `usluga`, `product`, `service`.

MYWO updates a product by the first matching identifier in this order: product code, barcode, then exact name. A service is updated by exact name. Missing categories and brands from an imported file are created automatically.

Boolean values accept common variants such as `da/ne`, `true/false`, `1/0`, and `yes/no`.
