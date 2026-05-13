# Hybrid-Worker-Sub-Architektur

#stand #architektur #automation #hybrid-ad

Detail-Spiegel zur Entscheidung "Hybrid-Worker-Sub-Architektur" (2026-05-12, Migrationspfad-Etappe 9a Schritt 1). Begründungen und verworfene Alternativen pro Sub-Entscheidungspunkt — die Kurzfassung steht in [[Entscheidungen]] § "Hybrid-Worker-Sub-Architektur".

Stakeholder-Inputs (Plan-Mode 2026-05-12):
- Netz-Topologie: Linux-API und Windows-Worker on-prem im selben Subnetz/VPN; Worker erreicht zentrale Postgres direkt.
- Domänen-Auth: AD ≥ Windows Server 2012R2, gMSA wird unterstützt.

---

## 1. Worker-Deploymentmodell — dedizierte Windows-VM (domain-joined)

Eine eigenständige Windows-Server-VM (Windows Server 2022, domain-joined), auf der ein einzelner Windows-Service-Prozess das Worker-Binary fährt. Hyper-V/VMware-Gast genügt; kein zusätzlicher Container-Stack.

**Warum:**
- Robust und vorhersagbar: VM-Backup, WSUS-Patching, Standard-Monitoring greifen unverändert.
- Domain-Join ist Standard-AD-Operation, keine Sondersetups für gMSA-in-Container (CCG / `--security-opt credentialspec`) nötig.
- Eine einzelne Komponente, klare Betriebsverantwortung.

**Verworfen:**
- **Domain-joined Container (Windows-Server-Container mit gMSA via Credential Spec):** technisch möglich seit Windows Server 2019, zusätzliche Komplexität (CCG-Config, Image-Pflege, Image-Distribution on-prem). Lohnt sich nur, wenn bereits ein Windows-Container-Stack existiert.
- **Azure-Hybrid-Worker:** Automation-as-a-Service in Azure mit on-prem-Agent. Externe Cloud-Steuerschicht für etwas, das vollständig on-prem laufen kann. Widerspricht dem on-prem-Primat aus Z21-S2.

---

## 2. Transport API↔Worker — DB-Polling auf zentrale Postgres

Der Worker pollt die zentrale Postgres-DB direkt — dieselbe DB, in der bereits `automation_jobs` und `automation_job_attempts` liegen. Polling-Query selektiert Jobs mit `status='pending'` und einem neuen Diskriminator-Feld (`target_runtime='windows_worker'`); `SELECT … FOR UPDATE SKIP LOCKED` für sichere Mehrfach-Worker-Erweiterung.

**Warum:**
- Beide Komponenten on-prem im selben Netz → DB-Erreichbarkeit ist gegeben.
- Kein zweiter Dienst (keine REST-API-Surface auf der Linux-API nur für Worker-Pull, keine Message-Queue).
- `WorkflowAutomationHostedService` (Linux-API) bleibt Orchestrator und schreibt nur in `automation_jobs`; der Worker liest dieselbe Tabelle. Eine Wahrheit.
- Existing-Schema bereits vorhanden, nur additive Erweiterung (`target_runtime`-Spalte plus Lease-Spalten, siehe Punkt 7) nötig.

**Verworfen:**
- **HTTPS-Pull gegen die Linux-API:** macht die API zu einem zweiten Service-Pfad (Worker-Endpoint mit eigener AuthN/Z), verdoppelt Failure-Modes. Nur sinnvoll wenn der Worker outbound-only ist — ist er nicht.
- **Service-Bus / Message-Queue (RabbitMQ, Kafka, Azure Service Bus):** maximale Entkopplung, aber drittes betriebliches System nur für diesen einen Pfad. Kein Mehrwert solange Worker und DB im selben Netz erreichbar sind.

---

## 3. AD-Schreibmechanik — System.DirectoryServices.Protocols (LDAPS)

Direkte LDAP-Operationen über .NET 8s eingebaute `System.DirectoryServices.Protocols`-Bibliothek gegen einen LDAPS-Endpunkt (Port 636) des Domain-Controllers. Der Worker-Service-Prozess läuft unter dem gMSA-Sicherheitskontext; `LdapConnection.AuthType = AuthType.Negotiate` nutzt diesen Kontext transparent (SSPI/Kerberos). **Es werden keine Credentials explizit an `LdapConnection` übergeben** — das ist genau der Punkt von gMSA.

