# FRONTEND TODO

---

## 1. Zielbild

Das Frontend soll wirken wie professionelle interne Enterprise-Software: vertrauenswürdig, klar strukturiert, in wenigen Sekunden bedienbar ohne Handbuch. Es richtet sich an drei Nutzergruppen mit sehr unterschiedlichen Aufgaben (HR, Abteilungsleitung, Fachbereich) und muss für jede Gruppe eine klare, reibungslose Bedienung bieten.

**Konkrete Zielmerkmale:**
- Navigation sofort verständlich durch Icons + Label
- Transientes Feedback (Speichern, Fehler) immer sichtbar, unabhängig von Scroll-Position
- Keine technischen Interna in user-facing Views
- Konsistente Zustände: Laden, Leer, Fehler, Erfolg — immer klar erkennbar
- Jede Seite hat einen klaren primären Handlungspfad
- Kein unnötiges Rauschen durch Systeminformationen, die Nutzer nicht brauchen

---

## 2. Arbeitsregeln für KI-Umsetzung

Diese Regeln gelten für **alle** Implementierungen in diesem Projekt:

1. **Bestehende Patterns lesen, bevor du implementierst.** Neue Patterns nur einführen, wenn ein bestehender Task das explizit vorsieht.
2. **Keine spontanen Refactors.** Wenn beim Implementieren ein Fehler oder eine Verbesserung auffällt, die nicht im Task steht: notieren, aber nicht umsetzen.
3. **Keine riesigen Scopes.** Ein Task = eine klar abgegrenzte Änderung. Lieber enger als breiter.
4. **CSS-Klassen nicht umbenennen ohne Auftrag.** Die globalen Klassen (`.panel`, `.btn`, `.field` etc.) sind vielfach verwendet. Umbenennen ohne vollständige Suche bricht die UI.
5. **Bestehende Komponenten erweitern, nicht duplizieren.** Vor jeder neuen Komponente prüfen, ob eine bestehende erweiterbar ist.
6. **Keine Business-Logik ins Frontend.** Validierungsregeln, Prozesslogik und Rollenregeln bleiben im Backend. Frontend nur für Darstellung und UI-State.
7. **Kein neues State Management einführen.** React Query für Server State, React useState/useReducer für UI State — so bleibt es.
8. **Betroffene Dateien benennen, bevor du änderst.**
9. **Verifizierung am Ende:** Build läuft, keine TypeScript-Fehler, Änderung ist visuell nachvollziehbar.
10. **Sprache der UI ist Deutsch.** Neue Labels, Titel, Fehlermeldungen auf Deutsch schreiben.

---

## 3. Prioritäten

- **P0** = Kritisch / blockiert Enterprise-Qualitätseindruck / technische Schuld mit sofortigem UX-Schaden
- **P1** = Hoher Nutzen / starke UX- oder UI-Verbesserung / wichtige Konsistenz
- **P2** = Qualitätsverbesserung / Feinschliff / technische Konsistenz
- **P3** = Optional / Langfristperspektive

---

## 4. Task-Liste

---

### [erledigt] [P0] Icons in die Sidebar-Navigation einführen

**Ziel**
Jeder Navigations-Link in der Sidebar erhält ein passendes SVG-Icon links vom Label. Desktop-Sidebar und Mobile Drawer erhalten dieselben Icons.

**Warum**
Die Sidebar hat ausschließlich Textlinks ohne jede visuelle Differenzierung. Das ist der auffälligste Qualitätsunterschied zu echter Enterprise-Software (Jira, ServiceNow, SAP Fiori, Notion, Linear — alle haben Icons). Text-only wirkt wie eine Entwickler-Debug-Seite. Icons reduzieren außerdem die kognitive Last: Nutzer erfassen die Navigation schneller, ohne jeden Text zu lesen.

**Betroffene Bereiche**
- `web/src/components/layout/AppLayout.tsx` — NavLink-Rendering für Desktop und Mobile Drawer
- `web/src/navigation/useRoleAwareNavigation.ts` — NavItem-Typ erweitern um `icon`-Feld
- `web/src/styles/base.css` — `.sidebar-link`-Klasse für Icon + Label Alignment

**Umsetzungshinweise**
- Den `NavItem`-Typ in `useRoleAwareNavigation.ts` um ein `icon`-Feld erweitern (z. B. als `ReactNode` oder als Icon-Key-String)
- Einfachste Lösung: Inline-SVG-Icons als ReactNode direkt im NavItem. Keine externe Icon-Library nötig.
- Empfohlene Icons (Heroicons-Stil, 20x20 stroke):
  - Dashboard / Übersicht: Home-Icon
  - Neuer Vorgang / Änderung starten: Plus-Kreis-Icon
  - Laufende Vorgänge: List-Bullet-Icon
  - Vorgänge suchen: Lupe-Icon
  - Anforderungen Abteilungsleitung: Check-Badge-Icon
  - Meine Aufgaben: Clipboard-List-Icon
  - Administration: Cog-6-Tooth-Icon
- Icons im `.sidebar-link` links vom Label platzieren: `display: flex; align-items: center; gap: 0.6rem;`
- Icon-Größe: 18px × 18px, `flex-shrink: 0`
- Active-State: Icon erbt die Brand-Farbe des aktiven Links automatisch über `color`-Inheritance, wenn Icons als `currentColor` definiert sind
- Desktop-Sidebar und Mobile Drawer nutzen denselben `headerNavItems`-Array — die Änderung wirkt auf beide automatisch

**Definition of Done**
- Alle Navigations-Links haben ein Icon links vom Label
- Icons sind konsistent in Größe und Stil
- Active State: Icon ist visuell anders als inactive (Farbe reicht)
- Mobile Drawer zeigt dieselben Icons
- Kein Desktop-Regressionsbruch
- TypeScript-Fehlerfreiheit

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
mittel

