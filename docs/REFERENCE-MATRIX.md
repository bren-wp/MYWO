# MYWO v0.9 – reference parity matrix

Referentni ekrani iz razgovora ostaju vizualni source of truth. UI je implementiran kao stvarni WPF, bez korištenja screenshotova kao pozadine.

| Referentni ekran | Implementirani modul | Ključni zaključani elementi |
|---|---|---|
| Pregled | `DashboardPanel` | 6 KPI kartica, 30-dnevni graf, povijest objava, brzi podaci/akcije, value card |
| Proizvodi | `ProductsPanel` | 2-redna alatna traka, tablica, 422 DIP desni drawer, CSV i masovne radnje |
| Usluge | `ServicesPanel` | filteri, tablica, 420 DIP desni drawer, posebna prodaja i sidrena cijena |
| Kategorije i brendovi | `CategoriesPanel` / `BrandsPanel` | hijerarhija kategorija, odvojene tablice, uređivanje |
| Objava | `PublishPanel` | status kartice, XML/CSV/Sve/Integritet, preview, postavke, generirane datoteke |
| API ključevi | `ApiPanel` | KPI kartice, key table, one-time secret, endpoint reference |
| Arhiva | `HistoryPanel` | filteri, arhivska tablica, detalji, datoteka, integrity/restore akcije |
| Promjene cijena | `PriceChangesPanel` | KPI, graf, detaljna povijest, planirane promjene |
| Administracija | `SettingsPanel` / `CompaniesPanel` / `AuditPanel` | Audit/Tvrtke/Postavke, company cards, backup, obavijesti, update |

## Globalna geometrija

- početni shell: **1380 × 950 DIP**
- custom title strip: **42 DIP**
- sidebar: **214 DIP**
- top command bar: **68 DIP**
- content inset: **20 DIP**
- product drawer: **422 DIP**
- service drawer: **420 DIP**
- data row/header: **42 DIP**
- cards: **10 DIP radius**
- buttons: **8 DIP radius**

Na Windowsu treba validirati screenshot pri 100% scaleu; na drugim DPI vrijednostima prioritet imaju čitljivost i funkcionalnost uz zadržavanje istog dizajnerskog sustava.
