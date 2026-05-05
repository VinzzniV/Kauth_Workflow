# Code Review — kauth_workflow

## Zweck

- aktive technische Review-Priorisierung
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Primaerquelle fuer

- aktuellen Review-Fokus
- Reihenfolge der Nacharbeit
- offene zyklusuebergreifende technische Befunde

## Nicht verwenden fuer

- kurzfristige Session-Notizen
- tiefes Slice-fuer-Slice-History-Studium abgeschlossener Zyklen

Dafuer sind `MEMORY.md`, `CODEX_SYNC.md` und `CODE_REVIEW_ARCHIVE.md` zustaendig.

## Wann aktualisieren

- wenn ein neuer Review-Zyklus eroeffnet wird
- wenn sich Priorisierung oder Folge-Slices aendern
- wenn ein aktiver Zyklus abgeschlossen wird

## Verwandte Dateien

- `TODO.md`
- `MEMORY.md`
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Stand/Code-Review-Status.md`
- `KauthWorkflow/Architektur/Migrationspfad.md`

---

**Stand**: 2026-05-05 — Zyklus 9 abgeschlossen (`EntraDirectorySyncService`-Split / Testbarkeit; Z9-3 Coverage).
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith reduziert; Lifecycle-Service nach Z7 echte Commit-Grenze fuer zentrale Runtime-Mutationen; groesste Resthebel liegen jetzt bei Skalierbarkeit und Lastpfaden |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Mehrere Listen-, Sweep- und Dispatch-Pfade sind noch Kandidaten fuer SQL-Pushdown, Pagination oder N+1-Abbau |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen weniger in Benennung als in Hotspot-Pfaden unter Last |

---

## Abgeschlossener Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

**Status:** abgeschlossen 2026-05-05. Z9-3 Coverage gegen die neuen Interfaces ist gesetzt; Z9 damit geschlossen.

**Thema:** Z8 hat die Lastpfade gehaertet (Bulk-Lookups, Sweep-Batching, Group/Member-Bulk in `EntraDirectorySyncService.SyncAllAsync`). Die offene Grenze aus Z8 ist **nicht** ein neuer Last-Hotspot, sondern die **fehlende saubere Test-Isolation** der neuen Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`): sie sind `private` hinter dem 2.4k-Zeilen-Service, der direkt gegen Live-Graph + DB laeuft. Reflection-Probing waere fragil, ein End-to-End-Test ueber `SyncAllAsync` braucht einen Graph-Stub und einen sauberen Schnitt zwischen Orchestrierung, Graph-Zugriff und DB-Batch-Operationen.

LQ2-Z3 (`EntraDirectorySyncService` File-Split, 2591 Zeilen) trifft genau diesen Hebel: er bringt Wartbarkeit **und** testbare Abgrenzung, beides mit konkretem, aus Z8 begruendetem Nutzen — nicht als blindes Aufraeumen.

**Begruendung gegen alternative Zyklen:**
- *#6 Departments/Rollen Pagination*: erzeugt API-Vertrags- und FE-Folgen ohne aktuellen Last-Trigger. Wartet auf Anlass.
- *#8 RotationTaskGeneration*: bewusst deferred (admin-getriggert, kein kleiner SQL-Hebel).
- *Neue Last-Hotspots erfinden*: Z8 hat die priorisierten Pfade gepushed; ein synthetischer Last-Zyklus waere Bauchgefuehl-Refactor.
- *Reine Hygiene-Splits anderer grosser Dateien*: ohne Z8-Kopplung waere das genau das untersagte „blinde Aufraeumen".

