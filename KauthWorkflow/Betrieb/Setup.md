# Setup & Lokale Entwicklung

#betrieb #setup

Operative Doku für lokale Entwicklung, servernahes Deployment und Laufzeitkonfiguration.
Primärquelle im Repo war: `SETUP.md` (in Vault migriert)

---

## Architektur-Hinweise

- Seit T9 läuft der Automation Layer als API-interner Hosted Service — kein separater Worker-Container.
- Seit Phase 5 läuft ein API-interner täglicher Rotation-Notification-Worker — ebenfalls kein separater Container.
- Seit T10 gibt es `/builder` als eigenständige Builder-Seite; alte Admin-Einstiege leiten dorthin um.
- Seit T11 nutzt `/workflows/create` publizierte Workflow-Definitionen; `processTypeKey` ist nur noch Legacy-Alias.
- Seit T12 ist `Administration > System` die zentrale Betriebs- und Fehlerkonsole.

---

## Repo-Struktur für Betrieb

| Datei | Zweck |
|-------|-------|
| `compose.yml` | Gemeinsame Compose-Basis für `db`, `api`, `web` |
| `compose.dev-db.yml` | Lokales Override nur für PostgreSQL (Port 26432) |
| `compose.prod.yml` | Produktionsnahes Override mit Entra-Auth, Caddy |
| `.env.prod.example` | Vorlage für produktive Laufzeitvariablen |
| `scripts/start-vm.sh` | Linux-VM-Helfer für `dev`/`prod` inkl. `status`, `logs`, `stop`, `restart` |
| `web/.env.local` | Lokale Frontend-Entwicklung (unversioniert) |

---

## Lokale Entwicklung

### Schritt 1 — Datenbank starten

```powershell
docker compose -f compose.yml -f compose.dev-db.yml up -d db
```

### Schritt 2 — API starten

```powershell
dotnet run --project api/API/API.csproj --launch-profile API
```

Das lokale Launch-Profil setzt:
- `ASPNETCORE_ENVIRONMENT=Development`
- `AUTH_MODE=dev-sim`
- `ConnectionStrings__Default=Host=localhost;Port=26432;...;GSS Encryption Mode=Disable;SSL Mode=Disable`
- `PUBLIC_BASE_URL=http://localhost:5173`
- `DIRECTORY_GROUP_PREFIX=Onboarding-App-`
- `DIRECTORY_SYNC_SCHEDULED=true`
- `SWAGGER_ENABLED=true`
- `RUNTIME_HEALTH_STORAGE_PATHS` — optional; Format `label1=/pfad/1;label2=/pfad/2`; leer = keine Storage-Kacheln im Admin-Runtime-Health-Block

Hinweise:
- `automation`-Jobs werden von der API selbst gepollt
- Rotation-Benachrichtigungen werden von der API selbst täglich erzeugt
- Frontend-Fehler werden automatisch an `POST /client/log-events` gemeldet
- Erste Actions sind simuliert — kein externer Adapter nötig
- `dev-sim` zeigt keine kuenstlichen Demo-Benutzer
- im Dev-Modus importiert der Directory-Sync deshalb neue Identitaeten aus bereits gemappten Entra-Gruppen automatisch in lokale `app_users`, damit die Simulations-Login-Seite auf frischer DB nutzbar ist

### Schritt 3 — Web starten

`web/.env.local`:
```env
VITE_API_PROXY_TARGET=http://127.0.0.1:5001
VITE_AUTH_MODE=dev-sim
```

```powershell
cd web
npm install
npm run dev
```

Für lokale Entra-Tests zusätzlich in `.env.local`:
```env
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
VITE_ENTRA_AUDIENCE=api://00000000-0000-0000-0000-000000000000
VITE_ENTRA_REDIRECT_URI=https://workflow-test.example.local
```

---

## Lokale Health-Checks

```powershell
curl http://127.0.0.1:5001/health/live
curl http://127.0.0.1:5001/health/ready
curl http://127.0.0.1:5001/health
curl http://127.0.0.1:5001/auth/provider-info
curl http://127.0.0.1:5001/me
```

