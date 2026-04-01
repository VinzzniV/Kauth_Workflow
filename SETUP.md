# Setup

Diese Datei beschreibt den aktuellen Betriebs- und Entwicklungsweg des Projekts. Sie ist die operative Doku fuer lokale Entwicklung, servernahes Deployment und Laufzeitkonfiguration.

## Repo-Struktur Fuer Betrieb

- `compose.yml`
  Gemeinsame Compose-Basis fuer `db`, `api` und `web`.
- `compose.dev-db.yml`
  Lokales Override nur fuer PostgreSQL mit Dev-Init und Host-Port `25432`.
- `compose.prod.yml`
  Produktionsnahes Override mit Entra-Auth, produktivem DB-Init und Caddy-Reverse-Proxy.
- `.env.prod.example`
  Vorlage fuer produktive Laufzeitvariablen.
- `web/.env.local`
  Lokale Frontend-Entwicklung. Bleibt ungeversioniert.

## Lokale Entwicklung

Der normale lokale Weg ist:
1. Datenbank per Docker
2. API lokal per `dotnet run`
3. Frontend lokal per Vite

Ein kompletter lokaler Docker-Vollstack ist fuer den Standard-Workflow nicht noetig.

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

Das Launch-Profil `API` setzt lokal bereits:

- `ASPNETCORE_ENVIRONMENT=Development`
- `AUTH_MODE=dev-sim`
- `ConnectionStrings__Default=Host=localhost;Port=25432;...`
- `PUBLIC_BASE_URL=http://localhost:5173`
- `Cors__AllowedOrigins__0=http://localhost:5173`
- `NotificationEmail__FrontendBaseUrl=http://localhost:5173`
- `DIRECTORY_GROUP_PREFIX=Onboarding-App-`
- `DIRECTORY_SYNC_SCHEDULED=true`

Die API laedt beim Start optional eine `.env.prod` aus dem aktuellen oder einem uebergeordneten Verzeichnis. Das dient lokal als Fallback fuer fehlende `ENTRA_*`- oder Directory-Werte. Vorhandene Shell- oder Launch-Profile-Werte behalten Vorrang.

Fuer lokalen Verzeichnis-Sync braucht die API echte Entra-Zugangsdaten, entweder in der Shell, in Windows-Benutzervariablen oder in `.env.prod`:

```powershell
$env:ENTRA_TENANT_ID = "<tenant-id>"
$env:ENTRA_CLIENT_ID = "<app-id>"
$env:ENTRA_CLIENT_SECRET = "<client-secret-value>"
$env:ENTRA_AUDIENCE = "api://<app-id>"
```

Hinweis:

- Directory-Sync, Graph und Mailversand nutzen dieselbe `ENTRA_TENANT_ID` und `ENTRA_CLIENT_ID` wie die API-Auth.
- `GRAPH_CLIENT_SECRET` ist nur ein optionaler Runtime-Override fuer Graph-basierte Dienste, nicht ein separates zweites App-Modell.
- Die Admin-UI zeigt den Graph-Status nur read-only an. Secret-Rotation passiert ausserhalb der UI.

Wichtig:

- `AUTH_MODE=dev-sim` bedeutet lokal nur Simulations-Login, nicht kuenstliche Demo-Welt.
- Swagger ist lokal standardmaessig aktiv. Bei Bedarf kann er ueber `SWAGGER_ENABLED=false` abgeschaltet werden.
- Die Login-Liste kommt aus synchronisierten Verzeichnisidentitaeten.
- Ohne gueltige `ENTRA_*`-Werte bleibt diese Liste leer, bis ein Directory-Sync erfolgreich war.

### 3. Web lokal starten

Beispiel fuer `web/.env.local`:

```env
VITE_API_PROXY_TARGET=http://127.0.0.1:5001
VITE_AUTH_MODE=dev-sim
```

Optional fuer lokale Entra-Tests:

```env
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
VITE_ENTRA_AUDIENCE=api://00000000-0000-0000-0000-000000000000
VITE_ENTRA_REDIRECT_URI=https://onboarding-test.example.local
```

Dann:

```powershell
cd web
npm install
npm run dev
```

Das Frontend laeuft auf `http://localhost:5173` und nutzt den Vite-Proxy zur lokal gestarteten API.

### Lokale Checks

Sinnvolle lokale Smoke-Checks:

```powershell
curl http://127.0.0.1:5001/health/live
curl http://127.0.0.1:5001/health
curl http://127.0.0.1:5001/auth/provider-info
curl http://127.0.0.1:5001/me
```

Erwartung:

- `/health/live` liefert `200`
- `/health` liefert `200` oder `503` mit Status-JSON, aber nicht `404`
- `/auth/provider-info` zeigt den aktiven Auth-Modus
- `/me` liefert ohne gueltige Session typischerweise `401`

### Lokale Quality Gates vor Merge oder Release

Die minimalen Pflichtpruefungen fuer lokale Vorab-Checks sind:

```powershell
dotnet build api/API/API.csproj -c Release
dotnet test api/API.Tests/API.Tests.csproj -c Release -p:UseAppHost=false
cd web
npm ci
npm run lint
npm test
npm run build
```

Hinweise:

- Die Backend-Tests erwarten eine laufende PostgreSQL-Dev-Datenbank auf `localhost:25432` mit dem bekannten Dev-Init.
- Die GitHub-Actions-Pipeline richtet dafuer denselben DB-Zustand automatisiert ein.
- Fuer schnelle lokale Iteration reicht oft weiterhin ein gezielter Lauf; vor Merge oder Release sollten aber die kompletten Gates gruen sein.

