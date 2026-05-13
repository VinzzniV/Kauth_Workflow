# Worker-Setup (Stand Etappe 9a Schritt 6)

Dieser Ordner enthaelt die manuellen Installationsskripte fuer den Windows-Worker auf einer
Test-VM. Seit Schritt 3 ist der DPAPI-Schreibpfad **Default** und produktiv lauffaehig; gMSA-
Service-Account-Switch ist mit dabei. Schritt 6 hat den Temporary-Credentials-Vault produktiv
gemacht — der Vault-Schluessel folgt demselben DPAPI-Pattern.

## Voraussetzungen

- Windows Server 2019+ oder Windows 11 (PowerShell 5.1+).
- .NET 8 Runtime installiert.
- Erreichbarkeit zur Workflow-DB (Postgres).
- DB-User `kauth_worker` mit den Grants aus Etappe 9a Schritt 1 (SELECT auf View
  `automation_jobs_windows_worker`, UPDATE auf `automation_jobs`, INSERT auf
  `automation_job_attempts` und `automation_job_logs`, kein DELETE, kein Zugriff auf andere
  Tabellen) **plus INSERT auf `temporary_credentials`** (Etappe 9a Schritt 6 Sub-A).
- DB-User `kauth_api` mit SELECT + UPDATE auf `temporary_credentials` (Linux-API-Lese-Pfad);
  kein DELETE. Cleanup ist eigener Folge-Slice.
- Identischer **Vault-Schluessel** auf Worker (`vault.config.dpapi` oder `KAUTH_WORKER_VAULT_KEY`)
  und Linux-API (`KAUTH_VAULT_KEY`-Env-Var). Anders koennen die vom Worker geschriebenen
  `temporary_credentials.encrypted_value`-Eintraege nicht entschluesselt werden, und der
  Welcome-Mail-Pfad scheitert mit klarer Meldung.

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

# 2. Vault-Key schreiben (Default: DPAPI in %ProgramData%\KauthWorker\vault.config.dpapi)
# Derselbe Key muss auf dem Linux-API-Host als Env-Var KAUTH_VAULT_KEY hinterlegt sein.
.\install-vault-key.ps1 -SymmetricKey '<32+ Zeichen>'

# Dev-Alternative: Klartext-JSON
# .\install-vault-key.ps1 -SymmetricKey '<...>' -PlainJson

# 3. Worker veroeffentlichen (auf einer Build-Maschine)
dotnet publish ..\AdAutomationWorker\AdAutomationWorker.csproj -c Release -o C:\Apps\KauthWorker

# 4. gMSA einrichten (siehe Abschnitt unten), dann Service registrieren
.\install-windows-service.ps1 `
    -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
    -ServiceAccount 'DOMAIN\kauth-worker$'

# 5. Service starten
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

## Vault-Setup (Pflicht ab Schritt 6)

Der Vault-Schluessel verschluesselt das vom Worker generierte Initial-Passwort. Er muss
bitgenau identisch sein zwischen Worker (`vault.config.dpapi` / Env-Var) und Linux-API
(`KAUTH_VAULT_KEY`-Env-Var). Sonst kann die API die Eintraege in `temporary_credentials`
nicht entschluesseln, und die Welcome-Mail scheitert mit klarer Meldung.

Empfohlene Erzeugung (auf einer beliebigen Maschine, einmalig):

```powershell
# 32-Byte Random in Base64 -- 43+ Zeichen, ueberschreitet die Mindestlaenge sauber.
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Den so erzeugten Wert auf jedem Worker mit `.\install-vault-key.ps1 -SymmetricKey '<wert>'`
ablegen und auf dem Linux-API-Host als `KAUTH_VAULT_KEY` setzen (z. B. `systemd`-Unit oder
docker-compose-Env). DPAPI bindet die Worker-Datei an die Maschine — Kopie auf eine andere VM
verlangt einen erneuten `install-vault-key.ps1`-Lauf dort.

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

## E2E `CreateAdUserLdaps` gegen Test-DC (Schritt 3, Pflicht-Verifikation)

1. DB-Seed sicherstellen (`db/manual/2026-05-12_seed_create_ad_user_ldaps_action.sql` einspielen
   oder Dev-Seed neu laden).
2. Worker `appsettings.Development.json` (oder Prod-`appsettings.json`) auf den Test-DC zeigen
   lassen: `Worker.Ad.DcHost`, `Worker.Ad.BaseDn` ausfuellen.
3. gMSA-Setup wie oben durchgefuehrt; Service unter dem gMSA gestartet.
4. Im Builder Workflow bauen mit Action `CreateAdUserLdaps`. Pflicht-Input-Mapping:
   - `samAccountName` (z. B. aus `target_person.employeeNumber`-Mapping mit deterministischer Praefixierung)
   - `userPrincipalName` (aus `directory_identity.userPrincipalName` oder konstruiert)
   - `displayName`, `givenName`, `surname`, `mail`
   - `targetOu` als Distinguished Name der Ziel-OU, z. B. `OU=Test-Sales,OU=Users,DC=test,DC=local`
   - optional `employeeNumber`
5. Workflow starten. In DB:
   - `automation_jobs.target_runtime='windows_worker'`, durchlaeuft pending → running → succeeded.
   - `automation_job_attempts.output_json` enthaelt `distinguishedName`, `samAccountName`,
     `userPrincipalName`, `temporaryPassword`, `mustChangePasswordAtNextLogon=true`.
   - `completion_processed_at` wird vom Sweeper gesetzt, Workflow ist am Automation-Node vorbei.
6. In AD: `Get-ADUser <sam> -Properties pwdLastSet,userAccountControl,mail,employeeID` zeigt
   den User mit `pwdLastSet=0` und aktivem Account.
7. **Idempotenz-Test:** Workflow erneut starten (selbe Person) → Output liefert
   `alreadyExisted: true`, kein zweiter Eintrag, kein `temporaryPassword`.
8. **Permanent-Fail-Test:** Workflow mit ungueltigem `targetOu` (`OU=Nope,DC=test,DC=local`) →
   Job `failed`, `error_message` enthaelt `LDAP 32`. Sweeper finalisiert, Workflow geht in den
   konfigurierten Fehler-Pfad.