**Risiken / Hinweise**
- Nicht versehentlich den Icon-Stil des Sidebar-Logos (`sidebar-logo`) verändern
- Darauf achten, dass `aria-hidden="true"` auf den Icon-SVGs gesetzt wird — der Linktext bleibt der Screen-Reader-Text
- Mobile Drawer und Desktop Sidebar rendern die Links aus demselben Array — einmal ändern reicht, kein Duplikat nötig

---

### [P0] Toast-Notification-System für transientes Feedback einführen

**Ziel**
Ein globales Toast/Snackbar-System erstellen, das transiente Feedback-Meldungen (Erfolg, Fehler nach Mutations) am Bildschirmrand fixiert anzeigt, unabhängig von der Scroll-Position der Seite.

**Warum**
Aktuell erscheint Feedback nach Aktionen (Task-Status gespeichert, Kommentar gespeichert, Anforderungen gespeichert) als `panel-note` innerhalb des Seitencontents. Auf der WorkflowDetailPage, MyTasksPage und SupervisorStepPage kann das Feedback komplett außerhalb des Viewports sein, wenn der Nutzer zu einem tiefer liegenden Element scrollt, eine Aktion ausführt und dann auf Rückmeldung wartet. Das ist ein echter UX-Bruch.

**Betroffene Bereiche**
- `web/src/components/feedback/` — neue Datei `ToastProvider.tsx` erstellen
- `web/src/main.tsx` — ToastProvider in die Context-Provider-Chain einbinden
- `web/src/pages/WorkflowDetailPage.tsx` — taskNotice, taskError, requirementsSaveNotice, requirementsSaveError durch Toast-Calls ersetzen
- `web/src/pages/MyTasksPage.tsx` — notice, actionError durch Toast-Calls ersetzen
- `web/src/pages/SupervisorStepPage.tsx` — falls dort ähnliches Feedback existiert

**Umsetzungshinweise**
- `ToastProvider.tsx`: React Context + useState für eine Liste aktiver Toasts
- Toast-Item: `{ id: string; message: string; type: "success" | "error" | "info"; }`
- Toasts auto-dismiss nach 4 Sekunden (Erfolg) / 6 Sekunden (Fehler)
- Manuelles Schließen per X-Button
- Positionierung: `position: fixed; bottom: 1.5rem; right: 1.5rem; z-index: 500;`
- Maximal 3 Toasts gleichzeitig sichtbar (älteste zuerst raus)
- `useToast()` Hook exportieren: `const { showSuccess, showError } = useToast()`
- CSS in `web/src/styles/utilities.css` hinzufügen (`.toast-container`, `.toast`, `.toast-success`, `.toast-error`)
- Transition: `opacity` + `transform: translateY` für sanftes Ein-/Ausblenden
- Für Screen Reader: `role="status"` und `aria-live="polite"` auf dem Container
- Die bisherigen `taskNotice`/`taskError` States und deren Render-Logik aus den Pages entfernen, sobald Toast verwendet wird

**Definition of Done**
- Nach Mutation (Erfolg oder Fehler) erscheint ein Toast am unteren rechten Bildschirmrand
- Toast ist auch bei gescrollter Seite sichtbar
- Toast schließt sich nach 4-6 Sekunden automatisch
- Manuelles Schließen funktioniert
- Keine inline `panel-note`-Feedback-Reste mehr in WorkflowDetailPage und MyTasksPage für transiente Aktionsmeldungen
- TypeScript-Fehlerfreiheit

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
mittel

**Risiken / Hinweise**
- Nicht alle `panel-note`-Elemente entfernen — manche sind permanente Hinweise (z. B. "Es werden nur Aufgaben angezeigt, die Ihrer fachlichen Zuständigkeit zugeordnet sind"), keine Feedback-Meldungen
- Den `z-index` nicht zu hoch setzen — muss unter Confirmation-Dialog-Backdrop bleiben (ConfirmationDialogProvider)
- Nur transiente Mutations-Meldungen migrieren, keine statischen Hinweistexte

---

### [P0] Duplizierte Task-Interaction-Logik in Hook extrahieren + MyTasksPage auf Mutations umstellen

**Ziel**
Die fast identische Task-Status- und Kommentar-Logik aus `WorkflowDetailPage` und `MyTasksPage` in einen gemeinsamen Hook `useTaskInteraction` extrahieren. Gleichzeitig `MyTasksPage` auf React Query Mutations umstellen (statt direkter API-Calls + manuellem refetch).

**Warum**
Folgende State-Variablen und Handler existieren fast 1:1 in beiden Pages:
- `savingTaskIds`, `commentDrafts`, `savingCommentTaskIds`, `commentFeedbackTaskId`, `commentFeedbackMessage`
- `handleStatusChange`, `handleCommentDraftChange`, `handleTaskCommentSubmit`

Dazu nutzt `MyTasksPage` direkte API-Funktionen (`updateTaskStatusApi`, `addTaskCommentApi`) und dann manuelles `myTasksQuery.refetch()`, während `WorkflowDetailPage` React Query Mutations nutzt. Dieselbe Operation, zwei völlig verschiedene Patterns. Das ist technische Schuld, die bei der nächsten Änderung an Task-Logik teuer wird.

**Betroffene Bereiche**
- Neue Datei: `web/src/hooks/useTaskInteraction.ts`
- `web/src/pages/WorkflowDetailPage.tsx` — Hook einbinden, eigene Logik entfernen
- `web/src/pages/MyTasksPage.tsx` — Hook einbinden, direkte API-Calls + manuelles refetch ersetzen
- `web/src/services/mutations/workflowMutations.ts` — prüfen ob MyTasksPage-kompatible Mutations bereits existieren oder hinzugefügt werden müssen