**Warum:**
- Native .NET 8, kein PowerShell-Subprozess, kein RSAT, kein PS-Modul-Dependency. Worker-Image bleibt schlank.
- Integriert sauber mit gMSA-Auth (Negotiate/Kerberos läuft transparent über den Service-User-Kontext).
- Volle Kontrolle über jede Schreib-Operation, präzise Fehler-Klassifikation (ResultCode pro Operation), gute Test-Mockbarkeit (Interface-Wrapper).

**Verworfen:**
- **PowerShell-Modul `ActiveDirectory`:** einfacher zu schreiben (deklarative `New-ADUser`-Aufrufe), aber: braucht RSAT auf der Worker-VM, Subprocess-Spawn-Overhead pro Operation, schwerer zu testen, Fehler kommen als Strings statt typisierten Codes.
- **ADSI (COM-Interop):** Legacy, von Microsoft nicht mehr aktiv weiterentwickelt, COM-Marshalling-Overhead, schlechte Unit-Test-Eignung.

Trade-off bewusst akzeptiert: `S.DS.Protocols` ist verbose — pro Operation mehrere Zeilen Code. Mitigation: dünne Worker-interne Helper-Schicht (z. B. `AdUserWriter.CreateUser(spec)`) mit klaren Eingabe-Records pro Operation.

---

## 4. Domänen-Authentisierung des Workers — gMSA

Ein dedizierter gMSA in der Domäne (z. B. `gMSA-KauthWorker$`) wird als Service-Identity für den Windows-Service-Prozess konfiguriert. Passwort-Rollover läuft automatisch im KDC; keine manuelle Pflege.

**Warum:**
- Kein Klartext-Passwort, kein Secret-Store-Detour, kein Leak-Risiko.
- Domäne unterstützt es (bestätigt durch Stakeholder).
- Berechtigungen werden über AD-Gruppen-Mitgliedschaft des gMSA gesteuert (z. B. eine `KauthWorkerWritePermissions`-Gruppe mit Delegated-Rechten auf die User-OU).

**Verworfen:**
- **Klassischer Service-Account (User-Objekt mit Passwort):** funktioniert immer, ist aber pflegeaufwendiger und löst kein Problem, das gMSA nicht löst.

**Wichtiger Geltungsbereich:** gMSA löst **ausschließlich die AD/Domänen-Authentisierung**. Die Postgres-Authentisierung des Workers ist eine separate Entscheidung (siehe Punkt 6). Beide Auth-Pfade dürfen nicht vermischt werden.

**Vorbedingung für Live-Inbetriebnahme:** Domain-Admin legt das gMSA an und installiert es per `Install-ADServiceAccount` auf der Worker-VM. **Nicht** Blocker für den Code-Skeleton — Schritt 2 kann mit Test-Credentials oder einem lokalen AD-LDS-Testserver entwickelt werden; der gMSA-Switch erfolgt vor erstem produktivem Lauf.

---

## 5. Audit-Rückkanal — direkt in zentrale automation_jobs + automation_job_attempts

Der Worker verwendet dieselbe Postgres-Connection wie für das Polling und schreibt Status-Updates direkt in die zentralen Tabellen — kein API-Callback, keine zweite Schreibsenke. Die existing `automation_job_attempts`-Tabelle ist bereits dafür ausgelegt (`error_message`, `started_at`, `completed_at`, `status`).

**Warum:**
- Eine Wahrheit: dieselbe Tabelle, in der heute der `WorkflowAutomationHostedService` Audit schreibt, ist auch die Tabelle, in die der Worker schreibt. Linux-API liest beide Quellen einheitlich.
- Kein neuer Endpunkt auf der Linux-API für "Worker meldet Status zurück".
- Die Failed-Automation-Sicht aus TODO Z21-S5 (Stat-Tiles im Admin-Dashboard) funktioniert ohne Änderung — sie liest dieselben Tabellen.

**DB-Sicherheit / Least-Privilege:** Worker bekommt einen eigenen Postgres-User (`kauth_worker`). Postgres-GRANTS sind tabellen-/spaltengebunden — eine row-filterung per `WHERE target_runtime='windows_worker'` lässt sich **nicht** mit einem einfachen `GRANT … ON automation_jobs` ausdrücken. Drei tragfähige Optionen, **Entscheidung gehört in den Schritt-2-Slice** (DB-Migration):

