# PROD_TODO.md

## Zweck

- Produkt-/UX-/Produktionsreife-Slices aus dem aktuellen Z21-Review (Stand 2026-05-12)
- ein eigener Slice-Plan fuer den Schritt von „funktioniert im Dev-Modus" zu „produktiv verantwortbar"
- nicht doppelt zu `TODO.md` oder `FRONTEND_TODO.md` pflegen — diese Datei zeigt nur die Slice-Reihenfolge und den jeweils erwarteten Aufwand/Modell-Profil

## Primaerquelle fuer

- naechsten produkt-/produktivitaetsrelevanten Slice
- Reihenfolge der Z21-Nacharbeit
- Bewertung Reasoning Effort + Modell + Plan-Mode pro Slice

## Nicht verwenden fuer

- abgeschlossene Detailhistorie → `CODE_REVIEW_ARCHIVE.md`
- aktive Review-Befunde → `CODE_REVIEW.md` (Z21)
- frontend-spezifische Backlog-Items → `FRONTEND_TODO.md`
- backend-Review-Nacharbeit → `TODO.md`

## Verhaeltnis zu `TODO.md` (Slice-Nummerierung)

`PROD_TODO.md` und `TODO.md` fuehren beide eine eigene Z21-Slice-Nummerierung. Die Slices sind fachlich teilweise gleich, die Nummerierung ist aber nicht synchron — Grund: `TODO.md` listet zusaetzlich Folge-Aufbauten (Z21-S7..S10), die im PROD_TODO-Slice-Plan bewusst nicht gefuehrt werden.

| PROD_TODO | TODO.md | Inhalt |
|---|---|---|
| Z21-S1 | Z21-S1 | Simulation klar markieren + Mail-Dispatch-Health |
| Z21-S2 | Z21-S3 | Hybrid-AD-Architekturentscheidung |
| Z21-S3 | Z21-S2 | Workflow-Storno fuer laufende Vorgaenge |
| Z21-S4 | — | FE-UX-Buendel (Persona-Switcher, Detail-Tabs, directory_only, Nav-Reihenfolge) |
| Z21-S5 | Z21-S6 | Builder fachsprachlicher (Mapping-Labels + Condition-Wording) |
| Z21-S6 | — | `start-vm.sh dev`-Vorab-Check (Betriebs-Hygiene) |
| — | Z21-S4 | Realer Automation-Pfad aus Hybrid-AD-Entscheidung |
| — | Z21-S5 | Runtime-/Admin-Sicht fuer fehlgeschlagene Automation/Mailversand |
| — | Z21-S7..S10 | Folge-Aufbauten Durchlaufplanung-Absicherung, Workflow-Detail-Ergonomie, Mitarbeiter-Trennung, Produktions-Verifikationslauf |

Praktisch: bei Slice-Arbeit immer pruefen, ob die `TODO.md`-Sicht zusaetzliche Vorgaben (Prio, Status) traegt, und beide Listen im selben Arbeitsgang synchron mitziehen.

## Verwandte Dateien

- `CODE_REVIEW.md` — aktiver Z21-Review mit allen Findings und Belegen
- `TODO.md` — backend- und review-bezogene Nacharbeit, zyklusuebergreifend offene Befunde
- `FRONTEND_TODO.md` — frontend-spezifische Backlog-Items, UI-/UX-Slices
- `MEMORY.md` — kurzfristiger Fokus + aktive Watchouts
- `DOCS_CONTROL.md` — Doku-Lesereihenfolge und Pflege-Regeln
- `KauthWorkflow/Architektur/Entscheidungen.md` — fuer Architektur-Slices (z. B. S2)

## Wann aktualisieren

- wenn ein Slice abgeschlossen ist (Eintrag entfernen oder als done markieren und in `CODE_REVIEW.md` Z21-Block aktualisieren)
- wenn sich Priorisierung oder Slice-Schnitt aendert
- wenn ein Slice wegen einer Architektur-Entscheidung blockiert ist

## Pflicht vor jeder Aufgabe

