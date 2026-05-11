# CODE_REVIEW_ARCHIVE.md

Archiv fuer abgeschlossene, detaillierte Review-Zyklen.

Die aktive Primaerquelle fuer den aktuellen Review-Fokus bleibt `CODE_REVIEW.md`.

---

## Enthalten

- detaillierte Slice-Historie abgeschlossener Zyklen
- laengere Befund- und Umsetzungsdokumentation, die fuer die aktuelle Arbeitssteuerung nicht mehr im aktiven File stehen muss

## Nicht verwenden fuer

- naechsten Arbeitsschritt
- aktuelle Priorisierung
- kurzfristigen Session-Fokus

---

## Zyklen 8 bis 20 — aus der aktiven Review-Datei ausgelagert

### Zyklus 20 — Admin/Directory/Runtime Read Contracts Phase 2

- Status: abgeschlossen am 2026-05-11.
- Ergebnis: die nach Z10/Z11 zurueckgestellten Read-Vertraege fuer Admin-/Directory-/Runtime-Pfade sind vollstaendig auf die etablierten P1-/P2-/P3-Muster gezogen worden.
- Praktisch: offene Mischvertraege und ungecursorte Runtime-Subresources sind aus dem aktiven Backlog verschwunden; das FE liest die neuen Hulls ueber Adapter weiter kompatibel.

#### Z20-S1 — Inventur + Slice-Plan (done 2026-05-11)

- Breiter Folgezyklus statt Mikro-Slices festgelegt; B1 bis B4 als Bundle-Reihenfolge dokumentiert.
- Praktisch: gleicher Vertragsumbau wurde einmal sauber geplant, statt denselben Doku- und Review-Overhead pro Endpunkt zu wiederholen.

#### Z20-B1 — P3-Lookups (done 2026-05-11)

- `GET /workflow-definitions/startable` sowie die Notification-Preview-Target-Lookups fuer Workflows und Rotation-Plans auf `search`/`limit`-Lookup-Adapter gezogen.
- Praktisch: Typeahead-faehige Lookups mit einheitlichem P3-Vertrag.

#### Z20-B2 — Directory-Reads (done 2026-05-11)

- `/admin/directory/identities` auf P1 umgestellt; `/admin/directory/responsibility-gaps` und `/admin/directory/pending-imports` liefern jetzt saubere P1-Listen.
- Praktisch: der alte Composite-Mischvertrag ist aufgeloest; Directory-Folgearbeit haengt an konsistenten Listenendpunkten.

#### Z20-B3 — Templates + Runtime-Subresources (done 2026-05-11)

- `/admin/notification-templates` und `/admin/rotation/action-templates` auf P1; Runtime-Events und Runtime-Automation-Jobs auf P2-Cursor umgestellt.
- Praktisch: wachsende Settings-Listen und laengere Runtime-Verlaeufe schneiden nicht mehr still an hartem Listenformat ab.

#### Z20-B4 — Auth-/Identity-Listen (done 2026-05-11)

- `/admin/auth/{users,roles,groups,permissions}` liefern P1; `/admin/directory/unlinked-identities` nutzt den P1-Query-Parser.
- Praktisch: der verbleibende Admin-Identity-Block folgt jetzt demselben Listenvertrag wie die uebrigen Admin-Reads.

#### Verifikation

- `dotnet test api/API.Tests/API.Tests.csproj --artifacts-path .codex-artifacts` → 566 bestanden, 1 uebersprungen.
- `npm run build` in `web/` → erfolgreich.

### Zyklus 19 — Backend Full Review / Holistic Audit

- Status: abgeschlossen am 2026-05-11.
- Ergebnis: vollstaendiger Backend-Audit ueber Endpoints, Repositories, Services, Authorization/Auth, `Services/Directory/`, Background-Jobs, Schema-/Migrations-Hygiene und Test-Coverage; alle Umsetzungsslices S2..S9 im selben Tag abgearbeitet.
- Praktisch: mehrere teure Betriebsrisiken sind aus dem System entfernt worden, bevor sie als wiederkehrende Hotfix-Klasse haengen bleiben konnten.
- Wichtige Leitplanken: kein breiter Architektur-Umbau am Definition-/Runtime-/Automation-Layer, keine FE-Nacharbeit, keine Berechtigungslogik-Aenderung ohne konkreten Befund.

