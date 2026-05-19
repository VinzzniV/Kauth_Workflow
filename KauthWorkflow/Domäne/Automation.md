# Automation Layer

#domäne #automation

Wie automatische technische Aktionen im System kontrolliert ablaufen. Diese Datei erklärt den **heutigen Ist-Stand** der Automation-Infrastruktur. Das **Zielbild für Prod** (Admin-gated Automation mit Plan-Vorschau + Re-Auth + 360°-Karte) steht in [[Admin-Gated-Automation]]; die Plan-Vorschau, das Task-Automation-Binding, der Approval-Dialog mit echtem MSAL-`prompt: 'login'`-Popup und Backend-`auth_time`-Claim-Check (Bundle-Stepper), der Live-Log mit per-Action-Status, die Post-Execution 360°-Karte (Identitäts-Snapshot, Gruppen, Mailbox, Workflow-Spur, Vault-Status pro Person) und das Referenzuser-Mapping (Cloud-Gruppen-Übernahme via `person_lookup`-Form-Antwort) sind seit 2026-05-19 produktiv nutzbar — externe Pflicht-Konfiguration: `auth_time` als optional claim in der API-App-Registration (siehe [[Konfiguration]] und [[Deployment-Checkliste]]).

---

## Grundprinzip: Kontrolliert, nicht frei

Admins konfigurieren fachlich — sie hinterlegen **keine** freien technischen Aktionen.

Verboten: freies PowerShell, freies SQL, beliebige HTTP-Requests mit Secrets aus dem Admin-UI.
Erlaubt: freigegebene, validierte Actions aus dem Action-Katalog (`action_definitions`).

---

## Zwei Ausführungsumgebungen

Schreibende Automation läuft auf zwei Hosts, die zusammenarbeiten:

```
┌──────────────────────┐         ┌──────────────────────────┐
│   Linux-API          │         │   Windows-Worker         │
│   (Hauptserver)      │         │   (dedizierte VM)        │
│                      │         │                          │
│ - Workflow-Engine    │         │ - Schreibt in on-prem AD │
│ - Microsoft Graph    │         │   via LDAPS (Negotiate)  │
│   (Cloud-Calls)      │         │ - Läuft unter gMSA       │
│                      │         │   (kein Klartext-Pwd)    │
└──────────┬───────────┘         └────────────┬─────────────┘
           │                                  │
           │     beide reden mit derselben DB │
           ▼                                  ▼
        ┌─────────────────────────────────────────┐
        │  PostgreSQL                             │
        │  - automation_jobs  (Job-Queue)         │
        │  - automation_job_attempts  (Audit)     │
        │  - temporary_credentials  (Vault)       │
        └─────────────────────────────────────────┘
```

**Warum zwei Hosts?** AD ist on-prem und braucht einen domain-joined Windows-Host (gMSA = passwortlose Service-Identität). Cloud-APIs (Microsoft Graph für Mail + Exchange-Lizenz) sind aus der Linux-API einfacher. Die übergeordnete Architektur-Entscheidung „on-prem AD führt" steht in [[Entscheidungen]] § AD/Entra-Schreibrichtung.

**Warum DB statt HTTP/Queue?** Beide laufen on-prem im selben Netz, die DB ist eh schon da. Eine Wahrheit ist besser als zwei Schreibsenken. Jobs tragen ein `target_runtime`-Feld — der Worker holt `windows_worker`-Jobs, die Linux-API holt `NULL`-Jobs.

---

## Was das System konkret kann

Neun produktiv-taugliche Handler:

