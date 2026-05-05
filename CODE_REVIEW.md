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

**Stand**: 2026-05-05 — nach Abschluss von Zyklus 7. Zyklus 8 aktiv.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 eroeffnet).

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

## Aktiver Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

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
| Z8-2.1 | Hotspot #1 — `WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`: N+1 fuer `IsManagerCreatableDefinition` aufloesen (Bulk-/SQL-Pushdown) | **HIGH** — Naechster Schritt |
| Z8-2.2 | Hotspot #2+#3 (gemeinsamer Slice) — `RotationNotificationService` Daily-Sweep: `LIMIT`/Batch-Fetch + Batch-Update der Dispatch-Results | **HIGH** — wartet auf Z8-2.1 |
| Z8-2.3 | Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync`: Group-Member-Schleifen auf Batch-Upsert/-Insert umstellen | **HIGH** — wartet auf Z8-2.2 |
| Z8-3 | Sweep- und Dispatch-Performance Resthebel (`RotationTaskRegenerationEngine`-Sweep, weitere Notification-Pfade #5/#7) | MEDIUM — wartet auf Z8-2 |
| Z8-4 | Test-Coverage fuer die neu gepushten Pfade (Integration + Unit) | MEDIUM — wartet auf Z8-2 |

**Empfohlener Einstieg:** Z8-1.1 als reine Inventur — opus/high. Output: konkret nummerierte Hotspot-Liste mit Aufrufer-Pfad und Datenkardinalitaet, kein Code-Change. Erst auf dieser Basis entscheidet Z8-1.2, ob Pagination, Sortier-Pushdown oder N+1-Aufloesung den groessten Hebel hat.

### Z8-1.1 Hotspot-Inventur (2026-05-05)

Reine Inventur, kein Code-Change. Pro Hotspot: Datei/Symbol, Art `(a)` unbegrenztes Laden / `(b)` In-Memory-Filter-Sort / `(c)` N+1 / `(d)` Sweep-/Dispatch-Last, Kardinalitaetsgrund, Prio fuer Z8-1.2.

1. **`WorkflowCatalogService.GetStartableWorkflowDefinitionsAsync`** — `api/API/Services/WorkflowCatalogService.cs:8-42`. Art **(b)+(c)**. Laedt **alle** publizierten Definitionen ohne Limit und ruft danach pro Definition `repository.IsManagerCreatableDefinition(definitionKey)` in einer foreach-Schleife auf (Z. 34). Aufrufer: Workflow-Start (Master-Data-Endpoint). Kardinalitaet: pro Manager-User bei jedem Workflow-Anlegen 1 + N DB-Calls; bei wachsendem Definitionsbestand linear teurer. **Prio HIGH**.
2. **`RotationNotificationService.ExecuteDailySweepAsync` + `IRotationRepository.GetDispatchableRotationNotifications`** — `api/API/Services/RotationNotificationService.cs:11-62`, SQL in `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs`. Art **(a)+(d)**. Laedt **alle** dispatchable Notifications ohne `LIMIT` und uebergibt sie en bloc an den Mail-Sender. Kein Batching, kein Pagings. Kardinalitaet: bei Backlog (z. B. nach Mail-Ausfall) zehntausende Saetze in einem Aufruf — Memory- und Mail-Pfad. **Prio HIGH**.
3. **`RotationNotificationOperations.ApplyRotationNotificationDispatchResults`** — `api/API/Repositories/PostgresRotationRepository.NotificationOperations.cs:192-299`. Art **(c)+(d)**. Pro Result: Select + Update sequenziell statt Batch-Update. Kardinalitaet: skaliert 1:1 mit #2; verdoppelt die DB-Last des Sweeps. **Prio HIGH** (haengt logisch an #2 — bietet sich als gemeinsamer Slice an).
4. **`EntraDirectorySyncService.SyncAllAsync` (Group-Member-Schleifen)** — `api/API/Services/EntraDirectorySyncService.cs` ~Z. 153-218. Art **(c)+(d)**. Geschachtelte Schleifen `groups` × `members` mit `UpsertDirectoryIdentity` und `InsertGroupMembership` einzeln pro Member. Kein Batch-Insert. Aufrufer: `DirectorySyncHostedService` (24h-Sweep, 2h-Timeout aus Z6). Kardinalitaet: bei breiterer Org (>100 Gruppen × Dutzende Mitglieder) tausende Round-Trips pro Sweep. **Prio HIGH**, aber Timer-Pfad — Last zeigt sich erst beim Timeout. (Hinweis: vollstaendiger File-Split bleibt LQ2-Z3 deferred; hier geht es nur um die Sync-Schleifen, nicht um Strukturhygiene.)
5. **`WorkflowVisibilityService.ApplyWorkflowTaskPermissions`** — `api/API/Services/WorkflowVisibilityService.cs:59-110`. Art **(b)** (potentiell **(c)** je nach Policy-Service). Pro Task im Workflow drei Policy-Aufrufe (`CanUpdateTaskStatus`/`CanDecideTaskApproval`/`CanAddTaskComment`). Aufrufer: Workflow-Detail-Endpunkt. Kardinalitaet: grosse Workflows (≥100 Tasks) mit DB-gestuetzter Policy → spuerbarer Detail-Render. **Prio MEDIUM**, Hebel haengt davon ab, ob Policy-Service intern weitere Repo-Hits macht — vor Slice verifizieren.
6. **`WorkflowCatalogService.GetDepartmentsAsync` / `GetRolesAsync`** — `api/API/Services/WorkflowCatalogService.cs:44-52`, SQL in `api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs`. Art **(a)**. `ORDER BY` ohne `LIMIT`/Pagination. Kardinalitaet: in einer Filiale unkritisch, in groesseren Org-Strukturen wird die Master-Data-Liste ohne Pagination zum Hot-Path. **Prio MEDIUM** — Pagination/Suche im Frontend bedeutet auch FE-Folgen, deshalb fuer Z8-1.2 separat bewerten.
7. **`PostgresWorkflowNotificationDispatchOperations.BuildReadyTaskNotificationPreviewTargetsAsync`** — `api/API/Repositories/PostgresWorkflowNotificationDispatchOperations.cs` ~Z. 280-313. Art **(c)**. `foreach` ueber Recipient-Ids mit `LoadActiveUserNotificationRecipient` pro Empfaenger statt einem JOIN/IN-Set. Aufrufer: Notification-Preview. Kardinalitaet: skaliert mit Recipient-Anzahl pro Workflow. **Prio MEDIUM**.
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
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2485 Z.) Split | deferred ohne Trigger — Risiko niedrig (Timer-Pfad). Refactor erst bei Anlass | Zyklus 3 |

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
| 8 | 2026-05-05 (aktiv) | Skalierbarkeits- & Last-Haertung |
