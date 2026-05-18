# Worker-Setup

#betrieb #worker #setup

Linearer Wizard fuer die Einrichtung des Windows-Worker auf einer dedizierten VM. Wer die Worker-Komponente erstmals installiert, geht diese fuenf Schritte einmal durch — alles andere ist Wiederholung mit `Configure-Worker.ps1`.

> **Wofuer ist der Worker?** Er pickt `automation_jobs` mit `target_runtime=windows_worker` und schreibt per LDAPS in on-prem AD. Architektur: [[Hybrid-Worker-Sub-Architektur]]. Fachliche Einordnung: [[Automation]].

---

## Voraussetzungen

| Was | Wo |
|---|---|
| Windows Server 2019+ / Windows 11 | Worker-VM |
| Admin-Rechte (lokal) | Worker-VM |
| .NET 8 Runtime | Worker-VM |
| Domain-joined | Worker-VM |
| Konnektivitaet zum Domain Controller (LDAPS Port 636) | Worker-VM → DC |
| Konnektivitaet zur Workflow-Postgres-DB | Worker-VM → DB |
| gMSA-Anlage-Recht | Domain-Admin |
| OU-Delegation auf der Ziel-OU (Create Child Objects + Reset Password) | Domain-Admin |
| Identischer `KAUTH_VAULT_KEY` zwischen Linux-API und Worker | beide Hosts |

---

## Schritt 1 — gMSA anlegen

Schreibende AD-Handler brauchen einen gMSA-Kontext (kein Klartext-Service-Passwort, transparenter Negotiate-Auth).

**Auf einem Domain Controller (Domain-Admin):**

```powershell
New-ADServiceAccount `
    -Name kauth-worker `
    -DNSHostName worker-vm.example.local `
    -PrincipalsAllowedToRetrieveManagedPassword 'WorkerVm-Hostgroup'
```

`WorkerVm-Hostgroup` ist eine AD-Gruppe, in der die Worker-VM Mitglied ist (oder direkter Computername).

**Auf der Worker-VM (Admin, RSAT-AD-PowerShell vorausgesetzt):**

```powershell
Install-ADServiceAccount kauth-worker
Test-ADServiceAccount kauth-worker     # MUSS True liefern
```

**OU-Delegation (in `dsa.msc` auf der Ziel-OU):**

- `Create Child Objects` (Klasse `user`)
- `Reset Password` auf user-Objekten

Ohne diese Delegation scheitert `CreateAdUserLdaps` mit `LDAP 50 (Insufficient Rights)`.

---

## Schritt 2 — Postgres-Login fuer den Worker

In der Workflow-DB einen eigenen Login fuer den Worker anlegen. Grants strikt auf das Noetige:

```sql
CREATE USER kauth_worker WITH PASSWORD '<secret>';

-- View fuer Worker-Job-Claim
GRANT SELECT ON automation_jobs_windows_worker TO kauth_worker;
GRANT UPDATE ON automation_jobs TO kauth_worker;
GRANT INSERT ON automation_job_attempts TO kauth_worker;
GRANT INSERT ON automation_job_logs TO kauth_worker;

-- Vault (Etappe 9a Schritt 6)
GRANT INSERT ON temporary_credentials TO kauth_worker;
-- Kein DELETE auf temporary_credentials -- Cleanup ist eigener Folge-Slice.
```

Der Linux-API-Login (`kauth_api` oder Aequivalent) braucht parallel `SELECT + UPDATE` auf `temporary_credentials`. Beides ist in [[Setup]] und [[Konfiguration]] dokumentiert.

---

## Schritt 3 — Worker publishen (auf einer Build-Maschine)

```powershell
dotnet publish .\worker\AdAutomationWorker\AdAutomationWorker.csproj `
    -c Release `
    -o C:\Apps\KauthWorker
```

Anschliessend `C:\Apps\KauthWorker\` auf die Worker-VM kopieren (oder direkt auf der Worker-VM publishen).

---

## Schritt 4 — Configure-Worker.ps1 ausfuehren

Auf der Worker-VM, in einem **Admin-PowerShell** auf dem ausgepackten `worker/setup`-Ordner:

```powershell
.\Configure-Worker.ps1
```

Das Skript fuehrt durch die drei Sub-Schritte:

1. **DB-Konfiguration** — fragt `DbHost`, `Database`, `Username`, `Password` ab; schreibt DPAPI-verschluesselt nach `%ProgramData%\KauthWorker\db.config.dpapi`.
2. **Vault-Key** — fragt nach dem symmetrischen Schluessel (mind. 32 Zeichen Base64); schreibt nach `%ProgramData%\KauthWorker\vault.config.dpapi`.
3. **Windows-Service** — fragt `BinaryPath` (Default `C:\Apps\KauthWorker\AdAutomationWorker.exe`) + `ServiceAccount` (gMSA mit trailing `$`); registriert den Service `KauthAdAutomationWorker`.

Nach jedem Sub-Schritt zeigt das Skript klare Status-Ausgaben. Existiert der Service schon, wird er gestoppt + neu registriert.

### Non-interactive (CI / Automation)

```powershell
.\Configure-Worker.ps1 `
    -DbHost postgres.example.local `
    -Database kauth_workflow `
    -DbUsername kauth_worker `
    -BinaryPath C:\Apps\KauthWorker\AdAutomationWorker.exe `
    -ServiceAccount 'DOMAIN\kauth-worker$'
```

