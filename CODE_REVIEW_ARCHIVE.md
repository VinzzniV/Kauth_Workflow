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

## Zyklen 8 bis 13 — aus der aktiven Review-Datei ausgelagert (2026-05-07)

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
