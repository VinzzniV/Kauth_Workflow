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

## Schreibregel fuer Reviews und Findings (verbindlich)

Jedes Review-Finding und jeder Slice in dieser Datei muss neben dem technischen Befund in kurzen Saetzen erklaeren:

- **Was bedeutet das praktisch?** — was ein normal verstaendlicher Leser im Alltag merkt.
- **Warum lohnt es sich, das anzugehen?** — der konkrete Anlass oder das Risiko.
- **Was wird dadurch besser, sicherer, schneller oder wartbarer?** — der erwartete Nutzen.

Reine Technik-Beschreibung ohne Nutzen-/Bedeutung-Erklaerung ist nicht ausreichend. Die Regel gilt fuer alle neuen Zyklen, fuer einzelne Befunde und fuer den jeweils gefuehrten Slice-Plan. Bei zyklusuebergreifend offenen Befunden reicht ein kurzer Hinweis, warum sie aktuell nicht angegangen werden.

Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht fuer Claude unter Codex-Orchestrierung verankert.

---

**Stand**: 2026-05-11 — **Z19 vollstaendig abgeschlossen** (Backend Full Review / Holistic Audit). Alle Slices S1..S9 erledigt. Z18, FE-8 und der Entra-Retrofit-Block (A1, A2, A3, B, C) bleiben am 2026-05-08 abgeschlossen.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..06 Zyklus 2–13; 2026-05-07 Z14; 2026-05-08 Z15–Z18 + A1/A2/A3/B/C; 2026-05-11 Z19 eroeffnet + S1..S9 vollstaendig abgeschlossen).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith reduziert; Lifecycle-Service nach Z7 echte Commit-Grenze fuer zentrale Runtime-Mutationen; groesste Resthebel liegen jetzt bei Skalierbarkeit und Lastpfaden |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor; Z18 hat 1 HIGH-, 5 MEDIUM- und 3 LOW-Findings sichtbar gemacht, vor allem bei Search-Performance, Accessibility und UI-Semantik |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Mehrere Listen-, Sweep- und Dispatch-Pfade sind noch Kandidaten fuer SQL-Pushdown, Pagination oder N+1-Abbau |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen weniger in Benennung als in Hotspot-Pfaden unter Last |

---

## Aktiver Zyklus 19 — Backend Full Review / Holistic Audit (2026-05-11)

Eroeffnet 2026-05-11 als reiner Review-/Planungszyklus, analog zu Z18 (Frontend Full Review). Keine Implementierung in S1.

**Praktisch:** Erstmals seit den punktuellen Backend-Zyklen Z8 (Skalierbarkeit), Z9 (Directory-Sync-Split), Z11 (Listen-Vertraege), Z12/Z13 (Runtime-Health) bekommt der gesamte Backend-Stack einen zusammenhaengenden Review-Pass — Endpoints, Repositories, Services, Authorization, Auth-Pipeline, `Services/Directory/`, Background-Jobs, Schema-/Migrations-Hygiene und Test-Coverage. Ergebnis ist eine priorisierte Findings-Liste (HIGH/MEDIUM/LOW) im Stil von Z18, an der Folgeslices gezielt aufraeumen.