- **Option A (Recommended): View `automation_jobs_windows_worker`** als `CREATE VIEW … AS SELECT … WHERE target_runtime = 'windows_worker'`. Worker bekommt `SELECT` auf die View und `UPDATE` auf die Basistabelle. Einfach zu auditieren.
- **Option B: Row Level Security (`ALTER TABLE … ENABLE ROW LEVEL SECURITY`)** mit Policy `target_runtime = 'windows_worker'` für den Worker-User. Sauberer Filter für SELECT **und** UPDATE in einem, zusätzlicher Konfig-Pfad in der DB.
- **Option C: Bewusst akzeptierter breiterer GRANT** auf die ganze Tabelle. Risiko klein, weil der Worker eh nur auf `automation_jobs` zugreift; Threat-Model "kompromittierter Worker schreibt fremde Job-Zeilen" durch enge Worker-Codebase + Audit-Trail abgemildert.

Plus in jedem Fall: `INSERT` auf `automation_job_attempts`, **kein** `DELETE`, **kein** Zugriff auf andere Tabellen (`people`, `workflows`, …).

**Verworfen:**
- **HTTP-Callback an Linux-API:** zweite Schreibsenke, neue Endpoint-Surface, doppelte Audit-Pfade.
- **Lokales Worker-Logfile + späterer Sync:** zwei Audit-Wahrheiten, Sync-Verzögerung, Restore-Pfad nach Crash unklar.

---

## 6. Postgres-Authentisierung des Workers — eigener DB-User + DPAPI-Konfig

Der Worker authentisiert gegen die zentrale Postgres-DB mit einem eigenen, dedizierten DB-Login (`kauth_worker`), **nicht** über die gMSA-Identität. Das Passwort liegt auf der Worker-VM in einer DPAPI-geschützten Konfig-Datei (`%ProgramData%\KauthWorker\db.config.dpapi`), entschlüsselbar nur unter dem gMSA-Sicherheitskontext, in dem der Worker-Service läuft.

**Warum:**
- gMSA löst AD-Auth, nicht DB-Auth. Beide Ebenen sauber trennen.
- DPAPI-Schutz bindet das DB-Passwort an die Worker-Maschine **und** den Service-User — Klartext-Datei auf Disk ist eliminiert.
- Postgres-Login ist Standard (Username/Passwort), keine Sondersetups in der DB-Infrastruktur nötig.
- Rotation: `ALTER USER … PASSWORD …` + Neu-Schreiben der DPAPI-Datei. Nicht so elegant wie gMSA-Rollover, aber pflegbar.

**Verworfen:**
- **Postgres GSSAPI/SSPI-Auth gegen Kerberos:** technisch möglich (`pg_hba.conf` mit `gss`-Auth), würde direkt mit gMSA-Kontext laufen. Setzt voraus, dass die Postgres-Server-Seite GSSAPI sauber konfiguriert ist (Service-Principal-Name auf Postgres-Host, Kerberos-Keytab) — das ist on-prem-Linux-Postgres-Setup, das **nicht bestätigt** ist. Kann zukünftiger Slice werden, falls die DB-Umgebung GSSAPI trägt — dann fällt die DPAPI-Datei weg.
- **Plain-Text-Konfigdatei mit DB-Passwort:** Standardpraxis vieler Services, aber Passwort steht im Klartext auf Disk und ist von jedem Admin lesbar.
- **Secret-Store wie Vault:** zusätzliche externe Abhängigkeit, neuer Failure-Mode (Vault nicht erreichbar → Worker startet nicht).

**Folge-Item für Schritt 2:** Skeleton-Slice legt das DPAPI-Setup-Skript und die Lade-Logik im Worker an.

---

## 7. Job-Claim-Sicherheit (Lease/Heartbeat/Timeout/Requeue) — konkret entschieden in Schritt 2 (2026-05-12)

Beim Polling muss klar sein, was passiert, wenn der Worker einen Job abholt und dann abstürzt, bevor er den Status aktualisiert. Sonst bleibt der Job auf `running` hängen und niemand picked ihn wieder auf.

