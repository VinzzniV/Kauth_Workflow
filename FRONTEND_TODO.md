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
| Z18-F4 | **`WorkflowSearchPage` lädt bis zu 1000 Vorgänge ohne Pagination.** `SEARCH_PAGE_SIZE = 1000` in `WorkflowSearchPage.tsx` erzeugt bei Wachstum eine spürbar träge Suche: großer SQL-Payload, langsames Rendering, mögliche Memory-Spitze im Browser. Gleichzeitig hat `WorkflowListPage` mit `SavedViews`, echtem Paging und gleichen Filtern bereits den besseren Einstieg. Fix: entweder `/search` auf dasselbe Paging-Modell wie `WorkflowListPage` umstellen (proper `pageIndex`-State), oder `/search` zugunsten des Listenfilters deprecaten und als Redirect einrichten. | HIGH | 1–2 d | medium | sonnet | offen |

---

## Naechster sinnvoller Schritt

Z18-S3 (Batch B) abgeschlossen 2026-05-08: F6/F7 done. Verbleibend: Z18-F4 (HIGH — WorkflowSearchPage Pagination/Redirect). Naechster Schritt: Codex priorisiert Z18-F4.

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
- `2026-05-08` `Z18-S2` Batch A abgeschlossen: F1 `.card-form`-Klasse eingefuehrt, `.dashboard-card` von 5 Formular-Containern entfernt; F2 `rotation-form-card` Ghost-Klasse entfernt; F3 `web/src/utils/employmentStatus.ts` erstellt, Duplikate aus 5 Dateien entfernt; F5 `scope="col"` auf alle 6 `<th>` in `PeopleDirectoryPage` gesetzt; F8 `offset` als URL-Param `page` in `PeopleDirectoryPage` persistiert; F9 `aria-controls` auf Area-Tab-Buttons in `AdminWorkspaceNavigation` verdrahtet. Build + 274 Tests gruen.
- `2026-05-08` `Z18-S3` Batch B abgeschlossen: F6 `useSupervisorWorkflows`-Hook + `useSupervisorStep`-Hook neu in `web/src/hooks/useSupervisorWorkflows.ts`; `SupervisorStepPage` auf `useQuery`/`useMutation` umgestellt, kein manuelles `setQueueLoading`/`setQueueError`/`setIsSaving` mehr; F7 Auto-Select-Fallback `?? sortedRows[0] ?? null` aus `WorkflowListResults` entfernt; `useEffect` ergaenzt: Reset bei Seitenwechsel wenn uid nicht mehr in rows; 4 neue Tests `WorkflowListResults.test.tsx`. Build + 278 Tests gruen.

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
