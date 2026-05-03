# FE-9 — Spec-Carry-Over zwischen Definition-Versionen (Architekturskizze)

**Status:** Entwurf zur Abnahme — Vorbedingung fuer FE-9 (FRONTEND_TODO.md)
**Datum:** 2026-05-03
**Hintergrund:** LA5 hat Specs vom globalen `task_templates`-Cluster auf `workflow_node_task_specs.workflow_node_id` umgezogen. Specs haengen jetzt am Massnahmen-Node der **published** Version. Die Konsequenz wurde im LA5-Plan als Watch-Item festgehalten: "Wenn Admin per Builder eine neue Definition-Version published, werden Specs aktuell **nicht automatisch** vom alten zum neuen Massnahmen-Node geklont."

Dieses Dokument fixiert die Befunde, beschreibt drei Optionen und schlaegt eine Empfehlung vor. Implementierung beginnt erst nach expliziter Freigabe.

---

## 1. Befunde aus dem Code-Scan

### 1.1 Wo Specs heute aufgehaengt sind

```sql
-- Aus 01_schema.sql nach LA5
CREATE TABLE workflow_node_task_specs (
    id bigint PRIMARY KEY,
    workflow_node_id bigint NOT NULL
        REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    spec_key varchar(120) NOT NULL,
    -- ... fachliche Felder ...
    UNIQUE (workflow_node_id, spec_key)
);
```

`workflow_nodes` haengt an `workflow_definition_versions`. Damit:

- Eine Definition hat 1..N Versionen (`draft` + `published` + `retired`).
- Eine Version hat 1..N Nodes — fuer Maßnahmenflows konkret 1 `measure_*`-Node.
- Specs haengen am **konkreten Node** der konkreten Version. Beim Anlegen einer neuen Version wird ein **neuer** `workflow_node_id` erzeugt — Specs der alten Version werden automatisch nicht mitgenommen.

### 1.2 Wie der Admin heute auf "die Specs" zugreift

`PostgresWorkflowRepository.TaskTemplateAdminOperations.cs:304-323`:

```csharp
private static async Task<long?> ResolveMeasureNodeIdForDefinition(
    NpgsqlConnection connection, NpgsqlTransaction? transaction, int workflowDefinitionId)
{
    const string sql = @"
SELECT n.id
FROM workflow_definition_versions v
JOIN workflow_nodes n ON n.workflow_definition_version_id = v.id
WHERE v.workflow_definition_id = @workflowDefinitionId
  AND v.published_at IS NOT NULL                          -- (!)
  AND n.node_type LIKE 'measure_%'
ORDER BY v.published_at DESC, n.id
LIMIT 1;";
    // ...
}
```

**Implizite Annahmen:**
1. Es existiert **genau** eine published Version (`ORDER BY published_at DESC LIMIT 1` greift sonst die juengste).
2. Diese published Version hat **genau** einen `measure_*`-Node.
3. Der Admin pflegt Specs immer fuer **diese** Version — nicht fuer einen Draft.

Konsumenten dieser Auflösung (alle ueber `ResolveMeasureNodeIdForDefinition`):

| Endpoint | Datei |
|---|---|
| `GET /admin/config/task-templates?workflowDefinitionId=` | `TaskTemplateAdminOperations.cs:19` |
| `POST /admin/config/task-templates` | `TaskTemplateAdminOperations.cs:76` |
| `PATCH /admin/config/task-templates/{id}` | `TaskTemplateAdminOperations.cs:136` |
| `DELETE /admin/config/task-templates/{id}` | `TaskTemplateAdminOperations.cs` |
| `GET /admin/config/workflow-definitions/{id}/dependency-graph` | `TaskTemplateDependencyOperations.cs:27` |
| Conditions/Dependencies CRUD | mehrere Stellen |

### 1.3 Was beim Anlegen einer neuen Version heute passiert

`EnsureAdminWorkflowDefinitionWorkingDraft` (PostgresWorkflowRepository.WorkflowDefinitionAdminOperations.cs:294-357):

1. Falls Draft existiert → return existing.
2. Falls keine Quelle existiert → leeren Draft anlegen.
3. Sonst: **Nodes + Edges** der Quell-Version klonen via `PersistWorkflowDefinitionVersionGraph(versionId, sourceVersion.Nodes, sourceVersion.Edges)`.

