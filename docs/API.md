# MYWO local API — v0.5

Ugrađeni API namjerno sluša samo na `127.0.0.1`. Namijenjen je lokalnoj integraciji desktop aplikacije. Za javni internetski API koristite zaseban HTTPS deployment iza odgovarajućeg reverse proxyja, autentifikacije i rate limiting pravila.

## Autentifikacija

`GET /health` je javan. Sve `/api/v1/*` rute zahtijevaju aktivan MYWO API ključ. Podržana su oba oblika:

```http
X-API-Key: mywo_sk_live_REPLACE_ME
```

ili:

```http
Authorization: Bearer mywo_sk_live_REPLACE_ME
```

Puni ključ prikazuje se samo pri generiranju. U SQLite bazi pohranjuje se samo SHA-256 hash i kratki prefiks.

## Dozvole

- `company.read` — podaci aktivne tvrtke
- `taxonomy.read` — kategorije i brendovi
- `products.read` — proizvodi
- `services.read` — usluge
- `catalog.read` — kombinirani katalog; također dopušta products/services/taxonomy read endpointove
- `all` — svi read endpointi

## Endpointi

- `GET /health`
- `GET /api/v1/company`
- `GET /api/v1/products`
- `GET /api/v1/services`
- `GET /api/v1/categories`
- `GET /api/v1/brands`
- `GET /api/v1/catalog`

### Product filters

`/api/v1/products` prihvaća `q`, `availability`, `categoryId`, `brandId`, `includeInactive=true`, `page` i `pageSize`. `pageSize` je ograničen na 1–500. Odgovor sadrži `data` te `meta.total`, `meta.page` i `meta.pageSize`.

### Service filters

`/api/v1/services` prihvaća `q`, `categoryId`, `includeInactive=true`, `page` i `pageSize`.

## Primjer

```powershell
$headers = @{ "Authorization" = "Bearer mywo_sk_live_REPLACE_ME" }
Invoke-RestMethod "http://127.0.0.1:8787/api/v1/catalog" -Headers $headers
```

## Telemetrija lokalnog API-ja

MYWO lokalno evidentira metodu, putanju, HTTP status, trajanje i API ključ koji je autorizirao zahtjev. Ne zapisuje plaintext API ključ niti sadržaj Authorization headera. Statistika se koristi za prikaz broja zahtjeva i postotka uspješnih zahtjeva u aplikaciji.

CORS nije uključen prema zadanim postavkama. To smanjuje izloženost localhost API-ja proizvoljnim web stranicama.
