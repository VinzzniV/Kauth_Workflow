# Entscheidungen

#architektur #entscheidungen #stabil

Langfristige Architektur- und Produktentscheidungen. Diese ändern sich selten — wenn doch, ist das eine bewusste Richtungskorrektur.

Primärquelle im Repo: `PROJECT_CONTEXT.md` (DECISIONS.md in Vault migriert)

---

## Produktrichtung

### Die App wird eine Workflow-Plattform, kein größeres Onboarding-Tool

**Warum:** Onboarding ist fachlich wichtig, darf aber kein versteckter Produktkern sein. Neue Kernlogik darf nicht onboarding-spezifisch verengt werden. Langfristig sollen viele verschiedene interne Workflows auf derselben Basis laufen.

**Konsequenz:** Neue Features werden am generischen Plattformkern gemessen, nicht daran ob sie dem Onboarding-Flow helfen.

---

## Backend-Architektur

### Backend ist Source of Truth

**Warum:** Das Frontend ist Darstellung und Bedienoberfläche. Businessregeln zweimal zu pflegen (Frontend + Backend) führt zu Inkonsistenz und Bugs in Produktiv-Systemen.

**Konsequenz:** Kein Validierungslogik-Drift ins Frontend. UI-Regeln sind immer vom Backend-Verhalten ableitbar.

---

### Rollen ≠ Responsibilities

| Begriff | Bedeutung |
|---------|-----------|
| **Rolle** | Technischer Zugriff — wer darf was aufrufen |
| **Responsibility** | Fachliche Ownership — wer ist für was zuständig |

**Warum:** Die Trennung ermöglicht flexibles Zuständigkeitsmodell (z.B. "IT-Gruppe ist für Laptop-Aufgaben zuständig") unabhängig von Systemrollen.

**Konsequenz:** `assignment_type` bleibt strikt: `user` = persönlich, `responsibility` = geteilte Zuständigkeit. Keine impliziten Abkürzungen.

---

### Workflow-Definition ist eigener Kern

**Warum:** Task Templates und Prozessarten allein sind nicht das Endmodell. Die Plattform braucht einen expliziten Definition Layer mit Nodes, Edges und Versionierung — sonst kann kein Nicht-Entwickler Workflows ohne Codeänderung pflegen.

---

### Versionierung ist Pflicht

**Warum:** Laufende Instanzen dürfen durch spätere Admin-Änderungen nicht brechen. Ohne Versionierung würde jede Konfigurationsänderung laufende Prozesse gefährden.

**Konsequenz:** `workflow_instances` referenzieren immer eine feste Definition-Version. Admin-Änderungen erzeugen neue Versionen.

---

### Runtime ist mehr als Task-Generierung

**Warum:** Die ursprüngliche Engine war ein Task-Generator. Das ist zu eng. Eine echte Workflow-Engine aktiviert Nodes, wertet Entscheidungen aus, startet Automationen — Tasks sind nur eine Laufzeitwirkung von mehreren.

---

## Migrationsstrategie

### Migration statt Big Bang

**Warum:** Ein Big Bang birgt hohes Risiko für laufende Workflows in Produktion. Die bestehende Fachlogik in Templates, Conditions und Dependencies hat echten Wert, der nicht weggeworfen werden soll.

**Konsequenz:** Neue Architektur wird parallel zur Altwelt eingeführt. Altlogik erst nach Stabilität und Parität zurückbauen. Keine voreiligen Löschungen.

---

## Sicherheit & Kontrolle

### Keine freie technische Magie für Admins

**Warum:** Admins sind Fachanwender, keine Entwickler. Freie PowerShell, SQL oder HTTP-Requests mit Secrets wären ein unkontrollierbares Sicherheitsrisiko.

**Konsequenz:** Nur freigegebene, validierte Actions. Kein freies Scripting.

### Actions sind kontrollierte Produktelemente

**Warum:** Technische Actions wie `CreateAdUser` oder `SendWelcomeMail` sind definierte Bausteine mit bekanntem Verhalten, Retry-Logik und Logging — keine losen Skripte.

---

