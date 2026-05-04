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

## API-Kompatibilität & Naming

### `LegacyProcessTypeKey` ist der kanonische Name (2026-05-02, HQ1-Z4)

Der bisherige Begriff `ProcessTypeKey` wurde in allen öffentlichen API-Parametern, DTOs und internen Service-Signaturen zu `LegacyProcessTypeKey` umbenannt. Der "Legacy"-Prefix macht klar, dass dieser Key aus dem alten prozesstyp-basierten Modell stammt und langfristig durch `WorkflowDefinitionKey` abgelöst wird.

**Betroffene Endpoints (Parametername geändert):**

| Endpoint | Alter Param | Neuer Param |
|----------|------------|-------------|
| `GET /workflows` | `processTypeKey` | `legacyProcessTypeKey` |
| `GET /workflow-config` | `processTypeKey` | `legacyProcessTypeKey` |
| `GET /requirements` | `processTypeKey` | `legacyProcessTypeKey` |
| `GET /admin/config/workflow` | `processTypeKey` | `legacyProcessTypeKey` |
| `GET /workflows/derive-answers` | `targetProcessTypeKey` | `targetLegacyProcessTypeKey` |

**DTOs:** `CreateWorkflowRequest.LegacyProcessTypeKey`, `WorkflowNotificationDispatchTarget.LegacyProcessTypeKey`

**Deprecation-Ziel:** Wenn alle Workflows über `WorkflowDefinitionKey` gestartet werden können, entfällt `LegacyProcessTypeKey` vollständig. Kein konkreter Termin — erst nach Migrations-Parität.

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was gebaut wird
- [[Migrationspfad]] — In welcher Reihenfolge
- [[Identity]] — Identity-Modell im Detail
- [[Automation]] — Automation Layer