**Lohnenswert:** Skalierbarkeit (B-) und Testbarkeit (B) sind die schwaechsten Noten in der Gesamtbewertung. Deferred Hotspots (Z8-3.2/#8 `RegenerateDepartmentPlansAsync`, Z16-S4 Automation-Snapshot-Vertrag) und die jueengsten DB-Drift-Vorfaelle (manuelle SQL-Helfer fuer `approval_spec_key` und `directory_identities.job_title`, dokumentiert in `db/manual/`) zeigen systemische Resthebel, die einzeln klein wirken, in Summe aber Wartbarkeit und Betriebsfestigkeit kosten. Eine breite Bestandsaufnahme jetzt ist deutlich billiger als wiederholte Hotfixes unter Last und schafft die Andockflaeche fuer den naechsten gerichteten Schritt Richtung Definition Layer / Runtime / Automation Layer.

**Nutzen:** vollstaendige Inventur der noch nicht von Z8/Z9/Z11/Z12/Z13 adressierten Hotspots; klare Begruendung fuer deferred / nicht-jetzt-Befunde; ein dokumentierter Slice-Plan, an dem nachfolgende Umsetzungszyklen sauber andocken; bessere Priorisierung gegen das Zielbild statt isolierter Mikro-Optimierungen; transparenter Vertrag, was nicht zum Audit gehoert (kein breiter Architektur-Umbau).

| Slice | Inhalt | Prio | Status |
|-------|--------|------|--------|
| Z19-S1 | Backend Full Review pass: Audit ueber `api/API/Endpoints`, `api/API/Repositories`, `api/API/Services`, `Authorization/`, `Auth/`, `Services/Directory/`, Background-/Sweep-Jobs, Schema-/Migrations-Hygiene (insb. DB-Drift-Pfad und `db/manual/`-Workflow), Test-Coverage-Luecken. Liefert priorisierte Findings (HIGH/MEDIUM/LOW) mit Begruendung, Bereich und vorgeschlagenem Slice-Schnitt. **Doku-only**, keine Code-Aenderung. | HIGH | **done 2026-05-11** |
| Z19-S2 | Background-Sweep-Timeout fuer `DirectorySyncHostedService` analog `RotationNotificationHostedService.SweepTimeout` (2h-Cap). | HIGH | **done 2026-05-11** |
| Z19-S3 | Schema-Paritaets-Check zwischen `db/01_schema.sql` und `db/manual/*.sql` (Migrations-Manifest `db/manual/manifest.json` + `api/API.Tests/SchemaParityTests.cs`). | HIGH | **done 2026-05-11** |
| Z19-S4 | `WorkflowLifecycleService` und `WorkflowRuntimeService` durchgaengig auf `CancellationToken` umstellen (HTTP-Abbruch erreicht offene DB-Transaktion). | HIGH | **done 2026-05-11** (Service-/Interface-/Endpoint-Ebene; tiefe statische Repo-Helfer als Restrest dokumentiert) |
| Z19-S5 | `SystemEventLogService`: stilles Schlucken von `UndefinedTable` ersetzen, Cursor-Pagination statt LIMIT/OFFSET, eigene Unit-Tests fuer Redaction/Normalization/Filter. | MEDIUM | **done 2026-05-11** |
| Z19-S6 | `PostgresUserAuthorizationRepository.AdminOperations.cs` (1886 LOC) + `…AdminReadOperations.cs` (1002 LOC) nach Z9-Pattern aufteilen. | MEDIUM | **done 2026-05-11** |
| Z19-S7 | `WorkflowAutomationService.TryProcessNextPendingJobAsync` Failure-of-Failure absichern (Job-Claim leakt, wenn `CompleteAutomationJobFailure` selbst wirft). | MEDIUM | **done 2026-05-11** |
| Z19-S8 | Verdikt fuer deferred Befunde Z8-3.2/#8 (`RegenerateDepartmentPlansAsync`) und Z16-S4 (Automation-Snapshot-Vertrag): explizit weiter deferred mit Frist, oder eigener Slice. | MEDIUM | **done 2026-05-11** (beide formal deferred mit Begruendung — siehe S8-Ergebnis) |
| Z19-S9 | Hygiene-Batch: Stray-Verzeichnis `db/init/prod;C/` loeschen; doppeltes `Task.WhenAll` + serielles `BuildHostHealthAsync` in `AdminRuntimeHealthService` zusammenfuehren; leerer 4-Zeilen-Tombstone `PostgresWorkflowRepositoryProcessTypeIntegrationTests.cs` entfernen; `WorkflowRuntimeService.GetWorkflowsAsync` Token-Propagation. | LOW | **done 2026-05-11** (L1 + L4 in S2/S4-Commit; L2 + L3 in diesem Bundle) |

**Empfohlenes Modell/Effort fuer Folgeslices:**
- **Z19-S1** (Audit, abgeschlossen): `claude-opus-4-7` + `--effort high`.
- **Z19-S2..S4** (HIGH-Umsetzung): `claude-opus-4-7` + `--effort high` fuer Lifecycle-/Cancellation- und Schema-/Migrations-Eingriffe; **Z19-S5..S7** typisch `claude-sonnet-4-6` + `--effort medium`; **Z19-S8** ist eine Entscheidung, kein Code; **Z19-S9** `claude-sonnet-4-6` + `--effort medium`.

**Bewusst NICHT in Z19:** breite Architektur-Umbauten am Workflow-Definition-/Runtime-/Automation-Layer (Migrationspfad-Arbeit bleibt eigenstaendig), neue Frontend-Findings (Z18 vollstaendig abgeschlossen), Berechtigungsmodell-Aenderungen ohne konkretes Risiko, Mobile-/Tablet-Layout (R10 bleibt eigener Backlog).

**Naechster Schritt (nach diesem Bundle):** Z19-S6 (`PostgresUserAuthorizationRepository`-Split) — einziger offener MEDIUM-Slice. S5 + S7 + S8 + S9/L2 + S9/L3 sind am 2026-05-11 in diesem gebuendelten Commit erledigt.

---

## Aktiver Zyklus 19 — S1 Ergebnis (2026-05-11)

Audit-Pass ueber `api/API/Endpoints`, `api/API/Repositories`, `api/API/Services`, `Authorization/`, `Auth/`, `Services/Directory/`, Background-/Sweep-Jobs, `db/01_schema.sql`, `db/init/`, `db/manual/`, `api/API.Tests` ist abgeschlossen. Keine Code-Aenderungen, nur Dokumentation. Findings sind nach Risiko fuer Betrieb/Daten/Wartbarkeit priorisiert; jeder Befund erklaert kurz, was er praktisch bedeutet, warum er sich lohnt und was dadurch besser wird.

### HIGH

**H1 — `DirectorySyncHostedService` ohne Sweep-Timeout.**
`api/API/Services/DirectorySyncHostedService.cs` ruft `directorySyncService.SyncAllAsync(stoppingToken)` direkt auf; `RotationNotificationHostedService` setzt im Gegensatz dazu `SweepTimeout = TimeSpan.FromHours(2)` mit `sweepCts.CancelAfter(SweepTimeout)`. **Praktisch:** ein haengender Graph-Call (Drossellung, Netzwerk, Tenant-Latenz) blockiert den Sync-Loop unbemerkt bis zum Service-Restart — neue Mitarbeiter erscheinen nicht im Dev-Sim-Login, Re-Sync-Effekte verzoegern sich. **Lohnenswert:** der Vorfall ist betrieblich diagnostisch teuer (kein Healthcheck, der ihn als „haengend" zeigt) und das Pattern existiert bereits ein paar Meter weiter im selben Verzeichnis. **Nutzen:** harte Obergrenze fuer einen Einzelsweep, Sichtbarkeit im Admin-Runtime-Health-Block, kein einseitiger Hang.

**H2 — `db/manual/`-Workflow ohne Schema-Paritaetspruefung.**
`db/01_schema.sql` ist die einzige Wahrheit fuer frische DBs; bestehende DBs werden ueber manuelle SQL-Helfer in `db/manual/` (zuletzt `2026-05-08_rename_approval_task_template_key_to_approval_spec_key.sql` und `2026-05-08_add_directory_identities_job_title.sql`) nachgezogen. Es gibt keinen Test/Check, der sicherstellt, dass eine Inplace-DB nach Anwendung aller Helfer wirklich `01_schema.sql` entspricht. **Praktisch:** Drift faellt erst beim naechsten Laufzeitfehler auf (`column does not exist` beim Directory-Sync, `relation … does not exist` bei Startup-Validierung) — und nur, wenn ueberhaupt jemand den passenden Codepfad ausloest. **Lohnenswert:** zwei Vorfaelle innerhalb einer Woche zeigen, dass das aktuelle „Codex/Claude denkt dran"-Pattern nicht traegt. **Nutzen:** verlaesslicher Migrationspfad bis zur Live-Schaltung, keine stillschweigenden Schema-Abweichungen mehr; klare Andockflaeche fuer den spaeteren Wechsel auf additive Migrationen.

**H3 — `WorkflowLifecycleService`/`WorkflowRuntimeService` propagieren `CancellationToken` nicht.**
`api/API/Services/WorkflowLifecycleService.cs` nutzt fuenfmal `CancellationToken` und kennt weder fuer `CreateWorkflowInstanceAsync` noch fuer `UpdateTaskStatusAsync` einen Token-Parameter. `WorkflowRuntimeService.GetWorkflowsAsync` empfaengt zwar einen Token, reicht ihn aber nicht ans Repository weiter. **Praktisch:** wenn ein Client die HTTP-Anfrage abbricht (Browser-Reload, Timeout, Navigation), laeuft die DB-Transaktion samt Hold-Locks weiter — schlimmstenfalls werden Notification-Outbox-Inserts noch ausgefuehrt, die zugehoerige Lifecycle-Reaktion ist aber fuer den Anrufer egal. **Lohnenswert:** das ist genau der Pfad, der unter Last fuer Lock-Eskalationen sorgt (lange offene Tx auf `workflow_*`-Tabellen blockieren Sweep-Jobs). **Nutzen:** sauberer Abbruchpfad bis in den DB-Layer; weniger zombieartige Transaktionen unter Last; konsistenter Vertrag mit dem Rest des Services-Layers (Z9-Pattern).

### MEDIUM

**M1 — `SystemEventLogService` schluckt `UndefinedTable` still und paginiert ueber OFFSET.**
`api/API/Services/SystemEventLogService.cs` faengt in INSERT- und SELECT-Pfaden `PostgresException ex when ex.SqlState == PostgresErrorCodes.UndefinedTable` und liefert leise leeres Ergebnis. Listen-Read nutzt `LIMIT @limit OFFSET @offset` und ILIKE-Substring-Suche ueber viele Spalten; eine `SystemEventLogServiceTests.cs` existiert nicht. **Praktisch:** wenn die `system_event_log`-Tabelle wirklich fehlt (DB-Drift, neue Umgebung), faellt das Admin-Dashboard nicht auf — es zeigt einfach „keine Events". Suche skaliert ueber `OFFSET` linear mit dem Tabellenwachstum. **Lohnenswert:** das ist genau der Pfad, ueber den Operatoren Drift entdecken sollten. **Nutzen:** sichtbarer Healthcheck-Fail statt stiller Leere; vorhersagbare Suche unter Wachstum; gezielte Unit-Tests fuer Redaction/Normalization/Filter geben Z19-S5 einen kleinen, klar bewertbaren Hebel.

**M2 — `PostgresUserAuthorizationRepository` Repo-Monolith-Resthebel.**
`api/API/Repositories/PostgresUserAuthorizationRepository.AdminOperations.cs` hat 1886 LOC, `…AdminReadOperations.cs` 1002 LOC. Beides waechst weiter, beide kombinieren Admin-Mutation und Admin-Listen-Reads. **Praktisch:** lange Dateien sind im Review-/Merge-Pfad teurer, Konfliktrate steigt mit der naechsten Auth-/Berechtigungsaenderung. **Lohnenswert:** das Splitt-Pattern aus Z9 (Directory) und Z11 (Workflow-Definition-Admin) ist erprobt; ein weiterer Repo passt zum Pattern. **Nutzen:** kleinere, fokussiertere Partials; geringere Konfliktwahrscheinlichkeit; klare Read-/Write-Trennung.

**M3 — `WorkflowAutomationService.TryProcessNextPendingJobAsync` Failure-of-Failure.**
Beim Verarbeiten eines Automation-Jobs catcht der Service eine generische Exception und ruft `CompleteAutomationJobFailure` auf. Wirft dieser Call selbst (z.B. transienter DB-Fehler), bleibt der Job im `claimed`-Status haengen, ohne dass der Worker ihn jemals wieder zieht. **Praktisch:** ein einzelnes DB-Hiccup im Fehlpfad kann einen Automationsjob dauerhaft blockieren — sichtbar nur durch lange „in Bearbeitung"-Anzeigen im Admin. **Lohnenswert:** kleiner Patch, der eine ganze Klasse von „warum laeuft mein Automatisierungsschritt nie weiter"-Tickets schliesst. **Nutzen:** sichere Re-Claim-Logik, idempotentes Failure-Handling, kein menschliches Eingreifen mehr noetig.

**M4 — Verdikt fuer deferred Befunde aus frueheren Zyklen.**
`Z8-3.2/#8` (`RotationTaskGenerationService.RegenerateDepartmentPlansAsync`-Schleife) und `Z16-S4` (Automation-Snapshot-Vertrag) stehen seit mehreren Zyklen auf „deferred" ohne Frist. **Praktisch:** beide bleiben latent als „Admin loest aus, niemand weiss wie lange es dauert"-Pfade. **Lohnenswert:** Z19 ist die richtige Stelle, beide entweder mit Begruendung dauerhaft zu archivieren oder als eigenstaendige Slices zu schneiden. **Nutzen:** keine ewigen Watchout-Eintraege; klare Erwartung an Folgezyklen.

### LOW

**L1 — Stray `db/init/prod;C/` Verzeichnis.**
Neben `db/init/prod/` existiert ein leeres `db/init/prod;C/` — eindeutig ein Shell-Typo-Artefakt. **Praktisch:** verwirrt nur den naechsten Leser. **Lohnenswert:** kostet keinen Aufwand. **Nutzen:** weniger Rauschen in der Repo-Wurzel des `db/`-Layouts.

**L2 — `AdminRuntimeHealthService` doppeltes `Task.WhenAll`, serielles Host-Probe.**
`api/API/Services/AdminRuntimeHealthService.cs` Z. 37–41 ruft `await Task.WhenAll(...)` zweimal mit ueberlappenden Tasks auf; `BuildHostHealthAsync` laeuft serielle nach dem initialen WhenAll statt parallel mit den anderen Probes. **Praktisch:** unter Last sind das ein paar zusaetzliche Millisekunden auf dem Admin-Dashboard-Endpunkt, sonst keine Auswirkung. **Lohnenswert:** sauberer Read, ehrliche Parallelitaet. **Nutzen:** weniger Wait-Zeit, klareres Lese-Muster fuer kuenftige Probes.

**L3 — Leerer 4-Zeilen-Tombstone `PostgresWorkflowRepositoryProcessTypeIntegrationTests.cs`.**
Die Datei besteht nur aus `namespace API.Tests;` plus Kommentar „Entfernt in Slice 6.3d-iii". **Praktisch:** Datei wird vom Test-Runner geladen, leistet aber nichts. **Lohnenswert:** ein `git rm`. **Nutzen:** weniger Verwirrung beim Durchsehen des Test-Projekts.

**L4 — `WorkflowRuntimeService.GetWorkflowsAsync` schluckt `CancellationToken`.**
Methode hat einen `CancellationToken`-Parameter, gibt ihn aber nicht ans Repository weiter. **Praktisch:** subset von H3 fuer die Read-Seite. **Lohnenswert:** einzeiliger Fix. **Nutzen:** konsistente Token-Propagation.

### Slice-Schnitt-Empfehlung

- HIGH zuerst, in der Reihenfolge **S2 (Sweep-Timeout) → S3 (Schema-Paritaet) → S4 (Cancellation)**: jeder Slice ist eigenstaendig, klein und hat ein erprobtes Pattern als Vorbild.
- MEDIUM danach, in der Reihenfolge **S5 (SystemEventLog) → S6 (AuthZ-Repo-Split) → S7 (Automation-Failure-Failure) → S8 (Deferred-Verdikt)**.
- LOW zum Schluss als gebuendelter Hygiene-Batch **S9**.

---

## Aktiver Zyklus 19 — S2 + S4 + L4 Ergebnis (2026-05-11)

S2, S4 und das L4-Sub-Item von S9 sind in einem einzigen gebuendelten Commit erledigt; H1/H3 sind damit als Befund abgehakt, L4 ebenfalls.

**Was wurde geaendert:**
- `DirectorySyncHostedService` hat jetzt einen `SweepTimeout = TimeSpan.FromHours(2)`, gespiegelt aus `RotationNotificationHostedService`. Pro Sweep wird ein verlinkter `CancellationTokenSource` mit `CancelAfter(SweepTimeout)` aufgebaut; ein Timeout wird als Warnung geloggt und als Hosted-Log-Eintrag `scheduled_directory_sync_timeout` geschrieben, gefolgt von 5-Minuten-Backoff vor dem naechsten Versuch.
- `IWorkflowLifecycleService` traegt jetzt auf allen acht Methoden einen optionalen `CancellationToken cancellationToken = default`. Die Implementierung in `WorkflowLifecycleService` reicht den Token an `OpenAsync`/`BeginTransactionAsync`/`CommitAsync` und an alle direkten Aufrufe der scoped-Repo-Schnittstelle weiter. `ExecuteDefinitionRuntimeMutationAsync<T>` traegt den Token explizit.
- `IWorkflowLifecycleScopedRepository` reicht den Token jetzt auf den fuenf zentralen In-Scope-Methoden (`UpdateTaskStatusInScope`, `DecideTaskApprovalInScope`, `CompleteRuntimeTaskNodeInScope`, `TryAdvanceRuntimeSetupInScope`, `ApplyApprovalNodeDecisionInScope`) optional durch; bestehende Aufrufer ohne Token bleiben kompatibel.
- `IWorkflowRepository.GetFilteredWorkflows` trag jetzt einen `CancellationToken`-Parameter und reicht ihn ueber die Postgres-Partials (`WorkflowQueryOperations`, `TaskMetadataOperations`, `RequirementOperations`) bis in die `Read`-/`ExecuteReader`-Aufrufe weiter. Damit ist L4 vollstaendig erledigt: ein Client-Abbruch auf `/workflows` schneidet auch den DB-Reader.
- `TaskApplicationService`, `WorkflowRuntimeService.CreateWorkflowAsync`/`GetWorkflowsAsync`, `WorkflowDefinitionRuntimeService` und alle vier betroffenen Endpoint-Dateien (`/workflows` GET, `/tasks/{id}/status` PATCH, `/tasks/ref/{taskRef}/status` PATCH, `/tasks/{id}/approval-decision` POST, `/tasks/ref/{taskRef}/approval-decision` POST, `/admin/runtime/workflow-instances` POST, `…/form-completions` POST, `…/approval-completions` POST, `…/task-completions` POST) binden jetzt den Request-Token und reichen ihn durch.
- Test-Stubs in `WorkflowEndpointsTests`, `WorkflowAutomationServiceTests`, `WorkflowLifecycleServiceTests`, `AdminWorkflowDefinitionConfigEndpointsTests` haben die neuen Signaturen mit optionalem Default-Token uebernommen.

**Bewusst NICHT veraendert (Resthebel):**
- Tiefe statische Repository-Helfer (`PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal`, `CreateWorkflowNodeInstance`, `InsertWorkflowRuntimeEvent`, `LoadPublishedWorkflowDefinitionVersion`, einige Helfer in `PostgresRepositorySharedHelpers.*`) bekommen den Token nicht. **Praktisch:** ein abgebrochener Request schneidet jetzt zwar Verbindung und Transaktion am Commit-Punkt, die innerhalb der Tx bereits laufenden statischen Sub-Queries laufen aber bis zur naechsten `CommandText`-Grenze weiter. **Lohnenswert nicht jetzt:** das wuerde mehrere Hundert Signaturen anfassen und steht zur Z19-Slice-Definition explizit ausserhalb (siehe Brief). **Nutzen, wenn spaeter angegangen:** ehrliche End-to-End-Abbruchsemantik bis ins niedrigste DB-Lese-Statement; sinnvoll erst, wenn ein konkreter Lock-/Latenz-Befund das motiviert.
- L2 (`AdminRuntimeHealthService` doppeltes `Task.WhenAll` + serielles Probe) und L3 (Tombstone-Datei) bleiben als unbearbeiteter Rest von S9. L1 (`db/init/prod;C/` Stray) wurde gemeinsam mit Z19-S3 am 2026-05-11 entfernt.

**Tests:**
- `dotnet build` (API + Tests, Verify-Ausgabepfad zwecks laufender Dev-API-Lock-Datei): grueen, 0 Warnungen, 0 Fehler.
- `dotnet test … --filter "FullyQualifiedName~WorkflowLifecycleServiceTests|WorkflowEndpointsTests|AdminWorkflowDefinitionConfigEndpointsTests|DirectorySyncHostedServiceTests"`: 51/51 grueen.
- `dotnet test … --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Concurrency"` (gesamter Non-Integration-Block): 440/440 grueen.

---

## Aktiver Zyklus 19 — S3 + L1 Ergebnis (2026-05-11)

S3 (Schema-Paritaets-Check) und L1 (Stray-Verzeichnis) sind in einem gebuendelten Slice abgeschlossen; H2 ist damit als Befund abgehakt, L1 ebenfalls.

**Praktisch:** Bisher hat ein neues `db/manual/<datum>_*.sql` darauf vertraut, dass alle Beteiligten gleichzeitig `db/01_schema.sql` angefasst haben — Drift ist erst beim naechsten Laufzeitfehler in einer bestehenden DB aufgefallen (z. B. `column "approval_spec_key" does not exist`). Ab jetzt prueft der normale `dotnet test`-Lauf, ob jede manuelle Migration ihren End-Stand wirklich in `db/01_schema.sql` widerspiegelt und ob alte Marker (z. B. umbenannte Spaltennamen) dort wirklich verschwunden sind.

**Lohnenswert:** Zwei DB-Drift-Vorfaelle innerhalb einer Woche haben gezeigt, dass das „Codex/Claude denkt dran"-Pattern allein nicht traegt. Ein kleines Manifest plus 4 xUnit-Tests kostet quasi nichts, schliesst aber die konkrete Vorfallklasse vollstaendig.

**Nutzen:** verlaesslicher Migrationspfad bis zur Live-Schaltung, keine stillschweigenden Schema-Abweichungen mehr, klare Andockflaeche fuer den spaeteren Wechsel auf additive Migrationen.

**Was wurde geaendert:**
- `db/manual/manifest.json` (neu) — registriert jede `*.sql`-Datei mit `expect_in_schema` (Substrings, die nach der Migration in `db/01_schema.sql` stehen muessen) und `forbid_in_schema` (Substrings, die nach einer Umbenennung/Loeschung dort nicht mehr auftauchen duerfen). Aktuell registriert: `2026-05-08_rename_approval_task_template_key_to_approval_spec_key.sql` und `2026-05-08_add_directory_identities_job_title.sql`.
- `db/manual/README.md` (neu) — beschreibt Konvention, Workflow fuer neue Schema-Aenderungen und Verweis auf den Test.
- `api/API.Tests/SchemaParityTests.cs` (neu) — 4 xUnit-Tests: (1) jede `*.sql` in `db/manual/` ist im Manifest gelistet; (2) jeder Manifest-Eintrag verweist auf eine existierende Datei; (3) jeder `expect_in_schema`-Substring steht in `db/01_schema.sql`; (4) kein `forbid_in_schema`-Substring steht in `db/01_schema.sql`.
- `db/init/prod;C/` (entfernt) — leeres Stray-Shell-Typo-Verzeichnis ohne git-Tracking; L1 damit erledigt.
- `KauthWorkflow/Betrieb/Setup.md` — Hinweis auf Manifest + Paritaets-Test im Setup-Block fuer bestehende DBs.

**Bewusst NICHT veraendert:**
- kein voller Postgres-Syntax-Parser; bewusst substring-basiert. Der Check macht keine Aussage darueber, ob die SQL syntaktisch korrekt ist, sondern nur darueber, ob ihr Soll-Endzustand in `db/01_schema.sql` sichtbar ist.
- keine automatische Inplace-Anwendung; manuelle Migrationen bleiben absichtlich manuell ausfuehrbar (Backup/Snapshot vor Anwendung).
- kein Migrations-Framework-Umbau (Flyway/EF Migrations). Der Wechsel auf additive Migrationen ist erst nach Live-Schaltung vorgesehen; bis dahin reicht der Manifest-/Test-Pfad als Drift-Sichtbarkeit.

**Tests:**
- `dotnet test api/API.Tests/API.Tests.csproj --filter "FullyQualifiedName~SchemaParityTests" -p:OutputPath=bin/Verify/`: 4/4 gruen.
- `dotnet test api/API.Tests/API.Tests.csproj --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Concurrency" -p:OutputPath=bin/Verify/`: 444/444 gruen (440 vorher + 4 neu).
- `OutputPath=bin/Verify/` weiterhin notwendig, weil der lokale Dev-API-Prozess die Default-Output-Pfade sperrt — kein Code-Problem.

---

## Aktiver Zyklus 19 — S5 + S7 + S8 + S9/L2 + S9/L3 Ergebnis (2026-05-11)

S5, S7, S8, L2 und L3 in einem gebuendelten Commit erledigt. Z19 damit bis auf S6 vollstaendig abgeschlossen.

**S5 — SystemEventLogService:**
- UndefinedTable-Swallow in `WriteAsync` ersetzt: ILogger hinzugefuegt, LogError statt stillem Schlucken. Admins sehen jetzt im Application-Log, wenn die Tabelle fehlt.
- UndefinedTable-Catches aus `GetAdminLogsAsync` und `GetAdminLogSummaryAsync` entfernt: fehlende Tabelle propagiert als HTTP-500 zum Admin — sichtbares Drift-Signal statt stiller Leere.
- `GetAdminLogsAsync` jetzt Cursor-basiert (`CursorPageDto<AdminSystemLogEntryDto>` statt `IReadOnlyList`): keyset-Pagination ueber `(created_at DESC, id DESC)`, opaque Base64-Cursor (Z11-Pattern), `hasMore`-Flag. `LIMIT/OFFSET` entfernt.
- `SystemEventLogQuery.Offset` durch `Cursor` (string?) ersetzt. `BuildQueryCommand` in `BuildFilterCommand` aufgeteilt — Cursor-Parameter nur im List-Pfad.
- Frontend (`adminApi.ts`, `AdminSystemLogSection.tsx`): `offset` durch `cursor` ersetzt, Pagination auf „Nächste Seite / Zurück zum Anfang" umgebaut.
- 67 neue Unit-Tests (`SystemEventLogServiceTests.cs`): Redaction (token/secret/password/key/refresh/authorization), Body-Omission (htmlBody/textBody/mailBody/messageBody), Normalization (severity/source/message/optionalText), ShouldRedact/ShouldRedactBody, Cursor-En-/Decodierung.

**S7 — WorkflowAutomationService Failure-of-Failure:**
- `IWorkflowAutomationRepository.UnclaimAutomationJobAsync` hinzugefuegt; implementiert in `PostgresWorkflowRepository.AutomationOperations.cs` (setzt Job auf `pending` mit `available_at = NOW()`).
- `TryProcessNextPendingJobAsync` Failure-Pfad: `CompleteAutomationJobFailure` in try-catch gekapselt. Scheitert dieser Call, wird `UnclaimAutomationJobAsync` versucht, damit der Job wieder vom Worker aufgenommen werden kann. Scheitert auch Unclaim, wird LogError ausgegeben — kein Crash des Workers.
- `systemEventLogService.WriteAsync` im Failure-Pfad ebenfalls in try-catch (best-effort; kein Worker-Crash bei fehlendem System-Log).
- Neuer Test `TryProcessNextPendingJobAsync_UnclaimsJobWhenCompleteFailureThrows` prueft Unclaim-Pfad.

**S8 — Verdikt deferred Befunde:**
- **Z8-3.2/#8** (`RegenerateDepartmentPlansAsync`): formal deferred, kein Verfallsdatum. Kein kleiner SQL-/Batch-Hebel ohne breiten Umbau an `SynchronizeRotationGeneratedTasks`. Der richtige Zeitpunkt ist ein dedizierter Rotation-Scheduling-Umbau im Zuge der geplanten „explizit transaktionalen Rotation-Task-Generierung" (Zielarchitektur-Meilenstein). Bis dahin: Admin-getriggert, kein Hot-Path, Risiko akzeptabel. Eintrag in `CODE_REVIEW.md` § Offene Befunde bleibt.
- **Z16-S4** (Automation-Snapshot-Zeitstempel): formal deferred bis Produkt-Entscheidung. `AutomationPropertyCatalog.cs` referenziert `appUserId`/`directoryIdentityId` ohne formalen Snapshot-Zeitstempel. Kein technischer Blocker — der fehlende Snapshot betrifft rueckwirkende Klarheit bei Automation-Properties, nicht aktuelle Korrektheit. Sobald Produkt entscheidet, ob Snapshot in `workflow_automation_jobs` persistiert wird, ist Z16-S4 ein eigenstaendiger Slice. Eintrag in `KauthWorkflow/Architektur/Entscheidungen.md` bereits vorhanden.

**S9/L2 + L3:**
- `AdminRuntimeHealthService`: doppeltes `Task.WhenAll` entfernt. `BuildHostHealthAsync` wird jetzt parallel mit den anderen Probes gestartet, einziges `await Task.WhenAll(...)` ueber alle sechs Tasks. Keine Funktionsaenderung, ehrlichere Parallelitaet.
- Tombstone `PostgresWorkflowRepositoryProcessTypeIntegrationTests.cs` (4-Zeilen-Kommentar-Datei) entfernt.

**Tests:**
- `dotnet test … --filter "FullyQualifiedName~SystemEventLogServiceTests|FullyQualifiedName~WorkflowAutomationServiceTests"`: 70/70 gruen.
- `dotnet test … --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~Concurrency" -p:OutputPath=bin/Verify/`: 511/511 gruen (444 vorher + 67 neu).
- TypeScript-Build (`npx tsc --noEmit`): 0 Fehler.

**Offener Rest:** Z19-S6 (`PostgresUserAuthorizationRepository`-Split nach Z9-Pattern). Alle anderen Z19-Slices done.

---

## Archivstatus

- Die Detailzyklen **Z8 bis Z16** liegen in `CODE_REVIEW_ARCHIVE.md`.
- Die Detailhistorie von **Zyklus 7** liegt in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- In dieser aktiven Datei bleiben nur Gesamtbewertung, offene zyklusuebergreifende Befunde und die grobe Historie.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
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
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung — abgeschlossen |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit — abgeschlossen |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — abgeschlossen |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege in Umsetzung — abgeschlossen |
| 12 | 2026-05-06 | Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health — abgeschlossen |
| 13 | 2026-05-06 | Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block — abgeschlossen |
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — abgeschlossen |
| 15 | 2026-05-08 | Implementierung Mehrrollen-Persona: S1 Datenmodell/Hook done; S2 Override-Stellen done; S3 Persona-Switcher done — **vollstaendig abgeschlossen** |
| 16 | 2026-05-08 | Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag — **vollstaendig abgeschlossen** (Z16-S4 deferred) |
| 17 | 2026-05-08 | Light/Dark-Mode-Theme-Leaks: `.card-primary` nutzte `--surface-hero-background` (dunkelblau) auch im Light Mode — Z17-S1 behoben |
| 18 | 2026-05-08 | Frontend Full Review — Z18-S1 done; Z18-S2 done (Batch A); Z18-S3 done (Batch B: F6/F7); Z18-S4 done (F4: /search→/workflows Redirect) — **vollstaendig abgeschlossen** |
| A1 | 2026-05-08 | People-Import Backend-Fundament — `POST /admin/people/import-from-directory` + `GET /admin/directory/unlinked-identities`; job_title-Durchleitung mitgeliefert — **abgeschlossen** |
| A2 | 2026-05-08 | Import-UI (Admin) — Neue Sektion „Aus Entra importieren" mit Abteilungs-Gruppierung, Checkbox-Auswahl, Vorschau (Name, Stelle, Konto-Status) und Import-Button; deaktivierte Konten standardmäßig ausgeblendet — **abgeschlossen** |
| A3 | 2026-05-08 | Mitarbeiterkarte fehlende Felder + retroaktiver Status — PATCH /admin/people/{personId} (entry_date, badge_number); Inline-Edit in PersonOverviewSection (Admin-only, useMutation + Invalidierung); Lücken-Warnung; Badge „Retroaktiv importiert"; erklärender Text im leeren Vorgangsbereich — **abgeschlossen** |
| B | 2026-05-08 | Entra-Stellenbezeichnungen in Abteilungs-Stellen importieren — GET /admin/master-data/departments/{id}/entra-job-titles + POST …/positions/import-from-entra; Checkbox-UI in AdminOrganizationDepartmentEditor mit bereits-vorhanden-Markierung — **abgeschlossen** |
| C | 2026-05-08 | Mitarbeiter-Verzeichnis zeigt jetzt auch aktive `directory_identities` ohne Mitarbeiterkarte — `GetPeopleDirectory` per `UNION ALL`, Status `directory_only`, nullable `personId`, Inline-Import-Button pro Verzeichnis-Eintrag in `PeopleDirectoryPage` — **abgeschlossen** |
| 19 | 2026-05-11 | Backend Full Review / Holistic Audit — S1..S5 + S7..S9 done 2026-05-11; S6 (`AuthZ-Repo-Split`) offen |