## Datenmodell & Konsistenz

### Completion-Regel bleibt streng

Ein Workflow gilt erst als abgeschlossen, wenn alle relevanten Laufzeitpfade sauber beendet sind. Keine UI-Abkürzungen.

### Statuskonsistenz ist Pflicht

- `cancelled` nicht auf `completed` mappen
- `skipped` nicht als Reparatur für falsch generierte Tasks missbrauchen
- Statusunterschiede nicht verstecken

### Parallelität bleibt erlaubt

Mehrere Teams oder Rollen können parallel arbeiten. Das Modell darf keine unnötige serielle Einbahnstrasse erzwingen.

---

## Identity & Directory

### Die App ist nicht das führende Benutzersystem

AD / Entra liefern die technische Identität. Person und technische Identity bleiben getrennte Konzepte.

### Gruppen sind Standard für Zugriff

Standardzugriff kommt über Gruppen-Mapping. Lokale Sonderfälle bleiben Ausnahme.

### Entra-Sync setzt keine Zuständigkeiten mehr automatisch (2026-05-04)

Der Directory-Sync-Zyklus aktualisiert ausschließlich Identitätsdaten (`directory_identities`, `directory_groups`, `app_users`-Felder). `department_settings.department_lead_person_id` und alle weiteren Zuständigkeiten werden nicht mehr automatisch aus Entra-Gruppen-Mitgliedschaften abgeleitet.

**Warum:** Manuelle Zuweisungen durch Admins wurden beim nächsten Sync-Durchlauf überschrieben oder gelöscht, weil `SyncDepartmentLeadAssignmentsFromDirectory()` `department_settings` vollständig aus Gruppen-Kandidaten neu berechnete. Das Modell vermischte "wer ist im System bekannt" (Identität, Entra-Quelle) mit "wer ist für was zuständig" (Responsibility, Admin-Entscheidung).

**Konsequenz:** Admins weisen Abteilungsleitungen und Approver manuell zu. Der Sync liefert nur noch die Kandidaten-Basis (wer existiert, ist aktiv, in welcher Gruppe). Eine informative Anzeige ("X Personen in Entra-Gruppen ohne Zuweisung") unterstützt Admins dabei, offene Zuweisungen zu erkennen.

### AD/Entra-Schreibrichtung: on-prem AD führt via Windows-Worker (2026-05-12, Z21-S2)

Schreibende Lifecycle-Aktionen (Konto anlegen/deaktivieren, Gruppenmitgliedschaften pflegen, Postfach steuern) gehen ausschließlich gegen **on-prem AD**, ausgeführt von einem **dedizierten Windows-Worker-Service**. Entra-ID wird über AD Connect nachgeführt — die App schreibt **nicht** direkt gegen Microsoft Graph.

**Warum:** Die produktive IT-Landschaft hat on-prem-Primat. Ein Cloud-only-Schreibpfad (Graph) würde die Schreibhoheit umkehren und ist organisatorisch nicht tragbar. Beidseitige Spiegelung wurde verworfen, weil sie in der Praxis nie sauber "beides führend" hält.

**Konsequenz:**

- Der Stack wächst um eine zweite Laufzeitkomponente: eine Windows-VM bzw. ein domain-gebundener Windows-Service. Höhere Betriebskosten werden bewusst akzeptiert.
- Der bestehende `WorkflowAutomationHostedService` (Linux-API) bleibt **Orchestrator** und delegiert schreibende Jobs an den Worker. Er wird **nicht** zu einem Linux→AD-Schreiber umgebaut.
- `EntraGraphClient` bleibt strikt read-only (Sync-Quelle für `directory_identities`, `directory_groups`). Es entsteht **kein** schreibender Graph-Pfad.
- "Linux-only-Schreibhandler direkt aus dem API-Container" ist explizit verboten (siehe Guardrail in `PROJECT_CONTEXT.md`).
- Bis der Windows-Worker steht, bleibt der Automation-Layer offiziell im Simulationsmodus — Z21-S1 markiert das im UI sichtbar.

**Verworfen wurde:**