| Handler | Wo | Was er tut |
|---|---|---|
| `CreateAdUserLdaps` | Worker | Legt AD-User an. Pre-Search verhindert Doppelanlage; `EntryAlreadyExists`-Race-Fallback macht den Pfad idempotent. CSPRNG-Random-Passwort, `pwdLastSet=0` erzwingt Change-at-First-Login. |
| `AssignGroupsLdaps` | Worker | Fügt User zu AD-Gruppen. AD-Code 20 (`AttributeOrValueAlreadyExists`) = idempotent. Teilfehler werden als `PartiallyAdded` mit Detail-Output abgebildet. |
| `CreateMailboxGraph` | Linux-API | Weist Exchange-Online-Lizenz zu. Exchange Online provisioniert die Mailbox automatisch. Wartet auf Entra-Connect-Sync (bis zu ~41 min Retry-Budget). Liefert die echte primäre SMTP-Adresse zurück — **kein UPN-Fallback**. |
| `SendWelcomeMailGraph` | Linux-API | Entschlüsselt das Initial-Passwort aus dem Vault, sendet Welcome-Mail an die echte SMTP-Adresse mit Klartext-Passwort im Body. |
| `DisableAdUserLdaps` | Worker | Setzt das ACCOUNTDISABLE-Bit (`userAccountControl \|= 0x2`) via LDAPS. Idempotent: bereits deaktivierter User liefert `alreadyDisabled=true`. Offboarding-Handler. |
| `RemoveFromAllGroupsLdaps` | Worker | Entfernt User per LDAP-Filter `(&(objectClass=group)(member=<dn>))` aus allen Gruppen via ModifyRequest. Idempotent: Code 16 (NoSuchAttribute) = `AlreadyRemoved`. Teilfehler im `failed`-Array. Offboarding-Handler. |
| `RemoveMailboxLicense` | Linux-API | Entfernt Exchange-Online-Lizenz via Microsoft Graph. Idempotent: Lizenz nicht zugewiesen → `licenseNotAssigned=true`. Pflicht-Permissions: User.Read.All + LicenseAssignment.ReadWrite.All. Offboarding-Handler. |
| `MoveAdUserOuLdaps` | Worker | Verschiebt AD-User per ModifyDN in andere OU (RDN bleibt). Idempotent: User bereits in Ziel-OU → `alreadyInTargetOu=true`. PlanAsync zeigt current/target-OU. Change-Handler. |
| `UpdateAdUserAttributesLdaps` | Worker | Aktualisiert Whitelist-Attribute (manager, department, title, description) via ModifyRequest. JSON null = Clear. Nur tatsächlich geänderte Attribute werden geschrieben. PlanAsync zeigt current vs. planned. Change-Handler. |

Parallel dazu existieren simulierte Handler (`simulated_*`) für Bestands-Workflows und für Dev-Setups — diese tun nichts, melden aber Success. Das UI kennzeichnet simulierte Actions sichtbar.

---

## Ein End-to-End-Onboarding heute

```
HR füllt Formular aus
      │
      ▼
CreateAdUserLdaps  (Worker)
      │  AD-User existiert, Passwort verschlüsselt im Vault
      ▼
CreateMailboxGraph  (Linux-API)
      │  License zugewiesen, primary SMTP bekannt
      ▼
Decision-Node: alreadyExisted?
      │
      ├── true ──▶ End-Skip  (Welcome-Mail überspringen)
      │
      └── false ─▶ AssignGroupsLdaps  (Worker)
                            │
                            ▼
                  SendWelcomeMailGraph  (Linux-API)
                            │
                            ▼
                  Workflow complete
```

Welcome-Mail-Adresse kommt aus der echten Exchange-Provisioning-Antwort (kein Drift zwischen Eintrag und Mailbox). Initial-Passwort kommt aus dem Vault (lebt nur Mikrosekunden im Handler-Heap, nie in Logs oder JSON-Spalten).

---

## Die Konzepte

### Workflow-Definition = Graph aus Nodes + Edges

Versionierte Graphen: `start → form → automation → decision → ... → end`. Jeder Node-Typ hat eine eigene Semantik. Laufende Instanzen referenzieren eine feste Version und brechen nicht, wenn der Admin später eine neue Version published. Details in [[Workflow]].

### Mapping-Sources — wie Payloads befüllt werden

Ein `automation`-Node hat ein Input-Mapping wie:

```jsonc
{
  "userPrincipalName": { "source": "created_ad_user",  "nodeKey": "create-ad-user", "property": "userPrincipalName" },
  "skuId":             { "source": "static",           "value": "abc-1234-..." }
}
```

Die wichtigsten Quellen:

| Source | Liefert |
|---|---|
| `workflow` | fachliche Workflow-Felder (Vorname, Personalnummer, Eintrittsdatum, …) |
| `target_person` | Person, für die der Workflow läuft |
| `directory_identity` | Entra-Identity (greift erst nach Sync — nicht zwischen AD-Create und Mailbox-Create nutzen) |
| `created_ad_user` | Output vom vorherigen `CreateAdUserLdaps`: `distinguishedName`, `userPrincipalName`, `credentialVaultId` |
| `created_mailbox` | Output vom vorherigen `CreateMailboxGraph`: `primarySmtpAddress` |
| `static` | fester Wert |
| `answer` | Formular-Antwort |

