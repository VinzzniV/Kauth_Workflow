# Hybrid-Worker-Sub-Architektur

#architektur #automation #hybrid-ad

Detail-Doku zur Architektur des Windows-Workers für schreibende AD-Aktionen. Die Kurzfassung mit der übergeordneten Entscheidung (AD on-prem führt) steht in [[Entscheidungen]] § „AD/Entra-Schreibrichtung" und § „Hybrid-Worker-Sub-Architektur". Hier liegen die sieben Sub-Entscheidungen mit verworfenen Alternativen.

**Voraussetzungen aus dem Plan-Mode:** Linux-API und Windows-Worker on-prem im selben Subnetz/VPN; AD ≥ Windows Server 2012R2 mit gMSA-Unterstützung.

---

## 1. Worker-Deployment — dedizierte Windows-VM (domain-joined)

Eine Windows-Server-VM (2022, domain-joined), auf der ein einzelner Windows-Service-Prozess das Worker-Binary fährt. Hyper-V/VMware-Gast genügt; kein Container-Stack.

**Warum:** VM-Backup, WSUS-Patching, Standard-Monitoring greifen unverändert. Domain-Join ist Standard-AD-Operation — keine Container-Sondersetups (CCG / `--security-opt credentialspec`). Eine Komponente, klare Betriebsverantwortung.

**Verworfen:**
- **Domain-joined Container** mit gMSA via Credential Spec: technisch möglich, lohnt sich aber nur, wenn bereits ein Windows-Container-Stack existiert.
- **Azure-Hybrid-Worker:** externe Cloud-Steuerschicht für etwas, das vollständig on-prem laufen kann. Widerspricht dem on-prem-Primat.

## 2. Transport API↔Worker — DB-Polling auf zentrale Postgres

Der Worker pollt direkt dieselbe Postgres-DB, in der `automation_jobs` und `automation_job_attempts` liegen. Polling-Query: `status='pending' AND target_runtime='windows_worker'` mit `SELECT … FOR UPDATE SKIP LOCKED`.

**Warum:** Beide Komponenten on-prem im selben Netz. Kein zweiter Dienst (keine Worker-API-Surface, keine Message-Queue). Linux-API-Orchestrator schreibt in `automation_jobs`, Worker liest dieselbe Tabelle — eine Wahrheit.

**Verworfen:**
- **HTTPS-Pull gegen die Linux-API:** macht die API zum zweiten Service-Pfad mit eigener AuthN/Z, verdoppelt Failure-Modes.
- **Message-Queue** (RabbitMQ, Kafka, Service Bus): drittes betriebliches System ohne Mehrwert solange Worker und DB im selben Netz erreichbar sind.

## 3. AD-Schreibmechanik — `System.DirectoryServices.Protocols` (LDAPS)

Direkte LDAP-Operationen über .NET-8s eingebautes `System.DirectoryServices.Protocols` gegen LDAPS auf Port 636. Worker läuft unter gMSA-Kontext; `LdapConnection.AuthType = AuthType.Negotiate` nutzt SSPI/Kerberos transparent — **keine expliziten Credentials**.

**Warum:** Native .NET 8, kein PowerShell-Subprozess, kein RSAT, kein Modul-Dependency. Integriert sauber mit gMSA. Volle Kontrolle pro Operation, präzise Fehler-Klassifikation (ResultCode), gute Test-Mockbarkeit (Interface-Wrapper).

**Verworfen:**
- **PowerShell-Modul `ActiveDirectory`:** RSAT-Dependency, Subprocess-Spawn-Overhead, schwer zu testen, String-Fehler.
- **ADSI (COM-Interop):** Legacy, COM-Marshalling-Overhead, schlechte Unit-Test-Eignung.

Trade-off: `S.DS.Protocols` ist verbose. Mitigation: dünne Worker-interne Helper-Schicht (`IAdUserWriter`, `IAdGroupMembershipWriter`) mit klaren Eingabe-Records pro Operation.

## 4. Domänen-Auth — gMSA

Ein dedizierter gMSA (`gMSA-KauthWorker$`) als Service-Identity. Passwort-Rollover automatisch im KDC; keine manuelle Pflege. Berechtigungen über AD-Gruppen-Mitgliedschaft des gMSA (z. B. `KauthWorkerWritePermissions` mit Delegated-Rechten auf die User-OU).

**Warum:** Kein Klartext-Passwort, kein Secret-Store-Detour, kein Leak-Risiko.

**Verworfen:** **Klassischer Service-Account** (User mit Passwort) — funktioniert, ist aber pflegeaufwendiger und löst kein Problem, das gMSA nicht löst.

**Wichtig:** gMSA löst **ausschließlich die AD-Auth**, nicht die DB-Auth. Beide Pfade dürfen nicht vermischt werden (siehe Punkt 6).

## 5. Audit-Rückkanal — direkt in zentrale Tabellen

Der Worker verwendet dieselbe Postgres-Connection für das Polling und schreibt Status-Updates direkt in `automation_jobs` + `automation_job_attempts`. Kein API-Callback, keine zweite Schreibsenke.

**Warum:** Eine Wahrheit — dieselbe Tabelle, in die der Linux-API-Orchestrator schreibt. Kein neuer Endpoint. Die Failed-Automation-Sicht im Admin-Dashboard liest dieselben Tabellen ohne Änderung.

**DB-Sicherheit / Least-Privilege:** Worker-User `kauth_worker` bekommt SELECT auf die View `automation_jobs_windows_worker` (`WHERE target_runtime='windows_worker'`) + UPDATE auf die Basistabelle + INSERT auf `automation_job_attempts`. Kein DELETE, kein Zugriff auf andere Tabellen (`people`, `workflows`, …). Row-Level-Security wurde als Alternative erwogen, ist aber für den aktuellen Bedarf zu schwergewichtig.