- **Option 1 (Entra führt, Graph-only):** technisch leichter und in Wochen machbar, widerspricht aber dem on-prem-Primat der IT-Landschaft.
- **Option 3 (Beidseitige Spiegelung):** doppelte Idempotenz, komplexe Konfliktauflösung, kein realer Mehrwert gegenüber AD Connect.

**Folge-Entscheidungen:** entschieden 2026-05-12 als Migrationspfad-Etappe 9a Schritt 1 — siehe nächster Abschnitt "Hybrid-Worker-Sub-Architektur".

---

### Hybrid-Worker-Sub-Architektur (2026-05-12, Migrationspfad-Etappe 9a Schritt 1)

Die in Z21-S2 parkierten Sub-Entscheidungen sind festgezurrt, ergänzt um zwei zusätzliche Punkte (Postgres-Auth, Job-Claim-Sicherheit), die im Plan-Review aufgekommen sind. Stakeholder-Inputs: Linux-API und Windows-Worker beide on-prem im selben Subnetz/VPN; AD-Domäne ≥ 2012R2 mit gMSA-Unterstützung.

1. **Worker-Deployment**: dedizierte Windows-Server-VM (domain-joined). Domain-joined Container und Azure-Hybrid-Worker verworfen.
2. **Transport API↔Worker**: DB-Polling auf zentrale Postgres mit `target_runtime`-Diskriminator. HTTPS-Pull und Message-Queue verworfen.
3. **AD-Schreibmechanik**: `System.DirectoryServices.Protocols` (LDAPS) auf .NET 8. Worker läuft unter gMSA-Kontext, `AuthType.Negotiate` nutzt diesen Kontext transparent — keine expliziten Credentials. PowerShell-Modul und ADSI verworfen.
4. **Domänen-Auth**: gMSA. Löst ausschließlich die AD-Authentisierung, nicht die DB-Auth.
5. **Audit-Rückkanal**: Worker schreibt direkt in zentrale `automation_jobs` + `automation_job_attempts`. HTTP-Callback und lokales Logfile + Sync verworfen. Konkrete GRANT-Form (View vs. Row Level Security vs. breitere Rechte) gehört in den Schritt-2-Slice.
6. **Postgres-Auth des Workers**: eigener DB-Login `kauth_worker` mit Passwort in DPAPI-geschützter Konfig auf der Worker-VM (maschinen- und service-user-gebunden). Postgres-GSSAPI als denkbarer Folge-Slice, wenn die DB-Umgebung das trägt — bis dahin DPAPI.
7. **Job-Claim-Sicherheit**: `SELECT … FOR UPDATE SKIP LOCKED` + `claimed_at`/`claimed_by`-Spalten in der `target_runtime`-Migration. Heartbeat-Frequenz, Timeout-Schwellwert und Requeue-Verantwortung werden im Schritt-2-Slice konkretisiert.

**Warum:** Diese sieben Entscheidungen bilden zusammen die Architektur des Windows-Workers vollständig ab. Mit ihnen ist Etappe 9a Schritt 2 (Worker-Skeleton) als Code-Slice konkret beschreibbar, und damit löst sich auch der einzige offene HIGH-Slice `TODO.md Z21-S4` aus seiner Blockierung.

**Konsequenz:**
- Der Stack bekommt eine neue Windows-Server-VM. Betriebskosten und Patching-Pflicht werden bewusst akzeptiert.
- Schema-Migration in Schritt 2: `automation_jobs` bekommt `target_runtime`, `claimed_at`, `claimed_by`, `heartbeat_at`.
- Der `WorkflowAutomationHostedService` (Linux-API) bleibt **Orchestrator**: erstellt Jobs mit `target_runtime='windows_worker'`, der Worker holt sie. Linux-API führt weiter `target_runtime IS NULL`-Jobs selbst aus (Simulationsmodus + alle künftigen Linux-fähigen Handler).
- gMSA und DB-Login sind **getrennte** Auth-Pfade. AD-Auth läuft transparent (kein Secret), DB-Auth über DPAPI-geschützte Konfig.

