# Code Review — kauth_workflow

**Reviewer**: Claude (Senior Software Architect Perspective)  
**Datum**: 2026-04-23  
**Scope**: Vollständige Backend- und Frontend-Analyse

---

## 1. Funktionsanalyse — Was kann das System wirklich?

### Vorhandene Features

| Feature | Zweck | Zielnutzer | Nutzen |
|---------|-------|------------|--------|
| Workflow-Erstellung | Standardisierte Onboarding/Offboarding-Prozesse | HR | Reproduzierbare Abläufe |
| Workflow-Antworten | Formulardaten pro Instanz | HR | Anforderungserfassung |
| Aufgabensystem | Task-Generierung, -Zuweisung, -Kommentierung | IT, Fachabteilung | Arbeitsverteilung |
| Supervisor-Schritt | Freigabeschritt mit Genehmigung | Manager | Kontrollpunkt |
| Abteilungsdurchlauf (Rotation) | Planung und Durchführung von Stellenwechseln | HR, IT, Abteilungen | Strukturierter Wechselprozess |
| Rotations-Aufgaben | Generierte Aufgaben pro Station/Template | IT, Fachabteilung | Automatische Aufgabenverteilung |
| Admin-Konfiguration | Rollen, Zuständigkeiten, Berechtigungen | Admin | Systemsteuerung |
| Workflow-Builder | Visuelle Definition von Workflows | Builder-Admins | Prozessgestaltung |
| Automatisierung | Job-Queue für automatische Aktionen | System | Reduzierung manueller Schritte |
| Verzeichnis-Sync | Entra/AD-Synchronisation | Admin | Identity-Integration |
| Mail-Vorlagen | Konfigurierbare E-Mail-Benachrichtigungen | Admin | Kommunikation |
| System-Log | Zentrales Event-Logging | Admin | Nachvollziehbarkeit |
| Personen-Verwaltung | Mitarbeiter-Datensätze, Lifecycle-Verlauf | HR | Personenstammdaten |

### Redundante / problematische Features

- **Doppelte Prozesslogik**: `process_types` (Legacy) und `workflow_definitions` (neu) existieren parallel — keine klare Abgrenzung, wann welches greift.
- **Doppelter Routing-Code**: `tasks/ref/{taskRef}` unterstützt `wf:*` und `rot:*` — gute Abstraktion, aber dahinter liegen zwei völlig getrennte Codepfade ohne gemeinsame Basis.
- **Simulation-Login**: `AUTH_MODE=dev-sim` nur für Entwicklung gedacht, aber kein Guard, der verhindert, dass es versehentlich in Prod aktiviert wird.
- ~~**`completed-onboardings`-Endpunkt**~~: ✓ erledigt (Schritt 7A, 2026-05-01). Endpoint und DTO entfernt; Frontend nutzt jetzt `/workflow-target-person-sources` und `/people/rotation-eligible`.

### Fehlende Kernfunktionen

- **Keine Stations-Überschneidungsprüfung**: Zwei Stationen desselben Plans können sich zeitlich überschneiden — kein Code- oder DB-Guard dagegen.
- **Kein Datenbereinigungsmechanismus**: Draft-Pläne, abgebrochene Stationen, stornierte Aufgaben akkumulieren unbegrenzt.
- **Kein DAG-Validierungscheck vor Publish**: Workflow-Definitionen können mit offenen Pfaden veröffentlicht werden.
- **Keine Benutzerlösch-Logik beim Sync**: Entra-gelöschte User werden nicht deaktiviert.

---

## 2. Nutzersicht — Ist das sinnvoll nutzbar?

### Klare Workflows

- **HR-Workflow-Erstellung**: Klar — Workflow anlegen, Antworten ausfüllen, Aufgaben abarbeiten.
- **Rotation planen**: Klar — Person wählen, Stationen anlegen, Aufgaben generieren.
- **Admin-Konfiguration**: Funktionell vollständig, aber 46 Admin-Komponenten ohne klare Informationsarchitektur.

### Sackgassen und unklare Zustände

- **Rotation ohne Zuständigkeit**: ✓ Behoben (2026-04-24) — `DefaultResponsibilityId` ist jetzt Pflichtfeld. Bestehende Templates ohne Zuständigkeit zeigen weiter eine Warnung in der Admin-Ansicht.
- **Workflow-Builder ohne Publish-Validierung**: Man kann einen Workflow bauen und veröffentlichen, der logisch unvollständig ist (kein Exit-Knoten geprüft).
- **Automation-Keys ohne Binding**: Ein Template mit falschem `automation_key` schlägt erst beim Job-Execute fehl — kein Fehler beim Speichern.
- **Filter "Wechsel in X Tagen"**: War bis zum letzten Fix auf die gesamte Aufgabenliste angewendet — hat alle Aufgaben jenseits von 14 Tagen versteckt.
- **Auth-Simulation**: `SimulationLoginPage` zeigt statische Benutzer — kein Hinweis, was welcher User kann. Neue Entwickler müssen raten.

### Inkonsistente Bedienung

- **Zwei Task-Typen, eine UI**: Workflow-Aufgaben und Rotations-Aufgaben sehen identisch aus (`/tasks/ref/`), haben aber unterschiedliche Felder und Bearbeitungsrechte.
- **Audit-Log**: Für Workflows vorhanden, für Rotation separat — keine gemeinsame Ansicht.
- **Mail-Vorlagen**: 6 vordefinierte Typen, kein Hinweis welcher Typ wann ausgelöst wird.

---

## 3. Funktionsprüfung — Funktioniert das logisch?

### Aufgabensichtbarkeit (Rotation)

**Problem**: `GetTasksAsync` lädt alle Tasks aus der DB, filtert dann in-memory via `CanUpdateTaskStatus`. Für Rotationsaufgaben prüft `MatchesTaskAssignment()` die primäre Zuweisung (`rotation_task_assignments WHERE is_primary=TRUE`). Wenn kein Eintrag vorhanden, ist die Aufgabe für alle Nicht-Admins unsichtbar. Diese Logik ist korrekt, aber fehleranfällig durch Template-Konfigurationsfehler (NULL responsibility).

**Risiko**: Skalierungsproblem. Alle Aufgaben werden aus der DB geladen, dann gefiltert. Bei 10.000+ Aufgaben blockiert das.

### Personen-Matching beim Onboarding

**Methode**: `GetCompletedOnboardingSource()` nutzt `LATERAL JOIN` mit Employee-Number-Fallback.  
**Risiko**: Wenn Personendaten unvollständig (kein employee_number), kann das falsche Person matchen. Kein Log-Eintrag, kein Audit-Trail für die Matching-Entscheidung.

### Transaktionssicherheit

- `RotationPlan`-Erstellung: Plan anlegen + Stationen anlegen — kein expliziter Transaktions-Scope. Wenn Station-Insert fehlschlägt, existiert verwaister Plan.
- `SynchronizeRotationGeneratedTasks`: Erstellt/updated/cancelt Tasks — mehrere SQL-Statements ohne explizite Transaktion im Repository-Code sichtbar.

### Automatisierungsjobs

- 3 Versuche, fest codiert: 1min, 5min, 5min.
- Kein Dead-Letter-Queue, kein Alert bei dauerhaftem Fehler.
- Payload-Schema nicht beim Speichern validiert.

### Notification-Templates

- Platzhalter (`{{PERSON_NAME}}` etc.) werden zur Laufzeit ersetzt.
- Kein Check beim Template-Speichern, ob Platzhalter zur Datenstruktur passen.
- Render-Fehler erst beim Versenden sichtbar.

---

## 4. Architektur & Design

### Trennung UI / Logik / Datenhaltung

**Backend: Grundsätzlich sauber**
- Services kapseln Fachlogik
- Repositories trennen SQL-Zugriff
- Endpoints sind thin (keine Logik, nur Routing + Auth)

**Hauptproblem: Monolithisches Repository**

`PostgresWorkflowRepository.cs` ist über 35 Partial-Files mit **23.758 Zeilen** verteilt. Eine Klasse. Enthält:
- Workflow-CRUD
- Runtime-Events
- Rotations-Generierung (1.444 Zeilen)
- Automatisierungs-Jobs
- Notifications
- Audit-Logs
- Personen-Matching