`DbPassword` und `VaultKey` werden weiter als SecureString abgefragt — die wandern bewusst nicht ueber CLI-History.

### Spezialfaelle

- `-SkipServiceInstall` — nur DB-Konfig + Vault-Key neu schreiben (z. B. nach Vault-Key-Rotation).
- `-PlainJson` — Dev-Modus mit Klartext-JSON statt DPAPI. Loader gibt eine laute Warnung aus.
- `-WhatIf` — Trockenlauf, zeigt was passieren wuerde ohne die `install-*.ps1` aufzurufen.

---

## Schritt 5 — Verifikation

```powershell
# Service-Status
Get-Service KauthAdAutomationWorker

# DPAPI-Files vorhanden?
Test-Path "$env:ProgramData\KauthWorker\db.config.dpapi"
Test-Path "$env:ProgramData\KauthWorker\vault.config.dpapi"

# gMSA testet sich:
Test-ADServiceAccount kauth-worker      # True erwartet

# Service starten
Start-Service KauthAdAutomationWorker
Get-Service KauthAdAutomationWorker     # Running erwartet
```

**Smoke-E2E (`CreateAdUserLdaps` gegen Test-DC):** der Workflow `CreateAdUserLdaps`-Action triggert sich ueber den Builder; im Erfolg steht in der DB `automation_jobs.status='succeeded'` + `automation_job_attempts.output_json` mit `distinguishedName` und `temporaryPassword`. In AD ist der User mit `pwdLastSet=0` angelegt. Idempotenz-Test: zweiter Lauf mit derselben Person → `alreadyExisted: true`, kein zweiter Eintrag. Details: `worker/setup/README.md` § „E2E `CreateAdUserLdaps` gegen Test-DC".

---

## Fehlerbilder + Reaktion

| Symptom | Ursache | Reaktion |
|---|---|---|
| `Test-ADServiceAccount` liefert False | `Install-ADServiceAccount` nicht gelaufen oder VM nicht in `PrincipalsAllowedToRetrieveManagedPassword`-Gruppe | Schritt 1 wiederholen, `Get-ADServiceAccount kauth-worker -Properties *` pruefen |
| Service startet, schreibt aber keinen Job | DB-Konfig falsch | Logs unter `worker/AdAutomationWorker/`; `install-db-config.ps1` neu mit korrektem Host/Port |
| Welcome-Mail scheitert mit „Vault-Decrypt fehlgeschlagen" | Worker-Vault-Key != API-`KAUTH_VAULT_KEY` | Beide Seiten neu setzen, gleicher Wert |
| `CreateAdUserLdaps` failt mit LDAP 50 | OU-Delegation fehlt | `dsa.msc` → Ziel-OU → Delegation pruefen |
| `CreateAdUserLdaps` failt mit LDAP 32 | Ziel-OU existiert nicht | Builder-Mapping korrigieren |
| Service laeuft unter LocalSystem | `Configure-Worker.ps1` ohne `ServiceAccount` | mit `Configure-Worker.ps1 -ServiceAccount 'DOMAIN\kauth-worker$'` neu installieren |

---

## Maschinenwechsel

Die DPAPI-Files sind an die Maschine gebunden (LocalMachine-Scope). Wenn die Worker-VM neu aufgesetzt wird oder migriert:

1. Auf der neuen VM Schritt 1 (gMSA `Install-ADServiceAccount`) wiederholen.
2. `Configure-Worker.ps1` erneut ausfuehren — die DPAPI-Files werden mit dem Maschinen-Schluessel der neuen VM neu erzeugt.
3. **Der Vault-Key bleibt derselbe** — die symmetrische Geheimnis-Wahrheit zwischen Worker und API ist unabhaengig von der Maschine.

---

## Verwandte Dateien

- [[Konfiguration]] — Variablen-Tabelle inkl. Worker-Sektion (`Worker:*`-Keys + DPAPI-Files)
- [[Setup]] — gesamte Setup-Doku (lokal + VM-Deploy + DB-Inits)
- [[Automation]] — fachliche Einordnung des Worker
- [[Hybrid-Worker-Sub-Architektur]] — Architektur-Detail (Polling, Lease, Sweeper)
- `worker/setup/README.md` — Kurz-Doku am Skript-Ordner; E2E-Test-Drehbuecher fuer `simulated_windows_worker_ping` und `CreateAdUserLdaps`