**Fokus:**
1. Boundary-Inventur: oeffentliche API, Aufrufer, interne Achsen (Graph-Zugriff, DB-Batch, Orchestrierung, DepartmentLead-Resolution, Import-Pfad).
2. Extract-Plan: konkreter File-/Klassen-Schnitt, der Graph- und DB-Seite hinter Interfaces stellt und `SyncAllAsync` zur duennen Orchestrierung schrumpft.
3. `SyncAllAsync`-Zuschnitt nach Plan; Graph-/Batch-Helfer hinter Interface bringen.
4. Coverage nachziehen: insbesondere die Z8-2.3-Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`) plus DepartmentLead-Resolver.

**Leitplanken:**
- Kein paralleler zweiter Hebel. Nur LQ2-Z3.
- Keine semantischen Aenderungen an Sync-Verhalten oder DB-Spalten — reine Strukturarbeit mit Test-Nutzen.
- Keine FE-Folgen erwartet (Service ist Backend-/Timer-Pfad).
- API-Vertraege der Admin-Endpunkte bleiben unveraendert.

**Geplante Slices (Erst-Definition, nicht Umsetzung):**

| ID | Aufgabe | Prio | Reasoning | Modell | Status |
|----|---------|------|-----------|--------|--------|
| Z9-1.1 | Boundary-/Split-Inventur: oeffentliche API, Aufrufer (`DirectorySyncHostedService`, Admin-Endpunkte), interne Achsen (Graph-Zugriff, DB-Batch-Helfer, `SyncAllAsync`-Orchestrierung, `SyncDepartmentLeadAssignmentsFromDirectory`, `ImportDirectoryIdentitiesAsync`); pro Achse: Abhaengigkeiten, Test-Isolations-Hindernisse | HIGH | high | opus | offen |
| Z9-1.2 | Extract-Plan: konkreter File-/Klassen-Schnitt (Kandidaten z. B. `EntraGraphClient`/-Adapter, `EntraDirectoryBatchOperations`, `EntraDepartmentLeadResolver`, schlanker `EntraDirectorySyncOrchestrator`), Reihenfolge der Extraktionen, Test-Strategie (Graph-Stub vs. echtem Client), explizite Nicht-Ziele | HIGH | high | opus | offen |
| Z9-2.1 | Pre-Cleanup (Dead-Code raus) + `SyncAllAsync` in Phasen-Methoden zerlegen, ohne Verhaltensaenderung, noch in derselben Datei | HIGH | medium..high | sonnet | done 2026-05-05 — Detail im Sync-Log und in `CODEX_SYNC.md` |
| Z9-2.2 | Graph-Zugriff hinter Adapter-Interface; Adapter testbar (Stub) machen | HIGH | medium..high | sonnet | done 2026-05-05 — `IEntraGraphClient`/`EntraGraphClient` unter `api/API/Services/Directory/`; `Microsoft.Graph` aus Hauptdatei raus |
| Z9-2.3 | DB-Batch-Helfer (`UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`) hinter dediziertes, testbares Operations-Modul ziehen | HIGH | medium | sonnet | done 2026-05-05 — `IEntraDirectorySyncOperations` + `EntraDirectorySyncOperations` unter `api/API/Services/Directory/`; Service konsumiert per Konstruktor; Reflection-Test auf direkten Aufruf umgestellt; Detail im Sync-Log |
| Z9-3 | Coverage nachziehen: Integration-Tests fuer Batch-Helfer (aus Z8-4 verschoben) + Unit-Tests fuer Orchestrator gegen Graph-Stub | MEDIUM | medium | sonnet | done 2026-05-05 — Unit-Tests `EntraDirectorySyncServiceTests` (3/3 gruen) gegen `IEntraGraphClient`-/`IEntraDirectorySyncOperations`-Stubs; Integration-Tests `EntraDirectorySyncOperationsIntegrationTests` fuer `UpsertDirectoryIdentitiesBatchAsync` und `InsertGroupMembershipsBatchAsync` ueber bestehende Postgres-Fixture (lokal ohne Docker/Postgres dieselbe Fixture-Gating-Grenze wie Z8-4.1). Detail unten § Z9-3. |

### Z9-1.1 Boundary-/Split-Inventur (2026-05-05)

Reine Inventur, kein Code-Change. Datei: `api/API/Services/EntraDirectorySyncService.cs` (2591 Z., `internal sealed class EntraDirectorySyncService : IDirectorySyncService`).

**Konstruktor-Abhaengigkeiten** (4): `IGraphApplicationConfigurationService` (Graph-Credentials), `LifecycleRuntimeSettings` (ConnectionString, DirectoryGroupPrefix, DirectoryExplicitGroupIds, DevSimulationEnabled), `ISystemEventLogService` (Audit/Eventlog), `ILogger<EntraDirectorySyncService>`. Kein Repository-Interface — der Service oeffnet `NpgsqlConnection` direkt aus dem ConnectionString.

#### Oeffentliche API (`IDirectorySyncService` → `EntraDirectorySyncService`)

| Methode | LOC | Aufruf-Pfad | Kohaesionsbereich |
|---------|-----|-------------|-------------------|
| `SyncAllAsync(groupPrefixOverride?, ct)` | 34-393 | HostedService + Admin POST `/admin/directory/sync` | Sync-Orchestrierung (Graph + DB + Eventlog) |
| `GetSyncStatusAsync(ct)` | 467 | Admin GET `/admin/directory/status` | DB-Read |
| `GetGroupsAsync(ct)` | 537 | Admin GET `/admin/directory/groups` | DB-Read |
| `GetIdentitiesAsync(limit, offset, ct)` | 647 | Admin GET `/admin/directory/identities` | DB-Read |
| `GetMappingAuditAsync(limit, ct)` | 726 | Admin GET `/admin/directory/audit` | DB-Read |
| `UpsertGroupRoleMappingAsync(req, actor?, ct)` | 788 | Admin POST `/admin/directory/group-mappings` | DB-Write + Mapping-Audit |
| `DeleteGroupRoleMappingAsync(id, actor?, ct)` | 882 | Admin DELETE `/admin/directory/group-mappings/{id}` | DB-Write + Mapping-Audit |
| `GetResponsibilityGapsAsync(ct)` | 928 | Admin GET `/admin/directory/responsibility-gaps` | DB-Read (komplexe Aggregation) |
| `GetPendingImportsAsync(ct)` | 1022 | Admin GET `/admin/directory/pending-imports` | DB-Read |
| `ImportIdentitiesAsync(req, actor?, ct)` | 1104 | Admin POST `/admin/directory/import` | DB-Write (Import-Pfad, ruft `ImportSingleIdentityAsync`) |

#### Echte externe Aufrufer

- `DirectorySyncHostedService` (`api/API/Services/DirectorySyncHostedService.cs`): nur `SyncAllAsync(ct)` — periodisch (DIRECTORY_SYNC_INTERVAL_MINUTES), gated durch `DirectorySyncEnabled` + `DirectorySyncScheduled`.
- `AdminDirectorySyncEndpoints` (`api/API/Endpoints/AdminDirectorySyncEndpoints.cs`): saemtliche `IDirectorySyncService`-Methoden ueber das Admin-API.
- DI-Registrierung: `LifecycleServiceCollectionExtensions.cs` (Z. 165 `IGraphApplicationConfigurationService`; `IDirectorySyncService → EntraDirectorySyncService` ist scoped registriert in derselben Datei).
- Tests: ausschliesslich `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs` — **Reflection-Zugriff** auf zwei `private`-Methoden (siehe Test-Isolations-Hindernisse).

Keine weiteren Aufrufer (verifiziert via `Grep` ueber Repo).

#### Interne Achsen / Kohaesionsbereiche innerhalb der Datei

1. **Sync-Orchestrierung** (`SyncAllAsync`, 34-393, ~360 Z.) — Connection-Open, Graph-Client-Bau, Group-Loop, Cross-Cutting-Eventlog, Status-Aggregation, Final-`LogSyncRun`. Direkt an Graph **und** DB **und** SystemEventLog gekoppelt.
2. **Graph-Zugriff** (Microsoft.Graph) — `LoadSecurityGroupsAsync` (395), `LoadGroupMembersAsync` (424), `ResolveGraphCredentialsAsync` (453), `GraphServiceClient`-Bau in `SyncAllAsync` (61-89). Einzige Stellen mit `Microsoft.Graph.*`-Abhaengigkeit.
3. **DB-Batch-Helfer (Sync-Pfad)** — `UpsertDirectoryIdentitiesBatch` (1528), `InsertGroupMembershipsBatch` (1596) — die in Z8-2.3 eingefuehrten `unnest`-Bulk-Statements; aktuell `private static`. Daneben Single-Row-Helfer `UpsertDirectoryGroup` (1446), `UpsertDirectoryIdentity` (1469, dead-code-Kandidat — nach Z8-2.3 nicht mehr aufgerufen, pruefen), `ClearGroupMemberships` (1500), `InsertGroupMembership` (1511), `AutoLinkIdentitiesToAppUsers` (1619).
4. **Projection / Activation** (DB-Batch im Sync-Pfad) — `EnsureDirectoryProjectionUserColumnsAsync` (1996), `EnsureDirectoryDepartmentsExist` (1732), `UpdateExistingAppUsersFromDirectory` (1770), `UpdateDirectoryUserActivationStates` (2018), `EnsureDevelopmentDefaultGroupMappings` (2484). Reine SQL-Pfade, keine Graph-Kopplung.
5. **Sync-Logging / Audit** — `LogSyncRun` (1633), `LogMappingAuditAsync` (1358), `LogDirectoryAuditEventAsync` (1390), `CreateDepartmentLeadAuditSnapshot` (1422). Schreiben in `directory_sync_log` / `directory_mapping_audit_log`. Nicht zu verwechseln mit `_systemEventLogService` (zweiter Audit-Kanal).
6. **Group-/Filter-Helfer (CPU)** — `ShouldSyncGroup` (1711), `MatchesGroupPrefix` (1665), `ResolveEffectiveGroupPrefix` (1680), `ParseExplicitGroupIds` (1696), `Normalize` (1691), `NormalizeScope` (1659), `ParseDirectoryEmployeeNumber` (2524). Reine statische CPU-Helfer ohne IO.
7. **Admin-Read/Write-Methoden** (Status, Groups, Identities, MappingAudit, GroupRoleMapping CRUD, ResponsibilityGaps, PendingImports) — DB-Zugriffe + Mapping-Audit. Gehoeren fachlich zu „Admin-Konfigurations-Sicht", nicht zur Sync-Orchestrierung.
8. **Import-Pfad** — `ImportIdentitiesAsync` (1104) + `ImportSingleIdentityAsync` (1179). Eigene Achse: erzeugt `app_users`-Datensaetze aus bekannten `directory_identities`. Kein Graph-Zugriff. Findet `FindExistingMappingIdAsync` (1263) und `GetGroupRoleMappingByIdAsync` / `…OrNullAsync` (1295/1343) als Mapping-Lookup-Helfer.
9. **DepartmentLead-Resolver (toter Pfad in Prod)** — `SyncDepartmentLeadAssignmentsFromDirectory` (2099), `LoadDepartmentLeadSyncStatesAsync` (2242), `EnsureDirectoryManagedPersonRecordAsync` (2307), `UpsertDepartmentLeadAssignmentAsync` (2439), `ClearDepartmentLeadAssignmentAsync` (2470). **Wichtig:** `SyncAllAsync` ruft diese Methoden **nicht** mehr auf (Z. 306-317 schreibt explizit `directory_department_assignments_skipped`); einziger lebender Aufruf ist die **Reflection-Invocation aus dem Test** (siehe unten). `KauthWorkflow/Architektur/Entscheidungen.md` dokumentiert den bewussten Stop. Status: **dead code im Prod-Pfad**, im Test als Black-Box-Subroutine genutzt.

#### Was haengt direkt an Microsoft Graph

- `using Azure.Identity;`, `using Microsoft.Graph;`, `using Microsoft.Graph.Models;` (Z. 1-4).
- `GraphServiceClient`-Konstruktion (`SyncAllAsync` 61-89).
- `LoadSecurityGroupsAsync` (395), `LoadGroupMembersAsync` (424).
- `ResolveGraphCredentialsAsync` (453) — bezieht Credentials aus `IGraphApplicationConfigurationService`; ist nicht selbst Graph-call, gehoert aber in den Adapter, weil sie pure Graph-Bootstrap-Logik ist.
- Alles andere ist Graph-frei.

#### Was ist DB-Batch / Projection / Audit

- **Batch-Operations (Sync):** `UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch`, `ClearGroupMemberships`, `AutoLinkIdentitiesToAppUsers` (Z8-2.3 Hebel). Plus die Single-Row-Pendants `UpsertDirectoryGroup`, `UpsertDirectoryIdentity` (Legacy/dead?), `InsertGroupMembership`.
- **Projection:** `EnsureDirectoryProjectionUserColumnsAsync`, `EnsureDirectoryDepartmentsExist`, `UpdateExistingAppUsersFromDirectory`, `UpdateDirectoryUserActivationStates`, `EnsureDevelopmentDefaultGroupMappings`.
- **Audit:** `LogSyncRun`, `LogMappingAuditAsync`, `LogDirectoryAuditEventAsync`, `CreateDepartmentLeadAuditSnapshot` (Audit ueber `directory_*`-Tabellen, nicht `_systemEventLogService`).

#### Was ist `SyncAllAsync`-Orchestrierung (im engeren Sinn)

Genau Z. 34-393. Schritte in dieser Reihenfolge:
1. Settings/Prefix-Resolution + Connection-Validierung.
2. Graph-Credential-Aufloesung + Graph-Client-Bau (Fehlerpfade `failed`).
3. `EnsureDirectoryProjectionUserColumnsAsync`.
4. `LoadSecurityGroupsAsync` + `ShouldSyncGroup`-Filter + Selection-Eventlog.
5. Group-Loop: `UpsertDirectoryGroup` → `LoadGroupMembersAsync` → `ClearGroupMemberships` → `UpsertDirectoryIdentitiesBatch` → `InsertGroupMembershipsBatch`. Per-Group Try/Catch → Status `partial`.
6. `AutoLinkIdentitiesToAppUsers`.
7. `EnsureDirectoryDepartmentsExist` + Eventlog.
8. `UpdateExistingAppUsersFromDirectory` + Eventlog.
9. `EnsureDevelopmentDefaultGroupMappings` (nur DevSim).
10. `UpdateDirectoryUserActivationStates` + per-Change-Eventlog.
11. „skipped"-Eventlog fuer DepartmentLead.
12. Final `LogSyncRun` + Sammel-Eventlog. Rueckgabe `DirectorySyncResult`.

#### Test-Isolations-Hindernisse (aktuell)

- **Reflection-Zugriffe** auf `private`-Methoden in `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs`:
  - Z. 103-105: `EntraDirectorySyncService.SyncDepartmentLeadAssignmentsFromDirectory` (Instance, NonPublic) — fragil und testet einen Pfad, den `SyncAllAsync` nicht mehr aufruft. Bricht beim Rename/Move.
  - Z. 177-179: `EntraDirectorySyncService.UpdateExistingAppUsersFromDirectory` (Static, NonPublic) — analog fragil.
- **Live-Graph-Kopplung in `SyncAllAsync`**: `GraphServiceClient` wird inline gebaut (Z. 61-89). Es gibt **kein** `IGraphClient`/Adapter-Interface, das im Test gestubbt werden koennte. End-to-End-Test gegen `SyncAllAsync` braucht entweder echtes Tenant oder einen Adapter-Schnitt.
- **DB-Batch-Helfer hinter `SyncAllAsync`-2.4k-Z**: `UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch` sind `private static`. Direktes Coverage erfordert Reflection (genau das fragile Pattern oben) oder Sichtbarkeits-Aenderung. In Z8-4.1 wurden sie deshalb explizit nicht abgedeckt.
- **Verstreute `_systemEventLogService.WriteAsync`-Calls**: `SyncAllAsync` schreibt mehrfach Eventlog-Eintraege im Sync-Pfad. Trennung Orchestrierung ↔ Eventing ist nur durch das DI-Interface (`ISystemEventLogService`) gegeben; Stub `StubSystemEventLogService` existiert bereits in Tests.
- **Direkte `NpgsqlConnection`-Konstruktion** im Service statt Repository-Interface — End-to-End-Tests brauchen weiterhin Postgres-Fixture; saubere Unit-Tests gegen einen reinen Orchestrator brauchen ein DB-Operations-Interface oder einen Connection-Factory-Stub.
- **`UpsertDirectoryIdentity`-Single-Row-Helfer (Z. 1469)**: nach Z8-2.3 evtl. ungenutzt (im Hot-Pfad ersetzt durch Batch). Vor dem Split pruefen, ob er noch lebt — wenn nicht, gehoert er nicht in den Split.

#### Versteckte Kopplungen

- `LifecycleRuntimeSettings.DevSimulationEnabled` schaltet `EnsureDevelopmentDefaultGroupMappings` an (Z. 274). Diese Verzweigung ist kein Graph-/DB-Schnitt, sondern eine Dev-Sim-Sonderlogik.
- `DirectoryExplicitGroupIds`-Setting wird **nur** in `SyncAllAsync` (via `ParseExplicitGroupIds`) genutzt — Filter-Logik gehoert in den CPU-Helfer-Block.
- Audit-Logs schreiben in **zwei** Kanaele (`directory_*`-Tabellen via `LogDirectoryAuditEventAsync`, plus `system_event_log` via `_systemEventLogService`) — der Split muss klar machen, welcher Kanal beim Orchestrator bleibt und welcher beim DB-Operations-Modul.

#### Was explizit **nicht** in den ersten Split gehoert

- **DepartmentLead-Resolver** (Achse 9): toter Pfad in Prod. Vor dem Split entscheidet Z9-1.2, ob loeschen, in eigene Datei isolieren oder unangetastet lassen — **nicht** in den Sync-Adapter ziehen, sonst zementiert der Split einen ungenutzten Pfad.
- **Admin-Read/Write-Methoden** (Achse 7) und **Import-Pfad** (Achse 8): logisch eigene Verantwortung („Admin-Sicht / Import"), aber **nicht** Treiber von Z9. Der Trigger ist Test-Isolation der **Sync-Lastpfade** aus Z8-2.3. Admin-Methoden in dieselbe Iteration zu ziehen vergroessert den Slice ohne Z8-Kopplung. → in Folge-Slices oder spaeter, nicht in Z9-2.x.
- **Single-Row-Helfer** `UpsertDirectoryIdentity` / `InsertGroupMembership` (falls nach Z8-2.3 dead): vor dem Split Lebendigkeit pruefen; wenn dead, in Z9-1.2 als Loesch-Kandidat markieren statt im Split „mitnehmen".
- **`LifecycleRuntimeSettings`-Auswertung**: bleibt im Service-Konstruktor / Orchestrator. Kein eigenes Modul.
- **Eventlog-Integration (`ISystemEventLogService`)**: nicht hinter neuen Wrapper. Der bestehende DI-Stub (`StubSystemEventLogService`) reicht fuer Tests.
- **CPU-Helfer** (Achse 6): koennen in eine `internal static`-Helper-Klasse, **muessen** aber nicht — keine Test-Isolations-Wirkung.

#### Schnittland-Empfehlung (informativ, nicht Plan)

Aus Z8-Kopplung getrieben, mit klarem Test-Nutzen:
- **Graph-Adapter** (Achse 2) hinter `IEntraGraphClient`-aehnlichem Interface. Bringt `SyncAllAsync` einen Stub-Punkt.
- **DB-Sync-Operations-Modul** (Achsen 3+4 nur fuer den Sync-Pfad: Batch-Helfer, Projection, Activation). Bringt `UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch` aus dem `private`-Schatten und macht sie integration-testbar.
- **`EntraDirectorySyncOrchestrator`** als duenner `SyncAllAsync`-Treiber gegen Graph- + DB-Interface + `ISystemEventLogService`.
- Achsen 5 (Sync-Logging) und 6 (CPU-Helfer) folgen passiv im jeweils naechstgelegenen Modul.
- Achsen 7+8+9 bleiben in dieser Iteration **ausserhalb** des Splits.

Z9-1.2 entscheidet die konkrete Reihenfolge und die Test-Strategie (Graph-Stub vs. echter Client, Coverage-Reihenfolge).

**Reihenfolge / Abhaengigkeiten:**
- Z9-1.1 → Z9-1.2 sequenziell (Inventur vor Plan).
- Z9-2.x: 2.1 zuerst (Orchestrierungs-Schnitt), danach 2.2 und 2.3 unabhaengig moeglich, aber sequenziell halten, damit der Tree pro Slice klar bleibt.
- Z9-3 erst, wenn die Batch-/Graph-Schnitte stehen. Coverage darf den Refactor nicht treiben.

### Z9-2.2 Graph-Adapter extrahiert (2026-05-05)

Slice gemaess Z9-1.2 § Z9-2.2 abgeschlossen.

- Neue Files: `api/API/Services/Directory/IEntraGraphClient.cs`, `api/API/Services/Directory/EntraGraphClient.cs` (Namespace `API.Services.Directory`).
- `IEntraGraphClient` exponiert `InitializeAsync` (Status: `Ready` / `MissingCredentials` / `Failed` plus `ErrorMessage`/`ExceptionType`), `LoadSecurityGroupsAsync`, `LoadGroupMembersAsync`. Rueckgabewerte sind plain DTOs (`EntraSecurityGroup`, `EntraDirectoryUser`), damit `Microsoft.Graph.*` aus der Hauptdatei verschwindet.
- `EntraGraphClient` kapselt `IGraphApplicationConfigurationService`-Lookup, `ClientSecretCredential`- und `GraphServiceClient`-Bau, Pagination beim Group-/Member-Load. Nicht-User-Members werden bereits hier herausgefiltert (vorher im Orchestrator via `is not Microsoft.Graph.Models.User`); Verhalten unveraendert, da der Orchestrator diese Members ohnehin uebersprang.
- `EntraDirectorySyncService.cs`:
  - Konsumiert `IEntraGraphClient` per Konstruktor; `IGraphApplicationConfigurationService` und `_logger` als Graph-Bootstrap-Empfaenger entfallen (Field + Argument geloescht).
  - `SyncAllAsync` ersetzt den Inline-Credential/Client-Aufbau (vorher Z. 61-89) durch `await _graphClient.InitializeAsync(ct)` mit identischen Eventlog-Eintraegen `directory_sync_missing_graph_credentials` und `directory_sync_graph_client_failed` (inkl. `error`/`exceptionType` aus `EntraGraphInitResult.ExceptionType`).
  - `RunGroupSyncAsync` verliert den `GraphServiceClient`-Parameter und ruft `_graphClient.LoadSecurityGroupsAsync` / `LoadGroupMembersAsync`.
  - `validUsers`-Tupel und `UpsertDirectoryIdentitiesBatch`-Signatur tragen jetzt `EntraDirectoryUser` statt `Microsoft.Graph.Models.User` (Property-Mapping unveraendert: `UserPrincipalName`/`Mail`/`DisplayName`/`AccountEnabled`/`Department`/`EmployeeId`).
  - `LoadSecurityGroupsAsync`/`LoadGroupMembersAsync`/`ResolveGraphCredentialsAsync` aus der Hauptdatei entfernt.
  - `using Azure.Identity;` / `using Microsoft.Graph;` / `using Microsoft.Graph.Models;` aus der Hauptdatei entfernt; `using API.Services.Directory;` ergaenzt.
- DI: `services.AddScoped<API.Services.Directory.IEntraGraphClient, API.Services.Directory.EntraGraphClient>()` direkt vor der `IDirectorySyncService`-Registrierung in `LifecycleServiceCollectionExtensions`. Scoped passt zum Per-Run-Scope von `DirectorySyncHostedService`.
- `PROJECT_STRUCTURE.md` mitgezogen: `api/API/Services/Directory/` als neuer Sub-Namespace eingetragen.
- Verhaltensgleichheit: gleiche Fehlerpfade fuer fehlende Credentials und Client-Bau, gleiche Eventlog-Eintraege, gleiche per-Group-Try/Catch-Logik mit Status `partial`.
- Verifikation: `dotnet build API/API.csproj` (0 Warn / 0 Err), `dotnet build API.Tests/API.Tests.csproj` (3 vorhandene CS8602-Warnungen, 0 Err).

### Z9-1.2 Extract-Plan (2026-05-05)

Reine Planung, kein Code-Change. Verbindlicher Schnitt fuer Z9-2.x, basierend auf Z9-1.1.

#### Lebendigkeitspruefung (vorab verifiziert)

Repo-weite `Grep`-Pruefung der drei Z9-1.1-Verdachtsfaelle:
- `EntraDirectorySyncService.SyncDepartmentLeadAssignmentsFromDirectory` (Z. 2099): einziger lebender Aufrufer ist die Reflection-Invocation in `api/API.Tests/PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:104`. **Dead code im Prod-Pfad** bestaetigt. Sub-Helfer `LoadDepartmentLeadSyncStatesAsync`, `EnsureDirectoryManagedPersonRecordAsync`, `UpsertDepartmentLeadAssignmentAsync`, `ClearDepartmentLeadAssignmentAsync`, `CreateDepartmentLeadAuditSnapshot` sind ausschliesslich Sub-Aufrufer dieser toten Methode → ebenfalls dead.
- `UpsertDirectoryIdentity` (Single-Row, Z. 1469): keine Aufrufer im Repo. **Dead**.
- `InsertGroupMembership` (Single-Row, Z. 1511): keine Aufrufer im Repo. **Dead**.

Damit ist der Pre-Cleanup eindeutig: alles drei wird in Z9-2.1 entfernt, **bevor** der File-Split startet. Begruendung: ein File-Split, der toten Code mitnimmt, zementiert ihn in einem neuen Modul und vergroessert die Splittsflaeche unnoetig.

#### Ziel-Endzustand nach Z9-2.x

Drei Files unter neuem Namespace `api/API/Services/Directory/`:

1. **`EntraDirectorySyncService.cs`** (alte Datei, deutlich verschlankt; bleibt am bestehenden Pfad fuer DI-Stabilitaet):
   - `IDirectorySyncService`-Implementation aller 11 oeffentlichen Methoden.
   - `SyncAllAsync` als duenner Treiber: orchestriert `_graphClient` (Z9-2.2) + `_syncOps` (Z9-2.3) + `_systemEventLogService`.
   - Admin-Read/Write (Achse 7): `GetSyncStatusAsync`, `GetGroupsAsync`, `GetIdentitiesAsync`, `GetMappingAuditAsync`, `UpsertGroupRoleMappingAsync`, `DeleteGroupRoleMappingAsync`, `GetResponsibilityGapsAsync`, `GetPendingImportsAsync`. Bleiben hier.
   - Import-Pfad (Achse 8): `ImportIdentitiesAsync` + `ImportSingleIdentityAsync` + `FindExistingMappingIdAsync` + `GetGroupRoleMappingByIdAsync`/`…OrNullAsync`. Bleiben hier.
   - Mapping-Audit-Helfer (`LogMappingAuditAsync`) bleibt hier (Admin-Pfad-Bedarf).
   - CPU-Helfer (Achse 6) bleiben als `private static` hier — keine Test-Isolations-Wirkung.
   - Erwartete LOC: ~1200 (von 2591).

2. **`api/API/Services/Directory/IEntraGraphClient.cs` + `EntraGraphClient.cs`** (Z9-2.2):
   - Interface kapselt: `Task<IReadOnlyList<Group>> LoadSecurityGroupsAsync(ct)`, `Task<IReadOnlyList<DirectoryObject>> LoadGroupMembersAsync(string groupId, ct)`.
   - `EntraGraphClient` impl: `ResolveGraphCredentialsAsync` + `GraphServiceClient`-Bau intern (Konstruktor-Inject `IGraphApplicationConfigurationService`). Einzige Stelle mit `Microsoft.Graph.*`-Abhaengigkeit nach dem Schnitt.
   - DI: `services.AddScoped<IEntraGraphClient, EntraGraphClient>()` in `LifecycleServiceCollectionExtensions`.

3. **`api/API/Services/Directory/IEntraDirectorySyncOperations.cs` + `EntraDirectorySyncOperations.cs`** (Z9-2.3):
   - Interface enthaelt nur die **Sync-Pfad**-DB-Operationen aus Achsen 3+4+5 (Sync-Logging):
     - `EnsureDirectoryProjectionUserColumnsAsync`
     - `UpsertDirectoryGroup`
     - `ClearGroupMemberships`
     - `UpsertDirectoryIdentitiesBatch` (Z8-2.3-Hebel)
     - `InsertGroupMembershipsBatch` (Z8-2.3-Hebel)
     - `AutoLinkIdentitiesToAppUsers`
     - `EnsureDirectoryDepartmentsExist`
     - `UpdateExistingAppUsersFromDirectory`
     - `EnsureDevelopmentDefaultGroupMappings` (DevSim-Pfad bleibt; Verzweigung weiterhin im Orchestrator entschieden)
     - `UpdateDirectoryUserActivationStates`
     - `LogSyncRun`
     - `LogDirectoryAuditEventAsync` fuer Sync-Pfad-Eintraege
   - Methoden werden `public` auf der Klasse → integration-testbar ohne Reflection.
   - Konstruktor: `LifecycleRuntimeSettings` (ConnectionString) + `ILogger<EntraDirectorySyncOperations>`. **Kein** `ISystemEventLogService`-Cross-Cutting hier; Eventlog-Schreiben bleibt im Orchestrator.
   - DI: `services.AddScoped<IEntraDirectorySyncOperations, EntraDirectorySyncOperations>()`.

#### Was explizit **draussen** bleibt

- **Admin-Read/Write-Methoden** (Achse 7) und **Import-Pfad** (Achse 8): kein Z8-Trigger, kein Test-Isolations-Druck. Bleiben in `EntraDirectorySyncService.cs`. In Folgezyklen ggf. eigener Schnitt.
- **Mapping-Audit** (`LogMappingAuditAsync`): wird auch von Admin-Pfaden (UpsertGroupRoleMapping/Delete) verwendet. Bleibt in der Hauptklasse, nicht ins Operations-Modul ziehen.
- **CPU-Helfer** (Achse 6): keine Test-Isolations-Wirkung. Bleiben `private static` in der Hauptklasse.
- **`ISystemEventLogService`-Wrapper**: nicht einfuehren. Bestehender DI-Stub `StubSystemEventLogService` reicht.
- **Repository-/Connection-Factory-Abstraktion**: nicht in Z9. `EntraDirectorySyncOperations` oeffnet weiterhin `NpgsqlConnection` aus `LifecycleRuntimeSettings.ConnectionString`. Integration-Tests laufen ueber bestehende Postgres-Fixture.
- **Namespace-Massenmove** anderer Services: nur die drei neuen Files unter `Services/Directory/`. Bestehende Datei behaelt ihren Pfad.

#### Umgang mit Dead Code (verbindlich)

- **DepartmentLead-Resolver** (`SyncDepartmentLeadAssignmentsFromDirectory` + 5 Sub-Helfer): in **Z9-2.1 loeschen**. Begruendung gegen "in eigene Datei isolieren": der Pfad ist seit der bewussten Stop-Entscheidung (`KauthWorkflow/Architektur/Entscheidungen.md`) aus dem Prod-Pfad raus; eine isolierte Datei dafuer waere Konservierung von totem Code in einem neuen Modul. Reflection-Test in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:103-105` wird im selben Slice mit entfernt. Test fuer `UpdateExistingAppUsersFromDirectory` (Z. 177-179) bleibt — Methode wird in Z9-2.3 nach `EntraDirectorySyncOperations` verschoben und dort `public`, der Test kann auf direktem Aufruf umgestellt oder einstweilen entfernt werden (siehe Z9-2.3-Test-Strategie).
- **Single-Row-Helfer** `UpsertDirectoryIdentity` (1469) und `InsertGroupMembership` (1511): in **Z9-2.1 loeschen**. Verifiziert ohne Aufrufer im Repo. Begruendung gegen "im Operations-Modul behalten als Convenience": kein einziger Aufrufer existiert, Behalten bedeutet ungetestete tote API im neuen Modul.