Das ist kein Repository-Pattern — das ist eine Daten-God-Klasse. Unit-Tests sind strukturell unmöglich, ohne alles zu mocken.

**Frontend: Zustand-Management-Lücke**

- TanStack Query für Server-State: korrekt eingesetzt.
- Lokaler UI-Zustand in großen Pages (564–617 Zeilen): zu viel.
- `RotationOperationsPage.tsx` und `RotationPlanDetailPage.tsx` handhaben State, Filter, Berechnungen und Rendering in einer Komponente.
- Keine Error-Boundaries sichtbar — ein Render-Fehler bricht die gesamte Seite.

**Versteckte Abhängigkeiten**

- `AuthorizationPolicyService.cs` kennt `EffectiveResponsibilities` direkt. Wenn das Modell sich ändert, bricht die Policy.
- `RotationTaskGenerationOperations.cs` kodiert `"enter"`/`"exit"` als Strings, nicht als enum — Tippfehler erzeugen Silent Failures.

---

## 5. Realitätscheck (Kritisch)

### Overengineered

- **Workflow-Builder mit Canvas-UI**: Visueller DAG-Builder für Workflows, die im Moment noch hauptsächlich manuell von Admins verwaltet werden. Aufwand vs. Nutzen unklar, da die Nutzerbasis vermutlich keine drag-and-drop Workflow-Konfiguratoren erwartet.
- **Dual-Mode Auth (dev-sim + entra)**: Sinnvoll für Entwicklung, aber die Sim-Schicht ist zu komplex — statische User, fest kodierte Rollen. Würde eine einfache .env-getriebene Fallback-Logik reichen?
- **Automatisierungs-Layer mit Handler-Registry**: Für zwei simulierte Handler (CreateAdUser, SendWelcomeMail) ist eine Registry mit Retry-Queue und Versuch-Tracking massiv übergebaut. Noch kein echter Handler existiert.

### Unnötig komplex

- **RotationTaskGenerationOperations.cs** (1.444 Zeilen): Die Regenerierungslogik könnte in einer domänenorientierten Service-Klasse mit klaren Methoden liegen. Stattdessen ist sie SQL + C#-Mapping ohne klare Phasengrenzen.
- **46 Admin-Komponenten**: Für eine interne Plattform mit kleiner Nutzerbasis viel UI-Komplexität. Viele Formulare haben ähnliche Struktur (CRUD), werden aber separat implementiert.
- **RotationPlan-Konfliktvalidierung**: Mehrschichtige Validierung (DB-Trigger + Service + API), jede Ebene prüft andere Aspekte — schwer zu verstehen welche Ebene was verhindert.

### Was kein Nutzer je anfassen wird

- **Workflow-Definition-Versioning**: Publish-Flow mit Versionsnummern und DAG-Replacement. In der Praxis ändern Admins vermutlich 1–2x pro Jahr eine Definition. Das Feature ist für Nutzungsfrequenz über-engineered.
- **`/client/log-events` Endpoint** ohne Auth: Frontend-Fehler landen im System-Log. Kein Missbrauchsschutz, kein Rate-Limiting sichtbar.

---

## 6. Offene Punkte (nach Review-Zyklus 2026-04-23)

Alle priorisierten Aufgaben (COD-1..6, CLA-1..4) sind abgeschlossen. Folgende Punkte wurden bewusst ausgeklammert oder entstanden als Folgearbeit:

### Nicht im Zyklus adressiert

- **C2** (HIGH): ✓ erledigt (2026-04-24) — `DefaultResponsibilityId` ist jetzt Pflichtfeld in Service + Frontend. SQL-Sichtbarkeitsfilter korrigiert (`rta.responsibility_id` → `rta.assignee_responsibility_id`, `rta.user_id` → `rta.assignee_user_id`).
- **H6** (HIGH): ✓ erledigt (2026-05-02) — `RotationTaskRegenerationEngine` als pure-static-Domain-Engine in `api/API/RotationTaskRegenerationEngine.cs` extrahiert. `SynchronizeRotationGeneratedTasks` in `PostgresRotationRepository.TaskGenerationOperations.cs` ruft jetzt `RotationTaskRegenerationEngine.Plan(existing, desired)` auf und fuehrt den zurueckgegebenen Plan (ToCreate/ToUpdate/ToCancel/UnchangedCount) via SQL aus. 12 Unit-Tests in `RotationTaskRegenerationEngineTests.cs` decken die Partitionierungslogik ab — ohne DB. `RotationDesiredTaskRecord` + `RotationGeneratedTaskRecord` von `private sealed` zu top-level `internal sealed class` promotiert.
- **L1**: ✓ erledigt (2026-05-02) — Automation-Retry-Defaults via env-vars konfigurierbar (`WORKFLOW_AUTOMATION_MAX_ATTEMPTS`, `WORKFLOW_AUTOMATION_FIRST_RETRY_DELAY_SECONDS`, `WORKFLOW_AUTOMATION_SUBSEQUENT_RETRY_DELAY_SECONDS`). Defaults erhalten bisheriges Verhalten (3/60s/300s). Eigene Settings-Klasse `WorkflowAutomationRetrySettings` als Singleton.
- **L2**: Datenbereinigung für Drafts/abgebrochene Pläne fehlt. **Unklar geblieben** — Scope (Auto-Delete nach X Tagen? Admin-Endpoint? Soft-Archive? Cron-Job?) ist Produktentscheidung, nicht Implementierung. Bleibt offen bis konkrete Vorgabe.
- **L3**: ✓ erledigt (2026-05-02) — `/client/log-events` mit ASP.NET Core Rate-Limiter abgesichert: 60 Requests/Minute pro Remote-IP, partitionierter Fixed-Window-Limiter. Endpoint markiert mit `RequireRateLimiting("client-log-events")`, gibt 429 bei Überschreitung.
- **L5**: ✓ erledigt (2026-05-02) — Optionales `reason text`-Feld auf `auth_permission_audit_log` (Migration `68_permission_audit_reason.sql`). `LogPermissionAuditAsync` nimmt optional `string? reason = null`, `AdminPermissionAuditEntryDto.Reason` liefert es zurück. Bestehende Caller bleiben unverändert (default null). Admin-Endpoint-Integration für Reason-Eingabe als Folgearbeit.
- **L6**: ✓ erledigt (Guard war bereits vorhanden — Doku-Verifikation 2026-05-02) — `LifecycleServiceCollectionExtensions.AddLifecycleApiServices` (Zeile 39-44) wirft beim Service-Registrieren `InvalidOperationException` wenn `IsProduction && AuthMode != "entra"`, also auch bei dev-sim in Production. Zusätzlich: `LifecycleStartupValidationExtensions` loggt Defensive-Warning. Dev-sim-Resolver werden nur registriert wenn `DevSimulationEnabled=true`, was in Production durch den Hard-Throw nie erreicht wird.
- **L7**: `WorkflowBuilderPage` + Canvas-Feature für vermutlich sehr seltene Nutzung übergebaut. **Bleibt offen** — Feature-Removal braucht explizite Bestätigung, „vermutlich sehr seltene Nutzung" ist keine Beweislage.

---

## 7. Gesamtbewertung

| Bereich | Note | Hauptbegründung |
|---------|------|-----------------|
| Backend-Architektur | **B-** | Gute Service-Trennung, aber monolithisches Repository und fehlende Transaktionen |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints, klare Tabellen — leichte Redundanz in Sync-Triggern |
| Auth & Berechtigungen | **B** | Durchdacht, aber Kanten-Cases (gelöschte User, leere Zuständigkeiten) unbehandelt |
| Rotation-Feature | **B-** | Funktioniert, aber Kernlogik im falschen Layer (Repository statt Service) |
| Frontend-Architektur | **C+** | Seiten zu groß, fehlende Error-Boundaries, Filter-Bug (gefixt) belegt strukturelles Problem |
| Testbarkeit | **D** | Monolithisches Repository verhindert Unit-Tests; nur Integration-Tests sichtbar |
| Skalierbarkeit | **C** | In-Memory-Filter für Tasks ist ein strukturelles Scaling-Problem |
| Sicherheit | **B** | Auth konsistent umgesetzt; `/client/log-events` ohne Rate-Limiting ist ein kleines Leck |