**Verworfen** (Details in [[Hybrid-Worker-Sub-Architektur]]): Windows-Container für gMSA, HTTPS-Pull-Transport, PowerShell-AD-Modul, klassischer Service-Account, Postgres-GSSAPI als V1 (zu viele Vorbedingungen am Postgres-Host), Plain-Text-DB-Konfig, Vault-Secret-Store.

**Vorbedingungen für Live-Inbetriebnahme** (nicht für Code-Skeleton):
- Domain-Admin legt `gMSA-KauthWorker$` an und installiert es per `Install-ADServiceAccount` auf der Worker-VM.
- Postgres-User `kauth_worker` anlegen + DPAPI-Setup-Skript auf der Worker-VM.

**Folge-Slice:** Etappe 9a Schritt 2 (Worker-Skeleton) — ✓ 2026-05-12 umgesetzt. Konkrete Lease-Werte: Heartbeat 30s, Stale-Timeout 5min, Lazy-Cleanup im Worker selbst (`ReleaseStaleClaimsAsync` vor jedem Claim). GRANT-Form V1: View `automation_jobs_windows_worker` für SELECT + UPDATE-Grants auf der Basistabelle (hartes Row-Level-Filtering ist bewusst Folge-Slice, sobald der erste echte AD-Schreib-Handler kommt). Worker-Layout: eigene `worker/Worker.sln` mit `AdAutomationWorker.Core` (net8.0, plattform-neutral) + `AdAutomationWorker` (net8.0-windows-Host) + `AdAutomationWorker.Tests`. External-Completion-Pfad in der Linux-API: dedizierter `ExternalAutomationJobCompletionSweeper` triggert `OnExternalAutomationJobSucceededAsync`/`OnExternalAutomationJobFailedAsync`; gemeinsame Retry-Quelle in `WorkflowAutomationRetryPolicy`. **Nächster Slice:** Etappe 9a Schritt 3 (erster echter Handler `CreateAdUser` gegen Test-DC, LDAPS, gMSA-Live, DPAPI-Encryption produktiv, Linux-API-Sweep für stale Worker-Claims).

---

## Dokumentation & Prozess

### Dokumentation ist Teil der Architekturarbeit

Zielbild, Migration und Begriffe müssen im Repo nachvollziehbar sein. Architekturarbeit ohne Doku gilt nicht als fertig.

### Refactors brauchen einen klaren Grund

Keine großen unstrukturierten Refactors nebenbei. Bei Kernumbauten zuerst Zielmodell und Migrationsschnitt klären.

### Action-Mapping-Editor bleibt flach (LQ2, 2026-05-02)

Der `WorkflowBuilderActionMappingEditor` rendert das Eingabe-Mapping einer Automation-Action ausschließlich als flache Liste `paramName → {source, value}`. Das Backend kann mehr: `ResolveAutomationMappingValue` läuft rekursiv über JSON-Objekte und Arrays, sodass eine Action-Definition theoretisch verschachtelte Strukturen erwartet (z. B. `nested.user.firstName`). Der Form-Editor unterstützt das **bewusst nicht**:

- Aktuell hat keine produktive Action ein verschachteltes Input-Schema.
- Der Form-Builder würde mit einer Tree-Eingabe deutlich komplexer und schwerer testbar.
- Wenn eine Action mit nested-Schema benötigt wird, kann der Power-Modus (raw JSON) genutzt werden — der Builder fällt automatisch in diesen Modus zurück, wenn das geparste Mapping ungültig ist oder Top-Level-Keys nicht ins Schema passen.

Falls in Zukunft mehrere Actions mit nested-Schemas auftauchen, ist das ein Anlass den Editor zu erweitern (z. B. eine kollabierbare Sub-Liste pro nested object) — aber **nicht** vorab spekulativ bauen.

---

## Mitarbeiterakte & Personenverzeichnis

### Mitarbeiterakte bekommt eigenen Navigationsbereich (2026-05-08, Z16-S1)

Person ist der fachliche Primaeranker. Die 360°-Akte (`/people/:personId`) ist vollwertig implementiert, hat aber keinen eigenen Navigationseinstieg — HR muss ueber Workflows oder Rotation navigieren, um eine Person zu oeffnen.