#### Slice-Schnitt Z9-2.1 / 2.2 / 2.3 (verbindlich)

**Z9-2.1 — Pre-Cleanup + SyncAllAsync-Strukturierung**
- Loeschen: `SyncDepartmentLeadAssignmentsFromDirectory` und Sub-Helfer (`LoadDepartmentLeadSyncStatesAsync`, `EnsureDirectoryManagedPersonRecordAsync`, `UpsertDepartmentLeadAssignmentAsync`, `ClearDepartmentLeadAssignmentAsync`, `CreateDepartmentLeadAuditSnapshot`); zugehoerige `DepartmentLeadSyncSummary`/`DepartmentLeadSyncState`/`DepartmentLeadSyncOutcome`-Records, falls nicht anderweitig genutzt; Reflection-Test in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:103-105`.
- Loeschen: `UpsertDirectoryIdentity` (1469), `InsertGroupMembership` (1511).
- `SyncAllAsync` intern in drei klar benannte Phasen-private-Methoden zerlegen (`RunGroupSyncAsync`, `RunDirectoryProjectionAsync`, `RunActivationAsync`) — **noch in derselben Datei**, kein neuer Namespace. Bereitet 2.2/2.3 vor.
- LOC-Erwartung: ~600 Zeilen weniger in der Hauptdatei.
- **Test-Strategie**: keine neuen Tests. Bestehende API.Tests-Suite bleibt gruen (minus den geloeschten Reflection-Test). Verifikation: `dotnet build` + `dotnet test --filter Category!=Integration`.

**Z9-2.2 — Graph-Adapter extrahieren**
- Neue Files unter `api/API/Services/Directory/`: `IEntraGraphClient.cs`, `EntraGraphClient.cs`.
- Verschieben: `LoadSecurityGroupsAsync`, `LoadGroupMembersAsync`, `ResolveGraphCredentialsAsync`, GraphServiceClient-Bau aus `SyncAllAsync` Z. 61-89.
- `EntraDirectorySyncService` bekommt `IEntraGraphClient` per Konstruktor.
- Microsoft.Graph-`using` aus der Hauptdatei entfernen.
- DI-Registrierung in `LifecycleServiceCollectionExtensions`.
- **Test-Strategie**: Smoke-Test, dass Build gruen ist und bestehende Suite gruen bleibt. Echte Stub-getriebene Orchestrator-Tests kommen erst in Z9-3, sobald `IEntraDirectorySyncOperations` (2.3) ebenfalls steht — sonst muesste man halb-fertige Test-Doubles bauen.

**Z9-2.3 — DB-Sync-Operations-Modul extrahieren**
- Neue Files unter `api/API/Services/Directory/`: `IEntraDirectorySyncOperations.cs`, `EntraDirectorySyncOperations.cs`.
- Verschieben: die unter "Ziel-Endzustand Punkt 3" gelisteten Methoden inkl. `UpsertDirectoryIdentitiesBatch` und `InsertGroupMembershipsBatch`. Methoden werden `public` auf der Operations-Klasse.
- `EntraDirectorySyncService` bekommt `IEntraDirectorySyncOperations` per Konstruktor; `SyncAllAsync` ruft `_syncOps.UpsertDirectoryIdentitiesBatch(...)` etc.
- Reflection-Test fuer `UpdateExistingAppUsersFromDirectory` in `PostgresWorkflowRepositoryAdminConfigIntegrationTests.cs:177-179`: auf direkten `EntraDirectorySyncOperations`-Aufruf umstellen (Methode ist jetzt public). Falls der Aufwand das Slice unnoetig aufblaeht, alternativ entfernen — die echte Coverage kommt in Z9-3.
- DI-Registrierung in `LifecycleServiceCollectionExtensions`.
- **Test-Strategie**: bestehende Suite gruen halten. Direkte Integration-Tests gegen die neuen `public`-Methoden kommen in Z9-3, nicht hier. Begruendung: Coverage darf den Refactor nicht treiben (Leitplanke Z9).

#### Test-Strategie zusammengefasst (pro Slice)

| Slice | Neue Tests | Bestehende Tests | Verifikation |
|-------|-----------|------------------|--------------|
| Z9-2.1 | keine | Reflection-Test fuer `SyncDepartmentLeadAssignmentsFromDirectory` entfernen | `dotnet build`; `dotnet test --filter Category!=Integration` gruen |
| Z9-2.2 | keine | Build muss gruen bleiben; keine Verhaltensaenderung | `dotnet build`; Suite gruen |
| Z9-2.3 | keine (Coverage in Z9-3) | Reflection-Test fuer `UpdateExistingAppUsersFromDirectory` umstellen oder entfernen | `dotnet build`; Suite gruen |
| Z9-3 | Integration-Tests fuer `UpsertDirectoryIdentitiesBatch`, `InsertGroupMembershipsBatch` + Unit-Tests fuer Orchestrator gegen `IEntraGraphClient`-Stub und `IEntraDirectorySyncOperations`-Stub | bestehende gruen halten | `dotnet test` inkl. Integration |

Begruendung gegen "Tests pro Slice mitziehen": die Z9-2.x-Slices sind reine Strukturarbeit ohne Verhaltensaenderung; sinnvolle Coverage haengt erst nach 2.3 an stabilen Interfaces. Tests in 2.2 zu schreiben, die in 2.3 erneut umgebaut werden muessen, ist verschwendet.

#### Risiken / Watchouts

- **DI-Reihenfolge**: `EntraDirectorySyncService`, `EntraGraphClient`, `EntraDirectorySyncOperations` muessen alle `Scoped` registriert werden, damit der Sweep-Lebenszyklus konsistent bleibt (`DirectorySyncHostedService` erstellt pro Run einen Scope).
- **`LifecycleRuntimeSettings`-Sharing**: das Settings-Objekt wird jetzt in Service + Operations gleichzeitig konsumiert — kein Problem (Singleton/Scoped Snapshot), aber bei Aenderungen von `ConnectionString`/`DirectoryGroupPrefix` einheitlich halten.
- **DevSim-Verzweigung**: `EnsureDevelopmentDefaultGroupMappings` ruft die Operations, aber die Entscheidung "DevSim aktiv?" bleibt im Orchestrator — Operations-Modul kennt die DevSim-Flag nicht.
- **Reflection-Test-Umstellung in 2.3**: wenn die Test-Anpassung die Fixture-Initialisierung beruehrt, kann das den Slice unerwartet vergroessern. Fallback: Test entfernen, Coverage in Z9-3 neu schreiben.
- **Naming**: `Services/Directory/` als Sub-Namespace ist neu. `PROJECT_STRUCTURE.md` muss in Z9-2.2 mitgezogen werden, sobald die ersten Files dort liegen.

**Frontend-Folgen:** **keine**. Reiner Backend-Refactor. `FRONTEND_TODO.md` wird nicht angefasst.

**Abgrenzung zu Z8:**
- Z8 hat `SyncAllAsync` an der Last-Front gehaertet (Group/Member-Bulk). Z9 fasst die so eingefuehrten Helfer **nicht inhaltlich** an, sondern nur strukturell, damit sie isoliert testbar werden.
- LQ2-Z3 wird als aktiver Zyklus 9 aus dem zyklusuebergreifend-offenen Block herausgehoben; die Coverage fuer die Z8-2.3-Batch-Helfer wandert formal nach Z9-3 (vorher: aus Z8-4 nach LQ2-Z3 verschoben).

### Z9-3 Coverage gegen die neuen Interfaces (2026-05-05)

Nach Abschluss der Strukturarbeit (Z9-2.1..2.3) deckt Z9-3 die in Z8-2.3 eingefuehrten und in Z9-2.3 unter `IEntraDirectorySyncOperations` verschobenen Batch-Helfer plus den nun duennen Orchestrator ab.

**Unit-Tests Orchestrator** — `api/API.Tests/EntraDirectorySyncServiceTests.cs` (neu, 3 Tests):
- `SyncAllAsync_MissingConnectionString_FailsBeforeGraphInit` — `LifecycleRuntimeSettings.ConnectionString = null`; verifiziert: Status `failed`, `directory_sync_missing_connection_string`-Eventlog, **kein** Graph-`InitializeAsync`, **kein** `UpsertDirectoryIdentitiesBatchAsync`.
- `SyncAllAsync_GraphMissingCredentials_FailsAndLogsEvent` — Graph-Stub liefert `EntraGraphInitStatus.MissingCredentials`; verifiziert: Status `failed`, `directory_sync_missing_graph_credentials`-Event, kein `LoadSecurityGroupsAsync`, `AppliedGroupPrefix` aus `groupPrefixOverride` durchgereicht.
- `SyncAllAsync_GraphFailed_FailsAndLogsErrorWithDetails` — Graph-Stub liefert `Failed` mit `ErrorMessage`/`ExceptionType`; verifiziert: Status `failed`, `ErrorMessage` durchgereicht, `directory_sync_graph_client_failed`-Event geschrieben.

Diese drei Pfade sind die im aktuellen Service ohne reale Postgres-Verbindung sauber stub-fahigen Orchestrierungs-Pfade. Alles jenseits des `MissingConnectionString`/Graph-Init-Gates oeffnet eine echte `NpgsqlConnection` und gehoert in Integration-Tests; ein zusaetzlicher Connection-Factory-Schnitt waere ein neuer Refactor und damit gegen die Leitplanke "kein weiterer Produktiv-Refactor in Z9-3". Offen benannt.

**Integration-Tests Operations** — `api/API.Tests/EntraDirectorySyncOperationsIntegrationTests.cs` (neu, 3 Tests, `[Trait("Category", "Integration")]`, gleiche `PostgresWorkflowRepositoryIntegrationCollection`-Fixture wie Z8-4.1):
- `UpsertDirectoryIdentitiesBatchAsync_InsertsNewIdentitiesAndReturnsMapping` — zwei frische `entra_object_id`s; verifiziert RETURNING-Mapping (beide Schluessel vorhanden) sowie persistierte Spalten (`display_name`, `mail`, `account_enabled`, `department_name`, `employee_number` inkl. `Normalize`/`ParseDirectoryEmployeeNumber`).
- `UpsertDirectoryIdentitiesBatchAsync_UpdatesExistingAndDedupsLastWins` — zweite Batch trifft denselben `entra_object_id` zweimal; verifiziert `ON CONFLICT … DO UPDATE` (selbe `id` zurueck) und das in der Implementation dokumentierte „last-wins"-Verhalten der internen Dedup-Map.
- `InsertGroupMembershipsBatchAsync_InsertsAndIsIdempotent` — zwei Memberships, zweimaliger Aufruf; verifiziert exakt zwei Eintraege (ON CONFLICT DO NOTHING) plus den Empty-Array-No-Op-Pfad.

**Verifikation:**
- `dotnet test --filter "FullyQualifiedName~EntraDirectorySyncServiceTests"` (im Test-Projekt): 3/3 gruen.
- `dotnet test --filter "FullyQualifiedName~EntraDirectorySyncOperationsIntegrationTests"`: lokal **nicht** ausfuehrbar — der `PostgresWorkflowRepositoryDatabaseFixture` faellt mit `ArgumentException : Docker is either not running or misconfigured` zurueck (kein Docker / kein Postgres unter `127.0.0.1:26432`). Identisches Fixture-Gating wie alle anderen `[Category=Integration]`-Tests im Projekt (vgl. Z8-4.1). Tests sind im etablierten Fixture-Muster angelegt; Verifikation auf einem CI-/Lokal-Setup mit Docker oder live-DB.
- `dotnet build api/API.Tests/API.Tests.csproj`: erfolgreich (3 vorbestehende CS8602-Warnungen in `PostgresWorkflowRepositoryConcurrencyTests.cs`, 0 Fehler).

**Bewusst draussen:**
- Tests fuer `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync`-Pfade als reine Unit-Tests — verlangen Connection-Factory-Schnitt, der explizit nicht in Z9 angefasst wird.
- Coverage fuer `EnsureDirectoryProjectionUserColumnsAsync`, `AutoLinkIdentitiesToAppUsersAsync`, `EnsureDirectoryDepartmentsExistAsync`, `EnsureDevelopmentDefaultGroupMappingsAsync` — nicht in der Z9-Auftragsliste; wuerden den Slice ohne neuen Hebel verbreitern.

**Z9-Abschluss:** Mit Z9-3 ist Zyklus 9 abgeschlossen. Naechster Schritt liegt zyklusuebergreifend (`#6` Pagination, `#8` Rotation-Regeneration) oder bei einem neuen, durch Anlass getriggerten Zyklus.