**Stand nach Review-Zyklus 2026-04-23:** CRITICAL- und HIGH-Punkte des Zyklus abgeschlossen. Backend-Architektur und Sicherheit deutlich verbessert. Offene Punkte sind in Abschnitt 6 dokumentiert.

---

# Code Review — Zyklus 2 (2026-05-02)

**Reviewer**: Claude  
**Scope**: Stand nach L7-Phase-2/3 + Restposten R1–R7+R9; Backend unveraendert seit Zyklus 1.  
**Methode**: Diff-Analyse seit 2026-04-23, Re-Read der Architektur-Notizen aus `KauthWorkflow/Stand/Code-Review-Status.md`, gezielte Probe-Reads von `RotationOperationsPage.tsx` (564), `RotationPlanDetailPage.tsx` (617), Edge-Cases im neuen Form-Editor.

## Was sich seit Zyklus 1 strukturell veraendert hat

- **Repository-Monolith aufgespalten** (Phase 8 — H1): `PostgresWorkflowRepository` von 23.758 → in 8 Subsystem-Klassen extrahiert; implementiert nur noch 2 Interfaces (vorher 4).
- **Rotation-Engine unit-testbar** (H6): `RotationTaskRegenerationEngine` als pure-static-Domain-Service; 12 Tests ohne DB.
- **WorkflowBuilder komplett neu** (L7): Canvas-UI weg, Form-Editor mit 12 Schritt-Typen, typ-spezifischen Inline-Editoren, Form-Builder fuer Eingabe-Mapping, strukturierter Bedingungs-Builder, Anlege-/Loesch-UI, Click-zu-Springen, topologische Kartenreihenfolge.
- **React Flow + dagre raus**: AdminConfigPage-Bundle 449 → 228 kB (gzip 124 → 50 kB). DependencyGraphEditor als Tabellen-Editor neu.
- **Haertungen** (L1, L3, L5, L6): Retry-Delays env-konfigurierbar, `/client/log-events` rate-limited, Permission-Audit hat Reason-Feld, dev-sim-Prod-Guard verifiziert.

## Neue Befunde (CRITICAL)

Keine. CRITICAL-Liste leer.

## Neue Befunde (HIGH)

### HQ1 — Decision-Condition-Migration (latenter Runtime-Bug)

`PostgresWorkflowRuntimeRepository.ParseDecisionCondition` wirft `InvalidOperationException("Decision condition is not valid JSON")` bei jeder Decision-Edge, deren `conditionExpression` kein JSON-Objekt mit `answerKey`/`operator` ist. Bestandsdaten aus dem alten Canvas-Editor (Freitext-Strings, `hardwareTakeover == true`-Stil) gehen damit zur Laufzeit kaputt — der Workflow scheitert beim Erreichen der Decision.

**Prüfung notwendig**: SELECT auf `workflow_definition_edges WHERE source_node_type='decision' AND condition_expression IS NOT NULL AND condition_expression NOT LIKE '{%'` — jede Treffer-Zeile ist ein Defekt.

**Optionen**: Migrations-Skript, das alte Strings parst (best-effort) und in JSON umschreibt; oder Validation beim Definitions-Save, die invalid condition_expression ablehnt; oder beides.

### HQ2 — In-Memory-Task-Filter (done 2026-05-02)

Ausgangsproblem: `TaskApplicationService.GetTasksAsync` rief fuer Non-Admin-Nutzer `GetTasksForUser(userId, responsibilityIds)` auf — die SQL lud aber **alle** Workflow-Tasks (`LoadTasks(connection, null, null)`), weil die `responsibilityIds` nirgendwo in den WHERE-Clause gewandert sind. Das Filtern lief erst im C#-`.Where(canUpdate || canDecide)`. Bei 10.000+ Tasks ist das ein Skalierungsblocker.

Loesung: SQL-Vorfilter, der die Garantien der C#-`MatchesTaskAssignment` exakt nachbildet:

- Neue private `WorkflowTaskListNarrowingFilter(long UserId, int[] ResponsibilityIds)`-Struct in `PostgresWorkflowRepository.ReadOperations.cs`.
- `LoadTasks` akzeptiert sie als optionales `narrowing`-Parameter; wenn gesetzt, wird die WHERE-Clause um zwei Predicates erweitert: `w.status <> 'completed'` + `EXISTS (SELECT 1 FROM task_assignments wta WHERE wta.workflow_task_id = t.id AND wta.is_primary = TRUE AND ((wta.assignment_type = 'user' AND wta.assignee_user_id = @narrowUserId) OR (wta.assignment_type = 'responsibility' AND wta.assignee_responsibility_id = ANY(@narrowResponsibilityIds))))`.
- Neue public Repo-Methode `GetTasksForUserNarrowed(userId, responsibilityIds)` — gleicher Rotation-Pfad wie `GetTasksForUser`, aber Workflow-Tasks pre-filtered.

Routing in `TaskApplicationService.GetTasksAsync`:
- Admin (`CanManageAdminConfiguration`) → `GetTasks()` (unfiltered, unchanged)
- Non-Admin **ohne** `TasksAssignOverride`-Permission → `GetTasksForUserNarrowed` (NEW)
- Non-Admin **mit** `TasksAssignOverride`-Permission → `GetTasksForUser` (unfiltered, unchanged — Override-Nutzer koennen jede Task bearbeiten und brauchen die volle Sicht)

Der C#-`.Where(canUpdate || canDecide)` bleibt drin — SQL-Narrowing ist eine **Obermenge** dessen, was am Ende durchkommt: phase-aware Checks wie `CanRegularlyEditWorkflow` (Worker fuer `WaitingForDepartment`, Manager fuer `WaitingForSupervisor`) lassen sich nicht ohne Permission-Tabelle in SQL abbilden. Das ist als Safety-Net dokumentiert.

**Tests:** Neue Integration-Suite `PostgresWorkflowRepositoryTaskNarrowingIntegrationTests.cs` mit 2 Tests: (1) Narrowed liefert nur Tasks auf nicht-terminalen Workflows mit passendem Primary-Assignment (User ODER Responsibility); ausgeschlossen werden Tasks auf `completed`-Workflows und Tasks mit fremder Responsibility. (2) Mit leeren responsibilityIds matcht nur direkter User-Assignment-Pfad. Die Tests setzen jeweils ein isoliertes Department + 2 Workflows + 4 Tasks + 2 Responsibilities + 1 User auf und vergleichen `GetTasksForUser` (unfiltered) gegen `GetTasksForUserNarrowed` per Task-ID. **384 passed, 1 skipped, 0 failed.**

**Korrektheits-Garantie:** Fuer Regular-Non-Override-Nutzer ist Narrowing eine **strikte Obermenge** dessen, was die in-memory `CanUpdateTaskStatus || CanDecideTaskApproval` durchlaesst:
- Beide Predicates verlangen `!IsTerminal(workflow.WorkflowStatus)` → SQL-`w.status <> 'completed'` ist exakte Spiegelung (`IsTerminal` returnt true nur fuer "completed").
- Beide Predicates verlangen `MatchesTaskAssignment` → SQL-EXISTS spiegelt die Logik in `MatchesPrimaryAssignment` 1:1 (`assignment_type = 'user' AND assignee_user_id = userId` ODER `assignment_type = 'responsibility' AND assignee_responsibility_id IN responsibilityIds`).

Damit gilt: keine Task, die der C#-Filter durchlassen wuerde, wird vom SQL-Narrowing unterdrueckt. Risiko = 0 fuer Sichtbarkeitsregressionen, solange der Override-Pfad korrekt routet.

### HQ3 — Personen-Matching ohne Audit-Trail (done 2026-05-02)

Audit-Tabelle `person_match_audit_log` (Migration `db/_archive/70_person_match_audit_log.sql`, in `db/01_schema.sql` gespiegelt) protokolliert pro Match-Entscheidung: `matched_person_id`, `app_user_id`, `directory_identity_id`, `employee_number`, `match_strategy` (`employee_number` / `created_new` / `app_user_upsert`), `match_score` (1.00 fuer ID-Match, NULL fuer fallback-Insert), `fallback_used` (true wenn keine bestehende Person gefunden wurde), `source` (`directory_sync_bulk` / `directory_sync_per_user`), optional `detail` (jsonb).

