# Konfiguration

#betrieb #konfiguration

Eine Wahrheit für alle Konfig-Werte. Wer eine Variable sucht, einen Wert ändern will oder verstehen will warum etwas wo steht, fängt **hier** an.

---

## Zweck

- Vollständige Variablen-Übersicht über Backend + Frontend + Worker
- Ein kanonischer Speicherort pro Variable
- Verweise auf Setup-Skripte und Wizard

## Verwandte Dateien

- [[Setup]] — wie startet man lokal / auf der VM (operatives Wie)
- [[Deployment-Checkliste]] — produktiver Roll-out
- `scripts/Configure.ps1` — interaktiver Wizard für `.env.prod` (siehe K3)
- `worker/setup/Configure-Worker.ps1` — Worker-Setup-Wrapper (siehe K4)
- `.env.prod.example` — kanonisches Template für Prod

---

## Grundprinzip

Drei Hosts, drei Konfig-Pfade:

| Host | Wo Werte herkommen |
|---|---|
| **Linux-API (Container)** | `.env.prod` → Compose → Env-Vars im Container → `IConfiguration` |
| **Linux-API (Dev-lokal)** | `api/API/Properties/launchSettings.json` → Env-Vars → `IConfiguration` |
| **Web-Container** | Compose-Env-Vars → `web/docker-entrypoint.d/40-write-app-config.sh` → `public/app-config.js` (Runtime) |
| **Web (Vite-Dev)** | `web/.env.local` → Build-Zeit → `import.meta.env.VITE_*` |
| **Windows-Worker** | `worker/AdAutomationWorker/appsettings.json` + DPAPI-Files unter `%ProgramData%\KauthWorker\` |

---

## API-Runtime (`LifecycleRuntimeSettings`)

Gelesen via `LifecycleRuntimeSettingsResolver.Resolve(IConfiguration)` in [api/API/LifecycleRuntimeSettings.cs:32](api/API/LifecycleRuntimeSettings.cs#L32).

| Variable | Was | Default | Required | Wo heute gesetzt | Notes |
|---|---|---|---|---|---|
| `AUTH_MODE` | `dev-sim` oder `entra` | `none` (prod) / `dev-sim` (dev) | empfohlen | `compose.yml:25`, `compose.prod.yml:12`, `launchSettings.json:9` | API-Authentifizierungsmodus; nicht mit `WEB_AUTH_MODE` verwechseln |
| `PUBLIC_BASE_URL` | öffentliche https-URL | `http://localhost:5173` (dev) | prod: ja | `compose.yml:27`, `compose.prod.yml:13`, `launchSettings.json:11`, `.env.prod` | wird auch auf `Cors__AllowedOrigins__0`, `NotificationEmail__FrontendBaseUrl` und `ENTRA_REDIRECT_URI` gespiegelt |
| `ConnectionStrings__Default` | Postgres-Connection-String | — | ja | `compose.yml:26`, `launchSettings.json:10` | Container baut sie aus `POSTGRES_*` zusammen |
| `SWAGGER_ENABLED` | Swagger-UI an? | `false` (prod) / `true` (dev) | nein | `launchSettings.json:16` | in Prod **verboten** |
| `ENTRA_TENANT_ID` | Entra-Tenant-GUID | — | prod: ja | `compose.yml:30`, `compose.prod.yml:16`, `.env.prod` | |
| `ENTRA_CLIENT_ID` | App-Registration-Client-ID | — | prod: ja | `compose.yml:31`, `compose.prod.yml:17`, `.env.prod` | |
| `ENTRA_AUDIENCE` | API-Audience (z.B. `api://...`) | — | prod: ja | `compose.yml:32`, `compose.prod.yml:18`, `.env.prod` | |
| `ENTRA_CLIENT_SECRET` | Entra-App-Secret | — | prod: bedingt | `compose.yml:33`, `compose.prod.yml:19`, `.env.prod` | mindestens eins von `ENTRA_CLIENT_SECRET` oder `GRAPH_CLIENT_SECRET` |
| `GRAPH_CLIENT_SECRET` | optionaler Graph-Secret-Override | — | nein | `compose.yml:34`, `compose.prod.yml:20`, `.env.prod` | nur Override für Graph/Mail; Tenant + Client-ID bleiben bei `ENTRA_*` |
| `KAUTH_VAULT_KEY` | symmetrischer Vault-Key | — | prod: ja | `.env.prod` | **heute nicht im Compose-Passthrough** — K2 fügt `KAUTH_VAULT_KEY: ${KAUTH_VAULT_KEY:-}` im API-Service-Block ergänzt; gelesen via [EnvVaultKeyProvider.cs:12](api/API/Services/EnvVaultKeyProvider.cs#L12); muss identisch sein zum Wert im Worker-`vault.config.dpapi` |
| `DIRECTORY_GROUP_PREFIX` | Prefix für Directory-Sync-Gruppen | — | empfohlen | `compose.yml:35`, `launchSettings.json:14`, `.env.prod` | z.B. `Onboarding-App-` |
| `DIRECTORY_EXPLICIT_GROUP_IDS` | komma-getrennte Liste expliziter Gruppen-IDs | — | nein | `compose.yml:36`, `.env.prod` | |
| `DIRECTORY_SYNC_SCHEDULED` | Sync-Hosted-Service aktiv? | `true` | nein | `compose.yml:37`, `launchSettings.json:15`, `.env.prod` | |
| `DIRECTORY_SYNC_INTERVAL_MINUTES` | Sync-Intervall in Minuten | `15` (Compose) / `60` (Code) | nein | `compose.yml:38`, `.env.prod` | empfohlen 60–480 für prod |
| `AUTO_PROVISION_DEFAULT_ROLE_KEY` | Default-Rolle für Auto-Provision | — | nein | `compose.yml:39`, `.env.prod` | |
| `RUNTIME_HEALTH_STORAGE_PATHS` | Label/Pfad-Liste (`label1=/p1;label2=/p2`) | — | nein | `.env.prod` | leer = keine Storage-Kacheln im Admin-Dashboard |
| `HOST_RUNTIME_HEALTH_ENABLED` | Host-/VM-Block im Admin-Dashboard? | `false` | nein | `compose.prod.yml:25`, `.env.prod` | nur Linux; `start-vm.sh dev` setzt automatisch auf `true` |
| `HOST_RUNTIME_PROCFS_PATH` | procfs-Pfad | `/proc` | nein | `compose.prod.yml:26` | auf Docker-VM typisch `/host-proc` |
| `HOST_RUNTIME_ROOT_PATH` | Root-FS für Free-Space-Messung | `/` | nein | `compose.prod.yml:27` | auf Docker-VM typisch `/host-root` |

---

## Automation-Retry (API)

Gelesen via `BuildAutomationRetrySettings` in [api/API/Extensions/LifecycleServiceCollectionExtensions.cs:294-296](api/API/Extensions/LifecycleServiceCollectionExtensions.cs#L294-L296).

| Variable | Was | Default | Wo heute gesetzt |
|---|---|---|---|
| `WORKFLOW_AUTOMATION_MAX_ATTEMPTS` | max. Versuche pro Job | `3` | Env, kein Compose-Eintrag heute |
| `WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS` | Delay nach erstem Fail | `60` | dito |
| `WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS` | Delay nach weiteren Fails | `300` | dito |

Per-Action-Override existiert in der DB-Spalte `action_definitions.max_attempts_override` / `subsequent_retry_delay_seconds_override` (z.B. `CreateMailboxGraph` nutzt das für ein größeres Retry-Budget bei Entra-Connect-Sync-Waits).

---

## Container/Topology (Compose-Inputs)

| Variable | Was | Default | Wo gelesen |
|---|---|---|---|
| `PUBLIC_HOSTNAME` | DNS-Name (ohne Schema) | — | `compose.prod.yml:60` (Caddy) |
| `WEB_BIND_HOST` | Bind-Adresse für Caddy | `0.0.0.0` | `compose.prod.yml:62` |
| `WEB_HTTP_PORT` | HTTP-Port | `80` | `compose.prod.yml:62` |
| `WEB_HTTPS_PORT` | HTTPS-Port | `443` | `compose.prod.yml:63` |
| `POSTGRES_DB` | DB-Name | `appdb` | `compose.yml:7` |
| `POSTGRES_USER` | DB-User | `app` | `compose.yml:8` |
| `POSTGRES_PASSWORD` | DB-Passwort | `app_pw` (dev) | `compose.yml:9` |
| `DEV_DB_PORT` | Host-Port für Dev-DB | `26432` | `compose.dev-db.yml` |
| `WEB_AUTH_MODE` | Auth-Modus im Web-Container | `dev-sim` | `compose.yml:50`; **heute in `compose.prod.yml:42` hart auf `entra` überschrieben** — K2 stellt das auf `${WEB_AUTH_MODE:-entra}` um, damit `.env.prod` wirken kann |

---

## Script-only Dev-Helfer

Werte, die nur von Start-Skripten gelesen werden und **kein** Runtime-Input für API/Web/Worker sind:

| Variable | Was | Wo gelesen |
|---|---|---|
| `DEV_PUBLIC_BASE_URL` | öffentliche Dev-URL für `start-vm.sh dev` | [scripts/start-vm.sh:515](scripts/start-vm.sh#L515) |
| `ONBOARDING_TEST_CONNECTION_STRING` | Connection-String für DB-gebundene Backend-Tests | `api/API.Tests/`-Test-Setup |

---

## Frontend (Vite-Build / `web/.env`)

| Variable | Was | Default | Wo gelesen |
|---|---|---|---|
| `VITE_API_PROXY_TARGET` | API-Proxy-Ziel für Vite-Dev-Server | `http://127.0.0.1:5001` | [web/vite.config.ts:4](web/vite.config.ts#L4) |
| `VITE_AUTH_MODE` | `dev-sim` oder `entra` | `dev-sim` | Frontend-Auth-Init |
| `VITE_ENTRA_CLIENT_ID` | nur bei `VITE_AUTH_MODE=entra` | — | MSAL-Init |
| `VITE_ENTRA_TENANT_ID` | nur bei `VITE_AUTH_MODE=entra` | — | MSAL-Init |
| `VITE_ENTRA_AUDIENCE` | API-Audience | — | MSAL-Init |
| `VITE_ENTRA_REDIRECT_URI` | Redirect-URI | — | MSAL-Init |

---

## Frontend (Container-Runtime über `app-config.js`)

Im Prod-Web-Container schreibt das Entrypoint-Skript [web/docker-entrypoint.d/40-write-app-config.sh](web/docker-entrypoint.d/40-write-app-config.sh) zur Container-Startzeit eine `public/app-config.js`, die diese Env-Vars in das Frontend einblendet:

- `API_BASE` (z.B. `/api`)
- `AUTH_MODE` (vom Compose-`WEB_AUTH_MODE` befüllt)
- `ENTRA_CLIENT_ID`, `ENTRA_TENANT_ID`, `ENTRA_AUDIENCE`, `ENTRA_REDIRECT_URI`

`ENTRA_REDIRECT_URI` wird in `compose.prod.yml:46` automatisch aus `PUBLIC_BASE_URL` gespiegelt — keine zweite Eingabe nötig.

---

## Worker (`worker/AdAutomationWorker/appsettings.json`)

Gelesen via `IOptions<WorkerSettings>` aus [worker/AdAutomationWorker.Core/Configuration/WorkerSettings.cs](worker/AdAutomationWorker.Core/Configuration/WorkerSettings.cs). Alle Keys hängen unter dem `Worker`-Prefix.

| Key | Was | Default |
|---|---|---|
| `Worker:WorkerId` | Eindeutige Worker-Kennung (`<hostname>:<pid>:<startup-uuid>`) | leer (wird beim Start gesetzt) |
| `Worker:PollingIntervalSeconds` | Polling-Intervall | `5` |
| `Worker:HeartbeatIntervalSeconds` | Heartbeat-Intervall | `30` |
| `Worker:StaleClaimTimeoutMinutes` | Stale-Claim-Timeout | `5` |
| `Worker:ClaimBatchSize` | Anzahl Jobs pro Claim | `1` |
| `Worker:Ad:DcHost` | FQDN eines erreichbaren DCs | — |
| `Worker:Ad:BaseDn` | Base-DN für Pre-Search | — |
| `Worker:Ad:ConnectionTimeoutSeconds` | LDAPS-Connection-Timeout | `30` |
| `Worker:Vault:TemporaryCredentialTtlSeconds` | Vault-TTL (Sekunden) | `604800` (7 Tage) |

### Worker-DPAPI-Files

Werden von [worker/setup/install-db-config.ps1](worker/setup/install-db-config.ps1) und [worker/setup/install-vault-key.ps1](worker/setup/install-vault-key.ps1) geschrieben:

| Datei | Inhalt |
|---|---|
| `%ProgramData%\KauthWorker\db.config.dpapi` | Postgres-Connection-Daten (DPAPI-LocalMachine-verschlüsselt) |
| `%ProgramData%\KauthWorker\vault.config.dpapi` | Symmetrischer Vault-Key (DPAPI-LocalMachine-verschlüsselt) — **muss identisch sein zum API-`KAUTH_VAULT_KEY`** |

---

## Graph-Permissions (Admin-Consent in Entra)

Application-Permissions, die der Tenant-Admin freigeben muss:

| Permission | Wofür |
|---|---|
| `Mail.Send` | `SendWelcomeMailGraph` |
| `User.Read.All` | Directory-Sync, `CreateMailboxGraph`-Pre-Read |
| `LicenseAssignment.ReadWrite.All` | `CreateMailboxGraph`-Lizenzzuweisung |
| `Group.Read.All` | `reference_user.groups`-Mapping-Source (Onboarding-Referenzuser) |

---

## Verifikationspfade

### „Was ist aktuell aktiv?"

- **API-Container:** `docker exec <api-container> env | sort` zeigt die finale Env-Sicht.
- **Compose-Dry-Run:** `docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml config` rendert alle Variablen aufgelöst.
- **Runtime-Diagnose (nach K5):** `Administration > System > Konfiguration` zeigt die aktiven Werte mit Secret-Redaction.

### „Wo soll der Wert wirklich leben?"

- `PUBLIC_BASE_URL` / `ENTRA_*` / `DIRECTORY_SYNC_*` / `KAUTH_VAULT_KEY` / `AUTO_PROVISION_DEFAULT_ROLE_KEY` / `WEB_AUTH_MODE` / `WORKFLOW_AUTOMATION_*` → **`.env.prod`** (prod) / **`launchSettings.json`** (dev).
- `POSTGRES_*` → `compose.yml` (Container) / `launchSettings.json` (lokal).
- `Cors:AllowedOrigins` (Liste) + `NotificationEmail` (Provider-Konfig) → bleibt in `api/API/appsettings.json`.

### „Worker läuft korrekt?"

1. `Get-Service KauthAdAutomationWorker` zeigt `Running`
2. `Get-Content "$env:ProgramData\KauthWorker\db.config.dpapi"` existiert (binär)
3. `Get-Content "$env:ProgramData\KauthWorker\vault.config.dpapi"` existiert (binär)
4. Postgres-Login `kauth_worker` kann sich verbinden (`automation_jobs`-SELECT-Test)

---

## Bewusst nicht hier dokumentiert

- **Tagesoperatives** (welcher Wert genau auf Server X heute steht) → `.env.prod` selbst
- **Code-Defaults** (was im Code als Fallback steht) → Code-Datei selbst, hier nur referenziert
- **Hot-Path-Tuning** wie Postgres-Pool-Größen → liegt im Code (Npgsql) und in `compose.yml`-Healthchecks
