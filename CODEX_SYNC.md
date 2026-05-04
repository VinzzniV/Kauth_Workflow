# CODEX_SYNC.md

Dieses Dokument ist das Handoff-Protokoll zwischen Codex und Claude.

## Zweck

- Codex traegt hier nach jeder abgeschlossenen Aufgabe einen Eintrag ein.
- Claude liest diese Datei am Anfang jeder Session, um sich ueber Codex-Aenderungen zu informieren.
- So entsteht kein blinder Fleck, wenn beide KIs am selben Repo arbeiten.

## Regeln

- Eintrag pro abgeschlossener Aufgabe (nicht pro Commit).
- Format: Datum | Aufgaben-ID | betroffene Dateien | kurze Zusammenfassung | offene Risiken.
- Wenn eine Aufgabe nur teilweise umgesetzt wurde, Eintrag mit Status `PARTIAL` und Erklaerung.
- Claude prueft bei jedem Sync-Eintrag, ob die Aenderung die eigene Arbeit beeinflusst.
- Veraltete Eintraege (Task `done` in TODO.md) koennen entfernt werden — Detail-Historie bleibt im git log.

---

## Sync-Log

| Datum | Aufgabe | Status | Betroffene Dateien | Zusammenfassung | Offene Risiken |
| --- | --- | --- | --- | --- | --- |
| 2026-05-04 | FE-ROADMAP-2026-05-04 | done | `FRONTEND_TODO.md`, `PROJECT_STRUCTURE.md`, `MEMORY.md`, `CODEX_SYNC.md` | Frontend-Review in konkrete FE-Backlog-Items `FE-25` bis `FE-34` ueberfuehrt, in 5 Abarbeitungsbloecke gegliedert und mit Reasoning-Effort/Modell + naechstem Startschritt dokumentiert. | Roadmap ist priorisiert, aber noch nicht in `TODO.md`; Umsetzung startet erst mit explizitem FE-Arbeitszyklus. |
| 2026-05-04 | FE-25 | done | `web/src/components/feedback/confirmationDialogContext.ts`, `web/src/components/feedback/ConfirmationDialogProvider.tsx`, `web/src/hooks/useAdminWorkflowBuilder.ts`, `web/src/components/admin-config/AdminWorkflowBuilderFormSection.tsx`, `web/src/styles/components.css`, `web/tests/WorkflowBuilderPage.test.tsx`, `web/tests/ConfirmationDialogProvider.test.tsx`, `FRONTEND_TODO.md`, `MEMORY.md`, `CODEX_SYNC.md` | Browser-`confirm` im Builder fuer Discard/Delete/Step-Remove auf den App-Dialog umgestellt; Dialog um Focus-Start, Focus-Trap, Focus-Restore und destruktives Styling erweitert; Builder-Discard nutzt jetzt expliziten Reload-Pfad statt Re-Select-Hack. | Andere historische `window.confirm`-Vorkommen ausserhalb des Builder-/FE-25-Scope existieren noch und fallen unter spaetere FE-Zyklen. |
| 2026-05-04 | FE-26 / FE-27 | done | `web/src/pages/AdminConfigPage.tsx`, `web/src/components/admin-config/AdminNotificationTemplateSection.tsx`, `web/src/components/feedback/AppErrorBoundary.tsx`, `web/src/pages/WorkflowListFilters.tsx`, `web/src/pages/WorkflowListPage.tsx`, `web/src/pages/workflowListPageModel.ts`, `web/src/pages/MyTasksPage.tsx`, `web/src/pages/RotationPlanningPage.tsx`, `web/src/pages/RotationOperationsPage.tsx`, `web/src/styles/components.css`, `web/src/styles/workflow.css`, `web/tests/WorkflowListPage.test.tsx`, `web/tests/RotationPlanningPage.test.tsx`, `FRONTEND_TODO.md`, `MEMORY.md`, `CODEX_SYNC.md` | Wiederkehrende Inline-Layouts aus Admin- und Feedback-Flows in gemeinsame CSS-Klassen überführt; Filterleisten von WorkflowList, MyTasks, RotationPlanning und RotationOperations um Reset-Aktionen und aktive Filter-Chips ergänzt und visuell angeglichen. | Filter-Chips sind bewusst lokal in den vier Pages implementiert; falls später weitere Listen dieselbe Mechanik brauchen, lohnt ein kleines Shared-UI-Primitive. |
| 2026-05-04 | FE-28 | done | `web/src/components/layout/ViewModeToggle.tsx`, `web/src/pages/WorkflowListResults.tsx`, `web/src/pages/MyTasksPage.tsx`, `web/src/pages/RotationOperationsPage.tsx`, `web/src/pages/PersonWorkflowHistoryPage.tsx`, `web/src/styles/components.css`, `web/tests/WorkflowListPage.test.tsx`, `web/tests/MyTasksPage.test.tsx`, `web/tests/RotationOperationsPage.test.tsx`, `web/tests/PersonWorkflowHistoryPage.test.tsx`, `web/src/components/admin-config/AdminConfigWorkspaceSections.tsx`, `web/src/components/admin-config/adminWorkspaceModel.ts`, `web/src/components/admin-config/workflowBuilderLabels.ts`, `web/src/components/admin-config/WorkflowBuilderStepCard.tsx`, `web/src/hooks/useAdminConfigPageView.ts`, `FRONTEND_TODO.md`, `MEMORY.md`, `PROJECT_STRUCTURE.md`, `web/README.md`, `CODEX_SYNC.md` | Gemeinsamen Karten-/Tabellen-Schalter und operative Tabellenklassen eingeführt; WorkflowList, MyTasks, RotationOperations und PersonWorkflowHistory haben nun Tabellenmodus mit sortierbaren Spalten, Sticky Headern und Mobile-Fallback auf Karten/Listen. Nebenbei fünf kleine Admin-/Builder-TypeScript-Buildfehler behoben. | Tabellenmodus sortiert clientseitig nur die aktuell geladenen/gefilterten Zeilen; echte serverseitige Sortierung bleibt ein spaeterer Performance-Hebel fuer sehr grosse Datenmengen. |
| 2026-05-04 | FE-29 | done | `web/src/pages/MyTasksPage.tsx`, `web/src/pages/WorkflowListResults.tsx`, `web/src/styles/components.css`, `web/tests/MyTasksPage.test.tsx`, `web/tests/WorkflowListPage.test.tsx`, `FRONTEND_TODO.md`, `MEMORY.md`, `web/README.md`, `CODEX_SYNC.md` | Split-Workspace fuer Listenarbeit eingefuehrt: `Meine Aufgaben` behaelt die Vorgangsauswahl links und bearbeitet Aufgaben rechts; `Laufende Vorgaenge` zeigt links Karten/Tabelle und rechts eine Vorgangs-Vorschau mit Detail-Link. | Split-View ist aktuell lokal in den beiden Seiten umgesetzt; falls weitere Listen folgen, sollte das Muster als kleines Layout-Primitive extrahiert werden. |
| 2026-05-04 | DOC-TODO-CLEANUP | done | `FRONTEND_TODO.md`, `CODEX_SYNC.md` | Abgeschlossene Frontend-TODOs aus der aktiven Liste entfernt und die sichtbare Roadmap auf verbleibende offene Blocks reduziert. | Das Cleanup betrifft bewusst nur `FRONTEND_TODO.md`; `TODO.md` hatte keine aktiven `done`-Eintraege. |
| 2026-05-04 | FE-31 | done | `web/src/pages/PersonWorkflowHistoryPage.tsx`, `web/src/hooks/usePersonWorkflowAggregates.ts`, `web/tests/PersonWorkflowHistoryPage.test.tsx`, `FRONTEND_TODO.md`, `MEMORY.md`, `web/README.md`, `CODEX_SYNC.md` | Personenakte zur 360°-Arbeitsflaeche ausgebaut: Statuschip-Streifen, Metric-Strip (aktive Vorgaenge / offene Aufgaben / pending+failed Notifications) und Tab-Strip (`Übersicht` / `Offene Aufgaben` / `Benachrichtigungen` / `Vorgänge`). Aggregation per neuem Hook `usePersonWorkflowAggregates` mit `useQueries` über bestehenden `WorkflowDetail`-Endpoint; Backend-Vertrag unveraendert. | Offene Aufgaben werden clientseitig nur ueber aktive (nicht-`completed`/nicht-archivierte) Vorgaenge aggregiert; bei sehr vielen aktiven Vorgaengen pro Person waeren parallele Detail-Fetches der naechste Performance-Hebel. |

_Keine offenen Sync-Eintraege. Done-Eintraege bis 2026-05-03 entfernt — siehe `git log` fuer Detail-Historie._