**Festgelegt:**
- Worker-Claim per `SELECT … FOR UPDATE SKIP LOCKED` auf View `automation_jobs_windows_worker` plus `UPDATE` auf die Basistabelle (Status `running`, `claimed_at = NOW()`, `claimed_by = <hostname>:<pid>:<startup-uuid>`, `heartbeat_at = NOW()`) plus Attempt-Insert — alles in einer Transaktion.
- **Heartbeat:** Worker schreibt `UPDATE … SET heartbeat_at = NOW() WHERE id = @jobId AND claimed_by = @workerId` im **30-Sekunden-Takt** (`WorkerHeartbeatLoop`). Der `claimed_by`-Filter ist Lease-Schutz: wenn der StaleReleaser zwischendurch andere Worker den Job übernommen hat, geht der alte Heartbeat nicht durch.
- **Stale-Timeout:** **5 Minuten** ohne Heartbeat — ein Job mit `status='running' AND heartbeat_at < NOW() - INTERVAL '5 minutes'` gilt als verwaist.
- **Requeue:** **Lazy-Cleanup im Worker selbst** vor jedem Polling-Zyklus (`ReleaseStaleClaimsAsync` setzt Status zurück auf `pending`, leert Lease-Spalten). Keine separate Sweep-Logik in der Linux-API in dieser Etappe — kommt erst in Schritt 3, wenn echte AD-Handler im Spiel sind und Multi-Worker zur Pflicht wird.
- **Sweeper-Two-Phase-Claim (Linux-API-Seite):** Für extern finalisierte Jobs (`status='succeeded'/'failed' AND target_runtime IS NOT NULL`) atomares `UPDATE … SET completion_claimed_at = NOW() … FOR UPDATE SKIP LOCKED` plus 5-min-Stale-Timeout, damit Mehrfach-API-Instanzen sich nicht denselben Job greifen.

---

## Schritt 3 (✓ 2026-05-12) — Erster echter LDAPS-Handler + DPAPI produktiv

`CreateAdUserLdaps` (Action ID 7, parallel zur Simulation ID 1) schreibt AD-User via LDAPS unter dem gMSA-Kontext. Layering: `IAdUserWriter`/`AdUserSpec`/`AdWriteOutcome` plattform-neutral im Core, `LdapsAdUserWriter` (`System.DirectoryServices.Protocols`, `AuthType.Negotiate`) im net8.0-Windows-Host. Idempotenz: Pre-Search + Race-Fallback bei `EntryAlreadyExists` (Code 68). Initial: Enabled mit CSPRNG-Random-Passwort + `pwdLastSet=0`. Minimal-Whitelist im Writer: Codes 49/50/32/21/19 → `PermanentFailure`, sonst `TransientFailure`. Bewusst akzeptierte Trade-offs (siehe [[Entscheidungen]]): `temporaryPassword` im `output_json`, `is_idempotent=true` ohne differenzierte Klassifikation.

DPAPI: `IDbConfigDecryptor` im Core, `WindowsDpapiDecryptor` (Scope LocalMachine) im Host. `install-db-config.ps1` Default schreibt `db.config.dpapi`; `-PlainJson` bleibt als Dev-Fallback mit Warn-Log. `install-windows-service.ps1` setzt den gMSA via `sc.exe config obj=`.

## Schritt 4 (✓ 2026-05-12) — Härtung: Failure-Klassifikation + Linux-Stale-Sweep

Zwei in Schritt 3 bewusst offen gelassene Trade-offs geschlossen:

- **`failure_kind`-Marker am Attempt** (`automation_job_attempts.failure_kind`, varchar(20), CHECK 'permanent'|'transient'|NULL). Worker schreibt ihn beim `MarkJobFailedAsync`: LDAP-Codes 49/50/32/21/19 und Payload-Validierungsfehler → `'permanent'`; alles andere → `'transient'`. `WorkflowAutomationRetryPolicy.EvaluateRetryOutcome` bekommt einen optionalen `failureKind`-Parameter; `'permanent'` → sofort `FinalFail` (überschreibt `is_idempotent` und `attemptNumber`). Damit fallen die unnötigen Retry-Backoff-Cycles bei klar permanenten Fehlern weg — der Audit-Trail wird ehrlich, die Worker-Job-Rate steigt nicht durch Wiederholungen von hoffnungslosen Calls. Linux-Handler ohne Tagging schreiben weiter `NULL` und behalten die bestehende Retry-Logik (Vorbereitung für künftige echte Linux-Handler).
- **Zentraler `StaleWorkerClaimSweeper`** als Linux-API-HostedService (60s-Polling, 5min-Stale-Default via `WorkerLeaseSettings`). Greift parallel zum Worker-internen `ReleaseStaleClaimsAsync` — Belt-and-Suspenders. Räumt verwaiste `running`-Jobs auch dann auf, wenn alle Worker tot sind und kein Worker den Lazy-Cleanup mehr ausführen kann.