**Die Whitelist pro Source ist hart.** Sensible Felder wie das Klartext-Passwort sind durch **keine** Mapping-Source erreichbar — das ist im Code erzwungen, nicht nur in Doku. Wer das Initial-Passwort braucht, muss die `credentialVaultId`-Verkettung verwenden, und der einzige Handler, der das Vault entschlüsseln darf, ist `SendWelcomeMailGraph`.

### Failure-Klassifikation

Jeder Fehler wird als `permanent` oder `transient` markiert:

- **`permanent`** → sofort Final-Fail, keine weiteren Versuche. Beispiele: falsche Passwort-Policy, unbekannte SKU, fehlende Graph-Permission, Payload-Validierungsfehler.
- **`transient`** → Retry mit Backoff. Beispiele: Entra-Sync hat noch nicht gelaufen, Netzwerk-Hänger, 429/5xx von Graph.

**Per-Action-Retry-Override:** Globaler Default ist 3 Versuche mit ~6 min Gesamtbudget. Einzelne Actions können längere Budgets bekommen (`max_attempts_override` + `subsequent_retry_delay_seconds_override` auf `action_definitions`). `CreateMailboxGraph` hat 10 Versuche × 300 s = ~41 min — sicher über einen 30-min-Entra-Sync-Zyklus, ohne dass andere Handler mit-laufen.

### Der Vault (Temporary Credentials)

Klartext-Initial-Passwörter dürfen **nirgendwo** als Klartext in der DB liegen.

```
Worker generiert Passwort
   ▶ schreibt es VERSCHLÜSSELT (pgcrypto) ins Vault
   ▶ schreibt nur die Vault-UUID ins output_json
   ▶ alles in EINER Transaktion (atomar, idempotent)

SendWelcomeMailGraph bekommt nur die Vault-UUID
   ▶ liest verschlüsseltes Passwort
   ▶ entschlüsselt im RAM, sendet die Mail
   ▶ Klartext lebt nur Mikrosekunden
```

GRANTs sind eng: Worker darf nur INSERT (kann nicht zurücklesen). Linux-API darf SELECT + UPDATE (Audit-Bump). Niemand darf DELETE — Cleanup ist Folge-Slice.

Der Verschlüsselungs-Key muss auf beiden Hosts bitgenau identisch sein:
- Worker: DPAPI-geschützt in `vault.config.dpapi` (LocalMachine-Scope, gMSA-gebunden)
- Linux-API: `KAUTH_VAULT_KEY`-Env-Var

### Decision-Nodes können auf Automation-Output verzweigen

Decision-Nodes verzweigen heute zusätzlich zu Formular-Antworten auch über den Output ihres direkten Predecessors. Wichtigster Use-Case: AlreadyExists-Branch nach `CreateAdUserLdaps`.

```jsonc
{
  "referenceKind": "automation_output",
  "sourceNodeKey": "create-ad-user",
  "property":      "alreadyExisted",
  "operator":      "is_true"
}
```

Dreifach abgesichert: Property-Whitelist (heute nur `alreadyExisted`), Direct-Predecessor-Pflicht (keine beliebige History-Tiefe), Action-Key-Whitelist (heute nur `CreateAdUserLdaps`). Verstöße produzieren `BuildFailurePlan` mit klarer Meldung.

---

## Datenmodell

```
action_definitions          — Katalog freigegebener Actions (key, handler_type, target_runtime,
                              max_attempts_override, subsequent_retry_delay_seconds_override)
workflow_node_actions       — Verknüpft Workflow-Nodes mit Actions

automation_jobs             — Eine Job-Instanz pro Ausführung
                              (target_runtime, claimed_at/by, heartbeat_at,
                              completion_processed_at/claimed_at)
automation_job_attempts     — Versuche (status, output_json, error_message, failure_kind,
                              attempt_number)

temporary_credentials       — Vault: workflow_node_instance_id × credential_type × encrypted_value
                              (UNIQUE, FK ON DELETE CASCADE)
```

---

## Wichtigste Backend-Pfade