**Umsetzungshinweise**
- `useTaskInteraction(queryKeyToInvalidate?: QueryKey)` erstellt:
  - Status: `savingTaskIds`, `commentDrafts`, `savingCommentTaskIds`
  - Handler: `handleStatusChange(taskId, status, currentStatus)`, `handleCommentDraftChange(taskId, value)`, `handleTaskCommentSubmit(taskId)`
  - Toast-Feedback direkt über `useToast()` (setzt P0 Toast-Task voraus — falls Toast noch nicht existiert, temporär als Rückgabewert `feedbackMessage` mitgeben)
- WorkflowDetailPage und MyTasksPage den Hook einbinden und die duplizierten State-Blöcke ersetzen
- MyTasksPage: Statt `updateTaskStatusApi(taskId, nextStatus)` + `myTasksQuery.refetch()` → Mutation aus `workflowMutations.ts` verwenden + React Query cache invalidation
- Der Hook sollte keinen spezifischen Query-Key kennen müssen — die Invalidierungsstrategie per Parameter oder Callback übergeben

**Definition of Done**
- `useTaskInteraction`-Hook existiert in `web/src/hooks/`
- WorkflowDetailPage und MyTasksPage nutzen den Hook
- MyTasksPage nutzt React Query Mutations (kein direkter API-Call mehr)
- Kein manuelles `.refetch()` nach Mutations in MyTasksPage
- TypeScript-Fehlerfreiheit, Build grün

**Empfohlene KI**
Codex

**Reasoning Effort**
high

**Task-Größe**
mittel

**Risiken / Hinweise**
- Der Hook muss mit beiden Pages kompatibel sein: WorkflowDetailPage hat eine einzelne UID, MyTasksPage hat keine feste UID (Tasks aus verschiedenen Workflows)
- Nicht versehentlich die `commentFeedbackTaskId`-Logik entfernen — diese steuert, welchem Task das Feedback zugeordnet wird. Erst wenn Toast vorhanden ist, ist das obsolet.
- Schrittweise: erst Hook erstellen + in einer Page testen, dann die zweite

---

### [P0] AppLayout: Duplikation zwischen Desktop-Sidebar und Mobile Drawer entfernen

**Ziel**
Den duplizierten JSX-Block (User-Section + Logout-Button in `sidebar-footer`) aus `AppLayout.tsx` in eine interne Komponente oder ein Fragment extrahieren.

**Warum**
In `AppLayout.tsx` ist der Sidebar-Footer (Benutzeranzeige + Abmelden-Button, ca. 25 Zeilen JSX) exakt zweimal vorhanden: einmal in der `<aside className="sidebar">` und einmal in der `<aside className="mobile-nav-drawer">`. Jede Änderung an der Benutzeranzeige muss manuell doppelt gemacht werden — klassische Copy-Paste-Schuld.

**Betroffene Bereiche**
- `web/src/components/layout/AppLayout.tsx`

**Umsetzungshinweise**
- Interne `SidebarFooter`-Komponente (nicht exportiert) direkt in der Datei definieren
- Oder: Inline-Fragment mit JSX-Variable `const sidebarFooter = (...)`
- Sowohl Desktop-Sidebar als auch Mobile Drawer nutzen dann dieselbe Instanz/Komponente
- Keine Props ändern, keine externe Komponente erstellen, keine Umstrukturierung des Layouts

**Definition of Done**
- Der Sidebar-Footer-JSX existiert nur noch einmal im Datei-Code
- Desktop-Sidebar und Mobile Drawer zeigen weiterhin denselben Footer
- Kein Regressionsbruch

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Sehr einfacher Task, kaum Risiken
- Darauf achten, dass beide `ref`-Attribute (mobileMenuButtonRef, mobileCloseButtonRef) im richtigen Element bleiben

---

### [P0] Technische Daten aus user-facing Views entfernen

**Ziel**
Zwei konkrete Stellen, an denen rohe System-Daten an Endnutzer exponiert werden, bereinigen.

**Warum**
Abteilungsmitarbeiter auf MyTasksPage sehen: `"Workflow-ID: abc-123 | Abhängigkeiten: 0"` als Fußnote unter jedem Task-Item. Das ist Entwickler-Debug-Output, kein nutzbares UI-Element. Gleiches Problem in Wizard-Step 2: `"5 Anforderungen geladen, 2 Vorbelegungen für Onboarding vorbereitet."` — diese Zeile sagt einem HR-Mitarbeiter nichts Verwertbares.

**Betroffene Bereiche**
- `web/src/pages/MyTasksPage.tsx` — Zeile mit `Workflow-ID: {workflowUid} | Abhängigkeiten: {row.task.dependencies.length}` entfernen
- `web/src/pages/CreateWorkflowPage.tsx` — Die `panel-muted`-Sektion "Prozesskonfiguration" mit dem Anforderungs-Count in Step 2 entfernen oder durch eine nutzerfreundliche Aussage ersetzen

**Umsetzungshinweise**
- **MyTasksPage**: Den `<p className="panel-note">Workflow-ID: ... | Abhängigkeiten: ...</p>`-Block vollständig entfernen. Der Workflow-Link (als separate P1-Aufgabe) ersetzt den nutzbaren Teil davon.
- **CreateWorkflowPage Wizard Step 2**: Die ganze `<section className="panel panel-muted">` mit h3 "Prozesskonfiguration" entfernen. Diese Sektion zeigt Systemmetadaten, die für HR/Manager nicht relevant sind. Der Wizard führt die Person — sie muss nicht wissen, wie viele Anforderungen intern geladen wurden.
- **Optional**: Falls ein anderer nutzbarer Hinweis in Step 2 sinnvoll ist (z. B. "Für diesen Prozesstyp sind bereits Vorbelegungen für Ihre Rolle hinterlegt"), kann ein einfacher Satz ohne System-Counts ersetzt werden.