## Schritt 5 (✓ 2026-05-12) — Vertrags-Plumbing + zwei weitere reale Handler

Drei Vertrags-Erweiterungen plus zwei reale Handler:

- **Enge `created_ad_user`-Mapping-Source** mit harter Property-Whitelist (nur `distinguishedName`) und `nodeKey`-Referenzierung. Sensible Output-Felder (insbesondere `temporaryPassword`) sind durch keine Input-Mapping-Source adressierbar — Vault-Grenze für Schritt 6 ist im Code, nicht nur in Doku. Alle vier Fehler-Cases werfen `InvalidOperationException` direkt beim Payload-Build (sichtbar im Workflow-Audit, kein kryptischer LDAP-Spätfehler).
- **Worker-Failure-Pfad trägt Output:** `MarkJobFailedAsync` schreibt jetzt `output_json` auch im Failure-Fall — `PartiallyAdded` bei `AssignGroupsLdaps` (X von N Gruppen erfolgreich, eine permanent gescheitert) liefert einen strukturierten Failure mit Detail-Output (newlyAdded/alreadyMember/failed).
- **Linux-Handler-Vertrag strukturiert:** `WorkflowAutomationHandlerResult` mit `IsSuccess` (default `true`), `ErrorMessage`, `FailureKind` + Success/Failure-Factories. Bestehende simulierte Handler bleiben rückwärtskompatibel. `WorkflowAutomationService` mappt Result-Failures mit FailureKind in die Retry-Policy; Exception-Pfad bleibt Default ohne Tagging.
- **`AssignGroupsLdaps`** als zweiter LDAPS-Handler im Worker (`LdapsAdGroupMembershipWriter`): Member-Add per Group; AD-Code 20 (`AttributeOrValueAlreadyExists`) als `AlreadyMember` → idempotent. Connection-Level-Fehler werden zu Top-Level-Failure aggregiert statt leerer PartiallyAdded.
- **`SendWelcomeMailGraph`** als erster echter Linux-side-Handler via Microsoft Graph App-only (`Users[senderEmail].SendMail.PostAsync`). `INotificationTemplateResolver` extrahiert die Catalog+DB-Override-Logik aus der frühreren privaten `NotificationTemplateService.GetEffectiveTemplate`. Neuer Catalog-Eintrag `welcome_mail` ohne `{{temporary_password}}`-Placeholder (Vault-Slice macht das in Schritt 6). Handler-Registry von Singleton auf Scoped umgestellt, damit Scoped-Handler resolved werden können.

## Schritt 6 (✓ 2026-05-13) — Temporary-Credentials-Vault (pgcrypto) + Welcome-Mail-Anbindung

End-to-End-Onboarding-Pfad mit echtem Initial-Passwort in der Welcome-Mail. Plain-Passwort verlaesst den Worker-Prozess nur kurzzeitig im Heap, landet weder in `automation_jobs.payload_json` noch in `automation_job_attempts.output_json`.