Wiring in `EntraDirectorySyncService`:
- Bulk-Link (`linkPeopleByEmployeeNumberSql`, ~Zeile 1447) — UPDATE per CTE mit RETURNING; INSERT ins Audit-Log fuer jede gematchte Person (strategy `employee_number`, score 1.00, fallback false, source `directory_sync_bulk`).
- Per-User (`EnsureDirectoryManagedPersonRecordAsync`, ~Zeile 1916) — sowohl der Match-Pfad (employee_number-Lookup) als auch der Insert-Pfad (kein Match → neue Person) loggen jetzt. Match-Pfad: strategy `employee_number`, score 1.00, fallback false. Insert-Pfad: strategy `created_new` oder `app_user_upsert` (via `xmax = 0`-Trick zur Unterscheidung), fallback true.

Beide Pfade nutzen CTE-INSERTs in derselben SQL-Anweisung — atomar, kein extra Roundtrip. Postgres-Garantie: data-modifying CTEs werden auch ohne Referenz im Final-Select ausgefuehrt.

**Tests:** 383 Tests passed (382 + 1 skipped) inkl. der Postgres-Integration-Suite, die den Sync-Pfad indirekt mitnimmt.

**Bewusst nicht aufgenommen**: `LoadTargetPerson`-Fuzzy-Name-Match in `PostgresRepositorySharedHelpers` (Zeile 596-606) — das ist ein READ-Pfad fuer das _aktuellste Workflow_ einer Person, kein Person-zu-Person-Matching. Wenn der Workflow-target_person_id sauber gesetzt ist (Standardpfad), greift der Fallback nicht; wenn doch, ist der Pfad self-correcting (Workflow zeigt direkt auf die ID). Audit dort waere disproportional — die Match-Falle ist im Directory-Sync, der gerade abgesichert wurde.

### HQ4 — Kein Test-DB-Setup für CI (Testbarkeit)

`API.Tests`-Suite hat 380+ Tests, aber **alle Postgres-/Integrations-Tests scheitern in der lokalen Dev-Umgebung mit `Npgsql.NpgsqlException: Failed to connect to 127.0.0.1:26432`**. Das ist seit Monaten dokumentiertes Dauerproblem. Nur die 324 Unit-Tests (Filter `!~Integration & !~Postgres`) laufen verlässlich.

**Folge**: Kein Schutz gegen Regressionen am SQL- und Repository-Pfad. Jede Repository-Änderung (z.B. der Phase-8-Split) ist nur durch Compiletime-Checks gesichert.

**Optionen**: Docker-Compose mit Postgres-Test-Container (entrypoint via `make test`/`pwsh script`); Testcontainers-NuGet einsetzen, das die DB pro Test-Run hochfährt; CI-Pipeline-Setup. Pflicht-Voraussetzung für ernsthafte Continuous Integration.

### HQ5 — Frontend-Page-Größe (done 2026-05-02)

Pages refactored:
- `RotationPlanDetailPage.tsx` — 617 → **317 Zeilen** (-49%)
- `RotationOperationsPage.tsx` — 564 → **367 Zeilen** (-35%)
- `AdminConfigPage.tsx` — 423 Zeilen (unverändert; nicht in HQ5-Scope, Bundle bleibt klein)

Extraktionen:
- `web/src/hooks/useRotationOperationsView.ts` — 8 Filter-States + 6 useMemo-Berechnungen + Helper `isOperationallyOpen`/`isUpcomingAnchorDate`. Page bekommt fertige `filteredRows`/`upcomingChanges`/`departmentSummaries`/`personSummaries` + `filters`/`setters`-Bündel.
- `web/src/hooks/useRotationStationForm.ts` — 5 useState (`editingStationId`, `stationForm`, `isSavingStation`, `isRegenerating`, `deletingStationId`) + alle 6 Handler (`openCreate/Edit`, `reset`, `handleSave/Delete/RegenerateTasks`) + `reloadPlanData`. Form-State ist jetzt unabhängig von Page-Render-Tree testbar.
- `web/src/components/rotation/RotationStationFormCard.tsx` — Form-JSX (~140 Zeilen) als eigene Sub-Komponente.
- `web/src/components/rotation/rotationLabels.ts` — 4 reine Label-Helper (`getPlanStatusLabel`, `getPlanStatusPillClass`, `getStationStatusLabel`, `getGeneratedTaskStatusLabel`) als Util.

**Tests**: Frontend 140/140 passed, `tsc --noEmit` clean. Pages bleiben unverändert in Verhalten und URL-Routing.

**Nicht aufgenommen**: `AdminConfigPage` (423 Zeilen) — nicht im HQ5-Scope, Bundle ist nach L7 klein, kann später separat angegangen werden falls Wartbarkeit zum Problem wird.

## Neue Befunde (LOW)

### LQ1 — Mapping-Editor-Property-Listen hardcodiert (done 2026-05-02)

Neuer Endpoint `GET /admin/config/automation-property-catalog` liefert die Source-/Property-Matrix (`workflow`/`target_person`/`directory_identity`/`answer`/`static`) aus `AutomationPropertyCatalog.cs`. `useAdminWorkflowBuilder` lädt den Catalog im selben Schritt wie die Action-Definitionen; `WorkflowBuilderActionMappingEditor` resolves die Property-Listen aus dem Catalog (Fallback auf hardcodierte Konstanten falls Endpoint fehlschlägt). Beim Aufdecken stellte sich heraus dass die alten Frontend-Konstanten zwei Mismatches gegen das Backend hatten: `currentPositionRoleId` statt `roleId` und kein `personId` — der Catalog bringt beides in Sync. Source-of-truth für die Listen bleibt aber der C#-Resolver in `PostgresWorkflowAutomationOperations.ResolveAutomationReference`; ein Code-Kommentar im neuen Catalog warnt vor dem manuellen Sync-Aufwand bei Erweiterungen.

### LQ2 — Mapping-Editor unterstützt nur Top-Level-Parameter (done 2026-05-02 — als Architektur-Entscheidung dokumentiert)

In `KauthWorkflow/Architektur/Entscheidungen.md` als bewusste Beschränkung festgehalten: Form-Builder bleibt flach (`paramName → {source, value}`); Power-Modus deckt nested Schemas ab. Erweiterung erst wenn mehrere produktive Actions nested-Schemas brauchen, nicht spekulativ vorbauen.

### LQ3 — Auth-Simulation ohne User-Beschreibung (done 2026-05-02)

`SimulationLoginUserOptionDto` erweitert um `RoleKeys` (Vereinigung aus `app_user_roles` + group-mapped roles via `directory_group_role_mappings`). SQL-Subquery liefert das Array per `array_agg`. Frontend rendert die Rollen sowohl im Dropdown-Eintrag (`Display Name (UPN) – role_a, role_b`) als auch im Detail-Untertitel.

### LQ4 — Notification-Templates ohne Platzhalter-Validierung (war bereits implementiert)

`NotificationTemplateService.UpdateAdminTemplate` ruft `ValidatePlaceholders(definition, subject, body)` auf, vergleicht extrahierte Placeholder gegen die `definition.Placeholders`-Whitelist und wirft `InvalidOperationException("Unbekannte Platzhalter ...")` bei Mismatch. Test-Coverage `NotificationTemplateServiceTests.UpdateAdminTemplate_RejectsUnknownPlaceholder`. Die Review-Note war stale.

### LQ5 — Frontend-Error-Boundaries fehlen

Grep nach `ErrorBoundary` im Frontend = 0 Treffer. Ein Render-Fehler in einer Sektion bricht die ganze Seite. Pro Page eine ErrorBoundary mit Fallback-Panel reicht.

### LQ6 — WorkflowBuilder hat nur 2 Smoke-Tests

`tests/WorkflowBuilderPage.test.tsx` testet nur ob die Sektions-Headings rendern. Die echten Komponenten (Form-Editor-Sektionen, Mapping-Editor, Condition-Editor, Step-Card mit allen 12 Typen) haben **keine Tests**. Keine Regressionen-Erkennung wenn jemand z.B. den Mapping-JSON-Roundtrip kaputtmacht.