**Verworfen:**
- **HTTP-Callback an Linux-API:** zweite Schreibsenke, neue Endpoint-Surface.
- **Lokales Worker-Logfile + Sync:** zwei Audit-Wahrheiten, Sync-Verzögerung, unklarer Restore-Pfad nach Crash.

## 6. Postgres-Auth des Workers — eigener DB-User + DPAPI-Konfig

Worker authentisiert mit eigenem DB-Login `kauth_worker` (**nicht** über gMSA). Passwort in DPAPI-geschützter Konfig auf der Worker-VM (`%ProgramData%\KauthWorker\db.config.dpapi`), entschlüsselbar nur unter dem gMSA-Sicherheitskontext.

**Warum:** gMSA löst AD-Auth, nicht DB-Auth — saubere Trennung. DPAPI bindet das DB-Passwort an Maschine + Service-User — Klartext-Datei auf Disk ist eliminiert. Postgres-Login ist Standard, keine Sondersetups in der DB-Infrastruktur. Rotation: `ALTER USER … PASSWORD …` + Neu-Schreiben der DPAPI-Datei.

**Verworfen:**
- **Postgres GSSAPI/SSPI gegen Kerberos:** technisch möglich, setzt sauber konfigurierte Server-Seite voraus (SPN, Keytab) — nicht bestätigt. Kann zukünftiger Slice werden.
- **Plain-Text-Konfigdatei:** Passwort von jedem Admin lesbar.
- **Secret-Store (Vault):** zusätzliche externe Abhängigkeit, neuer Failure-Mode (Vault nicht erreichbar → Worker startet nicht).

## 7. Job-Claim-Sicherheit — Lease + Heartbeat + Stale-Sweep

- **Claim:** `SELECT … FOR UPDATE SKIP LOCKED` auf der View + `UPDATE` auf die Basistabelle (Status `running`, `claimed_at`, `claimed_by = <hostname>:<pid>:<startup-uuid>`, `heartbeat_at`) + Attempt-Insert — alles in einer Transaktion.
- **Heartbeat:** alle **30 s** `UPDATE … SET heartbeat_at = NOW() WHERE id = @jobId AND claimed_by = @workerId`. Der `claimed_by`-Filter verhindert, dass ein nach Stale-Release übernommener Job vom alten Worker weiter geheartbeatet wird.
- **Stale-Timeout:** **5 min** ohne Heartbeat = verwaist.
- **Requeue:** Worker macht Lazy-Cleanup vor jedem Polling-Zyklus (`ReleaseStaleClaimsAsync`). Plus zentraler Linux-API-`StaleWorkerClaimSweeper` (60 s, 5 min) als Belt-and-Suspenders — greift, wenn alle Worker tot sind.
- **Sweeper-Two-Phase-Claim** (Linux-API-Seite): für extern finalisierte Jobs (`status IN ('succeeded','failed') AND target_runtime IS NOT NULL`) atomares `UPDATE … SET completion_claimed_at = NOW() … FOR UPDATE SKIP LOCKED` mit 5-min-Stale-Timeout, damit Mehrfach-API-Instanzen denselben Job nicht doppelt abarbeiten.

---

## Was die Etappe konkret liefert

Aktuell produktiv-tauglich auf diesem Worker-Pfad:

- **AD-Handler im Worker**: `CreateAdUserLdaps` (Pre-Search-Idempotenz, Race-Fallback bei `EntryAlreadyExists`; Random-Passwort mit Force-Change-at-Next-Logon) und `AssignGroupsLdaps` (Code 20 `AttributeOrValueAlreadyExists` = idempotent).
- **Failure-Klassifikation**: Worker tagt LDAP-Codes 49/50/32/21/19 + Payload-Validierungsfehler als `failure_kind='permanent'` → Retry-Policy macht sofort Final-Fail statt Backoff-Zyklen. Per-Action-Override-Spalten erlauben langlaufenden Handlern (Entra-Connect-Sync-Wait) ein größeres Budget.
- **Temporary-Credentials-Vault**: pgcrypto-Symmetric-Encryption für das Initial-Passwort. Worker schreibt Vault-Insert + Output-Patch + Job-Success in einer Tx. Decrypt nur in `SendWelcomeMailGraph`; Klartext lebt nirgendwo in `payload_json` oder `output_json`. Worker-Vault-Key über DPAPI (`vault.config.dpapi`), API-Seite über `KAUTH_VAULT_KEY`-Env — beide Hosts brauchen bitgenau denselben Key.

Linux-API-seitige Handler ergänzen den Worker-Pfad (Graph App-only): `SendWelcomeMailGraph`, `CreateMailboxGraph` (Exchange-Lizenz-Assignment mit SMTP-Strictness — kein UPN-Fallback).

Decision-Nodes können zusätzlich zu Formular-Antworten über `automation_output`-Bedingungen ihres direkten Predecessors verzweigen — initiale Whitelist `CreateAdUserLdaps.alreadyExisted`, damit der AlreadyExists-Fall ein gültiger Workflow-Pfad ist statt eines Permanent-Failures.

## Verwandte Notizen

- [[Entscheidungen]] — Kurzfassung dieser Sub-Architektur + AD/Entra-Schreibrichtung
- [[Migrationspfad]] — Wo Etappe 9a in der Gesamt-Migration steht
- [[Automation]] — Automation-Layer-Modell, in das der Worker einklinkt