**Definition of Done**
- "Workflow-ID / Abhängigkeiten"-Zeile nicht mehr in MyTasksPage sichtbar
- "Prozesskonfiguration"-Sektion in Wizard Step 2 nicht mehr vorhanden
- Kein TypeScript-Fehler, kein Build-Break

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Nur diese zwei spezifischen Stellen entfernen. Nicht pauschal alle `panel-note`-Elemente prüfen oder entfernen.

---

### [P1] "Freigabeschritt fehlt"-Rausch-Panel auf WorkflowDetailPage entfernen

**Ziel**
Den informationslosen Panel-Block `"Dieser Prozesstyp hat keinen separaten Schritt für die Abteilungsleitung"` auf der WorkflowDetailPage entfernen.

**Warum**
Dieser Panel erscheint auf der WorkflowDetailPage für alle Prozesstypen ohne Supervisor-Step. Er erklärt dem Nutzer das Fehlen eines Features, das er nie angefordert hat und dessen Fehlen für ihn keine Relevanz hat. Das ist UI-Rauschen, das die Seite länger macht, ohne Information zu liefern. Enterprise-Software zeigt keine leeren Zustände von Features, die nicht existieren.

**Betroffene Bereiche**
- `web/src/pages/WorkflowDetailPage.tsx` — den bedingten Render-Block mit `"Freigabeschritt"` Panel entfernen

**Umsetzungshinweise**
- Den Block `{!hasSupervisorStep(workflow) && workflow.workflowStatus !== "waiting_for_supervisor" ? (<section className="panel panel-muted">...</section>) : null}` vollständig entfernen
- Kein Ersatz nötig — der Panel hat keinen informativen Wert für den Nutzer

**Definition of Done**
- Der "Freigabeschritt"-Panel erscheint nicht mehr auf Prozesstypen ohne Supervisor-Step
- Wenn ein Prozesstyp einen Supervisor-Step hat, wird dieser weiterhin korrekt gerendert
- Kein TypeScript-Fehler

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Nur den leeren Zustand entfernen. Den tatsächlichen `WorkflowRequirementsPanel` für Prozesstypen MIT Supervisor-Step nicht anfassen.

---

### [P1] Direkter Workflow-Link aus Tasks in MyTasksPage

**Ziel**
Jede Aufgabe in der MyTasksPage erhält einen direkten Link zur Vorgangs-Detailseite des zugehörigen Workflows.

**Warum**
Ein Fachbereichsmitarbeiter sieht seine Aufgaben in der MyTasksPage, möchte aber oft den Gesamtkontext eines Vorgangs sehen (welche anderen Aufgaben laufen, was ist der aktuelle Stand, wer ist zuständig). Aktuell gibt es keinen direkten Weg von der Aufgabe zur Vorgangsseite — der Nutzer müsste die Workflow-UID kopieren, zur Suche navigieren, eintippen. Das ist inakzeptable Reibung.

**Betroffene Bereiche**
- `web/src/pages/MyTasksPage.tsx` — Task-Card-Render-Block
- `web/src/styles/workflow.css` oder `utilities.css` — ggf. minimales Styling für den Link

**Umsetzungshinweise**
- In der Task-Card einen Link `<Link to={`/workflows/${workflowUid}`}>Zum Vorgang</Link>` hinzufügen
- Platzierung: In der `task-meta`-Sektion als zusätzlicher `<div>` mit `<dt>Vorgang</dt><dd><Link...></dd>`, oder als separater sekundärer Button unterhalb der Meta-Informationen
- Label: "Zum Vorgang" oder "Vorgangsdetails öffnen"
- Styling: `.btn .btn-secondary` (klein) oder als reiner Text-Link mit Unterline
- Die `workflowUid` ist bereits als `row.workflow.workflowUid` verfügbar — kein API-Aufruf nötig

**Definition of Done**
- Jede Aufgabe in MyTasksPage hat einen klickbaren Link zur Vorgangs-Detailseite
- Der Link navigiert korrekt zu `/workflows/{workflowUid}`
- Kein visueller Bruch mit dem restlichen Task-Card-Layout

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Auf die Platzierung achten — der Task-Card ist bereits informationsdicht. Der Link sollte nicht prominent sein, sondern als sekundäre Aktion wahrnehmbar.

---

### [P1] "Zum Vorgang"-Link nach erfolgreicher Workflow-Erstellung

**Ziel**
Nach erfolgreicher Erstellung eines Workflows in der CreateWorkflowPage erscheint neben "Zur Übersicht" auch ein Link "Zum neuen Vorgang", der direkt zur Detailseite des erstellten Workflows führt.

**Warum**
Nach dem Anlegen eines Vorgangs möchte der HR-Mitarbeiter in 90% der Fälle sofort den neuen Vorgang prüfen — nicht erst zur Übersichtsliste navigieren und ihn dort suchen. `createdWorkflowUid` wird bereits vom Hook zurückgegeben, aber nicht für Navigation genutzt.

**Betroffene Bereiche**
- `web/src/pages/CreateWorkflowPage.tsx` — der Submit-Success-Block in Step 3 (Review)

**Umsetzungshinweise**
- Im Success-Panel neben dem bestehenden `<Link to="/workflows">Zur Übersicht</Link>` einen weiteren Link hinzufügen:
  `<Link to={`/workflows/${createdWorkflowUid}`}>Zum neuen Vorgang</Link>`
- Der neue Link erhält die primäre Button-Klasse (`.btn.btn-primary`), "Zur Übersicht" erhält die sekundäre (`.btn.btn-secondary`)
- Der Link soll nur erscheinen wenn `createdWorkflowUid` vorhanden ist (ist bereits der Fall durch bestehende Bedingung)

