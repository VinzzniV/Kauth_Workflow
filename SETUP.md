# Setup

Diese Datei beschreibt den aktuellen Betriebs- und Entwicklungsweg des Projekts.
Sie ist operative Doku fuer lokale Entwicklung, servernahes Deployment und Laufzeitkonfiguration.

Architekturhinweis:
- Der aktuelle Code laeuft noch auf dem bestehenden lifecycle-/task-getriebenen Kern.
- Die aktive Feature-Planung fuer Rotation/Durchlauf steht in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`; das stabile Plattform-Zielbild steht in `PRODUCTIVE_TARGET_ARCHITECTURE.md`.
- Seit T9 laeuft der erste Automation Layer als API-interner Hosted Service; es gibt lokal und produktiv keinen separaten Worker-Container.
- Seit T10 steht mit `/builder` eine eigenstaendige Builder-Seite fuer Definitionen, Versionen, Nodes, Edges und Automation-Actions zur Verfuegung; alte Admin-Einstiege werden dorthin umgeleitet.
- Seit T11 nutzt der normale Start-Flow `/workflows/create` publizierte Workflow-Definitionen; `processTypeKey` und `/process-types` bleiben nur noch als Legacy-Alias fuer eine Uebergangsrelease bestehen.

## Repo-Struktur fuer Betrieb

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

1. Datenbank per Docker starten
2. API lokal per `dotnet run` starten
3. Frontend lokal per Vite starten

### 1. Datenbank starten

```powershell
docker compose -f compose.yml -f compose.dev-db.yml up -d db
```

### 2. API lokal starten

```powershell
dotnet run --project api/API/API.csproj --launch-profile API
```

Das lokale Launch-Profil setzt u. a.:
- `ASPNETCORE_ENVIRONMENT=Development`
- `AUTH_MODE=dev-sim`
- `ConnectionStrings__Default=Host=localhost;Port=25432;...;GSS Encryption Mode=Disable;SSL Mode=Disable`
- `PUBLIC_BASE_URL=http://localhost:5173`
- `DIRECTORY_GROUP_PREFIX=Onboarding-App-`
- `DIRECTORY_SYNC_SCHEDULED=true`
- `SWAGGER_ENABLED=true`

Hinweis:
- `automation`-Jobs werden von der API selbst gepollt und verarbeitet, sobald die Anwendung laeuft.
- Die ersten Actions sind simuliert; fuer lokale Entwicklung ist deshalb kein externer Provisioning-Adapter noetig.
- Der Guided Builder speichert Drafts weiter ueber den bestehenden Vollersatz-Endpunkt; lokale JSON-Fehler in Node-`config` oder Action-`inputMapping` blockieren Save bereits im UI.

Fuer lokalen Directory-Sync braucht die API gueltige `ENTRA_*`-Werte.

### 3. Web lokal starten

`web/.env.local`:

```env
VITE_API_PROXY_TARGET=http://127.0.0.1:5001
VITE_AUTH_MODE=dev-sim
```

Optionale lokale Entra-Tests:

```env
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
VITE_ENTRA_AUDIENCE=api://00000000-0000-0000-0000-000000000000
VITE_ENTRA_REDIRECT_URI=https://workflow-test.example.local
```

```powershell
cd web
npm install
npm run dev
```

## Lokale Checks

```powershell
curl http://127.0.0.1:5001/health/live
curl http://127.0.0.1:5001/health/ready
curl http://127.0.0.1:5001/health
curl http://127.0.0.1:5001/auth/provider-info
curl http://127.0.0.1:5001/me
```

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

Hinweis:
- DB-gebundene Backend-Tests erwarten lokal PostgreSQL auf `127.0.0.1:25432`; ohne laufenden Docker-DB-Container schlagen diese Tests fehl.

## Linux-VM Deployment

### 1. Produktive Konfiguration anlegen

```bash
cp .env.prod.example .env.prod
```

Pflichtwerte:
- `PUBLIC_HOSTNAME`
- `PUBLIC_BASE_URL`
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `ENTRA_TENANT_ID`
- `ENTRA_CLIENT_ID`
- `ENTRA_AUDIENCE`
- `ENTRA_CLIENT_SECRET` oder `GRAPH_CLIENT_SECRET`

Wichtige Regeln:
- `PUBLIC_BASE_URL` muss `https://` verwenden
- Graph-Secrets werden nicht in der Datenbank gepflegt
- `SWAGGER_ENABLED=true` ist in Production verboten

### 2. Produktionsstack starten

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

### 3. Status pruefen

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f
```

## Datenbank-Initialisierung

- Dev verwendet `db/init/dev/00_init.sql`
- Production verwendet `db/init/prod/00_init.sql`
- Production laedt `02_bootstrap.sql`, aber nicht `02_seed.sql`
- Dev laedt `02_seed.sql` inklusive lokaler Defaults

## Handoff / Release-ZIP

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Prepare-Handoff.ps1
```

Das Handoff-ZIP enthaelt bewusst keine lokalen oder generierten Artefakte wie:
- `.git`
- `node_modules`
- `dist`
- `bin` / `obj`
- `.env.prod`
- `web/.env.local`
- `*.log`
