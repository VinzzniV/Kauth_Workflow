# Setup

## Struktur

Das Repo trennt jetzt nach Verantwortung statt nach historischen Szenarien:

- `compose.yml`
  Gemeinsame Container-Basis fuer `db`, `api` und `web`.
- `compose.dev-db.yml`
  Lokale Entwicklungs-Ergaenzung nur fuer PostgreSQL mit Dev-Init und Host-Port `25432`.
- `compose.prod.yml`
  Serverseitiges Override fuer den Linux-Stack mit Entra-Auth, produktivem DB-Init und Caddy als HTTPS-Reverse-Proxy.
- `.env.prod.example`
  Vorlage fuer die serverseitige Konfiguration. Die echte Datei heisst `.env.prod` und bleibt lokal.
- `web/.env.local`
  Lokale Frontend-Entwicklung. Wird nicht eingecheckt.

## Lokal entwickeln

### 1. Datenbank starten

```powershell
docker compose -f compose.yml -f compose.dev-db.yml up -d db
```

Die lokale DB laeuft dann auf `localhost:25432` mit:

- Datenbank: `appdb`
- Benutzer: `app`
- Passwort: `app_pw`

### 2. API lokal starten

```powershell
dotnet run --project api/API/API.csproj --launch-profile API
```

Der Launch-Profile `API` setzt lokal bereits:

- `AUTH_MODE=demo`
- `ConnectionStrings__Default=Host=localhost;Port=25432;...`
- `PUBLIC_BASE_URL=http://localhost:5173`
- CORS / Notification-Frontend auf `http://localhost:5173`

Damit ist fuer lokale Entwicklung kein zusaetzliches Shell-Environment noetig.

### 3. Web lokal starten

In `web/.env.local` reicht im Normalfall:

```env
VITE_API_PROXY_TARGET=http://127.0.0.1:5001
VITE_AUTH_MODE=demo
```

Dann:

```powershell
cd web
npm install
npm run dev
```

Das Frontend laeuft auf `http://localhost:5173` und nutzt den Vite-Proxy auf die lokal gestartete API.

### Lokaler Auth-Modus

- Lokal ist `AUTH_MODE=demo` der Standard.
- Gesteuert wird das fuer die API ueber `api/API/Properties/launchSettings.json`.
- Das Frontend nutzt lokal `web/.env.local` als Fallback und laeuft standardmaessig ebenfalls im Demo-Modus.
- Fuer lokalen Entra-Test kann das Frontend optional in `web/.env.local` auf `VITE_AUTH_MODE=entra` gestellt werden; serverseitig bleibt der echte Entra-Betrieb aber der servernahe Stack.

## Linux-VM deployen

### 1. Konfiguration anlegen

```bash
cp .env.prod.example .env.prod
```

Pflichtwerte in `.env.prod`:

- `PUBLIC_HOSTNAME`
- `PUBLIC_BASE_URL`
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `ENTRA_TENANT_ID`
- `ENTRA_CLIENT_ID`
- `ENTRA_AUDIENCE`

Optional bzw. je nach Betrieb:

- `ENTRA_CLIENT_SECRET`
- `GRAPH_CLIENT_SECRET`
- `DIRECTORY_GROUP_PREFIX`
- `DIRECTORY_EXPLICIT_GROUP_IDS`
- `DIRECTORY_SYNC_SCHEDULED`
- `DIRECTORY_SYNC_INTERVAL_MINUTES`
- `WEB_BIND_HOST`
- `WEB_HTTP_PORT`
- `WEB_HTTPS_PORT`

Wichtig:

- `PUBLIC_BASE_URL` muss exakt zur oeffentlichen HTTPS-URL passen.
- Dieselbe URL muss in Entra als SPA-Redirect-URI gepflegt sein.
- Ohne `ENTRA_CLIENT_SECRET` bleibt Login funktionsfaehig, aber Directory-Sync / Graph-Zugriffe koennen Warnungen erzeugen.

### 2. Stack starten

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

### 3. Status pruefen

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f
```

Sinnvolle Checks nach dem Deploy:

```bash
curl -k https://<PUBLIC_HOSTNAME>/api/health/live
curl -k https://<PUBLIC_HOSTNAME>/api/health
curl -k -i https://<PUBLIC_HOSTNAME>/api/me
```

Erwartung:

- `/api/health/live` liefert `200`
- `/api/health` liefert `200` oder `503` mit Health-JSON, aber niemals `404`
- `/api/me` liefert ohne Login in Entra typischerweise `401`, aber niemals `404`

## Serverseitiger Auth-Modus

- Der servernahe Stack setzt im Compose-Override hart `AUTH_MODE=entra`.
- Die API erwartet in diesem Modus `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID` und `ENTRA_AUDIENCE`.
- Das Web bekommt seine Laufzeitkonfiguration ueber `app-config.js`, das beim Containerstart aus den Container-Umgebungsvariablen erzeugt wird.
- Dadurch braucht das Web fuer URL-/Entra-Aenderungen keinen Neubuild mehr.

## HTTPS / Caddy

- `compose.prod.yml` bringt einen repo-eigenen Caddy-Reverse-Proxy mit.
- Caddy nutzt standardmaessig `tls internal` fuer LAN-/Testbetrieb.
- Clients im Netz muessen dem internen Caddy-Root-Zertifikat vertrauen, sonst schlagen Browser-Warnungen und Entra-Redirects fehl.
- Die oeffentlichen API-Checks laufen ueber `https://<PUBLIC_HOSTNAME>/api/...`.
- Der Proxy strippt intern den `/api`-Praefix, die API selbst bleibt auf Root-Routen wie `/health` und `/me`.

## Ersetzt / entfernt

Diese Dateien wurden durch die neue Struktur ersetzt:

- `docker-compose.yml` -> `compose.yml`
- `docker-compose.prod.yml` -> `compose.prod.yml`
- `docker-compose.prod.env.example` -> `.env.prod.example`
- `docker-compose.demo.yml` entfernt
- `docker-compose.local-entra.yml` entfernt
- `docker-compose.local-entra.env.example` entfernt
- `start-demo.ps1` entfernt
- `start-local-entra.ps1` entfernt
- `start-prod.ps1` entfernt
- `ENVIRONMENTS.md` entfernt
- `docs/ENTRA_SETUP.md` entfernt

## Wichtige Konfigurationsschalter

### Backend

- `AUTH_MODE`
  Erlaubte Werte: `demo`, `entra`, `dual`.
- `ConnectionStrings__Default`
  Bevorzugter DB-Connection-String.
- `CONNECTION_STRING`
  Nur noch Legacy-Fallback.

### Frontend

- Lokal: `web/.env.local`
- Serverseitig: Runtime-Config in `app-config.js`

Wichtige Frontend-Werte:

- `apiBase`
- `authMode`
- `entraClientId`
- `entraTenantId`
- `entraRedirectUri`
