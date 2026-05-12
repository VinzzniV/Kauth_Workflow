# Worker-Setup (Stand Etappe 9a Schritt 3)

Dieser Ordner enthaelt die manuellen Installationsskripte fuer den Windows-Worker auf einer
Test-VM. Seit Schritt 3 ist der DPAPI-Schreibpfad **Default** und produktiv lauffaehig; gMSA-
Service-Account-Switch ist mit dabei.

## Voraussetzungen

- Windows Server 2019+ oder Windows 11 (PowerShell 5.1+).
- .NET 8 Runtime installiert.
- Erreichbarkeit zur Workflow-DB (Postgres).
- DB-User `kauth_worker` mit den Grants aus Etappe 9a Schritt 1 (SELECT auf View
  `automation_jobs_windows_worker`, UPDATE auf `automation_jobs`, INSERT auf
  `automation_job_attempts` und `automation_job_logs`, kein DELETE, kein Zugriff auf andere
  Tabellen).

## Installation

```powershell
# 1. DB-Konfig schreiben (Default: DPAPI in %ProgramData%\KauthWorker\db.config.dpapi)
.\install-db-config.ps1 `
    -DbHost postgres.example.local `
    -Database kauth_workflow `
    -Username kauth_worker `
    -Password <secret>

# Dev-Alternative: Klartext-JSON (loud warning beim Service-Start)
# .\install-db-config.ps1 -DbHost ... -Password ... -PlainJson

# 2. Worker veroeffentlichen (auf einer Build-Maschine)
dotnet publish ..\AdAutomationWorker\AdAutomationWorker.csproj -c Release -o C:\Apps\KauthWorker

# 3. gMSA einrichten (siehe Abschnitt unten), dann Service registrieren
.\install-windows-service.ps1 `
    -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
    -ServiceAccount 'DOMAIN\kauth-worker$'

# 4. Service starten
Start-Service KauthAdAutomationWorker
```

## gMSA einrichten (Pflicht fuer Schritt 3)

Schreibende AD-Handler (`CreateAdUserLdaps` und Folge-Handler in Schritt 4) brauchen einen
gMSA-Kontext. AD-Auth laeuft transparent ueber `AuthType.Negotiate` — kein Secret im Service.

```powershell
# Auf einem DC (Domain-Admin):
New-ADServiceAccount `
    -Name kauth-worker `
    -DNSHostName worker-vm.example.local `
    -PrincipalsAllowedToRetrieveManagedPassword 'WorkerVm-Hostgroup'

# Auf der Worker-VM (Admin, RSAT-AD-PowerShell vorausgesetzt):
Install-ADServiceAccount kauth-worker
Test-ADServiceAccount kauth-worker   # muss True liefern

# Anschliessend Service-Installation mit gMSA:
.\install-windows-service.ps1 -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
                              -ServiceAccount 'DOMAIN\kauth-worker$'
```

**Delegated-Rechte (Pflicht):** Der gMSA braucht auf der Ziel-OU
- `Create Child Objects` (Klasse `user`)
- `Reset Password` auf user-Objekten

Diese Rechte werden in `dsa.msc` ueber die OU-Delegation gesetzt.

## DPAPI-Hinweise

- `install-db-config.ps1` verwendet `DataProtectionScope.LocalMachine`. Damit ist die Datei
  an die Maschine gebunden, nicht an einen einzelnen User — der gMSA-Service-User kann sie
  ohne Spezial-Setup entschluesseln.
- **Maschinenwechsel = neuer Setup-Lauf.** Wenn die VM migriert wird, ist die `.dpapi`-Datei
  auf der neuen Maschine nicht mehr entschluesselbar. Der Loader liefert eine klare Fehler-
  meldung mit Verweis auf `install-db-config.ps1`.
- Klartext-Pfad mit `-PlainJson` bleibt fuer Dev. Der Loader gibt dann eine `WARNING`-Logzeile
  aus und priorisiert den DPAPI-Pfad, falls beide existieren.

## Verifikation (Skeleton-E2E)

1. DB-Migration `db/manual/2026-05-12_automation_jobs_target_runtime.sql` einspielen.
2. Dev-Seed neu laden — die Action `simulated_windows_worker_ping` muss in `action_definitions`
   mit `target_runtime='windows_worker'` stehen.
3. Linux-API starten (`./scripts/start-vm.sh dev`). Der `ExternalAutomationJobCompletionSweeper`
   laeuft mit.
4. Worker im Vordergrund starten (Konsole): `dotnet run --project worker/AdAutomationWorker`.
   Alternativ Env-Var setzen statt JSON-Konfig: `setx KAUTH_WORKER_DB_CONNECTION
   "Host=..."`.
5. Im Builder einen Workflow bauen mit einer Action-Node, die auf `simulated_windows_worker_ping`
   zeigt; Workflow starten.
6. In der DB pruefen:
   - `automation_jobs`: Job hat `target_runtime='windows_worker'`, durchlaeuft `pending` →
     `running` (mit `claimed_at`, `claimed_by`, `heartbeat_at`) → `succeeded` (mit
     `completed_at`).
   - Sekunden spaeter: `completion_processed_at` ist gesetzt (Sweeper hat angefasst, Workflow
     wurde fortgeschaltet).
   - `automation_job_attempts`: eine Zeile mit `status='succeeded'`, `output_json` enthaelt
     `{"result":"pong"}`.
   - `automation_job_logs`: mindestens ein Log-Eintrag pro Job.
7. **Wichtigster Beleg:** der Workflow ist am Automation-Node vorbei und Folge-Tasks/-Actions
   sind aktiv.

## Stale-Claim-Test (manuell)

Workflow mit Payload `{"delaySeconds": 120}` starten. Sobald der Worker den Job zieht und im
Delay ist, Worker per Ctrl-C killen. `automation_jobs.status` bleibt `running`, `heartbeat_at`
veraltet. Worker neu starten — der `ReleaseStaleClaimsAsync`-Schritt setzt den Job vor dem
naechsten Claim auf `pending` zurueck, der Worker pickt erneut, faehrt durch.