---

## Abgeschlossener Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

**Status:** geschlossen 2026-05-05. Alle priorisierten Hotspots abgearbeitet (#1, #2/#3, #4, #5 verifiziert, #7 Bulk-Lookup) oder mit Begruendung deferred (#8). Coverage-Pflicht-Slices erledigt (Z8-2.1 FactsTest, Z8-2.2 Sweep-Batching-Test, Z8-4.1 Integration-Tests fuer den Bulk-Recipient-Helper). Bewusst deferred bleiben:
- **#8 / Z8-3.2** — admin-getriggert, kein kleiner SQL-/Batch-Hebel ohne breiten Umbau.
- **EntraDirectorySync-Batch-Helfer-Coverage** — `private` hinter `SyncAllAsync` im 2.4k-Zeilen-Service; saubere Test-Isolation verlangt LQ2-Z3-File-Split + Graph-Stub. Wandert nach LQ2-Z3.
- **#6 Departments/Rollen Pagination** — bewusst nicht in Z8 angefasst (FE-Folgen ohne Last-Trigger). Wartet auf konkreten Anlass.

Naechster Zyklus offen — siehe Zyklus-Historie.

### Zyklus-8-Detail (Historie)

**Thema:** Nach Abschluss der Lifecycle- und Validation-Hygiene aus Zyklus 7 ist Skalierbarkeit (Note **B-**) die niedrigste Gesamtbewertung und damit der naechste sinnvolle Hebel. Z2 hat den Workflow-Task-Filter SQL-pre-narrowed, aber an mehreren Stellen laufen Listen, Filter und Sweeps weiter ungebremst durch In-Memory-Pfade. Das ist keine Theorie-Schwaeche, sondern wird bei realer Last sichtbar (Workflow-Liste, MyTasks, RotationOperations, Notification-Dispatch, RotationTask-Sweep).

**Begruendung gegen alternative Zyklen:**
- *EntraDirectorySyncService Split (LQ2-Z3, 2485 Z.)* bleibt deferred ohne Trigger — Timer-Pfad, kein User-Pfad, keine offene Beschwerde. Reine Bewegung.
- *Auth-Haertung* — Note B+ stabil, keine konkrete neue Luecke seit Zyklus 4.
- *Frontend-Polish* — nach FE-25..FE-31 stabil; B+ ohne offenen Schmerzpunkt.
- *Repository-Splits* ohne Anlass — reine Hygiene ohne klaren Last- oder Produkthebel.

**Fokus:**
1. Inventur saemtlicher Pfade mit unbeschraenktem Laden, In-Memory-Filter/-Sortierung und N+1-Risiko.
2. Top-Hotspots als SQL-Pushdown / Pagination loesen.
3. Background-Sweeps (RotationTask-Sweep, Notification-Dispatch) auf Last gegenpruefen.
4. Test-Coverage fuer die neu gepushten Pfade nachziehen.

**Priorisierung:**

| ID | Befund | Prio |
|----|--------|------|
| Z8-1.1 | Inventur: Endpunkte + Repos mit unbeschraenktem Laden, In-Memory-Filter/-Sort, N+1 | **done** (2026-05-05) — siehe § Z8-1.1 Hotspot-Inventur |
| Z8-1.2 | Top-3-Hotspot-Auswahl + Slice-Plan auf Basis der Inventur | **done** (2026-05-05) — siehe § Z8-1.2 Slice-Plan |
| Z8-2.1 | Hotspot #1 — `WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`: N+1 fuer `IsManagerCreatableDefinition` aufloesen (Bulk-/SQL-Pushdown) | **done** (2026-05-05) — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 | Hotspot #2+#3 (gemeinsamer Slice) — `RotationNotificationService` Daily-Sweep: `LIMIT`/Batch-Fetch + Batch-Update der Dispatch-Results | **done** (2026-05-05) — Service-Loop mit `DispatchBatchSize=200`; Apply mit Bulk-Metadata + Bulk-UPDATE via `unnest` |
| Z8-2.3 | Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync`: Group-Member-Schleifen auf Batch-Upsert/-Insert umstellen | **done** (2026-05-05) — `UpsertDirectoryIdentitiesBatch` (Bulk-Upsert via `unnest` + RETURNING) und `InsertGroupMembershipsBatch` ersetzen pro-Member Round-Trips |
| Z8-3.1 | Hotspot #5 verifizieren + Hotspot #7 Recipient-Bulk-Lookup | HIGH | **done** (2026-05-05) — #5 false positive (CPU/Policy-Pfad ohne Repo-Hits); #7 nutzt jetzt `LoadActiveUserNotificationRecipientsBulk` einmalig pro Preview/Create statt pro Recipient |
| Z8-3.2 | Hotspot #8 `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` | **deferred** (2026-05-05) — siehe § Z8-3.2 Defer-Begruendung. Z8-3 damit geschlossen. |
| Z8-4 | Test-Coverage fuer die neu gepushten Pfade (Integration + Unit) | **done** (2026-05-05) — Z8-4.1 Bulk-Recipient-Helper; Z8-2.1/Z8-2.2-Coverage bereits in den Umsetzungs-Slices enthalten; EntraDirectorySync-Batch-Helfer offen benannt (LQ2-Z3) |

### Z8-4.1 Coverage `LoadActiveUserNotificationRecipientsBulk` (2026-05-05)

Direkte Integration-Coverage fuer den Bulk-Recipient-Helper aus Z8-3.1 ergaenzt. Neuer Test-File `api/API.Tests/PostgresRepositorySharedHelpersIntegrationTests.cs` mit 3 Faellen ueber die bestehende `PostgresWorkflowRepositoryIntegrationCollection`-Fixture:

- `LoadActiveUserNotificationRecipientsBulk_MapsRolesAndSkipsInactive` — drei aktive User (`auth_admin`, `auth_manager`, ohne Rollen) + ein inaktiver + eine unbekannte Id; verifiziert PreferredPath-Mapping (`/workflows`, `/supervisor`, `/tasks/my`), DisplayName/Email/IdentityKey-Treue und das `is_active = TRUE`-Filter.
- `LoadActiveUserNotificationRecipientsBulk_EmptyInput_ReturnsEmpty` — leere Eingabe → leere Map ohne SQL-Roundtrip-Fehler.
- `LoadActiveUserNotificationRecipientsBulk_PrefersNotificationEmailAndExternalKey` — `notification_email`/`external_key` mit umgebenden Whitespace werden via `BTRIM`/`COALESCE` korrekt ueberschrieben.

`EntraDirectorySyncService.UpsertDirectoryIdentitiesBatch` / `InsertGroupMembershipsBatch`: bewusst nicht zusaetzlich abgedeckt. Die Helfer sind `private` innerhalb des 2.4k-Zeilen-Service und liegen hinter `SyncAllAsync` (Live-Graph + DB). Reflection-Probing waere fragil und ohne Mehrwert; ein End-to-End-Test ueber `SyncAllAsync` braucht einen Graph-Stub und gehoert zur LQ2-Z3-File-Split-Arbeit. Grenze offen benannt.

**Verifikation:** `dotnet build api/API.Tests` (0 Fehler), `dotnet test --no-build --filter "Category!=Integration&FullyQualifiedName!~Integration&FullyQualifiedName!~Concurrency"` → 364/364 gruen. Die drei neuen Faelle laufen ueber die testcontainers-/Postgres-Fixture und konnten lokal nicht ausgefuehrt werden (kein Docker-Daemon, kein Postgres auf 127.0.0.1:26432); sie folgen exakt dem Pattern der bestehenden `PostgresWorkflowRepository...IntegrationTests` und greifen dieselbe Fixture, also dieselbe CI-Lauf-Erwartung.

**Empfohlener Einstieg:** Z8-1.1 als reine Inventur — opus/high. Output: konkret nummerierte Hotspot-Liste mit Aufrufer-Pfad und Datenkardinalitaet, kein Code-Change. Erst auf dieser Basis entscheidet Z8-1.2, ob Pagination, Sortier-Pushdown oder N+1-Aufloesung den groessten Hebel hat.

### Z8-1.1 Hotspot-Inventur (2026-05-05)

Reine Inventur, kein Code-Change. Pro Hotspot: Datei/Symbol, Art `(a)` unbegrenztes Laden / `(b)` In-Memory-Filter-Sort / `(c)` N+1 / `(d)` Sweep-/Dispatch-Last, Kardinalitaetsgrund, Prio fuer Z8-1.2.

1. **`WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`** — `api/API/Services/WorkflowCatalogService.cs:8-42`. Art **(b)+(c)**. Laedt **alle** publizierten Definitionen ohne Limit und ruft danach pro Definition `repository.IsManagerCreatableDefinition(definitionKey)` in einer foreach-Schleife auf (Z. 34). Aufrufer: Workflow-Start (Master-Data-Endpoint). Kardinalitaet: pro Manager-User bei jedem Workflow-Anlegen 1 + N DB-Calls; bei wachsendem Definitionsbestand linear teurer. **Prio HIGH**.
2. **`RotationNotificationService.ExecuteDailySweepAsync` + `IRotationRepository.GetDispatchableRotationNotifications`** — `api/API/Services/RotationNotificationService.cs:11-62`, SQL in `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs`. Art **(a)+(d)**. Laedt **alle** dispatchable Notifications ohne `LIMIT` und uebergibt sie en bloc an den Mail-Sender. Kein Batching, kein Pagings. Kardinalitaet: bei Backlog (z. B. nach Mail-Ausfall) zehntausende Saetze in einem Aufruf — Memory- und Mail-Pfad. **Prio HIGH**.
3. **`RotationNotificationOperations.ApplyRotationNotificationDispatchResults`** — `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs:192-299`. Art **(c)+(d)**. Pro Result: Select + Update sequenziell statt Batch-Update. Kardinalitaet: skaliert 1:1 mit #2; verdoppelt die DB-Last des Sweeps. **Prio HIGH** (haengt logisch an #2 — bietet sich als gemeinsamer Slice an).
4. **`EntraDirectorySyncService.SyncAllAsync` (Group-Member-Schleifen)** — `api/API/Services/EntraDirectorySyncService.cs` ~Z. 153-218. Art **(c)+(d)**. Geschachtelte Schleifen `groups` × `members` mit `UpsertDirectoryIdentity` und `InsertGroupMembership` einzeln pro Member. Kein Batch-Insert. Aufrufer: `DirectorySyncHostedService` (24h-Sweep, 2h-Timeout aus Z6). Kardinalitaet: bei breiterer Org (>100 Gruppen × Dutzende Mitglieder) tausende Round-Trips pro Sweep. **Prio HIGH**, aber Timer-Pfad — Last zeigt sich erst beim Timeout. (Hinweis: vollstaendiger File-Split bleibt LQ2-Z3 deferred; hier geht es nur um die Sync-Schleifen, nicht um Strukturhygiene.)
5. **`WorkflowVisibilityService.ApplyWorkflowTaskPermissions`** — `api/API/Services/WorkflowVisibilityService.cs:59-110`. Art **(b)** rein CPU. Pro Task drei Policy-Aufrufe (`CanUpdateTaskStatus`/`CanDecideTaskApproval`/`CanAddTaskComment`). **Z8-3.1 Verifikation (2026-05-05): false positive als DB-Hotspot.** Alle drei Methoden in `AuthorizationPolicyService` arbeiten ausschliesslich auf `CurrentUser` (Rollen/Permissions/Responsibilities) und dem `TaskWithWorkflowDto` (Status, Assignments) — keine Repo-Hits, kein N+1. Bleibt als reiner CPU-Pfad ohne aktuellen Hebel; aus Z8 herausgenommen.
6. **`WorkflowCatalogService.GetDepartmentsAsync` / `GetRolesAsync`** — `api/API/Services/WorkflowCatalogService.cs:44-52`, SQL in `api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs`. Art **(a)**. `ORDER BY` ohne `LIMIT`/Pagination. Kardinalitaet: in einer Filiale unkritisch, in groesseren Org-Strukturen wird die Master-Data-Liste ohne Pagination zum Hot-Path. **Prio MEDIUM** — Pagination/Suche im Frontend bedeutet auch FE-Folgen, deshalb fuer Z8-1.2 separat bewerten.
7. **`PostgresWorkflowNotificationDispatchOperations.BuildReadyTaskNotificationPreviewTargetsAsync`** — `api/API/Repositories/PostgresWorkflowNotificationDispatchOperations.cs` ~Z. 280-313. Art **(c)**. `foreach` ueber Recipient-Ids mit `LoadActiveUserNotificationRecipient` pro Empfaenger statt einem JOIN/IN-Set. Aufrufer: Notification-Preview. **Z8-3.1 done (2026-05-05):** ersetzt durch einmaligen `LoadActiveUserNotificationRecipientsBulk(@userIds = ANY)`. Selber Bulk-Helper auch in `CreateReadyTaskNotificationsAsync` genutzt (identisches Recipient-Pattern, minimaler Mitnahme-Refactor).
8. **`RotationTaskGenerationService.RegenerateDepartmentPlansAsync`** — `api/API/Services/RotationTaskGenerationService.cs:53-64`. Art **(c)**. Liest Plan-Ids einer Abteilung und ruft `SynchronizeRotationGeneratedTasks(planId)` pro Plan in einer foreach-Schleife. Aufrufer: Admin-/Regeneration-Pfad. Kardinalitaet: pro Abteilung × Plaene; admin-getriggert, kein Hot-Path. **Prio LOW-MEDIUM** — eher Z8-3-Material (Sweep/Regeneration-Performance) als Top-3-Kandidat.
9. **`EntraDirectorySyncService.ImportDirectoryIdentitiesAsync`** — `api/API/Services/EntraDirectorySyncService.cs` ~Z. 1114-1153. Art **(c)**. Sequenzieller Import pro Identity inkl. SystemEventLog-Schreiben pro Item. Aufrufer: Admin-Import-Endpoint. Kardinalitaet: blockiert UI bei Massen-Import; nicht permanent unter Last. **Prio LOW-MEDIUM**.

**False Positives / bewusst ausgenommen:**
- `WorkflowLifecycleService` — Commit-Grenze, kein Listenpfad.
- Workflow-/Task-Listen-Filter (`PostgresWorkflowRepository.WorkflowQueryOperations` + `TaskOperations` Filter): bereits durch Z2 SQL-pre-narrowed, keine doppelte Listung.
- `WorkflowDefinitionDraftValidator` / `SnapshotValidator` (Z7) — reiner CPU-Pfad pro Validierung, keine Listen-Last.
- Suche-Endpunkte `SearchWorkflowTargetPersonSources/People` — bereits mit `limit`-Parameter; nicht hier.

**Empfehlung fuer Z8-1.2:** Top-Kandidaten **#1, #2 (+#3 als gemeinsamer Slice), #4**. Begruendung:
- #1 ist nutzersichtbarer Hot-Path bei jedem Workflow-Anlegen → Pagination greift hier nicht, sondern SQL-seitige `IsManagerCreatable`-Auswertung pro Definition oder Bulk-Lookup.
- #2/#3 ist der naechste Skalierungs-Cliff im Background-Sweep mit klarem Hebel (LIMIT + Batch-Update).
- #4 ist der teuerste Sweep ohne Batching; alternativ kann #4 nach Z8-3 verschoben werden, wenn #1/#2 zuerst gehaertet werden.

**Frontend-Folgen:** aus #6 entstehen ggf. FE-Items (Pagination/Suche fuer Departments/Rollen). Erst bei Z8-1.2 entscheiden — bis dahin **kein** FE-Eintrag.

**Frontend-Folgen Status Z8 gesamt:** aktuell **keine**. Z8 ist backend-fokussiert. Wenn Z8-2 API-Vertraege aendert (z. B. Pagination-Tokens, Sortier-Parameter), entstehen erst dann FE-Items in `FRONTEND_TODO.md`. Bis dahin wird keine FE-Arbeit kuenstlich erzeugt.

### Z8-3.2 Hotspot #8 — Defer-Begruendung (2026-05-05)

`RotationTaskGenerationService.RegenerateDepartmentPlansAsync` (`api/API/Services/RotationTaskGenerationService.cs:53-64`) wurde als verbliebener Z8-3-Resthebel geprueft. Ergebnis: **deferred ohne Code-Change**. Begruendung:

- Pfad ist admin-getriggert (Department-Regeneration), kein Hot-/Sweep-Pfad und kein Background-Timer. Keine offene Last-Beschwerde.
- Die foreach-Schleife ruft pro Plan `SynchronizeRotationGeneratedTasks(planId)` auf — eine transaktionale Multi-Step-Synchronisation pro Plan (Diff vs. Bestand, Insert/Update/Delete generierter Tasks, Audit). Es gibt **keinen** kleinen SQL-/Batch-Hebel analog zu Z8-2.x: Bundling mehrerer Plaene in eine Statement-Schicht waere genau der untersagte breite Umbau an `SynchronizeRotationGeneratedTasks` und den zugehoerigen Repository-Pfaden.
- Parallelisierung der Schleife (`Task.WhenAll`) wuerde mehrere transaktionale Sync-Pfade auf dieselbe Connection/Tx-Grenze setzen und ist im aktuellen Pool-/Tx-Modell riskant — kein klarer kleiner Hebel.
- Keine Frontend-Folgen, keine API-Vertragsaenderung.

Damit ist Z8-3 abgeschlossen (Z8-3.1 done, Z8-3.2 deferred). #8 wandert in die zyklusuebergreifenden offenen Befunde mit Defer-Status; Re-Bewertung nur bei konkretem Last-Trigger oder wenn `SynchronizeRotationGeneratedTasks` ohnehin angefasst wird.

### Z8-1.2 Top-3-Auswahl + Slice-Plan (2026-05-05)

Reine Planung, kein Code-Change. Bestaetigt die Empfehlung aus Z8-1.1 und schneidet Z8-2.x.

**Bestaetigte Top-3 fuer Z8-2.x:**

1. **Hotspot #1 — `WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`** → Slice **Z8-2.1**.
   - User-sichtbarer Hot-Path bei jedem Workflow-Anlegen durch Manager.
   - Hebel: N+1 ueber `IsManagerCreatableDefinition` aufloesen (Bulk-Lookup oder SQL-seitige `EXISTS`-Auswertung im selben Statement, das die Definitionen liefert).
   - Pagination greift hier nicht (Auswahl-Liste, fachliche Vollstaendigkeit erforderlich) — daher gezielter Bulk- bzw. JOIN-Pushdown statt LIMIT.
   - Risiko: gering, klar lokal abgrenzbar; keine API-Vertragsaenderung erwartet → keine FE-Folge.
   - Modell: opus, Reasoning: medium..high.

2. **Hotspot #2 + #3 — `RotationNotificationService` Daily-Sweep + `ApplyRotationNotificationDispatchResults`** → **gemeinsamer Slice Z8-2.2**.
   - Begruendung fuer Bundling: #3 ist der DB-Schreibpfad genau fuer die Eintraege, die #2 lieferte. Getrennt zu schneiden hiesse, eine Haelfte (LIMIT) ohne die andere (Batch-Update) zu landen, was den Sweep-Cliff nur halb behebt und zusaetzliche Migrations-Schritte zwischen den Slices erzeugt. Gemeinsam ist der Slice immer noch klein und in einem Service-Namespace.
   - Hebel: dispatchable-Notifications mit `LIMIT`/Batch-Fenster laden, Apply-Phase auf Batch-Update statt Select+Update pro Item.
   - Risiko: gering, Background-Pfad ohne UI-Vertraege.
   - Modell: sonnet, Reasoning: medium..high.

3. **Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync` (Group×Member-Schleifen)** → Slice **Z8-2.3**, bewusst **hinter** #1 und #2/#3.
   - Entscheidung: #4 bleibt in der Top-3, aber **als letzter** der drei Z8-2-Slices. Begruendung: Timer-Pfad (24h-Sweep) ohne aktuelle User-Beschwerde, aber groesster Round-Trip-Hebel pro Sweep und einziger der Top-Sweeps mit Batch-Insert-Potenzial fuer Group-Memberships. Vor #1/#2 zu ziehen waere falsch priorisiert (kein User-Pfad). Nach Z8-3 zu schieben waere unsauber, weil Z8-3 explizit fuer `RotationTaskRegenerationEngine`-Sweep und Resthebel reserviert ist und der Entra-Sweep technisch denselben Batching-Ansatz wie #2 nutzt — also gehoert er thematisch zum Pushdown-Block, nicht zum Resthebel.
   - Hebel: Batch-`UpsertDirectoryIdentity` und Batch-`InsertGroupMembership` statt Item-by-Item; ggf. Set-Diff statt Vollabgleich pro Group.
   - Achtung: vollstaendiger File-Split bleibt LQ2-Z3 deferred — Z8-2.3 fasst nur die Sync-Schleifen an, keine Strukturhygiene.
   - Modell: sonnet, Reasoning: medium.

**Nicht in Top-3 fuer Z8-2.x (bewusst):**
- #5 `WorkflowVisibilityService.ApplyWorkflowTaskPermissions`: vor Slice noch verifizieren, ob Policy-Service intern Repo-Hits macht. Bleibt MEDIUM und wird nach Bedarf in Z8-3 oder einem Folgezyklus aufgenommen.
- #6 `GetDepartmentsAsync` / `GetRolesAsync`: erzeugt API-Vertragsaenderung (Pagination/Suche) und FE-Folgen. Ohne konkreten Last-Trigger nicht in Z8-2 — Re-Bewertung am Ende von Z8.
- #7 `BuildReadyTaskNotificationPreviewTargetsAsync`: gehoert zu Z8-3 (Notification-Pfad-Resthebel).
- #8 `RotationTaskGenerationService.RegenerateDepartmentPlansAsync`: admin-getriggert, kein Hot-Path → Z8-3.
- #9 `EntraDirectorySyncService.ImportDirectoryIdentitiesAsync`: Admin-Massen-Import, kein permanenter Last-Pfad → Folgezyklus.

**Frontend-Folgen Z8-2.x:** weiterhin **keine**. #1, #2/#3, #4 aendern keine API-Vertraege; #6 mit FE-Folgen bewusst nicht in Top-3. `FRONTEND_TODO.md` wird nicht angefasst.

**Reihenfolge / Abhaengigkeiten:**
- Z8-2.1 → Z8-2.2 → Z8-2.3 sequenziell. Keine harten Code-Abhaengigkeiten zwischen den Slices, aber sequenziell, damit der Worker nicht parallel mehrere Pfade halb anfasst und die Tests pro Slice klar zuordenbar bleiben.
- Z8-3 startet nach Z8-2.3 mit Resthebel #5/#7/#8.
- Z8-4 (Test-Coverage) parallel pro Slice mitziehen, nicht erst am Ende — Z8-4 bleibt als eigene ID nur fuer ergaenzende Coverage uebrig.

---

## Abgeschlossener Zyklus 7 — Kurzfassung

Zyklus 7 hat zwei zentrale Hygiene-Bloecke abgeschlossen:
- Lifecycle-Service-Konsolidierung: `WorkflowLifecycleService` ist jetzt die Commit-Grenze fuer Create/Form/Approval/Task.
- Validation-Split: `WorkflowDefinitionValidationService` wurde in Draft-/Snapshot-/Helper-/Catalog-Slices zerlegt.

Die Detailhistorie von Zyklus 7 liegt in:
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2591 → 1563 Z.) Split + Coverage `UpsertDirectoryIdentitiesBatch`/`InsertGroupMembershipsBatch` | **abgeschlossen als Zyklus 9** (2026-05-05) — Z9-1.1/1.2 Inventur+Plan, Z9-2.1/2.2/2.3 Splits, Z9-3 Coverage | Zyklus 3 / Z8 → Z9 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path; kein kleiner SQL-/Batch-Hebel ohne breiten Umbau an `SynchronizeRotationGeneratedTasks` | Zyklus 8 |

---

## Zyklus-Historie

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1 | 2026-04-23 | Code-Review + Hardening (C1–C4, H1–H7, L1/L3/L5/L6) |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, SQL-Task-Filter, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: Hook-Tests, Repo-Splits, Hook-Zerlegung |
| 4 | 2026-05-02 | Naming + Haertungen: LegacyProcessTypeKey, effectiveResponsibilityIds, Error-Boundaries |
| 5 | 2026-05-02..03 | Legacy-Abbau (LA1–LA5): LegacyWorkflowStatus, setup-Node, definition_key, HasLegacyRolePermission, Specs am Node |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope |
| 7 | 2026-05-05 | Lifecycle-Service-Konsolidierung + Validation-Split |
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung (Hotspots #1/#2/#3/#4/#7 gepushed; #5 verifiziert; #8 deferred; Z8-4 Coverage) |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit (LQ2-Z3 aktiviert) — eroeffnet |