**Definition of Done**
- Nach erfolgreicher Erstellung erscheint "Zum neuen Vorgang"-Button
- Link navigiert korrekt zur Workflow-Detailseite
- "Zur Übersicht"-Button bleibt als sekundäre Option

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Minimale Änderung. Keine Logik-Änderungen nötig.

---

### [P1] Breadcrumb-Navigation auf WorkflowDetailPage und PersonWorkflowHistoryPage

**Ziel**
WorkflowDetailPage und PersonWorkflowHistoryPage erhalten eine einfache Breadcrumb-Navigation, die den Rückweg zur Liste ermöglicht.

**Warum**
Detail-Seiten haben keine Back-Navigation außer dem Browser-Back-Button. Enterprise-Software nutzt Breadcrumbs als primäre Navigationshilfe auf Detail-Seiten. Ohne Breadcrumbs muss der Nutzer in der Sidebar zur Listenansicht klicken oder den Browser-Zurück-Button nutzen — beides ist Umweg-Navigation.

**Betroffene Bereiche**
- `web/src/components/layout/PageHeader.tsx` — optional: Breadcrumb-Prop hinzufügen
- `web/src/pages/WorkflowDetailPage.tsx`
- `web/src/pages/PersonWorkflowHistoryPage.tsx`
- `web/src/styles/base.css` oder `utilities.css` — Breadcrumb-CSS

**Umsetzungshinweise**
- Einfaches Breadcrumb-Pattern: `Laufende Vorgänge / Vorgangsdetails`
- Der erste Teil ist ein `<Link to="/workflows">Laufende Vorgänge</Link>`, der zweite ist plain text
- Platzierung: Direkt oberhalb des `<PageHeader>`, als eigenes `<nav aria-label="Breadcrumb">` Element
- CSS: `.breadcrumb { display: flex; align-items: center; gap: 0.4rem; font-size: 0.82rem; color: var(--text-secondary); margin-bottom: 0.5rem; }` — Trenner als `›` oder `/` zwischen den Items
- Keine komplexe Breadcrumb-Komponente nötig — inline JSX in der Page reicht
- WorkflowDetailPage: `Laufende Vorgänge / Vorgangsdetails`
- PersonWorkflowHistoryPage: `Vorgänge suchen / Personenverlauf` (je nach Einstiegskontext)

**Definition of Done**
- WorkflowDetailPage zeigt Breadcrumb mit Link zurück zur Workflow-Liste
- PersonWorkflowHistoryPage zeigt Breadcrumb
- Links funktionieren korrekt
- Breadcrumb ist visuell konsistent (kleine Schrift, gedämpfte Farbe)

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Kein komplexes Routing-Tracking nötig. Einfache statische Links reichen für diese Pages.
- Nicht für alle Pages gleichzeitig einführen — die zwei genannten Pages sind die dringendsten.

---

### [P1] Semantische Feedback-Panel-Klassen konsistent einführen

**Ziel**
Die Klasse `panel-muted` wird derzeit für Informationshinweise, Validierungsfehler, Review-Zusammenfassungen und Ruhezustände gleichermaßen verwendet. Klare semantische Varianten (`panel-warning`, `panel-error`) einführen und die bestehenden Stellen konsistent zuordnen.

**Warum**
`panel-muted` hat keine Bedeutung mehr — es ist ein catch-all für "nicht primär". Wenn Validierungsfehler, neutrale Hinweise und Warnungen visuell gleich aussehen, verliert der Nutzer die Orientierung über die Wichtigkeit einer Meldung. `panel-success` existiert bereits korrekt. Es fehlen `panel-warning` (Achtung, aber kein Fehler) und `panel-error` (Fehler, Handlung nötig).

**Betroffene Bereiche**
- `web/src/styles/base.css` oder `workflow.css` — CSS-Definitionen für `.panel-warning`, `.panel-error`
- `web/src/pages/CreateWorkflowPage.tsx` — Validierungsfehler-Panels
- `web/src/pages/WorkflowDetailPage.tsx` — Fehler-Panels
- `web/src/pages/MyTasksPage.tsx` — Fehler-Panels
- Ggf. weitere Pages mit `panel-muted` für fehlerhafte Zustände

**Umsetzungshinweise**
- CSS-Definitionen:
  - `.panel-warning { background: #fffbeb; border-color: #d97706; }` — für Warnungen (z.B. fehlende Kontextdaten, unvollständige Zielperson)
  - `.panel-error { background: #fef2f2; border-color: #dc2626; }` — für Fehler (Validation-Errors, Laden fehlgeschlagen im inline-Kontext)
  - `panel-muted` bleibt für neutrale/sekundäre Informationsinhalte
- In CreateWorkflowPage: Validierungslisten (contextStepIssues, processStepIssues) und Fehlermeldungen `panel-warning` statt `panel-muted`
- `submitError`-Block in Review-Step: `panel-error` statt `panel-muted`
- Erfolgsmeldungen (bereits `panel-success`): bleiben
- Neutrale Review-Zusammenfassungen (Übersicht in Step 3): bleiben `panel-muted`

**Definition of Done**
- Validierungsfehler und submit errors werden visuell anders dargestellt als neutrale Informationen
- CSS-Klassen `.panel-warning` und `.panel-error` sind definiert
- Keine bestehenden `panel-success`-Instanzen verändert
- Build grün, kein Regressionsbruch

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
mittel

**Risiken / Hinweise**
- Nicht pauschal alle `panel-muted`-Stellen ersetzen — nur die, die semantisch Fehler oder Warnungen sind
- Zuerst CSS definieren, dann gezielt ersetzen. Nicht von hinten anfangen.
- Die Farbwerte an die bestehenden Design-Tokens anlehnen (`--warning`, `--danger`, `--success`)

---

### [P1] Dashboard UI: Entwickler-Labels entfernen, "Schnellzugriff"-Bereich verbessern