- **Tabelle `temporary_credentials`** (pgcrypto symmetric, `pgp_sym_encrypt(@plain, @key)`). PK = UUID via `gen_random_uuid()`. FK `workflow_node_instance_id → workflow_node_instances` mit `ON DELETE CASCADE` (keine Vault-Waisen nach Workflow-Cancel). UNIQUE(workflow_node_instance_id, credential_type) erzwingt: pro NodeInstance max. eine Initial-Passwort-Zeile. Audit-Spalten `first_read_at`/`read_count` (kein Read-Block — Mehrfach-Lesen erlaubt fuer Retry-Pfade).
- **Eng geschnittene GRANTs:** `kauth_worker` darf nur INSERT (kein SELECT/UPDATE/DELETE — Worker kann das Vault nicht lesen). `kauth_api` SELECT + UPDATE (Decrypt + Audit-Bump). DELETE bleibt unzugewiesen — Cleanup ist Folge-Slice.
- **Atomare Schreib-Tx** im `PostgresWorkerJobStore`: Vault-Insert + Attempt-Insert + Job-Success in derselben Transaktion. `ON CONFLICT (workflow_node_instance_id, credential_type) DO NOTHING RETURNING id` + SELECT-Fallback fuer Stale-Claim-Retry. `credentialVaultId`-Platzhalter im output_json wird vor dem Attempt-Insert per `Utf8JsonWriter`-Patch durch die UUID ersetzt. Damit ist kein Crash-Pfad mehr moeglich, der Vault-Waisen oder fehlende Pointer erzeugt.
- **`CreateAdUserLdapsHandler`** schreibt `temporaryPassword` nicht mehr ins Output. Stattdessen liefert er einen `PendingVaultWrite { PlainSecret, WorkflowNodeInstanceId, CredentialType }` zurueck und setzt `credentialVaultId=null` als Platzhalter ins Output (Schema-stabil fuer Konsumenten unabhaengig vom Schreibpfad).
- **`created_ad_user`-Mapping-Source** akzeptiert jetzt zwei Properties: `distinguishedName` (wie bisher, hart-fail bei fehlend) und neu `credentialVaultId` (UUID, kein Geheimnis). Null/fehlt → null durchgereicht (Plan-Review-Korrektur: AlreadyExists-Case darf nicht den Payload-Build crashen — der Folge-Job-Handler validiert zur Run-time und liefert einen sauberen Permanent-Failure statt einen Sweeper-Loop zu triggern). Object/Array → hart-fail (Misconfig).
- **`SendWelcomeMailGraphHandler`** ist der einzige Vault-Konsument. Pflicht-Payload-Feld `credentialVaultId` (UUID). Handler ruft `ITemporaryCredentialRepository.ReadAdInitialPasswordByVaultIdAsync(uuid)` zur Run-time im eigenen Prozess auf. Plain-Passwort lebt nur Mikrosekunden im Handler-Heap, geht NIE ins output_json. Catalog-Template hat jetzt `{{temporary_password}}`-Placeholder; Bestandstemplates (DB-Override) muessen Admins selbst pflegen.
- **Vault-Key-Verteilung:** Worker-Seite `KAUTH_WORKER_VAULT_KEY`-Env > `vault.config.dpapi` (LocalMachine-Scope) > `vault.config.json` (Dev-Fallback). `install-vault-key.ps1` analog `install-db-config.ps1` aus Schritt 3. API-Seite `KAUTH_VAULT_KEY`-Env-Var (Singleton-Halter, einmal beim Start gelesen). Beide Hosts brauchen bitgenau denselben Schluessel.
- **Vault-Grenze ist im Code, nicht in Doku:** `ITemporaryCredentialRepository` exponiert ausschliesslich Lookup-by-UUID — keine Mapping-Source-Implementierung kann das Plain-Passwort beim Payload-Build entschluesseln. Konsumenten muessen die `created_ad_user.credentialVaultId`-Verkettung verwenden; die UUID ist kein Geheimnis.
- **AlreadyExists-Pfad** (extern-vorhandenes AD-Konto): kein Vault-Insert, `credentialVaultId=null` im Output. Welcome-Mail-Folge-Job liefert sauberen Permanent-Failure mit Hinweis "predecessor likely an AlreadyExists case". Workflow-Designer muss solche Faelle aktuell durch eigene Workflow-Auswahl abfedern — Branch-on-automation-output ist eigener Workflow-Engine-Slice.

Praktischer Nutzen: das Initial-Passwort liegt nirgendwo plain in der DB, nur kurz im RAM von Worker (Generierung → Tx-Commit) und API-Handler (Decrypt → Mail-Send). TTL macht es nach Ablauf automatisch wertlos — Lese-Pfad wirft mit klarer Meldung. End-to-End Onboarding ohne Helpdesk-Pickup ist produktionsreif (Fresh-Create).

## Verwandte Notizen

- [[Entscheidungen]] — Kurzfassung dieser Sub-Architektur + Hauptentscheidung Z21-S2 + Schritt-3/4/5/6-Update
- [[Migrationspfad]] — Etappe 9a mit fixiertem Schritt 1 + 2 + 3 + 4 + 5 + 6 (zuletzt 2026-05-13)
- [[Automation]] — Automation-Layer-Modell, in das der Worker einklinkt
