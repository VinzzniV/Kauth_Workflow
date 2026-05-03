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
| S7-Slice1.1 | **Pure Helpers extrahieren**: 7 Helper + `ActiveRuntimeNodeRecord` von `PostgresWorkflowRuntimeRepository` in neue `api/API/Services/WorkflowRuntimeEngine.cs`. Aufrufstellen via `WorkflowRuntimeEngine.X(...)`. Kein Verhaltenswechsel. | LOW | 0,5 d | medium | sonnet | ✓ done (2026-05-03) — 384 Tests gruen |
| S7-Slice1.2 | **Snapshot-Records definieren** (Input fuer `Engine.Plan`): `WorkflowRuntimeSnapshot` (Graph + Answers + NodeInstanceStatusByWorkflowNodeId + Context + ApprovalSpecByNodeId). Eigene Datei `WorkflowRuntimeEngineSnapshot.cs`. Reine Records, noch ohne Konsumenten. | LOW | 0,3 d | low | sonnet | ✓ done (2026-05-03) |
| S7-Slice1.3 | **Plan-Records definieren** (Output von `Engine.Plan`): `WorkflowRuntimePlan` (NodeSteps + Outcome), abstract `RuntimeNodeStep` + 5 konkrete Sub-Typen, abstract `RuntimeWorkflowOutcome` + 3 Sub-Typen, plus `WorkflowRuntimeApplyResult { ImmediatelyCompletedMeasureNodeId }` fuer Q6. Eigene Datei `WorkflowRuntimePlan.cs`. | LOW | 0,3 d | low | sonnet | ✓ done (2026-05-03) |
| S7-Slice1.4 | **`Engine.Plan(...)` schreiben**: pure Methode, die einen `WorkflowRuntimePlan` aus Snapshot + completedNode ableitet. Spiegelt die Logik aus `AdvanceRuntimeUntilWaitOrTerminal` 1:1, aber ohne DB-Calls. Wirft kein, signalisiert Failure als Plan-Variant. Noch nicht im Loop verwendet. | HIGH | 1,5–2 d | high | opus | ✓ done (2026-05-03) |
| S7-Slice1.5 | **Repo-Adapter `LoadRuntimeSnapshot` + `ApplyRuntimePlan`**: Snapshot in einem Read-Block laden (alle DB-Reads aus dem Loop konsolidiert); Plan-Apply schreibt INSERTs/UPDATEs + Tasks/Automation-Jobs. `ApplyResult.ImmediatelyCompletedMeasureNodeIds` als Liste fuer parallele Measure-Auto-Completions. | HIGH | 1–1,5 d | high | opus | ✓ done (2026-05-03) |
| S7-Slice1.6 | **`AdvanceRuntimeUntilWaitOrTerminal` auf Engine umstellen**: Loop wird zur duennen Schleife `LoadSnapshot → Plan → ApplyPlan → re-loop wenn ApplyResult triggert`. Bestehende Aufrufer aendern sich nicht. Verhaltens-Paritaet gegen Bestandstests. | HIGH | 0,5–1 d | high | opus | pending |
| S7-Slice1.7 | **Unit-Tests fuer `WorkflowRuntimeEngine.Plan`**: ~20–30 Tests gegen Plan-Granularitaet (Decision-Branching, Parallel-Join-Konvergenz, Auto-Complete-Bridge, Measure-Node-Aktivierung, Failure-Pfade, leere Outgoing-Edges). Datei `api/API.Tests/WorkflowRuntimeEngineTests.cs`. DB-frei. | MEDIUM | 1 d | medium | sonnet | pending |
| S7-Slice1.8 | **Integration-Tests gruen halten**: Bestandstests (384) muessen ohne Aenderung gruen sein nach Slice 1.6. Falls einer kippt, ist Slice 1.4/1.5 nicht verhaltens-aequivalent — vor Slice 2 fixen. | LOW | 0,2 d | low | sonnet | pending |
| S7-Slice2 | **Service-Brücke**: `IWorkflowLifecycleService` mit Connection-Scope. `TaskApplicationService` + `WorkflowAutomationService` rufen Lifecycle direkt. Repo-Brücken (`*FromTaskStatusUpdate`, `*FromWorkflowTask`) entfernt. | HIGH | 2–3 d | high | opus/sonnet | gated auf S7-Slice1.8 |

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