#### Z19-S1 — Audit-Pass (done 2026-05-11)

- Findings: H1 Sweep-Timeout fehlt in `DirectorySyncHostedService`; H2 keine Schema-Paritaetspruefung zwischen `db/manual/` und `db/01_schema.sql`; H3 fehlende `CancellationToken`-Propagation in Lifecycle-/Runtime-Pfaden; M1 `SystemEventLogService`; M2 AuthZ-Repo-Monolith; M3 Automation Failure-of-Failure; M4 Deferred-Verdikt; L1-L4 Hygiene.
- Praktisch: der Audit hat die Resthebel nicht abstrakt, sondern direkt entlang realer Betriebs- und Lastpfade geschnitten.

#### Z19-S2 — Sweep-Timeout (done 2026-05-11)

- `DirectorySyncHostedService` hat jetzt einen 2h-Sweep-Timeout analog `RotationNotificationHostedService`, inkl. Warn- und Hosted-Log bei Timeout.
- Praktisch: ein haengender Directory-Sync blockiert den Loop nicht mehr bis zum Restart.

#### Z19-S3 — Schema-Paritaet + L1 (done 2026-05-11)

- `db/manual/manifest.json`, `db/manual/README.md` und `SchemaParityTests.cs` eingefuehrt; stray-Verzeichnis `db/init/prod;C/` entfernt.
- Praktisch: manuelle DB-Helfer muessen jetzt ihren Soll-Endzustand sichtbar in `db/01_schema.sql` hinterlassen, sonst wird der Test rot.

#### Z19-S4 — CancellationToken-Propagation + L4 (done 2026-05-11)

- Lifecycle-/Runtime-Services, relevante Repository-Vertraege und betroffene Endpoints tragen den Request-Abbruch jetzt durch.
- Praktisch: HTTP-Abbrueche lassen deutlich weniger zombieartige DB-Transaktionen und Reader offen.

#### Z19-S5 — SystemEventLogService (done 2026-05-11)

- UndefinedTable-Stille entfernt, Cursor-Pagination nach Z11-Pattern eingefuehrt, 67 Unit-Tests fuer Redaction/Normalization/Filter ergaenzt.
- Praktisch: Admins sehen Schema-Drift jetzt als Fehler statt als leere Eventliste; Listen wachsen kontrollierter.

#### Z19-S6 — AuthZ-Repo-Split (done 2026-05-11)

- `PostgresUserAuthorizationRepository.AdminOperations.cs` und `...AdminReadOperations.cs` nach Z9-Pattern in fokussierte Department/Position/Responsibility/User/Group- und Read-Partials zerlegt.
- Praktisch: Review-, Merge- und Folgeaenderungen im Berechtigungsbereich landen nicht mehr in zwei sehr grossen Sammeldateien.

#### Z19-S7 — Automation Failure-of-Failure (done 2026-05-11)

- `UnclaimAutomationJobAsync` eingefuehrt; der Worker gibt Jobs bei scheiterndem Failure-Pfad wieder frei.
- Praktisch: ein transienter DB-Fehler im Fehlerpfad blockiert Automationsjobs nicht mehr dauerhaft.

#### Z19-S8 — Deferred-Verdikt (done 2026-05-11)

- `Z8-3.2/#8` und `Z16-S4` formal weiter deferred, jeweils mit Begruendung.
- Praktisch: die Watchouts bleiben bewusst sichtbar, aber nicht mehr als unklare „vielleicht spaeter“-Reste.

#### Z19-S9 — Hygiene-Batch (done 2026-05-11)

- `AdminRuntimeHealthService` Parallelitaet bereinigt, Tombstone-Testdatei entfernt, Rest-Hygiene abgeschlossen.
- Praktisch: weniger Rauschen im Code und klarerer Betriebsblock ohne semantische Aenderung.

### Zyklus 16 — Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag

- Status: abgeschlossen am 2026-05-08. Z16-S4 deferred (Produkt-Entscheidung Snapshot-Persistenz ausstehend).
- Ergebnis: `/people` als neue Listenseite mit Navigationseintrag fuer HR + Admin; `CanAccessPeopleDirectory`-Policy; `/people/search` von `CanCreateWorkflow` auf `CanAccessWorkflowOverview` korrigiert; `GET /admin/people` Admin-Listenendpunkt; `PeopleDirectoryPage` mit Suche (debounced, URL-param `q`), paginierter Tabelle (50/Seite), Status-Badge, Verzeichnis-Link-Status; Breadcrumb in `PersonWorkflowHistoryPage` differenziert nach Feature-Zugang (HR/Admin → `/people`, Manager/Reader → `/search`).
- Praktisch: HR und Admins koennen jetzt direkt ueber „Mitarbeiter" im Header zur Personenbestandsliste navigieren. Vorher gab es keinen Navigationseinstieg — die 360°-Akte war nur ueber indirekte Links aus Workflows oder dem Rotationsplan erreichbar.
- Wichtige Leitplanken: Person = fachlicher Anker, `directoryIdentityId` = technische Entra-Identity — nie vermischen; Backend ist Source of Truth; Permission-Vertrag zuerst im BE, dann FE.

#### Z16-S1 — Inventur + Vertragsentscheidung (done 2026-05-08)

Vollstaendige Inventur: kein Navigationseintrag fuer `/people/:personId`; `/people/search` an `CanCreateWorkflow` gehaengt (semantisch falsch); kein `snapshotAt`-Zeitstempel fuer Automation-Snapshots. Vertragsentscheidungen: eigene `/people`-Listenseite + Navigationseintrag fuer HR + Admin; neues FE-Feature `peopleDirectory`; BE-Policy `CanAccessPeopleDirectory`; Automation-Snapshot-Zeitstempel deferred (S4). Kein Code-Change in S1.

#### Z16-S2 — BE-Permission-Vertrag + Admin-People-Endpunkt (done 2026-05-08)

`CanAccessPeopleDirectory`-Policy (Admin + HR); `/people/search` von `CanCreateWorkflow` auf `CanAccessWorkflowOverview` umgestellt (breaking-risk geprueft — Praxisfaelle nicht betroffen); `GET /admin/people` P1-Listendpunkt mit Server-Suche und Pagination.

#### Z16-S3 — FE-Navigation + PeopleDirectoryPage + Nav-Eintrag (done 2026-05-08)

Neues `AppFeature "peopleDirectory"` in `roleModel.ts` (HR + Admin). Neue `PeopleDirectoryPage` unter `/people`. Navigationseintrag "Mitarbeiter" fuer HR + Admin im Header + Dashboard. Breadcrumb in `PersonWorkflowHistoryPage` differenziert. Neuer Query-Key `people.directory`, Hook `usePeopleDirectory`, API-Funktion `getPeopleDirectory`. Lint-Status: 2 pre-existing Fehler aus Z15, keine neuen. Build: gruen, 5.15s.

#### Z16-S4 — Automation-Snapshot-Vertrag formal (deferred)

`AutomationPersonIdentitySnapshotDto` + `snapshotAt`-Feld + Audit-Eintrag bei Automation-Start. Wartet auf Produkt-Entscheidung, ob Snapshot in `workflow_automation_jobs` persistiert wird.

---

### Zyklus 15 — Implementierung Mehrrollen-Persona

- Status: abgeschlossen am 2026-05-08.
- Ergebnis: Mehrrollen-Nutzer fallen nicht mehr auf `generic`; Login-Routing, Dashboard-Sicht und Persona-Switcher lesen denselben Vertrag fuer die aktive Ansicht.
- Praktisch: Power-User mit mehreren Rollen sehen wieder eine sinnvolle Startansicht statt eines leeren Generic-Dashboards und koennen zwischen ihren zulaessigen Personas umschalten.
- Wichtige Leitplanken: Rechte, Capabilities, Header-Navigation und Routen-Guards bleiben unberuehrt; geaendert wurde nur die sichtbare Persona-Steuerung.

#### Z15-S1 — Datenmodell `aktive Ansicht`