Vor jedem Slice zuerst lesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`
- `CODE_REVIEW.md` (aktiver Z21-Block)
- diesen File
- bei Frontend-Anteilen zusaetzlich `FRONTEND_TODO.md` und `web/README.md`

Schreibregel: jeder Slice muss in `CODE_REVIEW.md` neben der Technik kurz erklaeren, was er praktisch bedeutet, warum er sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird (siehe `CODE_REVIEW.md` § „Schreibregel").

---

## Slice-Plan Z21

Reihenfolge nach Risiko/Endnutzer-Nutzen. Die Slice-IDs `Z21-S1`..`Z21-S6` referenzieren die Findings im aktiven Z21-Block in `CODE_REVIEW.md`.

| # | Slice | Findings | Aufwand | Reasoning | Modell | Plan-Mode | Status |
|---|---|---|---|---|---|---|---|
| 1 | **Z21-S1** Simulation deutlich markieren + Mail-Dispatch-Health | P0-1 + P1-3 | klein-mittel (~½–1 Tag) | medium | sonnet | aus | **done 2026-05-12** |
| 2 | **Z21-S2** Hybrid-AD-Architekturentscheidung | P0-2 | klein in Zeilen, gross in Tragweite | high | opus | **an** | **done 2026-05-12** |
| 3 | **Z21-S3** Workflow-Storno fuer laufende Vorgaenge | P0-3 | mittel-gross (~1–2 Tage) | high | opus | **an** | **done 2026-05-12** |
| 4 | **Z21-S4** FE-UX-Buendel (Persona-Switcher, Workflow-Detail-Tabs, Listen-Trennung, directory_only) | P1-1 + P1-4 + P2-1 + P2-2 | mittel (~1 Tag) | medium | sonnet | aus | **done 2026-05-12** |
| 5 | **Z21-S5** Builder fachsprachlicher (Mapping-Labels + Wording „Schritt") | P1-2 (UX-Teil) + P3-2 | mittel (~½–1 Tag) | high | opus | **an** | **done 2026-05-12 (UX-Teil)** |
| 6 | **Z21-S6** `start-vm.sh dev`-Vorab-Check | P1-5 | klein (~½ h) | low | sonnet | aus | offen |

> Z21-S5 ist **nur der UX-Teil** des P1-2-Findings: Mapping-Editor mit Fach-/Technik-Optgroups + Wording-Vereinheitlichung. Der **AND/OR-Mehrbedingungen-Resthebel** im Condition-Editor (Runtime-Verhaltenswechsel in `WorkflowRuntimeEngine.ParseDecisionCondition`) ist eigener Folge-Slice und liegt als P1-2-Rest in `CODE_REVIEW.md` + Z21-S5b im aktiven Backlog (`TODO.md`).

### Z21-S1 · Simulation deutlich markieren + Mail-Dispatch-Health

**Buendelung:** P0-1 (Automation ist nur Simulation, im UI nicht erkennbar) und P1-3 (Mail-Dispatch-Health im Admin-Dashboard sichtbarer) berueren denselben Aggregator (Admin-Warnings + Status-Tiles).

**Praktisch:** Admin sieht im Builder und im Admin-Dashboard sofort, dass keine echten externen Wirkungen entstehen und ob aktuelle Mail-Dispatches fehlschlagen.

**Lohnenswert:** Verhindert produktive Fehlbedienung (Workflow „succeeded", aber AD-Konto existiert nicht). Aufwand klein gegenueber Risiko.

**Nutzen:** „Wahrheit ueber den tatsaechlichen Runtime-Stand" wird sichtbar ohne Logs zu kennen.

**Scope:**
- Backend: `action_definitions.is_simulated` (oder Ableitung aus `backing_kind LIKE 'simulated_%'`) im List-Endpoint + Action-Definition-DTO sichtbar machen.
- Optional kleiner Endpoint fuer aktuelle Dispatch-Failures (letzte N Stunden) als Aggregat fuer das Admin-Dashboard.
- FE: Badge „Simuliert" in `WorkflowBuilderActionEditor` neben jeder Action; Warning-Item im `AdminOverviewWorkspaceSection`; rotes Stat-Tile bei kuerzlich blockierten Dispatches.
- Tests: ein Backend-Test fuer das DTO, ein FE-Snapshot/Render-Test.

**Risiko/Reversibilitaet:** klein. Reines Anzeigen-/Aggregator-Slice, kein Lifecycle-Mutationspfad.

---

### Z21-S2 · Hybrid-AD-Architekturentscheidung — done 2026-05-12

**Entschieden:** Option 2 — AD on-prem fuehrt, schreibende Lifecycle-Aktionen laufen ueber einen dedizierten Windows-Worker. Entra wird ueber AD Connect nachgefuehrt. `EntraGraphClient` bleibt read-only.

**Belegt in:**
- `KauthWorkflow/Architektur/Entscheidungen.md` → Abschnitt „AD/Entra-Schreibrichtung: on-prem AD fuehrt via Windows-Worker (2026-05-12, Z21-S2)"
- `KauthWorkflow/Architektur/Migrationspfad.md` → neue Etappe 9a in der Reihenfolge-Tabelle + Detailblock „Etappe 9a · Schreibender Automation-Layer" mit 4-Schritte-Zielbild
- `PROJECT_CONTEXT.md` → Guardrail-Block „Automationen bleiben kontrolliert" um 2 Zeilen ergaenzt (Schreibrichtung + Kein-Graph-Schreibpfad)
- `CODE_REVIEW.md` → Z21-P0-2 als done markiert mit Verweis auf Entscheidungen.md/Migrationspfad.md

**Verworfen:**
- Option 1 (Entra fuehrt, Graph-only): widerspricht on-prem-Primat
- Option 3 (Beidseitige Spiegelung): doppelte Idempotenz ohne Mehrwert ueber AD Connect

**Folge-Sub-Entscheidungen** (bewusst NICHT in diesem Slice festgelegt, dokumentiert in Migrationspfad-Etappe 9a Schritt 1): Worker-Deploymentmodell, Transport API↔Worker, AD-Schreibmechanik, Domaen-Authentisierung, Audit-Rueckkanal. Diese sind eigener Plan-Mode-Slice vor jeder Code-Arbeit.

**Wirkt blockierend auf:** Z21-S5 „echte Handler" als denkbares Folgeslice — bleibt bis Etappe 9a abgeschlossen ist im Simulationsmodus. Z21-S1-Banner bleibt aktiv bis Action-Katalog kein `simulated_*` mehr enthaelt.

---

### Z21-S3 · Workflow-Storno fuer laufende Vorgaenge — done 2026-05-12

**Entschieden + umgesetzt:** Stornierbar aus allen aktiven Status (`in_progress`, `waiting_for_supervisor`, `waiting_for_department`). Vordefinierte Liste (`entry_cancelled`, `entry_postponed`, `wrong_person`, `started_by_mistake`, `other`) + Freitext-Detail (Pflicht bei `other`). `cancelled` ist terminal — kein Re-Open. Rotation: nicht betroffen, da `rotation_plans.source_workflow_id` ohnehin nur `completed`-Workflows zulaesst.

**Belegt in:**
- DB: `db/manual/2026-05-12_workflow_cancellation.sql` (4 neue Spalten + Status-Constraints fuer `workflows` und `workflow_tasks`), `db/01_schema.sql` synchron, Manifest-Eintrag, `SchemaParityTests`-Marker.
- Backend: `WorkflowStatusRules.Cancelled` + `IsCancellable`/`IsTerminal`, `WorkflowCancellationReasonCodes` + DTOs, `IWorkflowRepository.LookupWorkflowForCancellation`/`CancelWorkflow`, `WorkflowRuntimeService.CancelWorkflowAsync` (Outcome-Pattern), `AuthorizationPolicyService.CanCancelWorkflow`, `POST /workflows/{uid}/cancel`. Build `0 Warnings 0 Errors`.
- FE: `cancelWorkflow` + `useCancelWorkflow`, `CancelWorkflowDialog`, Button + Cancellation-Readonly-Block im `WorkflowManagementPanel`, Status-/Reason-Mapping in `mappers.ts`, Tests `CancelWorkflowDialog.test.tsx` + `WorkflowManagementPanel.cancel.test.tsx`. FE-Build + Typecheck clean, 297 FE-Tests gruen.
- Tests: 8 neue `WorkflowCancellationReasonCodesTests`, erweiterte `WorkflowStatusRulesTests` (cancellable + terminal), 5 neue `AuthorizationPolicyService.CanCancelWorkflow`-Tests, 8 + 7 FE-Tests. Backend-Test-Build steht auf bekannten 7 pre-existing Errors aus frueheren Refactorings — Z21-S3 fuegt KEINE neuen Errors hinzu (Stubs entsprechend erweitert).
- Doku: `CODE_REVIEW.md` P0-3 ✅ done, Bereich „Workflow-Storno" auf A-, Bereich „Laufende Vorgaenge"/„Workflow-Detail" auf voll produktiv. `KauthWorkflow/Domäne/Workflow.md` um Cancel-Lifecycle erweitert.

**Risiko/Reversibilitaet:** Migration additiv, idempotent. Status-Constraint erweitert (nicht entfernt). Kein Datenverlust-Pfad.

---

### Z21-S4 · FE-UX-Buendel — done 2026-05-12

**Umgesetzt:** 4 Sub-Slices in einem Commit.

- **P1-4 Persona-Switcher**: Hinweistext „Nur Anzeige – keine Rechteaenderung" als `.persona-switcher__hint` unter den Buttons (CSS: `flex-basis: 100%`, `color: var(--text-tertiary)`).
- **P2-2 Workflow-Detail-Tabs**: `WorkflowDetailPage` hat jetzt drei Tabs mit `.admin-tab-strip`/`.admin-tab`-Pattern. Tab „Status & Aufgaben" zeigt `WorkflowTaskAreasSection`, Tab „Anforderungen" zeigt `WorkflowRequirementsPanel`, Tab „Audit & Links" zeigt `WorkflowLinksPanel` + `WorkflowManagementPanel` + Notifications + AuditLog. `WorkflowHeaderPanel` bleibt immer sichtbar oberhalb der Tabs.
- **P1-1 directory_only-Trennung**: `PeopleDirectoryPage` trennt `isDirectoryOnlyEntry`-Eintraege in eigene Sektion „Aus Entra noch nicht uebernommen" mit Erklaerungstext. Abteilungsgruppen zeigen nur echte Mitarbeitende. P3-1 (Inline-Styles) bewusst nicht mitgenommen.
- **P2-1 Listen-Trennung**: `deriveNavigationContext` mit praegnanten, aktionsorientierten Beschreibungen. `collectActionKeys` fuer Pure-Worker-Persona: `departmentTasks` und `rotationOperations` werden vor `hrWorkflows` eingefuegt.

**Belegt in:** `PersonaSwitcher.tsx` + `dashboard.css`, `WorkflowDetailPage.tsx`, `PeopleDirectoryPage.tsx`, `useRoleAwareNavigation.ts`. FE-Build clean, 300 Tests gruen.

---

### Z21-S5 · Builder fachsprachlicher — UX-Teil done 2026-05-12

**Umgesetzt:**

- **Mapping-Labels (Backend-DTO):** `AutomationPropertyCatalog.cs` erweitert um `AutomationPropertyCatalogPropertyDto { Key, Label, Kind }`. Jede bekannte Property hat ein deutsches Label und ein `kind` (`"business"` oder `"technical"`). ID-Felder (`workflowId`, `personId`, `directoryIdentityId`, `appUserId`, `departmentId`, `roleId`, ...) sind als `technical` markiert, Fachfelder (`firstName`, `email`, `entryDate`, ...) als `business`.
- **Mapping-Editor UX:** `WorkflowBuilderActionMappingEditor.PropertyDropdown` rendert zwei `<optgroup>`: „Fachfelder" zuerst, „Technische Felder" am Ende. Wenn ein bereits gemapptes Property nicht (mehr) im Catalog steht, wird es unter „Technische Felder" mit `(unbekannt)`-Suffix gezeigt — verhindert stille Wertwechsel beim Reload.
- **P3-2 Wording „Schritt":** 2 Stellen im Builder-UI (`AdminWorkflowBuilderFormSection.tsx:1522`, `WorkflowBuilderStepCard.tsx:200`) von „Knoten" auf „Schritt" gehoben. „Maßnahmen-Baustein" bleibt unveraendert (fachlich verankert in `PROJECT_CONTEXT.md` § Phasenfluss).

**Belegt in:** `AutomationPropertyCatalog.cs`, `AutomationPropertyCatalogTests.cs` (neu), `adminConfigApi.ts`, `WorkflowBuilderActionMappingEditor.tsx`, `WorkflowBuilderActionMappingEditor.labels.test.tsx` (neu, 4 Tests gruen), `AdminWorkflowBuilderFormSection.tsx`, `WorkflowBuilderStepCard.tsx`. Backend-Build 0 Errors. FE-Build clean, 304 Tests gruen. Backend-Test-Build steht auf bekannten 7 pre-existing Errors aus frueheren Refactorings — Z21-S5 fuegt 0 neue Errors hinzu.

**Resthebel — eigener Folge-Slice (Z21-S5b in `TODO.md`):** AND/OR-Mehrbedingungen im Decision-Condition-Editor. Heute akzeptiert `WorkflowRuntimeEngine.ParseDecisionCondition` strikt nur eine einzelne Bedingung pro Edge — Mehrbedingungen waeren ein echter Runtime-Verhaltenswechsel mit Schema-/Rueckwaertskompat-Bedarf, gehoeren nicht in diesen UX-Slice.

---

### Z21-S6 · `start-vm.sh dev`-Vorab-Check

**Praktisch:** Skript meldet sofort, wenn `dotnet`/`npm` fehlen, statt halb durchzulaufen.

**Lohnenswert:** Spart Setup-Zeit bei Demo-/Test-VMs.

**Nutzen:** Schneller produktiver Dev-Stand auf einer neuen Linux-VM.

**Scope:**
- `scripts/start-vm.sh`: `command -v dotnet` / `command -v npm` mit klarer Fehlermeldung und Verweis auf `KauthWorkflow/Betrieb/Setup.md`.
- Setup-Doku-Hinweis prominenter.

**Risiko/Reversibilitaet:** trivial.

---

## Bewusst nicht im Z21-Slice-Plan

| ID | Grund |
|----|-------|
| P2-4 Frontend-Bundle | aktuell vertretbar, kein Endnutzer-Schmerz |
| P2-5 Tailwind-Tokens | unter `FRONTEND_TODO.md` „Bewusst NICHT angefasst" |
| P3-1 Inline-Styles `PeopleDirectoryPage` | wenn S4 P1-1 ohnehin in der Datei arbeitet, mitnehmen; sonst Backlog |
| P3-2 Wording „Schritt/Knoten/Baustein" | mitnehmen mit S5; sonst Backlog |
| P3-3 R8/R10 | offene Nutzer-Aufgaben (Browser-Verifikation, Mobile-Layout), nicht code-pruefbar |

---

## Reihenfolge-Empfehlung

1. **Z21-S1 zuerst.** ✅ erledigt 2026-05-12. Hoechster Risiko-Reduktionsnutzen pro Aufwand.
2. **Z21-S2 parallel.** ✅ erledigt 2026-05-12 — Richtungsentscheidung steht (Option 2 / Windows-Worker). Folge-Slice „Schreibender Automation-Layer" (Migrationspfad-Etappe 9a) ist eigener Etappenpfad und **nicht Teil des Z21-Slice-Plans**.
3. **Z21-S3.** ✅ erledigt 2026-05-12. Zweiter Show-Stopper raus.
4. **Z21-S4** als spuerbarer UX-Sprung im Alltag.
5. **Z21-S5** loest das Builder-Zielarchitektur-Versprechen ein.
6. **Z21-S6** als Aufraeumarbeit.

Abschluss eines Slices loescht den jeweiligen Eintrag aus der Tabelle oben und wandert als Abschnitt in den Z21-Block in `CODE_REVIEW.md` (mit „done"-Vermerk + Datum), nicht hierher.