| Datei | Zweck |
|-------|-------|
| `api/API/Services/WorkflowAutomationService.cs` | Job-Claiming, Ausführung, Retry für Linux-API-Handler |
| `api/API/Services/WorkflowAutomationHostedService.cs` | Background-Polling der Linux-API |
| `api/API/Services/WorkflowAutomationHandlerRegistry.cs` | Handler-Registry, Validierung beim Speichern |
| `api/API/Services/Graph*Handler*.cs` | `SendWelcomeMailGraph`, `CreateMailboxGraph`, `GraphMailboxProvisioner` |
| `api/API/Services/WorkflowAutomationRetryPolicy.cs` | Retry-Logik inkl. Per-Action-Override |
| `api/API/Repositories/PostgresWorkflowAutomationOperations.cs` | DB-Zugriff, Mapping-Source-Resolver |
| `api/API/Services/AutomationPropertyCatalog.cs` | Mapping-Source-Whitelist + Catalog-DTO fürs Frontend |
| `api/API/Services/WorkflowRuntimeEngine.cs` | Decision-Conditions, Plan(...)-Loop |
| `worker/AdAutomationWorker.Core/` | Plattform-neutrale Worker-Engine |
| `worker/AdAutomationWorker/` | Windows-Host (`net8.0-windows`) mit `LdapsAdUserWriter` etc. |

---

## Was vor Live-Inbetriebnahme operativ noch nötig ist

Nicht-code-prüfbar — muss manuell:

1. **Windows-Worker** aufsetzen, gMSA + DPAPI + Postgres-Login + Service — linearer Wizard in [[Worker-Setup]].
2. **Vault-Key** identisch verteilen — auf der Linux-API als `KAUTH_VAULT_KEY` (siehe `scripts/Configure.ps1`), auf dem Worker als `vault.config.dpapi` (siehe [[Worker-Setup]]).
3. **Microsoft-Graph-App-Registrierung** mit Admin-Consent für `Mail.Send` + `User.Read.All` + `LicenseAssignment.ReadWrite.All` + `Group.Read.All` (letzteres für die `reference_user.groups`-Mapping-Source).
4. **Exchange-SKU** per `Get-MgSubscribedSku` ermitteln, GUID im Workflow-Builder als `static`-Mapping eintragen.
5. **E2E-Test** gegen Test-Tenant durchspielen.

Details: [[Setup]], [[Konfiguration]], [[Worker-Setup]], [[Deployment-Checkliste]].

---

## Was inhaltlich noch offen ist

| Slice | Inhalt |
|---|---|
| `CreateErpEmployee` | ERP-Anbindung (Ziel InforLN). Wartet auf Stakeholder-Entscheidung. Größter inhaltlicher Resthebel. |
| Builder-UI für `automation_output`-Bedingungen | Heute nur per JSON-API / Dev-Seed konfigurierbar. Source-Dropdown + Property-Filter. |
| Vault-Cleanup-Sweeper | TTL-getriebener Delete-Job für `temporary_credentials`. |
| Key-Rotation | `key_version`-Spalte + Multi-Decrypt-Fan-Out im Vault-Pfad. |
| Lizenz-Pool-Monitoring | Operatives Dashboard für freie Exchange-SKUs. |
| Connection-Pooling LDAPS | Heute pro Call neue Connection — bei vielen Onboardings spürbar. |

---

## Optionale Ideen (noch nicht im Backlog)

- **Sub-License-Disabling** in `CreateMailboxGraph` (`disabledPlans`-Liste), falls nur Teil-Bundle aktiviert werden soll
- **Dry-Run-Modus** für Workflows: alle Handler in einer Preview-Tx ausführen, am Ende Rollback — Sicherheits-Vorschau für HR vor dem Real-Lauf
- **Retry-Audit-Dashboard**: pro Handler die letzten N Failures nach `failure_kind` gruppiert
- **Workflow-Designer-UX**: Visualisierung der Output-Ketten (welcher Node liefert welche Property an wen?)
- **Self-Service-Welcome-Mail-Resend**, falls User das Initial-Passwort verloren hat, bevor er sich eingeloggt hat
- **Multi-Tenant-SKU**: SKU-Auswahl pro Abteilung statt eine pro Workflow

---

## Verwandte Notizen

- [[Admin-Gated-Automation]] — **Zielbild für Prod**: Plan-Vorschau, Admin-Bestätigung mit Re-Auth, Post-Execution-Summary in der 360°-Karte
- [[Zielarchitektur]] — Automation Layer als Schicht im Gesamtbild
- [[Entscheidungen]] — Warum keine freie Automation; AD/Entra-Schreibrichtung
- [[Hybrid-Worker-Sub-Architektur]] — Worker-Architektur im Detail (7 Sub-Entscheidungen)
- [[Workflow]] — Wie Automation-Nodes in den Workflow-Flow eingebettet sind
- [[Migrationspfad]] — Wo Etappe 9a in der Gesamt-Migration steht
- [[Setup]] / [[Deployment-Checkliste]] — Operative Vorbereitung