**Was NICHT geklont wird:**
- `workflow_node_task_specs` — Specs der alten measure-Node-IDs werden nicht auf die neuen measure-Node-IDs uebertragen.
- `workflow_node_task_spec_conditions`
- `workflow_node_task_spec_dependencies`

`PersistWorkflowDefinitionVersionGraph` (WorkflowDefinitionGraphMappingOperations.cs:162-188) macht zudem:
```sql
DELETE FROM workflow_edges WHERE workflow_definition_version_id = @versionId;
DELETE FROM workflow_node_configs WHERE workflow_node_id IN (SELECT id FROM workflow_nodes WHERE workflow_definition_version_id = @versionId);
DELETE FROM workflow_nodes WHERE workflow_definition_version_id = @versionId;
```
Die Tabellen `workflow_node_task_specs/_conditions/_dependencies` haben `ON DELETE CASCADE` an `workflow_nodes(id)` — jeder Replace-Lauf wuerde damit alle Specs eines Drafts **wegloeschen**, sobald der Builder einen Save abschickt. Dass das heute nicht knallt, liegt daran, dass aktuell **keine Specs an Drafts haengen** (siehe 1.4).

### 1.4 Was beim Publish heute passiert

`PostgresWorkflowRuntimeRepository.PublishWorkflowDefinitionVersion`:

```sql
-- Schritt 1: alle anderen published Versions retiren
UPDATE workflow_definition_versions SET status = 'retired' WHERE workflow_definition_id = @did AND id <> @vid AND status = 'published';
-- Schritt 2: target Version publishen
UPDATE workflow_definition_versions SET status = 'published', published_at = NOW() WHERE id = @vid;
```

Specs werden **nicht angefasst**. Der neue Massnahmen-Node der frisch published Version hat dann genau die Specs, die an ihm hingen — also **null**.

### 1.5 Stale Comment im Code

`PostgresWorkflowRepository.WorkflowDefinitionGraphMappingOperations.cs:46-48`:

```csharp
// LA5: task/approval-Nodes referenzieren Specs ueber workflow_node_task_specs.workflow_node_id
// (statt frueher per legacyTemplateKey-String). Spec-Existenz wird beim Persistieren der
// Version sichergestellt (siehe ClonePreviousVersionTaskSpecs in PersistWorkflowDefinitionVersionGraph).
```

Die referenzierte Methode `ClonePreviousVersionTaskSpecs` **existiert nicht**. Der Kommentar war ein Plan-Artefakt aus LA5 — die Implementierung wurde damals als Watch-Item geparkt. Genau das ist FE-9.

### 1.6 Bug-Szenario, sobald der Builder echte Versions-Wechsel produziert

```
T0: Definition d1 hat published v1 (measure-node N1, 20 Specs an N1)
T1: Admin oeffnet Builder
    → EnsureAdminWorkflowDefinitionWorkingDraft erzeugt v2 (Draft) mit measure-node N2
    → N2 hat 0 Specs (Bug)
T2: Admin pflegt Specs im AdminTaskTemplate-Editor
    → ResolveMeasureNodeIdForDefinition liefert N1 (latest published) zurueck
    → Aenderungen landen in der live Production-Version v1, nicht im Draft v2
T3: Admin published v2
    → v1 retired, v2 published
    → ResolveMeasureNodeIdForDefinition liefert jetzt N2 zurueck → 0 Specs
    → Generator findet 0 Specs → frisch erzeugte Workflows haben keine Tasks
    → Existing running workflows referenzieren v1 (Versionierung bleibt korrekt) — die brechen nicht
```

Konsequenzen:

- **Kein Datenverlust auf laufende Instanzen** — Versionierung schuetzt sie.
- **Aber**: ab Publish hat die Produktion stille Daten-Drift. Tasks fehlen, Bedingungen fehlen, Dependencies fehlen. Erst wenn ein neuer Workflow erzeugt wird, faellt es auf.
- **Cross-Version-Leak**: Editiert der Admin Specs **waehrend** ein Draft offen ist, wirken die Aenderungen sofort in der live Production-Version (T2). Das ist keine theoretische Sorge — es passiert beim ersten parallelen Use-Case.