**Ziel**
Drei spezifische Stellen im Dashboard bereinigen, die entwicklerfacing sind oder unnötig Platz belegen.

**Warum**
- Der Abschnitt "Sekundäre Navigation" (Überschrift eines Panels ganz unten) ist Entwicklerjargon — Nutzer verstehen das nicht als UI-Konzept
- "Rolle: HR, Admin" wird als `panel-note` im Support-Panel angezeigt — diese Information ist bereits in der Sidebar (Benutzeranzeige mit Rolle). Dopplung ohne Nutzen.
- Der "Übersicht aktualisieren"-Button ist schwer zu finden, da er im Side-Panel unter dem Prozesstyp-Filter steht

**Betroffene Bereiche**
- `web/src/components/dashboard/DashboardOverview.tsx`

**Umsetzungshinweise**
- "Sekundäre Navigation" Panel: Den Abschnittstitel von "Sekundäre Navigation" in einen nutzerorientierten Begriff umbenennen, z.B. "Weitere Bereiche" oder ganz ohne Titel als implizite Linkliste
- "Rolle: X"-`panel-note` im Support-Panel entfernen (die Rolle ist bereits in der Sidebar sichtbar)
- "Übersicht aktualisieren"-Button: Behalten, aber Positionierung prüfen — ggf. als sekundären Link direkt im primary Panel oder als Icon-Button im Panel-Head

**Definition of Done**
- Kein "Sekundäre Navigation"-Entwickler-Label mehr im Dashboard
- Kein doppelter Rollen-Hinweis im Dashboard
- Aktualisieren-Funktion weiterhin zugänglich

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Minimale Änderungen. Keine Logik anfassen.
- Die `secondaryDashboardActions`-Render-Logik bleibt, nur der Label ändert sich.

---

### [P2] Loading Skeleton States für Workflow-Liste und Task-Liste

**Ziel**
Auf WorkflowListPage und MyTasksPage werden während des Ladens Skeleton-Platzhalter angezeigt, die die ungefähre Struktur der Karten/Listen simulieren, statt nur Ladetext zu zeigen.

**Warum**
Text-basiertes Laden ("Vorgänge werden geladen...") fühlt sich statisch an. Skeleton-States reduzieren die wahrgenommene Ladezeit, weil der Nutzer sieht, dass etwas passiert und sich die UI aufbaut. Das ist ein signifikanter Qualitätsunterschied zwischen Prototyp und Enterprise-Software.

**Betroffene Bereiche**
- Neue Datei: `web/src/components/feedback/SkeletonCard.tsx`
- `web/src/pages/WorkflowListPage.tsx` — LoadingState ersetzen
- `web/src/pages/MyTasksPage.tsx` — LoadingState ersetzen
- `web/src/styles/utilities.css` — Skeleton-Animation CSS

**Umsetzungshinweise**
- CSS-Animation für Skeleton: `@keyframes skeleton-shimmer { from { background-position: -200% 0; } to { background-position: 200% 0; } }` mit einem linearen Gradient (grau → heller grau → grau)
- `.skeleton-line { height: 0.85rem; border-radius: 4px; background: linear-gradient(90deg, #e5e7eb 25%, #f3f4f6 50%, #e5e7eb 75%); background-size: 200% 100%; animation: skeleton-shimmer 1.4s infinite; }`
- `SkeletonCard`-Komponente: Simuliert eine Karte mit 2-3 Skeleton-Lines in verschiedenen Breiten (z.B. 60%, 40%, 80%)
- WorkflowListPage: Im Lade-State statt `<LoadingState>` 6-8 `<SkeletonCard>`-Instanzen in einem `.workflow-grid` rendern
- MyTasksPage: Im Lade-State 4-5 `<SkeletonCard>`-Instanzen in einem Stack rendern
- Die `LoadingState`-Komponente für andere Stellen (z.B. Dashboard) nicht ersetzen — dort ist der Text-Loader akzeptabel

**Definition of Done**
- WorkflowListPage zeigt Skeleton-Cards beim Laden
- MyTasksPage zeigt Skeleton-Cards beim Laden
- Skeleton-Shimmer-Animation läuft flüssig
- Kein visueller Bruch nach dem Laden

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
mittel

**Risiken / Hinweise**
- Skeletons sollen ungefähr die Form der echten Karten simulieren, müssen aber nicht pixelgenau sein
- Darauf achten, dass `aria-busy="true"` am Container-Element gesetzt wird während das Laden läuft
- Nicht für alle Seiten gleichzeitig einführen — zuerst WorkflowListPage, dann MyTasksPage

---

### [P2] Konsistente Mutations-Verwendung in MyTasksPage (React Query)

**Ziel**
MyTasksPage nutzt für Statusänderungen und Kommentare direkte Service-Funktionen + manuelles `refetch()`. Das soll auf React Query Mutations umgestellt werden, konsistent mit WorkflowDetailPage.

**Warum**
Direkte API-Calls + manuelles `refetch()` umgehen React Querys Cache-Logik. Wenn die Cache-Invalidierungsstrategie sich ändert (z. B. optimistic updates, shared query keys), muss MyTasksPage separat angepasst werden. Inkonsistente Patterns kosten Wartungsaufwand.

**Betroffene Bereiche**
- `web/src/services/mutations/workflowMutations.ts` — prüfen ob `useUpdateTaskStatus` und `useAddTaskComment` für MyTasksPage nutzbar sind (sie sind derzeit an eine Workflow-UID gebunden)
- `web/src/pages/MyTasksPage.tsx`