**Prüfung**: Mindestens je ein Test pro neuer Komponente (`WorkflowBuilderConditionEditor`-Roundtrip, `WorkflowBuilderActionMappingEditor`-Roundtrip, `WorkflowBuilderStepConfigEditor`-Roundtrip, `topologicallyOrderNodes`-Edge-Cases).

### LQ7 — Hook-API hat noch kleine Tot-Code-Reste (done 2026-05-02)

`refreshReferenceData` (Export-only, kein Consumer im Form-Editor) entfernt. Der ungenutzte `referenceReloadTick`-State + Setter sind ebenfalls weg. `loadVersionDetail` bleibt drin — wird intern von `createDefinition` genutzt (kein Tot-Code, war nur missverstanden).

## Verbleibende Punkte aus Zyklus 1

- **L2** (Datenbereinigung): unverändert deferred — wartet auf Produkt-Entscheidung über Cleanup-Policy.
- **R8** (Form-Editor Browser-Verifikation): unverändert offen — Nutzer-Aufgabe; ohne Verifikation ist die Behauptung „L7 funktioniert" formal nicht gedeckt.
- **R10** (Mobile-Layout): backlog, kein konkreter Bedarf.

## Aktualisierte Gesamtbewertung

| Bereich | Note (Zyklus 2) | Note (Zyklus 1) | Hauptbegruendung |
|---------|----------------|-----------------|------------------|
| Backend-Architektur | **B+** | B- | Repository sauber gesplittet (H1, 8.A–8.C), Domain-Engine extrahiert (H6) |
| Datenbankdesign | **A-** | A- | unveraendert |
| Auth & Berechtigungen | **B+** | B | Permission-Audit hat Reason-Feld; Guard verifiziert |
| Rotation-Feature | **B** | B- | Engine als unit-testbarer Domain-Service |
| Frontend-Architektur | **B** | C+ | WorkflowBuilder Form-Editor + Bundle 50% kleiner; Rotation-Pages refactored (HQ5: 35-49% kleiner, Hooks + Sub-Komponenten extrahiert) |
| Testbarkeit | **C+** | D | Repository-Split + Engine-Extraktion ermoeglichen Unit-Tests; aber Test-DB-Setup fehlt fuer Integration (HQ4) |
| Skalierbarkeit | **B-** | C | Workflow-Task-Filter fuer Regular-Non-Override-Nutzer in SQL pre-narrowed (HQ2) — DB-Roundtrip schrumpft von „alle Tasks" auf „eigene primaer-zugewiesene auf nicht-terminalen Workflows" |
| Sicherheit | **B+** | B | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |

## Vorschlag fuer Priorisierung des naechsten Zyklus

**Reihenfolge nach Risiko × Aufwand:**

1. **HQ1** — Decision-Condition-Migration. Latenter Bug, zerstört Workflows zur Laufzeit. Erst Audit-SQL, dann Migrations-Skript. Aufwand: 0.5–1 d.
2. **HQ4** — Test-DB-Setup fuer CI. Voraussetzung fuer alles weitere. Aufwand: 0.5 d (Testcontainers) bis 1 d (CI-Integration).
3. **LQ6** — WorkflowBuilder-Tests. Schuetzt frische Investitionen. Aufwand: 0.5 d.
4. **LQ5** — Error-Boundaries. Schnell, sichtbarer Nutzen. Aufwand: 2 h.
5. **HQ5** — Frontend-Page-Refactor (zumindest die zwei 500+-Zeilen-Pages). Aufwand: 1 d.
6. **HQ2** — In-Memory-Task-Filter. Erst wenn HQ4 steht, weil Repository-Aenderung Integration-Tests braucht. Aufwand: 1 d.
7. **HQ3** — Personen-Matching-Audit. Aufwand: 0.5 d.

LOW-Punkte (LQ1, LQ2, LQ3, LQ4, LQ7) als Hardening-Bundle bei Bedarf, kein Blocker.

**Bewusst nicht aufgenommen**: R8 (Browser-Test bleibt Nutzer-Aufgabe), L2 (wartet auf Produkt-Entscheidung), R10 (Mobile, kein Bedarf).


---

# Code Review — Zyklus 3 (2026-05-02)

**Reviewer**: Claude (Opus, hohe Reasoning-Tiefe)
**Scope**: Stand nach Zyklus 2 (alle HIGH+LOW abgeschlossen). Frischer Risiko-Sweep ueber Backend, Frontend und Cross-Cutting.
**Methode**: Spawnte einen Explore-Agent fuer breite Suche, dann gezielte Verifikation jeder Behauptung per `wc -l` und Code-Read. Eine vom Agent gemeldete Transaktions-Luecke in `WorkflowRuntimeService.CreateWorkflowAsync` hat sich als **falsch positiv** erwiesen — der Repository-Pfad `PostgresWorkflowRuntimeRepository.CreateWorkflowDefinitionInstance` oeffnet selbst eine Transaktion (Z. 152, `BeginTransactionAsync`).

## Was sich seit Zyklus 2 strukturell veraendert hat

- **HQ1–HQ5 alle done**: Decision-Condition-Migration, In-Memory-Task-Filter SQL-Push (mit Korrektheits-Garantie), Personen-Matching-Audit-Tabelle, Test-DB via Testcontainers, Rotation-Pages-Refactor (35–49% kleiner durch Hooks + Sub-Komponenten).
- **LQ-Bundle done**: Property-Catalog-Endpoint loest hardcodierte Frontend-Konstanten ab (deckte zwei latente Mismatches gegen Backend auf), Sim-Login zeigt jetzt Rollen, Mapping-Editor flat-only-Limit ist dokumentierte Entscheidung, Hook-Tot-Code raus.
- **Notification-Linter** war bereits implementiert (Zyklus-2-Befund war stale). Test-Coverage existiert.

## Neue Befunde (CRITICAL)

Keine. CRITICAL-Liste leer.

## Neue Befunde (HIGH)

### HQ1-Z3 — HQ5-Hooks ohne Test-Coverage (done 2026-05-02)

Neue Test-Suiten:
- `web/tests/useRotationOperationsView.test.tsx` (~250 Zeilen, 18 Tests). Pure Helpers (`isOperationallyOpen`, `isUpcomingAnchorDate` mit Tagesgrenzen-Edge-Cases inklusive Window=0/14/15 + Past-Dates + NaN). Hook-Integration via `renderHook` + `QueryClient`-Wrapper + `CurrentUserContext`-Provider; mockt `getMyTasks`. Verifiziert: Non-Rotation-Filter, Search-Filter ueber displayName/title/taskRef, departmentFilter, onlyOpen-Default, `inferredOwnDepartmentId`-Resolve aus `permissionScopes`, `upcomingChanges`-Aggregation per plan+anchor+trigger, changeWindow-Cutoff, departmentSummaries/personSummaries-Counts inklusive earliest-anchor-Selection.
- `web/tests/useRotationStationForm.test.tsx` (~210 Zeilen, 14 Tests). Pure Helpers (`createEmptyStationForm`, `buildStationForm` mit null-Normalisierung, `normalizeStationPayload` mit dokumentierter Trim-Quirk + Number("")=0-Sentinel). Hook-Integration mit `ToastProvider` + `QueryClient`-Wrapper; mockt `createRotationStation`/`updateRotationStation`. Verifiziert: `openCreateStationForm` seedet orderIndex aus `orderedStationsCount`, `openEditStationForm` populiert form-state + editing-id, `resetStationForm` clear, `handleSaveStation` rejected bei leerer Form ohne API-Call, `handleSaveStation` ruft create vs update je nach editing-id, Form-Reset nach erfolgreichem Save.

**Test-Resultate**: Frontend `npm test` **172 passed** (vorher 140, **+32 neue Tests**). 39 Test-Files. `tsc --noEmit` clean. Aufwand: realistisch ~3 h dank pure-helper-Anteil.