### 1.7 Pre-Prod-Status (warum es heute nicht knallt)

| Faktor | Zustand | Konsequenz |
|---|---|---|
| Anzahl published Versionen pro Definition | 1 (Seed) | `ORDER BY published_at DESC LIMIT 1` ist deterministisch |
| Anzahl Drafts pro Definition | 0 oder 1 (vereinzelt) | Niemand published echte Versions-Wechsel |
| Anzahl produktiver `workflow_tasks`-Zeilen | 0 (Inventur LA5) | Keine Audit-Konsequenzen |
| Builder produziert echte Versions-Wechsel | nein | Bug latent, nicht aktiv |

Sobald **eine** der ersten drei Zellen kippt, knallt der Bug. Daher ist FE-9 kein Cosmetic-Item.

---

## 2. Optionen

### Option A — "Clone-on-Publish + Clone-on-Draft-Create"

Server-seitige Spec-Pflege, ausschliesslich am Versions-Lebenszyklus.

**Logik:**
1. `EnsureAdminWorkflowDefinitionWorkingDraft`: nach dem Klonen der Nodes + Edges einen weiteren Schritt — Specs der Quell-Version (matched auf `node_type` + `node_key`) auf die geklonten Nodes kopieren. Inkl. Conditions + Dependencies (Composite-FK-Remapping).
2. `PublishWorkflowDefinitionVersion`: vor dem Status-Flip pruefen, ob die zu publizierende Version Specs am `measure_*`-Node hat. Falls **nein** und es eine vorherige published Version gibt, deren Specs auf den frischen `measure_*`-Node klonen.
3. `ResolveMeasureNodeIdForDefinition` bleibt unveraendert (zeigt weiter auf published).

**Pro:**
- Keine Frontend-Aenderung. Builder, AdminTaskTemplate-Editor, Backend-Resolver bleiben gleich.
- Specs werden ein implizites Property der Version, ohne DTO-Schnitt zu aendern.
- Migration trivial: bestehende Welt funktioniert weiter, neue Welt ist additiv.
- Wenn Admin **vor** dem Builder-Lauf Specs gepflegt hat, klont der Draft sie 1:1.

**Contra:**
- Cross-Version-Leak bleibt: T2 im Bug-Szenario aendert weiterhin die live Production-Version. Loesung waere ein "lock published version while draft exists" — kein klarer Wunsch.
- Clone-Logik braucht stabile Identitaeten: `spec_key` ist pro `(workflow_node_id, spec_key)` eindeutig — Klon zwischen Nodes mit identischem `node_type` ist deterministisch.
- Dependencies klonen muss `id`-Remapping plus Composite-FK-Same-Node-Constraint einhalten.
- Wenn der Builder ein measure-Node mit *neuem* `node_key` baut (z.B. "measure_provision" durch "measure_change" ersetzt), gibt es keinen 1:1-Match — Clone-Source muss sich auf `node_type` allein stuetzen, sonst geht die Specs verloren. Empfehlung: Match auf `node_type LIKE 'measure_%'` und verlangen, dass es genau einen measure-Node pro Quell- und Ziel-Version gibt (Inventur LA5: heute 1:1).

### Option B — "Spec-DTO im Draft" — Specs in der Replace-Version-DTO

Specs werden Teil der Builder-Definition. `ReplaceWorkflowDefinitionVersionRequest` bekommt zusaetzlich eine Spec-Liste pro Node, der Builder pflegt sie inline, und `PersistWorkflowDefinitionVersionGraph` schreibt sie atomar mit.

**Logik:**
1. DTO-Erweiterung: `WorkflowDefinitionDraftNode` bekommt `Specs: List<WorkflowDefinitionDraftSpec>` mit Conditions + Dependencies inline.
2. Builder-UI-Erweiterung: der Massnahmen-Node-Inspector zeigt eine Spec-Liste (integriert oder als Sub-Editor).
3. `PersistWorkflowDefinitionVersionGraph` schreibt nach den Nodes auch Specs (mit `id`-Tracking fuer Dependencies).
4. AdminTaskTemplate-Editor wird entweder eingestellt **oder** liest immer den Draft (falls vorhanden), faellt auf published zurueck.
5. `ResolveMeasureNodeIdForDefinition` wird Versions-explizit: pro Aufruf wird klar, ob Draft oder Published gemeint ist.