**Umsetzungshinweise**
- Falls `useUpdateTaskStatus(uid)` einen fixen UID braucht, der in MyTasksPage nicht bekannt ist: Den Mutation-Hook auf eine UID-agnostische Variante erweitern oder eine neue Mutation ohne Workflow-UID-Binding erstellen
- MyTasksPage: `updateTaskStatusApi` und `addTaskCommentApi` direkt-Aufrufe durch Mutations ersetzen
- Manuelles `myTasksQuery.refetch()` nach Mutations durch `queryClient.invalidateQueries` ersetzen
- Dieser Task baut auf dem `useTaskInteraction`-Hook-Task auf — wenn der zuerst fertig ist, löst sich dieser Task teilweise von selbst

**Definition of Done**
- Keine direkten `Api`-Funktionsaufrufe mehr in MyTasksPage für Mutations
- React Query Mutation + Invalidation wird genutzt
- Verhalten aus Nutzerperspektive identisch

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
klein

**Risiken / Hinweise**
- Abhängig von P0-Task "useTaskInteraction". Wenn dieser Task zuerst erledigt wird, kann dieser Task übersprungen werden.
- Sorgfältig prüfen welche QueryKeys invalidiert werden sollen — falsches Invalidieren kann zu unnötigen Loads führen

---

### [P2] Page-Beschreibungen auf nutzerorientierte Sprache umschreiben

**Ziel**
Die Untertexte der `PageHeader`-Komponente auf allen Pages werden von technisch-beschreibenden Strings in echte Nutzer-Kontext-Sätze umgeschrieben.

**Warum**
Aktuell lesen sich viele Beschreibungen wie Datenbankfeld-Dokumentation:
- "Zentraler Überblick über Person, Prozesstyp, Prozessstand, Zuständigkeiten und offene Aufgaben." (WorkflowDetailPage)
- "Filtern und suchen Sie nach aktiven und abgeschlossenen Vorgängen." (WorkflowSearchPage)

Enterprise-Software schreibt Beschreibungen als Kontext für den Nutzer: Was wird er hier tun? Was hilft ihm das?

**Betroffene Bereiche**
- `web/src/pages/WorkflowDetailPage.tsx` — pageDescription
- `web/src/pages/WorkflowListPage.tsx` — pageDescription
- `web/src/pages/WorkflowSearchPage.tsx` — pageDescription
- `web/src/pages/MyTasksPage.tsx` — pageDescription
- `web/src/pages/AdminConfigPage.tsx` — falls Beschreibungen vorhanden
- `web/src/pages/PersonWorkflowHistoryPage.tsx` — pageDescription

**Umsetzungshinweise**
- Schreibregel: Beschreibung beantwortet "Was kann ich hier tun und warum hilft mir das?"
- Beispiele:
  - WorkflowDetailPage: "Prozessstand, Aufgaben und Zuständigkeiten eines einzelnen Vorgangs auf einen Blick."
  - MyTasksPage: "Alle Aufgaben, die Ihrer fachlichen Zuständigkeit zugeordnet sind – gefiltert nach Status und Bereich."
  - WorkflowListPage: "Aktive Vorgänge nach Status, Abteilung und Prozesstyp filtern und direkt öffnen."
- Kurz halten: max. 1 Satz, max. 80-90 Zeichen
- Keine technischen Begriffe wie "Prozessstand", "Zuständigkeiten", "Vorgangs-UID" in Beschreibungen

**Definition of Done**
- Alle genannten Pages haben nutzerorientierte Beschreibungen
- Kein Satz liest sich wie eine Datenbankdokumentation
- Kein TypeScript-Fehler

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Sehr einfacher Task, kein Risiko
- Keine Logik-Änderungen, nur String-Änderungen

---

### [P2] Filter-Toolbar Responsivität verbessern (MyTasksPage, WorkflowListPage)

**Ziel**
Die Filter-Toolbars auf MyTasksPage und WorkflowListPage brechen bei mittleren Bildschirmbreiten (900–1200px) schlecht um. Das Layout soll auf mittleren Breiten sinnvoll komprimiert werden, ohne alle Filter unsichtbar zu machen.

**Warum**
`.toolbar-row` ist ein einfacher Flex-Container. Bei 4-5 Filter-Elementen + Button auf 1000px Breite ist das Ergebnis entweder Overflow oder ungleichmäßiges Wrapping. Das ist ein konkretes Usability-Problem auf Laptop-Bildschirmen, die eine häufige Arbeitsumgebung für interne Tools sind.

**Betroffene Bereiche**
- `web/src/styles/utilities.css` oder `base.css` — `.toolbar-row`-Klasse und ggf. responsive Varianten
- `web/src/pages/MyTasksPage.tsx`
- `web/src/pages/WorkflowListPage.tsx`

**Umsetzungshinweise**
- `.toolbar-row` auf CSS Grid umstellen mit `grid-template-columns: repeat(auto-fill, minmax(160px, 1fr))` oder expliziten Column-Definitionen
- Der Such-Input soll breiter sein als die Dropdowns: `.grow`-Klasse nutzen oder explizit `grid-column: span 2` auf dem Such-Feld
- Auf kleinen Breiten (<600px) alle Felder in eine Spalte
- Kein JavaScript-Breakpoint-Handling nötig — rein CSS

**Definition of Done**
- Filter-Toolbar auf 1000px-Breite sieht sauber aus (kein unkontrolliertes Wrapping)
- Auf <600px alle Filter untereinander gestapelt
- Kein visueller Regressionsbruch auf Desktop

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Nicht beide Pages gleichzeitig verändern ohne zu verifizieren — erst eine, dann die andere
- `.toolbar-row` wird möglicherweise an anderen Stellen im Codebase genutzt — zuerst prüfen, welche anderen Komponenten diese Klasse verwenden (`grep -r "toolbar-row"`)

---

### [P2] WorkflowDetailPage: Requirement-Editor-State in Custom Hook auslagern