## Linux-VM Deployment

### 1. Produktive Konfiguration anlegen

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
- `ENTRA_CLIENT_SECRET` oder `GRAPH_CLIENT_SECRET`

Optional bzw. je nach Betrieb:

- `DIRECTORY_GROUP_PREFIX`
- `DIRECTORY_EXPLICIT_GROUP_IDS`
- `DIRECTORY_SYNC_SCHEDULED`
- `DIRECTORY_SYNC_INTERVAL_MINUTES`
- `WEB_BIND_HOST`
- `WEB_HTTP_PORT`
- `WEB_HTTPS_PORT`
- `AUTO_PROVISION_DEFAULT_ROLE_KEY`
- `SWAGGER_ENABLED=false`

Wichtig:

- `PUBLIC_BASE_URL` muss exakt zur oeffentlichen HTTPS-URL passen.
- Dieselbe URL muss als Entra SPA Redirect URI gepflegt sein.
- `ENTRA_AUDIENCE` muss die API App-ID-URI sein, aus der das Frontend den Scope `<audience>/access_as_user` anfordert.
- Mindestens eines aus `ENTRA_CLIENT_SECRET` oder `GRAPH_CLIENT_SECRET` muss gesetzt sein, sonst ist weder lokaler Directory-Sync noch produktive Entra-/Graph-Nutzung vollstaendig konfiguriert.
- `GRAPH_CLIENT_SECRET` ueberschreibt nur das fuer Graph/Mail verwendete Secret; Tenant und Client ID kommen weiterhin aus `ENTRA_TENANT_ID` und `ENTRA_CLIENT_ID`.
- Graph-Credentials werden nicht mehr in der Datenbank gepflegt. Rotation erfolgt ueber Environment bzw. Secret-Store und einen API-/Container-Neustart.
- Swagger ist in Production standardmaessig deaktiviert und darf dort nicht per `SWAGGER_ENABLED=true` aktiviert werden. Das Startup bricht in diesem Fall bewusst ab.

### 2. Produktionsstack starten

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
curl -k https://<PUBLIC_HOSTNAME>/api/auth/provider-info
curl -k -i https://<PUBLIC_HOSTNAME>/api/me
```

Erwartung:

- `/api/health/live` liefert `200`
- `/api/health` liefert `200` oder `503`, aber nicht `404`
- `/api/auth/provider-info` zeigt `mode=entra`
- `/api/me` liefert ohne Login typischerweise `401`, aber nicht `404`

## Auth-Modi

### Lokal

- API-Standard ist `AUTH_MODE=dev-sim`
- Frontend-Standard ist `VITE_AUTH_MODE=dev-sim`
- Simulations-Login basiert auf lokal synchronisierten Verzeichnisidentitaeten

### Servernah / produktiv

- `compose.prod.yml` setzt `AUTH_MODE=entra`
- Das Web bekommt Laufzeitwerte ueber `app-config.js`
- URL-/Entra-Aenderungen brauchen dadurch keinen Frontend-Neubuild

## Datenbank-Initialisierung

- Dev verwendet `db/init/dev/00_init.sql`
- Production verwendet `db/init/prod/00_init.sql`
- Production laedt `02_bootstrap.sql`, aber nicht `02_seed.sql`
- Dev laedt `02_seed.sql`, das den produktiven Bootstrap plus `90_dev_defaults.sql` kombiniert

Damit sind produktiver Bootstrap und lokale Defaults technisch getrennt.

## HTTPS / Reverse Proxy

- `compose.prod.yml` bringt einen repo-eigenen Caddy-Proxy mit
- `deploy/Caddyfile` nutzt standardmaessig `tls internal`
- `/api/*` wird an die API weitergereicht, der Rest an das Web
- Clients im Netz muessen dem internen Caddy-Root-Zertifikat vertrauen, sonst gibt es Browser-Warnungen und Redirect-Probleme

## CI / Quality Gates

Im Repo liegt eine minimale GitHub-Actions-Pipeline unter `.github/workflows/quality-gates.yml`.

Sie prueft auf Pushes nach `main` oder `master` sowie auf Pull Requests:

- Backend Restore
- Backend Build
- Backend Tests gegen eine initialisierte PostgreSQL-Dev-Datenbank
- Frontend `npm ci`
- Frontend Lint
- Frontend Tests
- Frontend Build

## Wichtige Konfigurationsschalter

### Backend

- `AUTH_MODE`
  Erlaubte Werte: `dev-sim`, `entra`
- `ConnectionStrings__Default`
  Bevorzugter DB-Connection-String
- `CONNECTION_STRING`
  Legacy-Fallback
- `PUBLIC_BASE_URL`
  Oeffentliche URL fuer CORS, Redirects und Mail-Links
- `DIRECTORY_*`
  Verzeichnis-Sync und Gruppenfilter
- `ENTRA_*`
  Entra- und API-Integration
- `SWAGGER_ENABLED`
  Optionaler Schalter fuer nicht-produktive Umgebungen; in Production verboten
- `GRAPH_CLIENT_SECRET`
  Optionaler Secret-Override fuer Graph- und Mail-Dienste bei gleicher Entra-App

### Frontend

- lokal: `web/.env.local`
- serverseitig: Runtime-Config in `app-config.js`

Wichtige Frontend-Werte:

- `apiBase`
- `authMode`
- `entraClientId`
- `entraTenantId`
- `entraAudience`
- `entraRedirectUri`

## Ersetzte Altstruktur

Diese frueheren Dateien oder Pfade werden durch die aktuelle Struktur ersetzt:

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