**Pro:**
- Konzeptuell sauber: "Eine Workflow-Version ist Graph + Specs". Versionierung ist automatisch korrekt.
- Kein Carry-Over noetig — das Problem entfaellt strukturell.
- Cross-Version-Leak entfaellt (Specs gehoeren immer zu **einer** Version).
- Aligned mit der "Builder als Single Source of Truth"-Richtung aus PROJECT_CONTEXT.md.

**Contra:**
- Grosser Schnitt: DTO + Frontend-Builder + alle Backend-Persistenz + Tests.
- DTO-Bloat: Snapshot zeigt 76 Specs ueber alle Definitions — moeglich, aber erhoeht Request-Size deutlich (geschaetzt 5–15 KB pro Replace-Request).
- AdminTaskTemplate-Editor entfaellt oder muss Versions-aware werden — UX-Frage.
- `workflow_tasks.workflow_node_task_spec_id` (FK ON DELETE SET NULL) verliert seinen Audit-Anker bei jedem Save, wenn Specs als "delete-and-reinsert" persistiert werden. Mitigation: Specs nicht hart loeschen, sondern Diff-basiert updaten (anhand `(workflow_node_id, spec_key)`). Das verkompliziert `PersistWorkflowDefinitionVersionGraph` deutlich.
- **Risiko hoch** — beruehrt den gerade stabilisierten Form-Editor und den Builder-Save-Pfad.

### Option C — "Clone-on-Publish + Versionsbewusster Spec-Editor"

Hybrid: Daten leben wie bei Option A am `workflow_node_id`. Aber `ResolveMeasureNodeIdForDefinition` wird Versions-aware: Admin waehlt explizit (oder implizit ueber Workspace-Kontext) "Live-Version pflegen" vs. "Draft pflegen".

**Logik:**
1. Backend: zwei Resolver — `ResolveMeasureNodeIdForLatestPublished` (= heutiges Verhalten) und `ResolveMeasureNodeIdForLatestDraft`.
2. AdminTaskTemplate-Editor bekommt einen Toggle "Live" / "Draft (in Bearbeitung)". Falls kein Draft existiert, ist der Toggle disabled.
3. Carry-Over-Logik aus Option A bleibt: beim Erzeugen des Drafts werden Specs der Live-Version geklont, beim Publish keine zusaetzliche Logik noetig.
4. Ist nicht mehr "leak" — Live-Version-Edits gehen nur ueber expliziten Toggle.

**Pro:**
- Adressiert beide Sub-Probleme: Carry-Over **und** Cross-Version-Leak.
- Daten-Modell bleibt wie LA5.
- Schnitt fuer Frontend ist begrenzt — ein Toggle, ein Versionskontext-Param.
- Wachstumspfad: spaeter kann der Editor in den Builder-Inspector verlegt werden (Option B Endzustand), ohne das Datenmodell zu drehen.

**Contra:**
- Admin braucht Versions-Verstaendnis — UX-Last steigt.
- Carry-Over-Logik aus Option A wird trotzdem gebraucht (gleicher Aufwand wie Option A + Toggle).
- Wenn ein Workflow-Definition keinen Draft hat, ist der Editor wie bisher — Komplexitaet schlaegt nur teilweise durch.

### Option D — "Nichts tun + Restrict"

Den Bug nicht fixen, aber den Builder so einschraenken, dass keine echten Versions-Wechsel passieren koennen, bevor FE-9 entschieden ist.

**Logik:**
- `PublishWorkflowDefinitionVersion` blockt, wenn die Version keine Specs am Massnahmen-Node hat **und** es eine vorherige published Version mit Specs gibt — error: "Use the migration tool".

**Pro:**
- Trivial zu bauen (~30 Min).
- Kauft Zeit fuer eine spaetere Entscheidung.

