# Zielarchitektur

#architektur #stabil

Stabile Beschreibung des Sollbilds. Änderungen hier bedeuten eine echte Richtungsänderung im Projekt.

Primärquelle im Repo: `PROJECT_CONTEXT.md` (PRODUCTIVE_TARGET_ARCHITECTURE.md in Vault migriert)

---

## Produktziel

Eine produktiv betriebene, **versionierte, kontrollierte Workflow-Plattform**.

Ein Nicht-Entwickler soll fachliche Workflows aus sicheren Bausteinen konfigurieren können — ohne Quellcodeänderung.

> Onboarding ist nur ein Workflow. Die Plattform trägt viele.

---

## Architektur-Schichten

```
┌────────────────────────────────────┐
│         Admin / Builder Layer      │  Definition anlegen, konfigurieren, publishen
├────────────────────────────────────┤
│         Automation Layer           │  Actions, Jobs, Retry, Logging
├────────────────────────────────────┤
│         Task-System                │  Human Tasks, Deadlines, Status, Zuweisung
├────────────────────────────────────┤
│         Runtime / Orchestrierung   │  Nodes aktivieren, Entscheidungen auswerten
├────────────────────────────────────┤
│      Workflow Definition Layer     │  Definitionen, Versionen, Nodes, Edges
├────────────────────────────────────┤
│         Fachobjekte                │  Person, Identity, Department, Responsibility...
└────────────────────────────────────┘
```

### Fachobjekte (stabile Kernobjekte)

| Objekt | Bedeutung |
|--------|-----------|
| Person | Kanonischer Mitarbeiteranker — fachliche Identität |
| technische Identity | AD/Entra-Account — technischer Zugang |
| Department | Abteilung |
| Responsibility | Fachliche Ownership/Zuständigkeit (≠ Rolle) |
| Workflow Instance | Laufende Instanz eines Workflows |
| Task | Arbeitspaket, Mensch oder System |
| Notification | Benachrichtigung |
| Audit | Unveränderlicher Ereignislog |

### Workflow Definition Layer

Neue fachliche Quelle des Ablaufs — **noch nicht vollständig im Einsatz**:

- `workflow_definitions`
- `workflow_definition_versions`
- `workflow_nodes`
- `workflow_edges`
- `workflow_node_configs`

### Runtime / Orchestrierung

Führt Definitionen aus:
- Aktiviert Nodes
- Wertet Entscheidungen aus
- Startet Tasks, Benachrichtigungen, Automationen
- Protokolliert Runtime-Ereignisse
- Behandelt Fehler- und Abbruchpfade

**Wichtig**: Die Runtime ist mehr als ein Task-Generator. Tasks sind nur eine mögliche Laufzeitwirkung.

Minimale Node-Typen:

| Node | Zweck |
|------|-------|
| `start` | Einstiegspunkt |
| `form` | Anforderungserfassung |
| `approval` | Fachlicher Gatekeeper |
| `measure_provision` / `measure_deprovision` / `measure_change` / `measure_rename` | Fachliche Maßnahmen-Blöcke (ersetzen den alten `setup`-Sammelblock) |
| `task` | Explizite Aufgabe |
| `decision` | Verzweigung |
| `parallel_split` / `parallel_join` | Parallelarbeit |
| `automation` | Technische Aktion |
| `end` | Abschlusspunkt |

`setup` ist kein Bestandteil des Soll-Sets mehr. Code, Seeds und Doku sind bereinigt; offen ist nur noch eine manuelle DB-Inventur gegen persistierte `workflow_nodes`-Einträge.

Später erweiterbar um `wait`, `notification`, `subworkflow`.

### Task-System (bleibt eigenständig)

- Human Tasks mit Zuweisung (`user` oder `responsibility`)
- Kommentare, Deadlines, Status
- Eskalation

Assignment-Regel: `user` = persönlich, `responsibility` = geteilte fachliche Zuständigkeit. Nie mischen.

### Automation Layer (strikt kontrolliert)

- `action_definitions` — freigegebene Bausteine (z.B. `CreateAdUser`, `SendWelcomeMail`)
- `automation_jobs` + `automation_job_attempts` + `automation_job_logs`
- Kein freies PowerShell, kein freies SQL, keine beliebigen HTTP-Requests
- Nur validierte, freigegebene Actions

### Admin / Builder Layer

Zunächst als Guided Builder:
- Definition anlegen und versionieren
- Nodes und Edges pflegen
- Konfiguration validieren
- Veröffentlichen

---

## Zentrale Prinzipien

1. **Workflow-Definition ≠ Task-Generierung** — die Engine ist ein Orchestrator, kein Task-Generator
2. **Versionierung ist Pflicht** — laufende Instanzen laufen auf fester Definition-Version weiter
3. **Migration parallel zur Altwelt** — erst nach Parität zurückbauen
4. **Actions sind Produktelemente** — keine losen Skripte
5. **Nicht-Entwickler konfigurieren fachlich** — keine technische Freiheit für Admins
6. **Onboarding ist nur ein Workflow** — kein versteckter Produktkern

---

## Business-Phase-Muster für Employee-Lifecycle

Standard-Flow für `onboarding`, `offboarding`, `department_change`:

```
start → form (Anforderungen) → [optional: approval] → maßnahmen-block (Fachaufgaben) → end
```

- `form` erfasst Anforderungen (Gatekeeper-Schritt)
- `approval` ist optionaler Freigabeschritt durch Abteilungsleitung
- der Maßnahmen-Block ist je nach Prozessart `measure_provision` / `measure_deprovision` / `measure_change` / `measure_rename` und fasst die parallelen Fachaufgaben zusammen
- `end` erst nach Abschluss aller Pflichtaufgaben

---

## Verwandte Notizen

- [[Migrationspfad]] — Wo wir aktuell stehen
- [[Entscheidungen]] — Warum so und nicht anders
- [[Workflow]] — Konzept im Betrieb
- [[Automation]] — Automation Layer im Detail
