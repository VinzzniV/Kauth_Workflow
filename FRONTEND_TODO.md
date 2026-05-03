# FRONTEND_TODO.md

Frontend-spezifische Backlog-Liste — analog zum globalen `TODO.md`, aber gescoped
auf React/Tailwind/CSS-Themen. Quelle der Findings: **Frontend-Review 2026-05-03**
(Sub-Agent + Sichtpruefung), kalibriert gegen die UI/UX-Pro-Max-Pre-Delivery-Checkliste.

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
- Bei Abschluss Status hier auf `done` setzen.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus dieser Liste)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

Format-Beispiel: *„Naechster Schritt: FE-1 — AdminConfigPage zerlegen. Reasoning: high. Modell: opus."*

---

## Prioritaeten

- `HIGH` = strukturell wichtig fuer Wartbarkeit oder blockiert spaetere Arbeit
- `MEDIUM` = sichtbarer UX-Gewinn oder reduziert Bugs
- `LOW` = sinnvolle Haertung oder Bereinigung ohne unmittelbaren Schmerz

---

## Aktiver Zyklus: Frontend-Polish (2026-05-03)

Detailanalyse in der Konversation vom 2026-05-03 (Frontend-Review-Pass).
Einige Items sind bereits in `TODO.md` als Watch-Items / Defer dokumentiert —
hier stehen sie strukturiert mit konkretem Aufwand + Modell-Empfehlung.

| # | Aufgabe | Prio | Aufwand | Reasoning Effort | Modell | Status |
|---|---------|------|---------|------------------|--------|--------|
| FE-1 | **`AdminConfigPage` zerlegen** (UI-2 / LQ4-Z3). 70+ Props an `workspaceContentProps`, 7 Custom-Hooks. Sub-Hook-Aufteilung (`useAdminConfigOrganization`, `useAdminConfigCoreData`, `useAdminConfigGraph` etc.) + Props-Buendelung in Domain-Bundles statt Flat-Spread. | HIGH | 1,5–2 d | high | opus | offen |
| FE-2 | **Frontend-Rename `AdminTaskTemplate` → `AdminTaskSpec`, `templateKey` → `specKey`** (LA5-Watch). ~22 Files mechanisch (Types, Hooks, Services, Komponenten, Tests). Sed-Skript moeglich, danach manuelle Pruefung. Backend-DTO-Form bleibt unveraendert (siehe Watch-Item in `Legacy-Abbau-Plan.md`). | LOW | 0,5 d | medium | sonnet | offen |
| FE-3 | **Builder-Komponenten-Tests ergaenzen** — 4 weitere sinnvolle Tests: (a) `parseCondition` mit nested-JSON Edge-Case, (b) `WorkflowBuilderActionEditor` Reorder-Lifecycle, (c) `WorkflowBuilderMeasurePreview` mit echten Conditions/Dependencies, (d) `topologicallyOrderNodes` Zyklen-Edge-Case. | LOW | 1 d | medium | sonnet | offen |
| FE-4 | **Pagination Inline-Select** (UI-9). `WorkflowListFilters.tsx:137–152` — separate Buttons → `<select>` fuer Seite + "Erste/Letzte"-Shortcut-Buttons. Kosmetisch, aber Tab-Reihenfolge wird besser. | LOW | 0,5 d | low | sonnet | offen |
| FE-5 | **Form-Pattern explicit `htmlFor`/`id`** (UI-1). Implicit-Association ist WCAG-konform, aber `htmlFor`-Pattern ist robuster bei Screen-Reader-Span-Click. Konsistent ueber `WorkflowListFilters`, `EmployeeForm`, `RoleSelection` ziehen. | LOW | 0,5 d | low | sonnet | offen |
| FE-6 | **Mobile/Tablet-Layout dichte Listen** (UI-5 / R10). `RotationOperationsPage` Task-List + `WorkflowListResults` brauchen `<1024px`-Card-Variante. Aktuell Desktop-First, intern genutzt — kein konkreter Schmerz. | LOW | 1,5 d | medium | sonnet | defer — kein konkreter Bedarf |
| FE-7 | **Dark-Mode Visual-Regression-Pass** (UI-10). 150+ Tokens im CSS-Variable-System. Sichtprueftest pro Page (Theme-Toggle + Screenshot). Storybook-Setup wuerde es automatisieren — separate Diskussion. | MEDIUM | 0,5 d sichtprueftest / 3 d Storybook | medium | sonnet | offen — manuell startbar |
| FE-8 | **`approval_task_template_key` → `approval_spec_key`** (LA5-Watch). Spalte auf `workflow_definitions` heisst nominell noch `_template_key`, semantisch ist es Spec-Key. ~30 Files Backend+Frontend Rename. Cosmetic-Schuld, kein Funktionsproblem. | LOW | 1 d | medium | sonnet | defer ohne Trigger |
| FE-9 | **Spec-Carry-Over zwischen Definition-Versionen** (LA5-Watch). Wenn Admin per Builder eine neue Definition-Version published, werden Specs aktuell **nicht automatisch** vom alten zum neuen Massnahmen-Node geklont. Pre-Prod ohne Versions-Wechsel-Praxis — wird relevant, sobald Builder echte Versionswechsel produziert. Braucht Architektur-Skizze (clone-on-publish vs. Spec-DTO im Draft). | HIGH | 2–3 d | high | opus | defer — erst bei Builder-Use-Cases |
| FE-10 | **Browser-Verifikation Form-Editor** (R8). Alle 12 Schritt-Typen durchklicken (start, end, form, approval, task, decision, parallel_split/join, automation, measure_*). Manuelle Nutzer-Aufgabe — KI kann nicht pruefen. | MEDIUM | 0,5 d | — | — | offen — Nutzer-Aufgabe |