**Contra:**
- Loest das Problem nicht. Der Builder bleibt fuer den Versions-Wechsel-Use-Case unbenutzbar.
- Macht den Stolperstein sichtbar, ohne ihn zu adressieren.
- Sinnvoll nur als Zwischenmassnahme, falls FE-9 noch laenger geparkt wird.

---

## 3. Empfehlung

### Kurzfristig: Option A (Clone-on-Publish + Clone-on-Draft-Create)

**Begruendung:**

1. **Loest den eigentlichen Bug.** Jede neue Version bekommt die Specs der Vorgaengerversion bei Draft-Erzeugung; falls vergessen, klont der Publish trotzdem. Damit kann der Builder echte Versions-Wechsel machen, ohne Daten zu verlieren.
2. **Minimaler Schnitt.** Drei neue Helper-Methoden im Backend, keine DTO-Aenderung, keine Frontend-Aenderung. Test-Surface klein und gezielt.
3. **Pre-Prod-passend.** Wir koennen FE-9 abschliessen, ohne den Builder anzufassen. Wenn spaeter die UI sich aendert (Option B/C), bleibt das Daten-Modell stabil.
4. **Cross-Version-Leak ist heute kein realer Use-Case.** Pre-Prod hat keinen Bedarf an "zwei Editier-Modi parallel". Wenn der Use-Case auftaucht, kann **dann** auf Option C aufgesetzt werden — Option A blockiert das nicht.

**Was Option A nicht loest:**
- Der Cross-Version-Leak aus T2 im Bug-Szenario bleibt. Empfehlung: als Watch-Item in MEMORY.md/FRONTEND_TODO.md als FE-9-Folge dokumentieren, fix-on-trigger.

### Mittelfristig: Option C aufsetzen, wenn der Builder produktiv genutzt wird

Sobald ein Pilotkunde die Builder-Workflows ernsthaft nutzt und den Cross-Version-Leak konkret beobachtet, ist Option C der natuerliche naechste Schritt. Frontend-Aufwand dann ~0,5 d zusaetzlich (Toggle + Versions-Param). Backend war mit Option A schon vorbereitet.

### Langfristig: Option B als Konsolidierung, wenn der Builder die einzige Admin-Surface wird

Sobald die separate AdminTaskTemplate-Editor-Sektion abgeschafft werden soll (passt zu PROJECT_CONTEXT-Direction "UI-gestuetzte fachliche Konfiguration mit Guardrails"), ist Option B die saubere Konsolidierung. Aufwand dann ~3 d, weil die Daten-Tabellen schon stehen.

---

## 4. Was Option A konkret bedeutet (high-level Slicing)

| Slice | Inhalt | Schaetzung |
|---|---|---|
| FE9-A | Tests, die den Bug reproduzieren (Draft anlegen → measure-Node hat 0 Specs; Publish ohne Specs → frischer Workflow hat 0 Tasks). Skipped, falls Draft-Lifecycle-Test schon existiert. | 0,3 d |
| FE9-B | Backend-Helper `CloneTaskSpecsBetweenMeasureNodes(connection, transaction, sourceMeasureNodeId, targetMeasureNodeId)` + Conditions/Dependencies-Klone mit `id`-Remapping (Composite-FK-Constraint einhalten). Pure SQL, kein DTO-Touch. | 0,5 d |
| FE9-C | `EnsureAdminWorkflowDefinitionWorkingDraft` erweitern: nach dem Graph-Klon den measure-Node-Match durchfuehren und `CloneTaskSpecsBetweenMeasureNodes` aufrufen. | 0,3 d |
| FE9-D | `PublishWorkflowDefinitionVersion` erweitern: vor dem Status-Flip pruefen, ob target-version-measure-node leer ist. Wenn ja und vorherige published Version existiert: klonen. | 0,3 d |
| FE9-E | Tests gruen. Stale Comment in `WorkflowDefinitionGraphMappingOperations.cs:46-48` aktualisieren auf den richtigen Methodennamen. | 0,3 d |
| FE9-F | Doku: `Migrationspfad.md`, `MEMORY.md`, `FRONTEND_TODO.md`, `Code-Review-Status.md`. Cross-Version-Leak als Watch-Item dokumentieren. | 0,3 d |

**Gesamt ~2 d**, im Rahmen der TODO-Schaetzung "2–3 d".

---

