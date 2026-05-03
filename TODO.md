# TODO.md

Diese Datei steuert die Reihenfolge der Umsetzung aktiver Review-Zyklen.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in `CODE_REVIEW.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen (Priorisierungs- und Analyseabschnitte).

Zusaetzlich immer mitlesen: `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `MEMORY.md`.

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz festhalten, welche Dokus mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Bei Abschluss Status hier auf `done` setzen.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus TODO.md)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

Format-Beispiel: *„Naechster Schritt: S7-Slice1 — Engine-Extraktion. Reasoning: high. Modell: opus."*

## Aufgabenteilung Codex/Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

---

## Aktiver Zyklus: Runtime-Lifecycle (Zyklus 6, 2026-05-03)

Migrationspfad-Schritt 7 ("Task-System an Node-Runtime anbinden"). Detail-Skizze + Slice-Plan in `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`. Option B (Engine als pure Domain-Service, analog H6) angenommen; Q1–Q6 entschieden.

| # | Aufgabe | Prio | Aufwand | Reasoning Effort | Modell | Status |
|---|---------|------|---------|------------------|--------|--------|
| S7-Slice0 | **Inventur**: Loop-Aufrufe, Eintrittspunkte, Plan-Schnitt-Vorschlag, Risiken — als §10 in der Skizze. | LOW | 0,5 d | medium | sonnet | ✓ done (2026-05-03) |
| S7-Q6 | **Q6 entscheiden**: rekursiver Loop-Pfad aus `TryCompleteRuntimeSetupNodeIfReady`. | HIGH | 0,2 d | high | opus | ✓ done (2026-05-03) — Option (a) Apply-seitige Iteration |
| S7-Slice1 | **Engine-Extraktion** als pure Domain-Service `WorkflowRuntimeEngine.Plan(...)` + Snapshot-Records + Plan-Records. Repo-Apply schreibt SQL. Bestandsverhalten unveraendert. ~20–30 Unit-Tests. | HIGH | 3–5 d | high | opus | gated auf lokale Postgres-DB |
| S7-Slice2 | **Service-Brücke**: `IWorkflowLifecycleService` mit Connection-Scope. `TaskApplicationService` + `WorkflowAutomationService` rufen Lifecycle direkt. Repo-Brücken (`*FromTaskStatusUpdate`, `*FromWorkflowTask`) entfernt. | HIGH | 2–3 d | high | opus/sonnet | gated auf S7-Slice1 |

---

## Watch-Items / Defer

| ID | Aufgabe | Status |
|----|---------|--------|
| LQ2-Z3 | `EntraDirectorySyncService` (2222 Z.) Split | defer ohne Trigger (Risiko niedrig — Timer-Pfad, kein User-Pfad) |

---

## Offene Restposten (zyklusuebergreifend)

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |

---

## Abgeschlossene Zyklen

Zyklen 1–5 (2026-04-23 bis 2026-05-03) sind abgeschlossen. Detail-Historie via `git log`; Highlights pro Zyklus in `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