**Entscheidung:** `/people` wird eine eigene Listen-/Suchseite mit Navigationseintrag fuer HR und Admin. Neues FE-Feature: `peopleDirectory`. Neue BE-Policy: `CanAccessPeopleDirectory` (Admin + HR).

**Warum:** Person ist nicht Anhang eines Workflows, sondern eigenstaendiger fachlicher Kern. Kein direkter Einstieg widerspricht dem Architekturanker aus `KauthWorkflow/Domäne/Identity.md`.

**Konsequenz:** `/people/search`-API von `CanCreateWorkflow` auf `CanAccessWorkflowOverview` umstellen (Suche nach Personen ist kein Workflow-Erstellungsakt). `CanAccessPeopleDirectory` gilt nur fuer die Listenseite; das Detail `/people/:personId` bleibt auf `workflowOverview` (Admin, HR, Manager, Reader).

### Automation-Identity-Snapshot braucht Zeitstempel (2026-05-08, Z16-S1)

Heute referenziert `AutomationPropertyCatalog.cs` `appUserId` und `directoryIdentityId` als Automation-Properties, aber ohne formalen Snapshot-Zeitstempel. Wenn ein Entra-Account spaeter verknuepft wird, ist retroaktiv unklar, welche Identity-Werte bei einer frueheren Automation galten.

**Entscheidung (deferred):** Zukuenftiger Automation-Snapshot muss `personId`, `appUserId`, `directoryIdentityId`, `directoryUserPrincipalName` und `snapshotAt` enthalten. Produkt-Entscheidung ausstehendem: ob Snapshot in `workflow_automation_jobs` persistiert wird (Z16-S4).

**Konsequenz fuer jetzt:** Trennung Person/technische Identity bleibt unveraendert. Kein neues Datenmodell in Z16-S2/S3.

---

## API-Kompatibilität & Naming

### `WorkflowDefinitionKey` ist der kanonische Name (aktiv seit 2026-05-11; Umbenennung begann 2026-05-02, HQ1-Z4)

`workflowDefinitionKey` ist der aktive fachliche Anker überall im System: Builder, Runtime-Engine, Validatoren, Gatekeeper-Regeln, Repositories und Notification-/Link-DTOs schreiben und lesen ausschließlich diesen Schlüssel. `legacyProcessTypeKey` existiert nur noch als read-only-Fallback für bereits persistierte Workflow-Definitionen im Altbestand — neue Definitionen müssen `workflowDefinitionKey` tragen.

**Historischer Hintergrund:** Mit HQ1-Z4 (2026-05-02) wurde `processTypeKey` in allen öffentlichen API-Parametern zu `legacyProcessTypeKey` umbenannt, um den Übergang sichtbar zu machen. Die Legacy-Key-Slices vom 2026-05-11 haben diesen Übergang vollständig abgeschlossen:

- Builder-internes `primaryLegacyProcessTypeKey` → `workflowDefinitionKey`
- `node.config.legacyProcessTypeKey` → `node.config.workflowDefinitionKey` als aktiver Anker; Fallback-Lesepfad für Altbestand bleibt erhalten
- `WorkflowNotificationDispatchTarget.LegacyProcessTypeKey` → `WorkflowDefinitionKey`; `ProcessTypeName` → `WorkflowDefinitionName`
- Gatekeeper-Record `LegacyProcessTypeKey` → `WorkflowDefinitionKey`; Fehlercode `unknown_form_legacy_process_type` → `unknown_form_workflow_definition_key`
- `PrimaryLegacyProcessTypeKey`-Cluster vollständig abgebaut

**Verbleibender Legacy-Rest:** `legacyProcessTypeKey` als read-only-Fallback in Builder/Runtime für persistierten Altbestand — bewusst behalten, kein Handlungsbedarf. Vollständige Entfernung erst nach Migrations-Parität aller persistierten Definitionen.

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was gebaut wird
- [[Migrationspfad]] — In welcher Reihenfolge
- [[Identity]] — Identity-Modell im Detail
- [[Automation]] — Automation Layer