- Neuer Hook `useActiveView` mit Fallback-Kaskade `localStorage -> Default-Persona -> generic`.
- Persistenzschluessel: `kauth.activeView.<username>`.
- Praktisch: Die sichtbare Persona hat jetzt eine einzige, testbare Quelle statt verteilter Sonderfaelle.

#### Z15-S2 — Override-Stellen auf `useActiveView`

- `useRoleAwareNavigation.ts` liest die Dashboard-Persona aus `activeView` statt aus einem Mehrrollen-Shortcut auf `generic`.
- `roleModel.ts` `getDefaultRoute` akzeptiert die aktive Persona und leitet Mehrrollen-Nutzer damit wieder passend weiter.
- Follow-up: `dashboardContext` aus dem `hasMultipleRoles`-Sonderfall geloest, damit Titel und Kontext wieder zur aktiven Ansicht passen.
- Praktisch: Sicht und Login laufen wieder zusammen; Mehrrollen-Nutzer sehen nach dem Start die erwartete Persona statt einer generischen Leerseite.

#### Z15-S3 — Persona-Switcher

- `PersonaSwitcher` wird nur fuer `hasMultipleRoles === true` gerendert.
- Die verfuegbaren Optionen werden direkt aus den Capabilities abgeleitet und ueber `setActiveView` gespeichert.
- Praktisch: Nutzer muessen nicht auf eine starre Vorrangsregel festgelegt bleiben, sondern koennen innerhalb ihrer erlaubten Rollenansichten explizit umschalten.

### Zyklus 14 — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung

- Status: abgeschlossen am 2026-05-07.
- Ergebnis: Problem sauber inventarisiert, Persona-/Sicht-Vertrag fuer Mehrrollen definiert und Folge-Slice-Plan fuer die Implementierung geschnitten.
- Praktisch: Mehrrollen-Nutzer wie Admin+HR oder Manager+Worker verlieren heute ihre fachlich erwartete Sicht, obwohl Rechte und Navigation weiter vorhanden sind; der Folgezyklus hat jetzt einen klaren Plan, wie diese `generic`-Falle ohne Berechtigungsumbau entfernt wird.
- Wichtige Leitplanken: Z14 war Doku-only; Rechte, Capabilities, Header-Navigation und Routen-Guards bleiben unberuehrt; es geht nur um die Ableitung der **Sicht**.

#### Z14-1.1 — Inventur Persona-/Mehrrollen-Kollisionen

- Override-Punkte: `web/src/navigation/useRoleAwareNavigation.ts:253` kollabiert Mehrrollen-Sicht auf `generic`; `web/src/auth/roleModel.ts:210` zwingt Mehrrollen-Login auf `/`.
- Betroffene Sicht-Konsumenten: `DashboardOverview`, `DashboardPage`, `useDashboardInsights`/Query-Key und der Insight-Lader-Switch.
- Praktisch: Admin-Betriebsblock, HR-/Manager-/Worker-spezifische Insight-Bloecke und passende Seitenkopf-Texte fallen bei Mehrrollen weg; der Nutzer landet auf einer leeren Generic-Seite.
- Wichtig: Header-Navigation, Schnellaktionen und Rechte sind **nicht** betroffen; das Problem sitzt in der Sicht, nicht in den Capabilities.

#### Z14-1.2 — Vertrags-/UX-Entscheidung

- Verbindliche Begriffstrennung:
  - **Rolle** = Berechtigung / Capability-Quelle
  - **Persona** = deterministischer Default aus der Vorrangskette `admin > hr > manager > worker > reader > generic`
  - **aktive Ansicht** = vom Nutzer beeinflussbare, persistierte Sicht-Praeferenz
- Optionen geschnitten: Persona-Switcher mit Persistenz, Aggregations-Persona, Admin-Vorrang fuer Admin+X, explizite Login-Auswahl.
- Vorzugsrichtung: Persona-Switcher mit Vorrangs-Default + Persistenz; Admin-Vorrang fuer Admin+X als Default-Regel; harter Fallback auf `generic`.
- Vertragspflichten: sichtbarer Schalter nur fuer Mehrrollen-Nutzer; Persistenz per `localStorage` mit person-/user-gebundenem Schluessel; Fallback-Kaskade aktive Ansicht -> Default -> `generic`; Login-Routing und Dashboard-Sicht muessen beide aus derselben Quelle lesen.

