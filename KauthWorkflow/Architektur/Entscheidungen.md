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