**Ziel**
Die Requirement-Editor-Logik in `WorkflowDetailPage.tsx` (ca. 80-100 Zeilen State + Handler für Requirement-Selektion) in einen eigenen Hook `useRequirementEditor` auslagern.

**Warum**
WorkflowDetailPage hat 15+ State-Variablen und über 10 Handler-Funktionen. Der Requirement-Editor-Anteil ist ein klar abgrenzbares Thema: `requirementSelections`, `requirementsSaveError`, `requirementsSaveNotice`, `isSavingRequirements`, `setRequirementBoolean`, `setRequirementText`, `setRequirementSelectedOption`, `toggleRequirementSelectedOption`, `handleRequirementSave`. Das kann ohne Funktionsverlust extrahiert werden.

**Betroffene Bereiche**
- Neue Datei: `web/src/hooks/useRequirementEditor.ts`
- `web/src/pages/WorkflowDetailPage.tsx`

**Umsetzungshinweise**
- Der Hook nimmt `workflow: WorkflowDetail | null` und `capabilities` (oder spezifisch: `canEditSupervisorRequirements: boolean`) als Parameter
- Gibt zurück: `requirementSelections`, `isSavingRequirements`, `canSaveSupervisorRequirements`, alle Handler + `handleRequirementSave`
- Den `updateSupervisorStepMutation`-Aufruf innerhalb des Hooks verwalten
- WorkflowDetailPage bindet den Hook ein und destructurt die Rückgabewerte

**Definition of Done**
- `useRequirementEditor.ts` existiert und enthält die extrahierte Logik
- WorkflowDetailPage ist merklich kürzer (ca. 80-100 Zeilen weniger)
- Requirement-Editor-Funktionalität bleibt identisch
- TypeScript-Fehlerfreiheit

**Empfohlene KI**
Codex

**Reasoning Effort**
medium

**Task-Größe**
mittel

**Risiken / Hinweise**
- Dieser Task setzt voraus, dass der `useTaskInteraction`-Hook (P0) bereits umgesetzt ist, damit beide Extractions konsistent sind
- Nicht die Mutations oder Query-Hooks innerhalb des neuen Hooks umbenennen oder refactoren — nur verschieben

---

### [P3] CSS-Scoping-Strategie evaluieren und Migrationsplan erstellen

**Ziel**
Die aktuelle Architektur aus globalen CSS-Klassen (`.panel`, `.btn`, `.field`, `.toolbar-row`, `.workflow-card`, ...) evaluieren und einen Migrationsplan für eine scoped CSS-Strategie erstellen.

**Warum**
Globale CSS-Klassen ohne Scoping skalieren schlecht. Jede Klasse wie `.panel` wirkt auf alle Stellen im Codebase gleichzeitig. Tailwind-Utilities sind bereits importiert — eine konsequentere Nutzung von Tailwind-Komponenten oder CSS Modules würde die Wartbarkeit verbessern. Das ist kein akutes Problem, wird aber bei weiterem Wachstum relevant.

**Betroffene Bereiche**
- `web/src/styles/` (alle CSS-Dateien)
- `web/src/components/` (alle Komponenten)

**Umsetzungshinweise**
- Zuerst: Bestandsaufnahme aller globalen Klassen und wo sie genutzt werden
- Optionen bewerten: Tailwind-Komponenten-Layer (`@layer components { .panel {...} }`), CSS Modules, oder Beibehaltung mit strikterem BEM-Naming
- Empfehlung: Tailwind `@layer components` für wiederverwendbare Komponenten-Klassen (`.panel`, `.btn`, `.field`) — damit bleiben sie nutzbar, werden aber als bewusstes Design-System-Element behandelt
- Migration in Wellen: zuerst neue Komponenten, dann nach und nach bestehende
- **Kein sofortiger Umbau**, sondern einen Entscheidungs-ADR erstellen

**Definition of Done**
- Entscheidung dokumentiert
- Klarer Migrationsplan vorhanden
- (Die eigentliche Migration ist ein separater, sehr großer Task)

**Empfohlene KI**
Claude

**Reasoning Effort**
high

**Task-Größe**
klein (nur Analyse und Plan)

**Risiken / Hinweise**
- Kein Code-Change in diesem Task. Nur Analyse und Entscheidung.
- Eine vorschnelle Migration des gesamten CSS würde Wochen dauern und ist riskant — der Plan muss inkrementell sein

---

### [P3] Skip-Link für Tastatur-Navigation

**Ziel**
Einen "Zum Hauptinhalt springen"-Link am Anfang des DOM einfügen, der für Tastaturnutzer sichtbar wird beim Fokus.

**Warum**
Ohne Skip-Link müssen Tastaturnutzer bei jedem Seitenwechsel die gesamte Sidebar-Navigation durchtabben, bevor sie zum Inhalt gelangen. Das ist eine Grundanforderung für Accessibility (WCAG 2.1 AA, Erfolgskriterium 2.4.1).

**Betroffene Bereiche**
- `web/src/components/layout/AppLayout.tsx`
- `web/src/styles/base.css`

**Umsetzungshinweise**
- `<a href="#main-content" className="skip-link">Zum Hauptinhalt springen</a>` als erstes Child in AppLayout, vor dem Mobile-Topbar
- Das `<main>`-Element (`.app-shell`) mit `id="main-content"` auszeichnen
- CSS: `.skip-link` ist standardmäßig `position: absolute; transform: translateY(-100%); opacity: 0;` und bei `:focus-visible` sichtbar und positioniert

**Definition of Done**
- Bei Tab-Navigation erscheint Skip-Link als erste fokussierbare Element
- Link führt korrekt zu `#main-content`
- Auf normalen Bildschirmen unsichtbar

**Empfohlene KI**
Codex

**Reasoning Effort**
low

**Task-Größe**
klein

**Risiken / Hinweise**
- Minimale Änderungen. Kein Risiko.