#### Z14-1.3 — Slice-Plan Folgezyklus

- **Slice I**: neues Datenmodell fuer `aktive Ansicht` als Hook mit Persistenz und Fallback; keine Sicht-Konsumenten anfassen.
- **Slice II**: die zwei Override-Stellen aus Z14-1.1 gemeinsam auf die neue Quelle umstellen, damit Sicht und Login-Routing wieder zusammenlaufen.
- **Slice III**: sichtbarer Persona-Switcher nur fuer `hasMultipleRoles === true`.
- Reihenfolge-Begruendung: Datenmodell zuerst, dann Routing/Sicht, dann UI. Das ist sicherer als ein grosser UI-Umbau, weil Persistenz, Routing-Regression und UI-/A11y-Risiken getrennt verifiziert werden koennen.
- Empfohlenes Modell/Effort fuer die Implementierung: `claude-sonnet-4-6` mit `--effort medium` pro Slice; Slice III darf bei breiterer UX-Abwaegung auf Opus eskalieren.

### Zyklus 13 — Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block

- Status: abgeschlossen am 2026-05-06.
- Ergebnis: `GET /admin/runtime-health` wurde um optionale Linux-Host-/VM-Metriken erweitert; sichtbarer Host-/VM-Block im Admin-Dashboard.
- Praktisch: Admins sehen RAM-, Root-FS- und Load-Signale direkt im Dashboard statt erst nach Server-Login.
- Wichtige Leitplanken: nur Linux-Host-Sicht, kein Docker-/Prometheus-Ausbau, explizite Aktivierung ueber `HOST_RUNTIME_HEALTH_ENABLED`.

### Zyklus 12 — Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health

- Status: abgeschlossen am 2026-05-06.
- Ergebnis: neuer Admin-Runtime-Health-Vertrag und sichtbarer Betriebsstatus-Block fuer App-/Runtime-Signale.
- Praktisch: API-, DB-, Directory-, Mail- und Storage-Signale sind fuer Admins im Dashboard gebuendelt sichtbar.
- Wichtige Leitplanken: klare Trennung App/Runtime vs. Host/VM; Host-Metriken waren bewusst noch nicht Teil von Z12.

### Zyklus 11 — Admin-/Master-Data-Listen-Vertraege in Umsetzung

- Status: abgeschlossen am 2026-05-06.
- Ergebnis: gemeinsame P1-/P2-Huellen fuer Master-Data-, Audit- und Builder-Lesepfade ausgerollt.
- Praktisch: Listen-, Audit- und Builder-Reads wachsen jetzt ueber konsistente Antwortformen statt ueber ad-hoc Arrays.
- Wichtige Leitplanken: Reihenfolge F1 → F2 → F3; keine parallele Vertragsvielfalt fuer dieselbe Listenklasse.

### Zyklus 10 — Master-Data-/Admin-Listen-Wachstum und Query-Kontrakt-Risiken

- Status: abgeschlossen am 2026-05-05.
- Ergebnis: Inventur, Vertrags-Skizze und Slice-Plan fuer die spaetere Umsetzung in Z11.
- Praktisch: die systematische Read-Vertragsgrenze wurde sauber beschrieben, bevor neue Pagination-/Search-Huellen gebaut wurden.
- Wichtige Leitplanken: P1 fuer offset/limit-Listen, P2 fuer Cursor-/Audit-Streams, keine Write-Pfad-Umbauten.

### Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit

- Status: abgeschlossen am 2026-05-05.
- Ergebnis: Graph-, Orchestrierungs- und Batch-Grenzen aus dem grossen Sync-Service extrahiert und testseitig abgesichert.
- Praktisch: Directory-Sync-Aenderungen sind besser isolierbar und billiger zu testen.
- Wichtige Leitplanken: kein reiner Clean-Code-Split, sondern Testbarkeits- und Wartbarkeitsarbeit an einem kritischen Timer-Pfad.

### Zyklus 8 — Skalierbarkeits- und Last-Haertung