## 5. Was diese Skizze NICHT festlegt (offene Detailpunkte)

- **Match-Heuristik fuer measure-Node:** `node_type LIKE 'measure_%'` allein, oder zusaetzlich `node_key` matchen? Empfehlung: nur `node_type` als Prefix-Match, weil Inventur 1 measure-Node pro Definition zeigt. Falls je mehrere measure-Nodes pro Definition zugelassen werden, braucht es `node_key` als Tie-Breaker.
- **Was passiert, wenn die Quell-Version mehrere measure-Nodes hat?** Heute kommt das nicht vor. FE9-B sollte einen Pre-Check einbauen, der bei Mehrdeutigkeit explizit abbricht: "Source version has N measure nodes — automatic spec carry-over not supported. Use explicit migration."
- **Was passiert mit Specs an `task`/`approval`-Nodes** (heute 0 in real data, aber strukturell vorgesehen)? Empfehlung: pro Node-Typ matchen — `task`-Specs werden zu `task`-Specs gemappt, anhand `node_key`. Falls der Builder `node_key` aendert, geht der Spec verloren — das ist OK, weil eine `task`-Node-Identitaetsaenderung absichtlich gemacht wird.
- **Audit-Konsequenz fuer `workflow_tasks.workflow_node_task_spec_id`:** Beim Klonen entstehen neue `workflow_node_task_specs.id`-Werte. Existing `workflow_tasks` zeigen weiterhin auf alte IDs. Das ist OK (laufende Workflows referenzieren ihre Spec-Snapshots), aber Reports, die "alle Workflows fuer Spec X" zaehlen wollen, muessen ueber `spec_key` aggregieren statt `id`. Empfehlung: `spec_key` bleibt der semantisch stabile Key.
- **Cross-Version-Leak:** wird in Option A bewusst nicht adressiert. Watch-Item.
- **Per-Node-Idempotenz beim Publish-Clone:** Falls Publish zweimal aufgerufen wird (Re-Publish nach Fehler), darf der Clone nicht doppeln. Empfehlung: Pre-Check `count(*) FROM workflow_node_task_specs WHERE workflow_node_id = @target` — wenn `> 0`, kein Clone.

---

## 6. Risiken + Mitigationen

| Risiko | Wahrscheinlichkeit | Mitigation |
|---|---|---|
| Composite-FK fuer Dependencies bricht beim Klonen, weil source/target node-id nicht matcht | mittel | Test der Dependency-Klon-Logik mit echtem Same-Node-Scope; Reihenfolge: erst alle Specs klonen, dann Dependencies mit Lookup ueber neue Spec-IDs |
| Match-Heuristik findet keinen Ziel-Node | niedrig | Pre-Check + expliziter Error-Throw mit klarer Message |
| Doppel-Klon beim Re-Publish | niedrig | Idempotenz-Pre-Check |
| Test-Fixture fuer Versions-Lifecycle bricht (LA5-Tests setzen heute Specs direkt) | niedrig | Bestehende Tests nutzen `EnsureAdminWorkflowDefinitionWorkingDraft` selten, meist `CreateAdminWorkflowDefinitionVersion` direkt |
| Cross-Version-Leak fuehrt zu Datenproblemen, bevor Option C kommt | niedrig (Pre-Prod) | dokumentiertes Watch-Item; bei erstem Pilotkunden umgehend C nachziehen |

---

## 7. Verwandte Dokumente

- [[LA5-TaskSpezifikation-Skizze]] — Ursprung der heutigen Spec-Datenarchitektur
- [[Zielarchitektur]] — Versionierungsanforderung an Definition Layer
- [[Migrationspfad]] — Schritt 4 (Definition Layer einfuehren) und Schritt 10 (Guided Builder)
- [[Entscheidungen]] — "Versionierung ist Pflicht"
- `FRONTEND_TODO.md` FE-9 — Backlog-Eintrag
- `MEMORY.md` — Active-Risk-Eintrag fuer LA5-Watch-Items

---

## 8. Naechster Schritt

Diese Skizze wartet auf Freigabe (Option A / B / C / D). Nach Entscheidung wird das Slicing in `TODO.md` als FE9-A..F eingetragen und implementiert.