**Quirk dokumentiert**: `Number("")` returns `0` (nicht NaN), weshalb der `<= 0`-Guard im Save-Handler die Sentinel-Logik fuer leere `departmentId`-Inputs traegt — ist im Test mit `expect(payload.departmentId).toBe(0)` festgehalten.

### HQ2-Z3 — `useAdminWorkflowBuilder` zerlegen (done 2026-05-02, partial)

**Erste Sub-Hook-Extraktion: Version-Reference-Data.**

Neu: `web/src/hooks/useAdminWorkflowVersionReferenceData.ts` (140 Zeilen). Pure-React-Hook: nimmt `versionDraft` + `definitions` als Args, beobachtet `versionDraft.nodes` und `versionDraft.primaryLegacyProcessTypeKey`, ermittelt referenzierte `legacyProcessTypeKey`-Werte aus den Node-Configs, lädt parallel `getAdminTaskTemplates` + `getAdminAnswerDefinitions` für jede referenzierte Workflow-Definition, dedupliziert und lädt dann pro Template noch Conditions + Dependencies. Returnt `{ taskTemplates, answerDefinitions, taskTemplateConditions, taskTemplateDependencies }`. Fehler werden swallowed (Editor-Anzeige ist nicht-kritisch).

Main-Hook `useAdminWorkflowBuilder` 865 → **761** Zeilen (-12%, -104 Zeilen). Entfernt: 4 useState-Slots fuer die obigen Listen, 99-Zeilen-useEffect, 8 manuelle `setXxx([])`-Calls in `resetLoadedVersion` + `applyLoadedVersionDetail`. Hook-API zur FormSection ist **unveraendert** — die 4 Properties (`taskTemplates`, `answerDefinitions`, `taskTemplateConditions`, `taskTemplateDependencies`) werden weiterhin returnt, jetzt aus dem Sub-Hook destructured.

**Tests**: 172 Frontend-Tests passed, `tsc --noEmit` clean.

**Verbleibende Splits deferred**: Die ursprünglich vorgeschlagenen 3 weiteren Sub-Hooks (`useAdminWorkflowDefinitionSelection`, `useAdminWorkflowVersionDetail`, `useAdminWorkflowBuilderValidation`) sind eng mit den verbleibenden 12 useState-Slots verzahnt — `loadDefinitions` setzt sowohl Reference-Data als auch Selection; `saveVersion` greift auf Validation, Draft, Definition gleichzeitig zu; `applyLoadedVersionDetail` fanned out in mehrere State-Slots. Ein sauberer Split braucht erst eine Trennung der Tx-aehnlichen Update-Pfade. Aktuell deferred — die jetzt erreichten 761 Zeilen sind handhabbar; wenn der Hook in Zukunft weiter waechst (z. B. durch neue Builder-Features), kann nachgezogen werden.

### HQ3-Z3 — Repository-Partials splitten (done 2026-05-02, partial)

**TaskTemplate-Familie komplett gesplittet:**
- `PostgresWorkflowRepository.TaskTemplateAdminOperations.cs` 1017 → **391** Zeilen (-61%) — bleibt zustaendig fuer Templates-CRUD + cross-cutting Helper (`GetAdminTaskTemplateById`, `EnsureProcessTypeExists`, `NormalizeNullableText`).
- Neu: `PostgresWorkflowRepository.TaskTemplateConditionOperations.cs` (251 Zeilen) — Conditions CRUD + `MapAdminTaskTemplateCondition`/`Validate`/`Bind`/`NormalizeOperator`/`EnsureAnswerKeyBelongsToProcessType`/`GetAdminTaskTemplateConditionById` + `AllowedAdminTaskTemplateConditionOperators`-Konstante.
- Neu: `PostgresWorkflowRepository.TaskTemplateDependencyOperations.cs` (391 Zeilen) — Dependencies CRUD + `GetAdminDependencyGraph` + `MapAdminTaskTemplateDependency`/`Validate`/`Bind`/`NormalizeStatus`/`LoadAdminTaskTemplateDependencyGraph`/`AdminTaskTemplateDependencyExists`/`WouldCreateDependencyCycle`/`GetAdminTaskTemplateDependencyById` + `AllowedAdminTaskTemplateDependencyStatuses`-Konstante.

**WorkflowDefinition: GraphMapping ausgelagert, Versions-Split deferred:**
- `PostgresWorkflowRepository.WorkflowDefinitionAdminOperations.cs` 1430 → **1103** Zeilen (-23%) — bleibt zustaendig fuer Definitions-CRUD + Versions-CRUD + Definition-spezifische Helper.
- Neu: `PostgresWorkflowRepository.WorkflowDefinitionGraphMappingOperations.cs` (350 Zeilen) — `BuildWorkflowDefinitionValidationSnapshot`, `MapValidationIssues`, `CreateDefinitionReferenceIssue`, `TryGetNodeConfigValue`, `ToDraftNode`, `ToDraftEdge`, `PersistWorkflowDefinitionVersionGraph` (160-Zeilen-Methode), `HasWorkflowNodePositionColumns`. Cross-File-Helper aus dem Definitions-Partial werden via partial-class-Mitgliedschaft erreicht (LoadDefinitionRequiresSupervisorStep, LegacyProcessTypeExists, LegacyTaskTemplateExists, NormalizeWorkflowDefinitionOptionalText).
- **Versions-Split bewusst deferred**: Die Version-Methoden (CreateAdminWorkflowDefinitionVersion, GetOrCreateAdminWorkflowDefinitionWorkingDraft, ReplaceAdminWorkflowDefinitionVersion, GetAdminWorkflowDefinitionVersionDetailById, etc.) sind eng mit `CreateAdminWorkflowDefinition` verzahnt (gemeinsame Transaction, gemeinsamer InsertWorkflowDefinitionVersion-Helper). Ein sauberer Split braucht erst eine Refactor-Pass-Through der Tx-Boundary — separater Folge-Task wenn die Definitions-CRUD-Datei weiter waechst.

**Tests**: 384 passed, 1 skipped, 0 failed (unveraendert gegenueber HQ1-Z3-Stand). Build clean.

## Neue Befunde (LOW)

### LQ1-Z3 — `RotationNotificationHostedService` Sweep ohne Timeout-Wrapper (done 2026-05-02)

`CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)` mit `CancelAfter(2h)` umschliesst jeden `ExecuteDailySweepAsync`-Aufruf. Bei Timeout fliegt `OperationCanceledException`, **separat** vom shutdown-cancel-Pfad behandelt: eigener `catch (OperationCanceledException ex)`-Block (filter `when (stoppingToken.IsCancellationRequested)` greift hier nicht, weil der Timeout-CTS aber nicht der stoppingToken cancelled wurde) loggt mit Timeout-Marker und faellt in den existierenden 5-min-Retry-Pfad. Konstante `SweepTimeout = TimeSpan.FromHours(2)` ist privat-static im Service.

**Tests**: 326 Backend-Unit-Tests gruen (kein dedizierter Timeout-Test geschrieben — der Pfad ist 2 SLoC und das Logging-Verhalten waere mit `ILogger`-Mock disproportional).

### LQ2-Z3 — `EntraDirectorySyncService.cs` 2222 Zeilen

Groesste Service-Datei. Mischt Graph-API-Plumbing, Directory-zu-Role-Mapping und Person-Matching (HQ3-Audit eingebaut). Tests bisher schwer, weil Graph-API-Mock fett.

**Risiko**: Aktuell niedrig — wird per Timer gefahren, kein User-Pfad. Aber ein Refactor (z. B. Wechsel auf Microsoft Graph SDK v6) wuerde sich an der Groesse verschlucken.

**Pruefung**: Aufschieben bis konkreter Anlass (Graph-SDK-Update, neue Sync-Anforderung). Wenn doch jetzt: `IGraphDirectoryClient` (Plumbing), `IRoleMappingResolver` (Mapping-Logik), `IPersonMatchService` (Matching + Audit) extrahieren.

**Aufwand**: 2–3 d. **Bewusst defer**.

### LQ3-Z3 — Doku-Drift: `KauthWorkflow/Stand/Code-Review-Status.md`