- Status: abgeschlossen am 2026-05-05.
- Ergebnis: priorisierte Hotspots in Workflow-Katalog, Notification-Sweep, Recipient-Bulk-Lookup und Directory-Sync gebatcht oder verifiziert; `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` bewusst deferred.
- Praktisch: mehrere Lastpfade skalieren besser und verursachen weniger N+1- oder Sweep-Backlog-Risiko.
- Wichtige Leitplanken: Fokus lag auf echten Lasthebeln; kein breiter Strukturumbau ohne klaren Runtime-Nutzen.

---

## Zyklus 7 — Lifecycle-Service-Konsolidierung (Detailarchiv)

### Z7-1 Lifecycle-Service-Konsolidierung (HIGH)

**Befund (nach Z7-1.1 Inventur, 2026-05-05).** Nach Abschluss von Schritt 7 ist `WorkflowLifecycleService` (`api/API/Services/WorkflowLifecycleService.cs`, 79 Z.) ein duenner Wrapper im Mid-State. Detail-Inventur in `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md` § 12.

- Vier Methoden (`CreateWorkflowInstanceAsync`, `CompleteFormNodeAsync`, `CompleteApprovalNodeAsync`, `CompleteTaskNodeAsync`) sind reine Pass-Throughs an `IWorkflowDefinitionRuntimeRepository`. Conn+Tx wird im Repo geoeffnet, nicht im Service. Einziger produktiver Aufrufer: `WorkflowDefinitionRuntimeService.CompleteAndDispatchAsync` (Notification-Dispatch + PersonLifecycleProjection bewusst ausserhalb der Tx).
- Drei statische Repo-Aufrufe (`PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide`, `TryAdvanceSetupNodeIfReady`, `ApplyApprovalNodeDecision`) im Service-Body (`WorkflowLifecycleService.cs:20/22/45`) lassen den Service an einer konkreten Repo-Klasse haengen.
- Dieselben drei statischen Aufrufe stehen ein zweites Mal in `PostgresWorkflowRepository.TaskOperations.cs:84/86/158`. **Aufgeloest:** Endpoint-Pfad ist bereits ueber `TaskApplicationService` am Lifecycle-Service; produktive Aufrufer von `repository.UpdateTaskStatus` / `DecideTaskApproval` existieren nicht mehr — nur Tests rufen sie noch (z. B. `PostgresWorkflowRepositoryConcurrencyTests`).
- Symmetrisch gilt das fuer `PostgresWorkflowRepository.AutomationOperations.cs:100` (`CompleteAutomationJobSuccess`-Wrapper): `WorkflowAutomationService` geht ueber `lifecycleService.OnAutomationJobCompletedAsync`, der Wrapper hat keinen produktiven Aufrufer mehr.
- Vor-Code in `PostgresWorkflowRuntimeRepository.cs:487-512` (Approval Task-Status-Sync + Audit) und `:547-572` (Task-Status-Sync + Audit) wandert mit Z7-1.2 mit. Wird das uebersehen, drohen doppelte Audit-Eintraege oder verlorene `task_status_changed`-Events.

**Wirkung.** Lifecycle-Service besitzt Conn+Tx fuer Task-Status, Approval und Automation (3 Pfade) — nicht fuer Definition-Runtime (4 Pass-Throughs).

