# Workflow

#domäne #workflow

Was ein Workflow in diesem System ist, wie er läuft, und welche Regeln gelten.

---

## Was ist ein Workflow?

Ein Workflow ist eine strukturierte, nachvollziehbare Bearbeitung eines Vorgangs für eine Person.

Beispiele:
- Onboarding eines neuen Mitarbeiters
- Offboarding eines ausscheidenden Mitarbeiters
- Abteilungswechsel (department_change)
- Rollenwechsel, Namensänderung, Positionswechsel

---

## Kernkonzepte

### Workflow Definition

Beschreibt den *Ablauf* — Nodes, Edges, Bedingungen, Node-Konfigurationen.
Wird versioniert: eine Änderung durch einen Admin erzeugt eine neue Version, laufende Instanzen bleiben auf ihrer Version.

```
workflow_definitions
  └── workflow_definition_versions
        └── workflow_nodes (start, form, approval, setup, task, decision, end, ...)
        └── workflow_edges (wer führt zu wem)
        └── workflow_node_configs (Konfiguration pro Node)
```

### Workflow Instance

Eine laufende Instanz eines konkreten Vorgangs. Referenziert immer eine feste Definition-Version.

Enthält:
- Ziel-Person (`targetPersonId`)
- Status (aktiv, abgeschlossen, abgebrochen)
- Antworten (Formulardaten)
- Verknüpfte Tasks

### Task

Ein Arbeitspaket innerhalb einer Instanz.

| Feld | Bedeutung |
|------|-----------|
| `assignment_type` | `user` oder `responsibility` |
| `user` | Genau diese Person ist zuständig |
| `responsibility` | Geteilte fachliche Zuständigkeit (z.B. IT-Gruppe) |
| Status | `pending`, `in_progress`, `completed`, `cancelled`, `skipped` |

**Wichtig:** Nur mit klarer Zuweisung ist eine Aufgabe für den Verantwortlichen sichtbar. Ein Task ohne Zuweisung ist für Nicht-Admins unsichtbar.

---

## Lebenszyklus einer Workflow-Instanz

```
Erstellt → Anforderungen erfasst (form) → [Freigabe] → Fachaufgaben aktiv → Abgeschlossen
                                                                                    ↓
                                                                    erst nach ALLEN Pflichtaufgaben
```

**Gatekeeper-Regel:** Vor der Aufgabengenerierung kommen immer:
1. Formular (Anforderungserfassung)
2. Optionale Freigabe (Abteilungsleitung)

Keine Fachaufgaben ohne vorherigen Gatekeeper-Schritt.

---

## Statusregeln

- `cancelled` wird **nie** auf `completed` gemappt
- `skipped` ist kein Reparaturwerkzeug für falsch generierte Tasks
- Der Gesamtstatus einer Instanz leitet sich aus den relevanten Aufgaben ab
- Completion erst wenn alle Laufzeitpfade sauber beendet sind

---

## Measure-Flows (Employee-Lifecycle-Muster)

Für `onboarding`, `offboarding`, `department_change`:

| Phase | Node-Typ | Zweck |
|-------|----------|-------|
| A | `measure_provision` | Bereitstellungsmaßnahmen |
| A | `measure_deprovision` | Abbaumaßnahmen |
| A | `measure_change` | Wechselmaßnahmen |
| C | `measure_rename` | Namensänderung |
| C | `measure_change` | Positions- und Rollenwechsel |

`setup` bleibt Legacy-Alias — nicht mehr als neuer Standard-Baustein anbieten.

---

## Abteilungsleiter-Sicht

Abteilungsleiter sehen nur explizit aufgelöste, beobachtbare Abteilungen:
- Quelle: `department_settings`, `department_lead`-Responsibilities
- **Nicht** durch globale Manager-Rolle oder `workflows.view_department`
- Kein abteilungsübergreifender Blick durch `auth_manager`-Rolle

---

## Wichtige Backend-Dateien

| Datei | Zweck |
|-------|-------|
| `api/API/Endpoints/` | Thin Endpoints, nur Routing + Auth |
| `api/API/Services/` | Fachlogik |
| `api/API/Repositories/` | SQL-Zugriff |
| `api/API/Contracts/` | DTOs |
| `db/41_workflow_definition_layer.sql` | Definition Layer Schema |
| `db/42_workflow_runtime_layer.sql` | Runtime Schema |

---

## Verwandte Notizen

- [[Zielarchitektur]] — Schichten und Node-Typen
- [[Identity]] — Personen, Rollen, Responsibilities
- [[Automation]] — Automation Layer
- [[Rotation]] — Spezieller Workflow-Typ