Listet Zyklus-2-Status, aber die Tabelle markiert HQ2/HQ3/HQ5/LQ-Bundle nicht als done (sind erst in dieser Session abgeschlossen). Nach Zyklus 3 muss die Tabelle aktualisiert werden — sonst lesen neue Devs „HQ2 offen" obwohl es done ist.

**Aufwand**: 30 min. **Im selben Arbeitsgang erledigen.**

### LQ4-Z3 — `AdminConfigPage.tsx` 423 Zeilen — Watch-Item

Nicht ueber 500-Schwelle, aber dicht: 7 Custom-Hooks, 20+ durchgereichte Props an Sub-Sections. Wenn ein achter Hook dazukommt (z. B. fuer Property-Catalog-Verwaltung), wird die Page kippen.

**Aufwand**: 1–2 d (wenn ueberhaupt). **Aktuell beobachten, nicht angreifen.**

## Verbleibende Punkte aus aelteren Zyklen

- **L2** (Datenbereinigung Drafts/abgebrochene Plaene): unveraendert deferred — wartet auf Produkt-Entscheidung.
- **R8** (Form-Editor Browser-Verifikation): unveraendert offen — Nutzer-Aufgabe.
- **R10** (Mobile-Layout): backlog, kein Bedarf.

## Aktualisierte Gesamtbewertung

| Bereich | Note (Zyklus 3) | Note (Zyklus 2) | Hauptbegruendung |
|---------|----------------|-----------------|------------------|
| Backend-Architektur | **A-** | B+ | TaskTemplate-Familie sauber 3-fach gesplittet (HQ3-Z3); WorkflowDefinition GraphMapping ausgelagert (HQ3-Z3 partial); Versions-Split bewusst deferred |
| Datenbankdesign | **A-** | A- | unveraendert |
| Auth & Berechtigungen | **B+** | B+ | unveraendert |
| Rotation-Feature | **B** | B | unveraendert; HQ5-Hooks brauchen Tests (HQ1-Z3) |
| Frontend-Architektur | **B+** | B | Pages OK; `useAdminWorkflowBuilder` 865 → 761 (HQ2-Z3 partial: Reference-Data extrahiert); 3 weitere Sub-Hook-Splits deferred bis Bedarf |
| Testbarkeit | **B-** | C+ | Testcontainers laeuft, neue Integration-Tests in HQ2/HQ3-Z2 — aber HQ5-Hooks ohne Tests druecken die Note (HQ1-Z3) |
| Skalierbarkeit | **B-** | B- | unveraendert |
| Sicherheit | **B+** | B+ | unveraendert |

## Vorschlag fuer Priorisierung

**Reihenfolge nach Risiko × Aufwand:**

1. **LQ3-Z3** — Doku-Drift `Code-Review-Status.md` aktualisieren. **30 min, im selben Schritt wie diese Review.**
2. **HQ1-Z3** — Tests fuer `useRotationOperationsView` + `useRotationStationForm`. Schuetzt frische HQ5-Investition. **1–2 d.**
3. **LQ1-Z3** — Timeout-Wrapper fuer `RotationNotificationHostedService`-Sweep. **30 min, sichtbarer Operability-Gewinn.**
4. **HQ3-Z3** — `WorkflowDefinitionAdminOperations.cs` (1430) + `TaskTemplateAdminOperations.cs` (1017) splitten. **1–2 d pro Datei.**
5. **HQ2-Z3** — `useAdminWorkflowBuilder` in Sub-Hooks zerlegen. **2–3 d. Hoeheres Regressions-Risiko, gut planen.**

LQ-Punkte (LQ2-Z3 EntraSync, LQ4-Z3 AdminConfigPage) bleiben Beobachtung. Kein Bedarf jetzt.

**Bewusst nicht aufgenommen**: Der Agent hatte eine Transaktions-Luecke in `WorkflowRuntimeService.CreateWorkflowAsync` gemeldet. **Falsch-positiv**: `PostgresWorkflowRuntimeRepository.CreateWorkflowDefinitionInstance` (Z. 152) oeffnet `BeginTransactionAsync` — Workflow + Tasks + Assignments werden atomar erzeugt. Verifikation: `grep BeginTransaction` in der Repo-Methode + Read der Implementation. Trust but verify.

---

# Code Review — Zyklus 4 (2026-05-02)

**Reviewer**: Claude (Opus, hohe Reasoning-Tiefe)
**Scope**: Frischer Sweep nach Zyklus 3, plus expliziter Naming-/Lesbarkeits-Audit (vom Nutzer angefragt).
**Methode**: Explore-Agent fuer breite Suche, dann manuelle Verifikation jeder Behauptung. **Drei Agent-Befunde als Falsch-positiv verworfen**: (1) "GetAdminWorkflowDefinitionVersionDetailById mutiert" — falsch, ist read-only; nur der Wrapper `GetOrCreateAdminWorkflowDefinitionWorkingDraft` mutiert (und der hat den Mutations-Hinweis schon im Namen). (2) "tt/pt/wta sind C#-Variablen" — falsch, sind SQL-Tabellen-Aliase in `@"..."`-Strings; standard SQL-Konvention. (3) "ParseDecisionCondition braucht Rename" — Name ist OK; nur Error-Kontext fehlt (separate LQ-Note).

## Was sich seit Zyklus 3 strukturell veraendert hat

- TaskTemplate-Familie sauber 3-fach gesplittet (HQ3-Z3): Templates 1017→391, Conditions 251, Dependencies 391.
- WorkflowDefinitionGraphMapping ausgelagert (HQ3-Z3): 1430→1103, GraphMapping 350.
- `useAdminWorkflowVersionReferenceData` extrahiert (HQ2-Z3): Main-Hook 865→761, Reference-Data 140.
- HQ5-Hooks haben jetzt Test-Coverage (HQ1-Z3): +32 Tests, gesamt 172.
- RotationNotification Sweep hat 2-h-Timeout (LQ1-Z3).

## Neue Befunde (CRITICAL)

Keine.

## Neue Befunde (HIGH)

### HQ1-Z4 — `ProcessTypeKey`/`processType` als Legacy-Naming-Drift in der API

`CreateWorkflowRequest` (`api/API/Contracts/WorkflowDtos.cs:124`) und `CreateRotationPlanRequest` (`api/API/Contracts/WorkflowDtos.cs:491`) enthalten ein Feld `ProcessTypeKey`, obwohl das Backend laengst auf `workflow_definitions.definition_key` umgestellt ist. Vier Endpoint-Dateien (`AdminRuntimeConfigEndpoints`, `WorkflowEndpoints`, `WorkflowLinkEndpoints`, `WorkflowMasterDataEndpoints`) propagieren es weiter. Im Runtime-Repo wird intern explizit `PrimaryLegacyProcessTypeKey` verwendet — die Doppel-Terminologie ist im Code sichtbar, im API-Vertrag aber nicht.

**Risiko**: Neue Devs fragen sich, was der "richtige" Wert ist. Frontend-Forms zeigen "Prozesstyp" und "Workflow-Definition" je nach Datei. Ein Migrations-Schritt "rename processTypeId to workflowDefinitionId" ist mehrdeutig.

**Pruefung**:
- (a) Field-Rename `ProcessTypeKey` → `LegacyProcessTypeKey` (signalisiert Deprecation).
- (b) Methoden-Rename in `PostgresRepositorySharedHelpers.ResolveWorkflowDefinitionLegacyProcessTypeId` ist bereits explizit (gut!) — diese Konvention auch ins DTO ziehen.
- (c) ARCHITECTURE-Doku: kurze Compatibility-Matrix dokumentieren — welche Endpoints akzeptieren welche Keys, Deprecation-Termin.

**Aufwand**: 2–3 h Code-Rename + Doku. Mechanisch (Find/Replace + DTO-Property + alle Callsite-Bindings). Risiko niedrig — Frontend-DTO muss nachgezogen werden.

### HQ2-Z4 — `responsibilityIds` ohne Kontext-Praefix in vielen Methoden-Signaturen

