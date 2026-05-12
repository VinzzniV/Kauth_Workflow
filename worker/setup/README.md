# Worker-Setup (Etappe 9a Schritt 2 — Skeleton)

Dieser Ordner enthaelt die manuellen Installationsskripte fuer den Windows-Worker auf einer
Test-VM. **Skeleton-Stand:** der Pfad ist absichtlich noch nicht hart abgesichert; mehrere
Schritte sind Stubs, die in Etappe 9a Schritt 3 verschaerft werden.

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
# 1. DB-Konfig schreiben (Skeleton: KLARTEXT in %ProgramData%\KauthWorker\db.config.json)
.\install-db-config.ps1 `
    -DbHost postgres.example.local `
    -Database kauth_workflow `
    -Username kauth_worker `
    -Password <secret>

# 2. Worker veroeffentlichen (auf einer Build-Maschine)
dotnet publish ..\AdAutomationWorker\AdAutomationWorker.csproj -c Release -o C:\Apps\KauthWorker

# 3. Service registrieren
.\install-windows-service.ps1 -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe

# 4. Service starten
Start-Service KauthAdAutomationWorker
```

## Was im Skeleton noch fehlt — Pflicht-Zuege fuer Etappe 9a Schritt 3

- **DPAPI-Encryption der DB-Konfig.** Aktuell wird `db.config.json` im Klartext geschrieben.
  Der Loader meldet das als WARN-Log; das ist ok fuer Skeleton/Dev, aber nicht fuer Prod.
  Schritt 3 fuegt einen `db.config.dpapi`-Pfad hinzu (ProtectedData, LocalMachine-Scope), der
  vom Loader bevorzugt vor dem Plain-JSON gelesen wird.
- **gMSA-Service-Account.** Aktuell laeuft der Service unter dem Default-Account. Schritt 3
  setzt `-Credential` auf den gMSA, sobald der erste echte AD-Handler kommt (LDAPS gegen den
  Test-DC).
- **Echte AD-Handler.** Aktuell ist nur `SimulatedWindowsWorkerPingHandler` registriert. Er
  beweist den Transport-/Audit-Pfad ohne AD-Zugriff. `CreateAdUserHandler` etc. kommen in
  Schritt 3.

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