**Slice-Plan (nach Inventur reordered):**
- ✅ 7.1.1 Inventur: erledigt. Ergebnis: §12 in der S7-Skizze.
- 7.2 (vorgezogen) Test-Coverage als Sicherheitsnetz, **bevor** die Conn+Tx-Bewegung greift.
- ✅ 7.1.2 Definition-Runtime-Mutationen ueber Scoped-Repo-Vertrag gefuehrt; neuer `IWorkflowDefinitionRuntimeScopedRepository`, Vor-Code aus `PostgresWorkflowRuntimeRepository.cs:487-512/547-572` bleibt im Runtime-Repo, aber unter Lifecycle-owned Tx. `WorkflowRuntimeService.CreateWorkflowAsync` routed jetzt ebenfalls ueber `IWorkflowLifecycleService`.
- ✅ 7.1.3 Statische Runtime-Helfer (`CompleteTaskNodeRuntimeSide`, `TryAdvanceSetupNodeIfReady`, `ApplyApprovalNodeDecision`) als `*InScope`-Instanzmethoden in `IWorkflowDefinitionRuntimeScopedRepository`/`IWorkflowLifecycleScopedRepository`. Lifecycle-Service haengt nur noch an Interfaces.
- ✅ 7.1.4 Dupletten in `TaskOperations.cs` (`UpdateTaskStatus`/`DecideTaskApproval`) und `AutomationOperations.cs` (`CompleteAutomationJobSuccess`-Wrapper) entfernt; `*ByRef`-Pfade rotation-only; Tests auf `WorkflowLifecycleService` migriert.
- ✅ 7.1.5a Public non-scope Wrapper (`CreateWorkflowDefinitionInstance`, `CompleteRuntimeFormNode`, `CompleteRuntimeApprovalNode`, `CompleteRuntimeTaskNode`) aus `PostgresWorkflowRuntimeRepository` + `IWorkflowDefinitionRuntimeRepository` entfernt; Integrationstests rufen jetzt `WorkflowLifecycleService` direkt; `StubWorkflowLifecycleService` in `WorkflowEndpointsTests` liefert das gestubte Create-Result selbst.
- 7.1.5b Inhalte der `*InScope`-Methoden physisch in den Lifecycle-Service ziehen, Scoped-Vertrag aufloesen. Aufgeteilt in:
  - ✅ 7.1.5b.i `CompleteRuntimeFormNodeInScope` in den Service gezogen (2026-05-05); Helper `LoadRuntimeWorkflowHeader`/`LoadNodeExecutionForUpdate`/`EnsureActiveRuntimeNode`/`CompleteRuntimeFormNodeInternal`/`GetWorkflowDefinitionRuntimeDetailInternal` auf `internal static`.
  - ✅ 7.1.5b.ii `CompleteRuntimeApprovalNodeInScope` in den Service gezogen (2026-05-05); Service injiziert `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService` fuer linked-task Status-Sync; `LoadWorkflowTaskIdByNodeInstanceId` auf `internal static`.
  - ✅ 7.1.5b.iii `CompleteRuntimeTaskNodeInScope` in den Service gezogen (2026-05-05); Service nutzt `IWorkflowAuditWriteOperations` + `IWorkflowStatusCalculationService` fuer linked-task Status-Sync und ruft `CompleteTaskNodeRuntimeSide` direkt; Scoped-Vertrag haelt nur noch `CreateWorkflowDefinitionInstanceInScope`.
  - ✅ 7.1.5b.iv `CreateWorkflowDefinitionInstanceInScope` in den Service gezogen (2026-05-05); Service injiziert `IWorkflowNotificationDispatchOperations` und ruft `LoadPublishedWorkflowDefinitionVersion`/`CreateWorkflowNodeInstance`/`NormalizeRuntimeOptionalText` (auf `internal static` gehoben) direkt; Scoped-Vertrag ist jetzt leer.
  - ✅ 7.1.5b.v Leeren Scoped-Vertrag `IWorkflowDefinitionRuntimeScopedRepository` und DI-Registrierung entfernt (2026-05-05); Lifecycle-Service-Ctor um den ungenutzten Parameter bereinigt; `PostgresWorkflowRuntimeRepository` implementiert nur noch `IWorkflowDefinitionRuntimeRepository`; Test-Stubs entfernt.

### Z7-2 Lifecycle-Service Test-Coverage

`api/API.Tests/WorkflowLifecycleServiceTests.cs` deckt Routing `wf:` vs `rot:`, Rollback bei Fehler im zweiten Lifecycle-Schritt und den Automation-Scope-Pfad ab.

### Z7-3 Validation-Service-Split

Der frühere Monolith `WorkflowDefinitionValidationService.cs` wurde in:
- `WorkflowDefinitionValidationCatalog`
- `WorkflowDefinitionValidationHelpers`
- `WorkflowDefinitionSnapshotValidator`
- `WorkflowDefinitionDraftValidator`

geschnitten. Der Service ist jetzt eine duenne Facade.
