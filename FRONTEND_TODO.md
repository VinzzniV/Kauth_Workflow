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
| FE-8 | **`approval_task_template_key` → `approval_spec_key`** (LA5-Watch). Spalte auf `workflow_definitions` heisst nominell noch `_template_key`, semantisch ist es Spec-Key. ~30 Files Backend+Frontend Rename. Cosmetic-Schuld, kein Funktionsproblem. | LOW | 1 d | medium | sonnet | defer ohne Trigger |
| Z18-F1 | **`dashboard-card` hat `cursor: pointer` und Hover-Transform auf Formular-Containern.** `.dashboard-card` setzt semantisch „klickbar" (Zeiger-Cursor, Hover-Animation), wird aber als Wrapper fuer nicht-interaktive Formulare verwendet: `RotationStationFormCard`, `AdminDepartmentsSection` (`.card-primary`), `AdminUsersSection` (`.card-primary`), `AdminResponsibilitiesAndRequirementsSection`. Nutzer sehen einen Zeiger-Cursor auf Flächen, die keinen Klick auslösen — das untergräbt das Feedback-System. Fix: separate CSS-Klasse `.card-form` (ohne pointer/hover) oder `.dashboard-card--no-action` als Modifier einführen und an den 4 Stellen substituieren. | MEDIUM | 0,5 d | low | sonnet | offen |
| Z18-F2 | **`rotation-form-card` ist eine Ghost-Klasse ohne CSS-Regeln.** Wird in 3 Dateien als className verwendet (`RotationStationFormCard.tsx`, `RotationPlanningPage.tsx`, `AdminResponsibilitiesAndRequirementsSection.tsx`), hat aber null CSS-Definitionen. Macht nichts, verwirrt aber künftige Entwickler und lässt sich als Anker für Z18-F1-Fix nutzen. Fix: entweder CSS-Regeln hinzufügen (z. B. als Alias für `.card-form`) oder aus allen TSX-Dateien entfernen. | LOW | 0,25 d | low | sonnet | offen |
| Z18-F3 | **`formatEmploymentStatus` und `formatDirectoryLinkStatus` sind in 3–4 Dateien dupliziert.** `formatEmploymentStatus` kommt vor in: `PeopleDirectoryPage.tsx`, `PersonWorkflowHistoryPage.tsx`, `CreateWorkflowPageSections.tsx`, `RotationPlanningPage.tsx`. `formatDirectoryLinkStatus` in: `PeopleDirectoryPage.tsx`, `PersonWorkflowHistoryPage.tsx`, `CreateWorkflowPageSections.tsx`. Außerdem `getEmploymentStatusClass` (PeopleDirectory) vs. `getEmploymentChipClass` (PersonWorkflowHistory) — gleiche Logik, verschiedene Namen. Wenn sich employment-Status-Werte ändern, müssen alle Kopien manuell gefunden werden. Fix: `web/src/utils/employmentStatus.ts` mit den 3–4 Funktionen erstellen, alle Importe umstellen. | MEDIUM | 0,5 d | low | sonnet | offen |
| Z18-F4 | **`WorkflowSearchPage` lädt bis zu 1000 Vorgänge ohne Pagination.** `SEARCH_PAGE_SIZE = 1000` in `WorkflowSearchPage.tsx` erzeugt bei Wachstum eine spürbar träge Suche: großer SQL-Payload, langsames Rendering, mögliche Memory-Spitze im Browser. Gleichzeitig hat `WorkflowListPage` mit `SavedViews`, echtem Paging und gleichen Filtern bereits den besseren Einstieg. Fix: entweder `/search` auf dasselbe Paging-Modell wie `WorkflowListPage` umstellen (proper `pageIndex`-State), oder `/search` zugunsten des Listenfilters deprecaten und als Redirect einrichten. | HIGH | 1–2 d | medium | sonnet | offen |
| Z18-F5 | **`PeopleDirectoryPage`-Tabelle hat keine `scope="col"`-Attribute auf `<th>`.** Alle anderen sortierbaren Tabellen im FE (WorkflowListResults, MyTasksPage, PersonWorkflowHistoryPage, RotationOperationsPage) setzen `scope="col"` korrekt. Der Mitarbeiter-Verzeichnis-Tabelle fehlt das, was Screen-Reader dazu bringt, Spaltenüberschriften und Zellen zuverlässig zu verknüpfen. Fix: `scope="col"` zu allen sechs `<th>`-Elementen in `PeopleDirectoryPage.tsx` hinzufügen. | MEDIUM | 0,1 d | low | sonnet | offen |
| Z18-F6 | **`SupervisorStepPage` verwendet manuelles async/await mit lokalem State statt React Query.** Alle anderen Seiten nutzen React-Query-Hooks (Caching, Retry, `staleTime`, `isFetching`). `SupervisorStepPage` hat eigene `setQueueLoading`, `setQueueError`, `setIsSaving` u.a. Fehlerpfade können auseinanderlaufen; kein automatisches Stale-while-Revalidate, kein Retry. Fix: neuen Hook `useSupervisorWorkflows` mit React Query erstellen; `SupervisorStepPage` auf den Hook umstellen. | MEDIUM | 0,5 d | medium | sonnet | offen |
| Z18-F7 | **Split-Workspace: Detail-Pane auto-selektiert ersten Eintrag ohne Nutzer-Geste.** In `WorkflowListResults` gilt: `const selectedWorkflow = sortedRows.find(uid) ?? sortedRows[0] ?? null` — auf Erstladen und nach Seitenwechsel wird sofort der erste Vorgang in der Detail-Pane gezeigt, obwohl der Nutzer nichts angeklickt hat. Das ist verwirrend: Der Eindruck entsteht, ein Vorgang sei ausgewählt worden. Fix: `selectedWorkflowUid` auf `null` setzen, bis der Nutzer erstmalig klickt; bei Seitenwechsel resetzen; `WorkflowPreviewPane` zeigt dann immer den richtigen „Wählen Sie links aus"-Hinweis. | MEDIUM | 0,5 d | low | sonnet | offen |
| Z18-F8 | **`PeopleDirectoryPage` Pagination: `offset` nicht in URL-Params persistiert.** Suchbegriff (`q`) wird korrekt in die URL geschrieben. Der Seitenzähler (`offset`) nicht. Wer sich auf Seite 3 befindet und den Tab neu lädt oder den Link teilt, landet wieder auf Seite 1. Konsistentes Verhalten mit `WorkflowListPage` (persistiertes `page`-Param) wäre erwartbar. Fix: `offset` als URL-Param `page` (0-basiert oder 1-basiert konvertiert) schreiben und beim Mount initialisieren. | LOW | 0,25 d | low | sonnet | offen |
| Z18-F9 | **Admin-Workspace-Navigation: `aria-expanded` auf Area-Tabs ohne `aria-controls`.** `AdminWorkspaceNavigation.tsx` setzt `aria-expanded={isAreaActive}` auf Area-Tab-Buttons, aber ohne `aria-controls` + passendes `id` auf der Sub-Navigation. Screen-Reader-Nutzer wissen, dass etwas expandiert ist, können aber nicht per Tastenkürzel direkt zur Sub-Nav springen. Fix: Sub-Nav-Container je eine `id` geben; die Area-Tab-Buttons mit `aria-controls={subNavId}` verdrahten. | LOW | 0,25 d | low | sonnet | offen |