---

## Bewusst NICHT angefasst

- **`tailwind.config.js` Theme-Migration** (UI-11). Tokens leben heute nur als CSS-Variablen, `extend: {}` ist leer. Migration zu Tailwind-Tokens waere 5836 Zeilen CSS reformatieren — riesiger Aufwand, kein konkreter Mehrwert. Das aktuelle System funktioniert, hat sauberes Dark-Mode-Mapping, ist gut typisiert.
- **`translateY`-Layout-Shift-Konzern** (UI-4 aus Sub-Agent-Review). Sub-Agent-Behauptung war **falsch** — `transform: translateY(-1px)` verursacht keinen Reflow (GPU-composited, ausserhalb Layout-Flow). Subtle-Hover ist Best-Practice.
- **Emoji-Icon-Audit**. 0 Emoji-Icons im gesamten Frontend. ✓
- **Loading/Empty/Error-State-Audit**. Feedback-Layer (`AppErrorBoundary`, `LoadingState`, `EmptyState`, `SkeletonCard`, `ToastProvider`, `ConfirmationDialogProvider`) komplett vorhanden und konsistent genutzt. ✓

---

## Abgeschlossene Arbeit (Frontend-Polish 2026-05-03)

Diese Punkte sind **erledigt** in der Session vom 2026-05-03 — hier nur als Referenz dokumentiert,
damit klar ist, dass die Stelle nicht erneut angefasst werden muss.

| ID | Aufgabe | Status |
|----|---------|--------|
| FE-DONE-1 | **6 TS-Errors gefixt** (Rename-Leftovers): `WorkflowBuilderStepCard.tsx` unused param, `AppErrorBoundary` `detailJson`→`details`, `useAdminAnswerDefinitionManagement` + `useAdminRoleAnswerDefaults` `selectedProcessTypeId`→`selectedWorkflowDefinitionId`, `PersonWorkflowSummary.status: WorkflowStatus` entfernt. | ✓ done |
| FE-DONE-2 | **9 Lint-Errors gefixt**: `workflowBuilderEditorHelpers.ts` neu (Helper-Extract aus 3 Komponenten-Files fuer `react-refresh/only-export-components`-Compliance); `useAdminWorkflowVersionReferenceData` Refactor (Set-Berechnung in `useMemo`, async-setData fuer `react-hooks/set-state-in-effect`-Rule). | ✓ done |
| FE-DONE-3 | **Lucide-Icons-Migration** (UI-3 + UI-6). 6 Files: `WorkflowBuilderStepCard`, `AdminWorkflowBuilderFormSection`, `WorkflowBuilderActionEditor`, `WorkflowBuilderMeasurePreview`, `DependencyGraphEditor`, `WorkflowBuilderActionMappingEditor`. Symbole `↑↓✕▼►` durch `ChevronUp/Down/Right`, `X` ersetzt. + 1 fehlendes `aria-label` ergaenzt. | ✓ done |
| FE-DONE-4 | **Builder-Komponenten-Tests** (UI-7 / LQ6). 4 neue Testdateien, **23 Tests**: `WorkflowBuilderConditionEditor` (6) · `WorkflowBuilderActionMappingEditor` (6) · `WorkflowBuilderStepConfigEditor` (6, inkl. LA5-Regression-Schutz) · `WorkflowBuilderStepCard` (5, inkl. Lucide-SVG-Render-Verifikation). | ✓ done |
| FE-DONE-5 | **3 obsolete Tests entfernt** in `adminWorkflowBuilderModel.test.ts` (testeten LA2-removed `setup`→`measure_*`-Auto-Migration). | ✓ done |

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Welcher FE-ID-Block wurde abgearbeitet?
3. Wie verhaelt sich die Aenderung zu den UX-Prinzipien des Reviews?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden ergaenzt oder fehlen noch?
6. Welche Doku musste mitgezogen werden? (Insbesondere `web/README.md` bei UI-Module-Verschiebungen)
7. Wurde der Status hier auf `done` gesetzt?