---

## Lokale Quality Gates

```powershell
dotnet build api/API/API.csproj -c Release
dotnet test api/API.Tests/API.Tests.csproj -c Release -p:UseAppHost=false
cd web
npm ci
npm run lint
npm test
npm run build
```

> DB-gebundene Backend-Tests erwarten lokal PostgreSQL auf `127.0.0.1:26432`.

---

## Linux-VM Deployment

### 1 — Produktive Konfiguration anlegen

```bash
cp .env.prod.example .env.prod
```

Pflichtwerte:
- `PUBLIC_HOSTNAME`
- `PUBLIC_BASE_URL` (muss `https://` verwenden)
- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
- `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE`
- `ENTRA_CLIENT_SECRET` oder `GRAPH_CLIENT_SECRET`

Regeln:
- `SWAGGER_ENABLED=true` in Production **verboten**
- Graph-Secrets kommen nicht aus der DB

### 2 — Stack starten

```bash
chmod +x scripts/start-vm.sh
./scripts/start-vm.sh prod
```

Fallback ohne Script:

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

### 3 — Status prüfen

```bash
./scripts/start-vm.sh prod status
./scripts/start-vm.sh prod logs
```

Fallback ohne Script:

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f
```

### 4 — Dev-Modus auf derselben VM

Wenn auf derselben Linux-VM statt des produktionsnahen Stacks kurzfristig der Entwicklungsmodus gebraucht wird:

```bash
chmod +x scripts/start-vm.sh
./scripts/start-vm.sh dev
```

Der Dev-Modus startet:
- PostgreSQL über `docker compose -f compose.yml -f compose.dev-db.yml`
- API als Hintergrundprozess auf `0.0.0.0:5001`
- Vite-Webserver als Hintergrundprozess auf `0.0.0.0:5173`

Nützliche Kommandos:

```bash
./scripts/start-vm.sh dev status
./scripts/start-vm.sh dev logs
./scripts/start-vm.sh dev stop
./scripts/start-vm.sh dev restart
```

Hinweise:
- Laufzeitdateien und Logs landen unter `.tmp-vm-dev/`
- Der Vite-Dev-Server braucht `node >= 20.19.0` (Node 18 reicht nicht)
- Das Script meldet `dev` erst als erfolgreich, wenn API (`/health/ready`) und Web wirklich erreichbar sind
- `dev stop` bereinigt auch haengengebliebene Port-Prozesse auf `5001` und `5173`
- Falls die öffentliche Dev-URL nicht automatisch passt: `export DEV_PUBLIC_BASE_URL=http://<vm-host>:5173`
- Für `dev` müssen auf der VM zusätzlich `dotnet` und `npm` installiert sein

---

## Datenbank-Initialisierung

| Umgebung | Init-Datei | Besonderheiten |
|----------|-----------|----------------|
| Dev | `db/init/dev/00_init.sql` | lädt `01_schema.sql` + `02_dev_seed.sql` |
| Production | `db/init/prod/00_init.sql` | lädt `01_schema.sql` + `02_bootstrap.sql` |

Solange das System nicht produktiv läuft, werden Schema- und Seed-Änderungen direkt in `01_schema.sql` / `02_bootstrap.sql` / `02_dev_seed.sql` gepflegt — nicht als neue Migrationen. Die ursprünglichen 60+ Migrationen liegen unter `db/_archive/` als Referenz. Sobald die Plattform live geht, sind diese drei Dateien einzufrieren und neue Änderungen kommen nur noch additiv über Migrationen.

---

## Handoff / Release-ZIP

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Prepare-Handoff.ps1
```

Das ZIP enthält bewusst **nicht**: `.git`, `node_modules`, `dist`, `bin`/`obj`, `.env.prod`, `web/.env.local`, `*.log`

---

## Verwandte Notizen

- [[Deployment-Checkliste]] — Schritt-für-Schritt für Production
- [[Zielarchitektur]] — Was der Stack leistet