`TaskApplicationService.GetTasksAsync`, `PostgresWorkflowRepository.GetTasksForUser`, `GetTasksForUserNarrowed`, `WorkflowTaskListNarrowingFilter(long UserId, int[] ResponsibilityIds)` — ueberall heisst der Parameter generisch `responsibilityIds` oder `ResponsibilityIds`. Aber: das sind **immer** die `EffectiveResponsibilities` des Nutzers (Vereinigung aus DirectResponsibilities + GroupResponsibilities), nicht eine beliebige Liste. Aktuell muss man die Aufrufkette oben in `TaskApplicationService.cs:26` lesen, um das zu wissen.

**Risiko**: Bei Erweiterung (z. B. neuer Endpoint mit explizitem `ResponsibilityFilter`) verwechselt jemand die Semantik und uebergibt die falsche Liste — Tasks werden mit ueberbreitem Filter geladen.

**Pruefung**: Rename `responsibilityIds` → `effectiveResponsibilityIds` an allen ~6 Methoden-Signaturen + Struct-Field. Pure mechanische Aenderung.

**Aufwand**: 1.5–2 h. Ueberall verteilt, aber linear durchziehbar.

## Neue Befunde (LOW)

### LQ1-Z4 — `GetOrCreateAdminWorkflowDefinitionWorkingDraft` nach .NET-Konvention `EnsureAdminWorkflowDefinitionWorkingDraft`

Der Name `GetOrCreate…` ist klar (besser als reines `Get…`), aber .NET-Konvention waere `Ensure…` (vgl. `EnsureCreated`, `EnsureSuccessStatusCode` im BCL). **Marginaler Wert** — die aktuelle Form ist explizit genug. Nur umbenennen wenn ohnehin am Repository gearbeitet wird.

**Aufwand**: 30 min (Interface + Impl + 1 Caller). **Defer.**

### LQ2-Z4 — `ParseDecisionCondition`-Exception ohne Workflow-Kontext

`PostgresWorkflowRuntimeRepository.ParseDecisionCondition` wirft `Decision condition is not valid JSON` ohne Workflow-ID, Edge-ID oder Source-Node. Bei Produktions-Troubleshooting muss DevOps die Quell-Edge manuell finden.

**Pruefung**: Workflow-ID + Source-/Target-Node-Key in die Exception-Message aufnehmen. Aufrufer der Methode kennen den Edge-Kontext.

**Aufwand**: 30 min. Keine Test-Aenderung noetig.

### LQ3-Z4 — Sub-Section-Error-Boundaries fehlen in grossen Pages

`AppErrorBoundary` (LQ5-Zyklus-2) wrappt jede Route. Aber innerhalb von `AdminConfigPage.tsx` (423 Zeilen, 7 Sub-Sections) oder `WorkflowDetailPage` gibt es keine Sub-Section-Boundary. Ein Render-Fehler in einer Sub-Section killt die ganze Page (statt nur die Sektion).

**Pruefung**: 2-3 zusaetzliche `<AppErrorBoundary scope="...">`-Wraps um die wichtigsten Sub-Sections.

**Aufwand**: 1–2 h. Defensive Coding, nice-to-have.

### LQ4-Z4 — `MatchesTaskAssignment` Predicate-Name liest mehrdeutig

Der Name verraet nicht, was "matches" bedeutet (User-ID? Responsibility-ID? Beides?). In der Praxis: User-ID-Match ODER Responsibility-ID in EffectiveResponsibilities (siehe `MatchesPrimaryAssignment`-Switch).

**Pruefung**: Rename zu `IsAssignedToCurrentUser` oder `UserOrResponsibilityMatchesAssignment`. **Marginaler Wert** — alle 5 Callsites sind in derselben Datei, das Verhalten ist im Tight-Scope nachlesbar.

**Aufwand**: 30 min. **Defer** bis Auth-Modul ohnehin angefasst wird.

## Naming-/Lesbarkeits-Audit — Gesamteindruck

**Gut:**
- Konventionen sind durchgaengig: `_repository`/`_logger`-Underscore-Praefix, PascalCase fuer Klassen, camelCase fuer Lokale, SQL-Aliase nur in SQL-Strings.
- DTO-Suffix `Dto` immer gesetzt.
- Cross-File-Helper sind seit Z3-Refactor mit "Shared helper — also referenced by ..."-Kommentaren markiert.
- Test-Bezeichner sind beschreibend (`GetTasksForUserNarrowed_ReturnsOnlyMatchingPrimaryAssignmentsOnNonTerminalWorkflows`).
- Variablen-Namen sind ueberwiegend voll ausgeschrieben (`taskTemplate` statt `tt` in C#-Variablen).

**Verbesserungswuerdig** (in HQ1-Z4 + HQ2-Z4 erfasst):
- Legacy-Begriff `processType` parallel zu `workflowDefinition` ohne explizite Deprecation-Markierung.
- Generic Param-Namen (`responsibilityIds`) ohne Kontext-Praefix.

**Falsch-Positive aus dem Agent-Bericht** (bewusst nicht aufgenommen):
- `tt`/`pt`/`wta` SQL-Aliase: Standard-Konvention in handgeschriebenem SQL; rename wuerde nur die SQL-Strings aufblaehen ohne Lesbarkeits-Gewinn.
- `GetAdminWorkflowDefinitionVersionDetailById` als "mutating" geflaggt: read-only verifiziert; nur der Wrapper `GetOrCreate…` mutiert (Name ist explizit).
- `NormalizeNullableText`: "normalize" ist im Codebase-Vokabular etabliert ("trim + null-coalesce" ist die kanonische Bedeutung); keine Aenderung noetig.

## Verbleibende Punkte aus aelteren Zyklen

- **L2** (Datenbereinigung): unveraendert deferred.
- **R8** (Form-Editor Browser-Verifikation): unveraendert offen — Nutzer-Aufgabe.
- **R10** (Mobile-Layout): backlog.
- **LQ2-Z3** (`EntraDirectorySyncService` Split): bleibt deferred — kein konkreter Anlass.
- **LQ4-Z3** (`AdminConfigPage` Watch): unveraendert 423 Zeilen — noch nicht ueber 500-Schwelle.

## Aktualisierte Gesamtbewertung

| Bereich | Note (Zyklus 4) | Note (Zyklus 3) | Hauptbegruendung |
|---------|----------------|-----------------|------------------|
| Backend-Architektur | **A-** | A- | unveraendert (TaskTemplate + GraphMapping seit Z3 sauber) |
| Datenbankdesign | **A-** | A- | unveraendert |
| Auth & Berechtigungen | **B+** | B+ | unveraendert; HQ2-Z4 Naming-Verbesserung empfohlen |
| Rotation-Feature | **B+** | B | HQ5-Hooks jetzt mit Test-Coverage; Sweep-Timeout (LQ1-Z3) verbessert Operability |
| Frontend-Architektur | **B+** | B+ | unveraendert seit HQ2-Z3 |
| Testbarkeit | **B** | B- | HQ5-Hooks-Tests (+32) erhoehen Coverage messbar |
| Skalierbarkeit | **B-** | B- | unveraendert |
| Sicherheit | **B+** | B+ | unveraendert |
| Lesbarkeit (NEU) | **B+** | — | Konventionen durchgaengig; HQ1-Z4 + HQ2-Z4 als gezielte Verbesserungen |

## Vorschlag fuer Priorisierung

**Reihenfolge nach Risiko × Aufwand:**

1. **HQ1-Z4** — `ProcessTypeKey` → `LegacyProcessTypeKey` Rename + Compat-Doku. **2–3 h.** Hoher Klarheits-Gewinn fuer neue Devs.
2. **HQ2-Z4** — `responsibilityIds` → `effectiveResponsibilityIds` Rename. **1.5–2 h.** Mechanisch.
3. **LQ2-Z4** — `ParseDecisionCondition`-Error-Kontext. **30 min.** Operability-Win.
4. **LQ3-Z4** — Sub-Section-Error-Boundaries. **1–2 h.** Defensive Coding.
5. **LQ1-Z4** + **LQ4-Z4** — Naming-Refinements. **defer** bis ohnehin am Code gearbeitet wird.

Watch-Items unveraendert: LQ2-Z3 EntraSync, LQ4-Z3 AdminConfigPage.

**Bewertung Zyklus 4**: Code ist strukturell und stilistisch in gutem Zustand. Die verbleibenden Verbesserungen sind Komfort-Aenderungen, keine Risk-Mitigations.
