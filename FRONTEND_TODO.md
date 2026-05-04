# FRONTEND_TODO.md

Frontend-spezifische Backlog-Liste — analog zum globalen `TODO.md`, aber gescoped auf React/Tailwind/CSS-Themen.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst die folgenden Dokumente lesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`
- diesen File (`FRONTEND_TODO.md`)
- bei groesseren Aenderungen zusaetzlich `web/README.md`

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz pruefen, welche Dokus mitgezogen werden muessen.
- Code-Aenderungen + Doku-Aenderungen gehen in denselben Arbeitsgang.
- Bei Abschluss den Eintrag aus der Tabelle "Offene Items" **entfernen** (nicht nur auf `done` setzen) und unten unter "Abgeschlossene Zyklen" kurz vermerken.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus dieser Liste)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

---

## Prioritaeten

- `HIGH` = strukturell wichtig fuer Wartbarkeit oder blockiert spaetere Arbeit
- `MEDIUM` = sichtbarer UX-Gewinn oder reduziert Bugs
- `LOW` = sinnvolle Haertung oder Bereinigung ohne unmittelbaren Schmerz

---

## Offene Items

| # | Aufgabe | Prio | Aufwand | Reasoning Effort | Modell | Status |
|---|---------|------|---------|------------------|--------|--------|
| FE-32 | **Globale Schnellnavigation / Command Search** (UX-Roadmap 2026-05-04, Block 5). Globales Such-/Springen-/Aktion-Feld fuer Personen, Vorgaenge, Tasks, Builder-Artefakte und Admin-Bereiche. | MEDIUM | 2–4 d | high | opus | offen |
| FE-34 | **Operative Serienarbeit / Bulk-Aktionen** (UX-Roadmap 2026-05-04, Block 5). Mehrfachselektion und Sammelaktionen fuer Aufgabenlisten, z. B. `in Bearbeitung`, `erledigt`, `zuweisen`. | MEDIUM | 2–3 d | high | opus | offen |
| FE-7-Browser | **Manueller Dark-Mode Browser-Sichtprueftest** (FE-7-Folge). Static-Pass + FE-14 done; manuelle Sichtprueftest (`data-theme="dark"`, alle 8 Pages aus Checkliste unten) bleibt Nutzer-Aufgabe. | MEDIUM | 0,5 d | — | — | offen — Nutzer-Aufgabe |
| FE-10 | **Browser-Verifikation Form-Editor** (R8). Alle 12 Schritt-Typen durchklicken (start, end, form, approval, task, decision, parallel_split/join, automation, measure_*). Manuelle Nutzer-Aufgabe — KI kann nicht pruefen. | MEDIUM | 0,5 d | — | — | offen — Nutzer-Aufgabe |
| FE-11 | **AdminTaskTemplate-Editor versions-aware** (FE-9-Folge). AdminTaskTemplate-Editor schreibt heute via `/admin/config/task-templates/...` immer auf die `latest published` Version's measure-Node. Wenn ein Builder-Draft offen ist und der Admin parallel Specs editiert, leakt die Aenderung in die Live-Version. Fix: Toggle "Live" / "Draft (in Bearbeitung)" plus Versions-Param in den Endpoints. Pre-Prod-akzeptabel offen. | MEDIUM | 0,5 d | medium | sonnet | defer — erst bei produktivem Pilot-Use-Case |
| FE-6 | **Mobile/Tablet-Layout dichte Listen** (UI-5 / R10). `RotationOperationsPage` Task-List + `WorkflowListResults` brauchen `<1024px`-Card-Variante. Aktuell Desktop-First, intern genutzt — kein konkreter Schmerz. | LOW | 1,5 d | medium | sonnet | defer — kein konkreter Bedarf |
| FE-8 | **`approval_task_template_key` → `approval_spec_key`** (LA5-Watch). Spalte auf `workflow_definitions` heisst nominell noch `_template_key`, semantisch ist es Spec-Key. ~30 Files Backend+Frontend Rename. Cosmetic-Schuld, kein Funktionsproblem. | LOW | 1 d | medium | sonnet | defer ohne Trigger |
| FE-13 | **Stale-Detection beim Builder-Save** (FE-9-Folge). Wenn AdminTaskTemplate-Editor und Builder parallel Specs aendern, kann der Builder-Save die juengere Edit-Generation ueberschreiben. Fix: `updatedAt`-Pruefung beim Replace, 409 Conflict bei Drift, UI fragt zurueck. | LOW | 0,5 d | medium | sonnet | defer — Pre-Prod kein realer Risk |
| FE-22 | **Navigations-Badges mit Aufgabenzähler** (UI/UX-Review 2026-05-04). Sidebar-Navigation zeigt keine Zähler. „Meine Aufgaben" und „Supervisor-Schritt" könnten Badge-Counts tragen; `useRoleAwareNavigation` hat Zugriff auf Insights. Optionales Feature — Navigation wird zur Aktions-Trigger-Fläche. | LOW | 1 d | medium | sonnet | defer — erst bei konkretem Nutzerfeedback |

---

## FE-7-Browser: Dark-Mode Sichtpruef-Checkliste

Jede Page im Browser oeffnen, Theme-Toggle aktivieren (`data-theme="dark"`), folgende Punkte pruefen:

### Pro Page pruefen
- [ ] Text-Kontrast ausreichend (keine grauen Texte auf dunklem Hintergrund)
- [ ] Karten-/Panel-Hintergrnde sichtbar abgegrenzt (nicht flach)
- [ ] Buttons (`btn-primary`, `btn-secondary`, `btn-ghost`) deutlich lesbar
- [ ] Form-Inputs (`form-input`, `form-select`, `form-textarea`) sichtbarer Rahmen
- [ ] Badges (`badge-*`) korrekte Farben
- [ ] Focus-Rings sichtbar bei Tab-Navigation
- [ ] Hover-States sichtbar

### Pages in Reihenfolge
- [ ] WorkflowListPage (Liste + Filter + Pagination)
- [ ] WorkflowDetailPage / SupervisorStepPage (Task-Cards, Kommentare, Audit-Log)
- [ ] CreateWorkflowPage (EmployeeForm, RoleSelection)
- [ ] AdminConfigPage (Tabs: Prozesstypen, Templates, Conditions, Dependencies, Antworten, Rollen-Defaults, Verzeichnis)
- [ ] WorkflowBuilderPage (StepCard, Condition-Editor, Action-Editor, MeasurePreview, Dependency-Graph)
- [ ] RotationPlanningPage / RotationPlanDetailPage
- [ ] RotationOperationsPage / RotationTaskDetailPage
- [ ] MyTasksPage

---

## UX-Roadmap 2026-05-04

Die folgenden Bloecke uebersetzen das Frontend-Review in eine konkrete Abarbeitungsreihenfolge. Jeder Block ist in sich geschlossen genug, dass man ihn als eigener Arbeitsgang mit Doku-Update, Tests und Abschlussbericht abarbeiten kann.

### Block 4 — Builder wirklich produktisieren

**Status**: abgeschlossen (FE-30, siehe "Abgeschlossene Zyklen").

### Block 5 — Produkthebel nach den Kernflaechen

**Ziel**
- Navigation, Personenarbeit und Serienoperationen als naechste Produkthebel ausbauen

**Items**
- `FE-31` Personenakte zur 360-Grad-Ansicht ausbauen — abgeschlossen (siehe "Abgeschlossene Zyklen")
- `FE-33` Gespeicherte Ansichten pro Rolle — abgeschlossen (siehe "Abgeschlossene Zyklen")
- `FE-32` Globale Schnellnavigation / Command Search
- `FE-34` Operative Serienarbeit / Bulk-Aktionen

**Empfohlene Reihenfolge**
1. `FE-33`
2. `FE-34`
3. `FE-32`

**Reasoning / Modell**
- `FE-32`: `high`, `opus`
- `FE-33`: `medium`, `sonnet`
- `FE-34`: `high`, `opus`

**Definition of Done**
- Personenarbeit, Listen-Presets und Serienbearbeitung sind als eigenstaendige Produktbausteine vorhanden

---

## Naechster sinnvoller Schritt

Wenn kein anderer Nutzerwunsch priorisiert wird, startet der naechste Frontend-Zyklus mit:

1. **Aufgabe**: `FE-34 Operative Serienarbeit / Bulk-Aktionen`
2. **Reasoning Effort**: `high`
3. **Empfohlenes Modell**: `opus`

Warum zuerst:
- `FE-31` und `FE-33` sind abgeschlossen; Personenakte und rollenbasierte Listen-Presets sind umgesetzt.
- Bulk-Aktionen (`FE-34`) sind der nächste Produkthebel für operative Effizienz.

---

## Bewusst NICHT angefasst

- **`tailwind.config.js` Theme-Migration** (UI-11). Tokens leben heute nur als CSS-Variablen, `extend: {}` ist leer. Migration zu Tailwind-Tokens waere 5836 Zeilen CSS reformatieren — riesiger Aufwand, kein konkreter Mehrwert. Das aktuelle System funktioniert, hat sauberes Dark-Mode-Mapping, ist gut typisiert.
- **`translateY`-Layout-Shift-Konzern** (UI-4 aus Sub-Agent-Review). Sub-Agent-Behauptung war **falsch** — `transform: translateY(-1px)` verursacht keinen Reflow (GPU-composited, ausserhalb Layout-Flow). Subtle-Hover ist Best-Practice.
- **Emoji-Icon-Audit**. 0 Emoji-Icons im gesamten Frontend. ✓
- **Loading/Empty/Error-State-Audit**. Feedback-Layer (`AppErrorBoundary`, `LoadingState`, `EmptyState`, `SkeletonCard`, `ToastProvider`, `ConfirmationDialogProvider`) komplett vorhanden und konsistent genutzt. ✓

---

## Abgeschlossene Zyklen

**Frontend-Polish 2026-05-03:** FE-1 bis FE-5 + FE-7 (static-pass) + FE-9 + FE-14 sowie 8 FE-DONE-Items aus dem Polish-Batch erledigt. Detail via `git log` (Commits 2026-05-03).

**UI/UX-Review-Zyklus 2026-05-04 (FE-15 bis FE-24):** Vollständiger UX-Audit + 10 Items identifiziert, 8 davon umgesetzt:
- FE-15: `cursor: pointer` auf allen hover-aktiven Cards (`dashboard.css`, `components.css`)
- FE-16: UUID-Kürzung auf 8 Zeichen + `title`-Attribut (`RotationPlanningPage`, `RotationPlanDetailPage`)
- FE-17: Back-Button aus Create-Mode in `RotationPlanningPage`
- FE-18: `prefers-reduced-motion`-Regel global in `utilities.css` (WCAG 2.1 AA)
- FE-19: Admin-Breadcrumb als `description` im PageHeader (`AdminConfigPage`)
- FE-20: Dashboard-Refresh ohne Layout-Jump via `lastInsightsRef` + Opacity-Dimming (`DashboardOverview`)
- FE-21: Dismissible Notice/Error-Panels + 5-s-Auto-Clear in `AdminConfigPage`
- FE-24: Rotation-Stationen als vertikale Timeline (`RotationStationTimeline.tsx`, `workflow.css`), inkl. `getStationStatusPillClass` und Status-Bug-Fix (alle Stationen waren hardcoded `open`)

**Frontend-Roadmap Block 1 bis 3 2026-05-04:** FE-25 bis FE-29 abgeschlossen. Dialog-Haertung, UI-System-Konsolidierung, vereinheitlichte Filterleisten, Tabellenmodus fuer operative Listen und Split-View fuer Listenarbeit sind umgesetzt. Details bleiben in `CODEX_SYNC.md` und `git log`.

**Frontend-Roadmap Block 4 2026-05-04 (FE-30):** Builder canvas-first abgeschlossen. Slice 1 (Canvas + Properties-Panel als Primärpfad, Step-Liste und Edge-Tabelle als sekundäre `details`), Slice 2 (Validation-Issues live an Nodes/Edges/Properties-Panel; lokale Validation `useMemo`-derived) und Slice 3 (Edge-Erzeugung direkt am Graph: „+"-Anker am Source-Knoten startet Connect-Mode, Klick auf Zielknoten legt die Verbindung über bestehenden `addEdge` an, Esc/Hintergrund bricht ab) sind umgesetzt. Details siehe `web/README.md` und `git log`.

**Frontend-Roadmap Block 5 FE-33 2026-05-04:** Gespeicherte Ansichten pro Rolle umgesetzt. `WorkflowListPage` zeigt rollenbasierte Ansichten-Leiste (`WorkflowListSavedViewsBar`); Preset-Definitionen in `workflowListSavedViews.ts`. Presets: HR (alle Phasen), Manager (Freigabe + Bearbeitung), Worker (Fachbereich-Status), Admin/Leser (Überblick). URL-stabil über bestehende `useSearchParams`-Filter. CSS in `components.css` (`.saved-views-bar`, `.saved-view-chip`). `web/README.md` aktualisiert.

**FE-12 Builder-Inline-Edit für Specs 2026-05-04:** Versioned Specs sind jetzt im Builder für alle Measure-Nodes direkt editierbar. `WorkflowBuilderSpecEditor.tsx` (Drawer) zeigt die `node.specs`-Liste aus dem Version-DTO: add/remove Specs, alle Felder (specKey, title, category, description, isRequired, isDepartmentPhaseTask, sortOrder, dueInDays, defaultResponsibilityId, processAreaLabel), Conditions (answerKey-Dropdown aus answerDefinitions, Operator-Enum, expectedValue) und Dependencies (dependsOnSpecKey-Dropdown auf andere Specs desselben Nodes). `MeasureSpecSummary` in `WorkflowBuilderStepCard` öffnet den Drawer für alle measure_*-Nodes; die bestehende `WorkflowBuilderMeasurePreview` (globaler Pool) bleibt erhalten. Speicherpfad via bestehendem `onUpdate({ specs })` → `buildVersionReplacePayload` → Replace-Endpoint (kein Backend-Vertrag geändert). CSS in `workflow.css` (`.wf-spec-*`). `web/README.md` aktualisiert.

**Frontend-Roadmap Block 5 Auftakt 2026-05-04 (FE-31):** Personenakte ist jetzt 360°-Arbeitsfläche. `PersonWorkflowHistoryPage` wurde von "Stammdaten + Liste" zu Tab-Workspace ausgebaut: Statuschips + Metric-Strip (aktive Vorgänge, offene Aufgaben, ausstehende/fehlgeschlagene Benachrichtigungen) und vier Tabs `Übersicht` (Stammdaten + Verzeichnis-Kontext + Schnellzugriff aktive Vorgänge), `Offene Aufgaben` (clientseitig aggregiert, sortiert nach SLA/Frist), `Benachrichtigungen` (failed + pending, gruppiert) und `Vorgänge` (bisherige Karten-/Tabellenansicht). Aggregation läuft über neuen Hook `usePersonWorkflowAggregates`, der für aktive Workflows parallel `getWorkflowByUid` (`useQueries`) zieht — Backend-Vertrag bleibt unverändert.

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Welcher FE-ID-Block wurde abgearbeitet?
3. Wie verhaelt sich die Aenderung zu den UX-Prinzipien des Reviews?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden ergaenzt oder fehlen noch?
6. Welche Doku musste mitgezogen werden? (Insbesondere `web/README.md` bei UI-Module-Verschiebungen)
7. Wurde der Eintrag aus der Tabelle "Offene Items" entfernt und unter "Abgeschlossene Zyklen" vermerkt?