---

## Naechster sinnvoller Schritt

Z18-S1 (Frontend Full Review) abgeschlossen 2026-05-08. 9 neue Findings (Z18-F1 bis Z18-F9) dokumentiert. Nächster Schritt: Codex priorisiert, welche Z18-Findings als Slices umgesetzt werden. Empfohlen für erste Umsetzung: Z18-F3 (Utility-Extraktion, reines Refactor, kein Seiteneffekt) und Z18-F5 (Accessibility, minimaler Aufwand).

---

## Bewusst NICHT angefasst

- **`tailwind.config.js` Theme-Migration** (UI-11). Tokens leben heute nur als CSS-Variablen, `extend: {}` ist leer. Migration zu Tailwind-Tokens waere 5836 Zeilen CSS reformatieren — riesiger Aufwand, kein konkreter Mehrwert. Das aktuelle System funktioniert, hat sauberes Dark-Mode-Mapping, ist gut typisiert.
- **`translateY`-Layout-Shift-Konzern** (UI-4 aus Sub-Agent-Review). Sub-Agent-Behauptung war **falsch** — `transform: translateY(-1px)` verursacht keinen Reflow (GPU-composited, ausserhalb Layout-Flow). Subtle-Hover ist Best-Practice.
- **Emoji-Icon-Audit**. 0 Emoji-Icons im gesamten Frontend. ✓
- **Loading/Empty/Error-State-Audit**. Feedback-Layer (`AppErrorBoundary`, `LoadingState`, `EmptyState`, `SkeletonCard`, `ToastProvider`, `ConfirmationDialogProvider`) komplett vorhanden und konsistent genutzt. ✓

---

## Abgeschlossene Zyklen

- `2026-05-05` `Z11-F1` P1-Hull fuer Master-Data/Lookups im FE aufgenommen: neuer typed Wrapper `web/src/services/api/adminList.ts`, `lookupApi.ts`/`adminApi.ts` auf `AdminListPage<T>` umgestellt, betroffene Consumer vorerst pragmatisch auf `page.items` + `limit: 200` adaptiert. UX-Prinzip: Vertrag zuerst vereinheitlichen, sichtbare Paging-UI erst im passenden Folgeslice statt halb fertiger Mischloesung.
- `2026-05-06` `Z11-F2` P2-Hull fuer Audit-Streams im FE aufgenommen: neuer typed Wrapper `web/src/services/api/cursorPage.ts`; `adminApi.ts`/`adminConfigApi.ts` auf `CursorPage<T>` umgestellt; `useAdminConfigData` akkumuliert bei „Mehr laden" (append), resettet bei Neuladen; beide Audit-Tabs (`AdminPermissionsSection`, `AdminDirectorySyncSection`) haben „Mehr laden"-Knopf. UX-Prinzip: kein stiller Cap mehr — Knopf erscheint nur wenn `hasMore=true`.
- `2026-05-06` `Z11-F3` P1-Hull fuer sieben scoped Builder-Endpunkte im FE aufgenommen: `services/adminConfigApi.ts`-Wrapper (`getAdminTaskTemplates`, `…Conditions`, `…Dependencies`, `getAdminAnswerDefinitions`, `getAdminRoleAnswerDefaults`, `getAdminWorkflowDefinitions`, `getAdminWorkflowActionDefinitions`) liefern jetzt `Promise<AdminListPage<T>>`; alle Builder-Hooks (`useAdminTaskTemplateData`, `useAdminAnswerDefinitionManagement`, `useAdminRoleAnswerDefaults`, `useAdminWorkflowVersionReferenceData`, `useAdminWorkflowBuilder`) lesen pragmatisch `page.items` mit `limit: 200`. UX-Prinzip: Vertrag zuerst vereinheitlichen — sichtbare Paging-/Filter-UI mit URL-Query-Filterzustand bleibt eigenstaendiger UI-Slice ausserhalb Z11 (bewusste Restgrenze).

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
