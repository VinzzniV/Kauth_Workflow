# LA5 — `task_templates` aus dem Definition-Layer loesen (Architekturskizze)

**Status:** Entwurf zur Abnahme — Vorbedingung fuer LA5 (TODO.md, Legacy-Abbau-Plan Schritt 9)
**Datum:** 2026-05-03
**Hintergrund:** Der TODO-Eintrag fuer LA5 lautet "Workflow-Definitionen muessen Task-Spezifikationen direkt besitzen statt per Legacy-Key auf `task_templates` zu zeigen." Der Code-Scan zeigt, dass das Problem groesser ist als das Wort `legacyTemplateKey` suggeriert: `task_templates` versorgt **zwei** Pfade (Per-Node-Tasks **und** Bulk-Maßnahmenbloecke), und beide haengen am gleichen Tabellen-Cluster.

Dieses Dokument fixiert die Befunde, beschreibt drei Optionen mit Tradeoffs und schlaegt eine Empfehlung vor. Implementierung beginnt erst nach expliziter Freigabe.

---

## 1. Befunde aus dem Code-Scan

### 1.1 Wo `legacyTemplateKey` aktuell wirkt

| Pfad | Datei | Wirkung |
|---|---|---|
| Build-time Validation | `WorkflowDefinitionValidationService.cs:565,609` | `task`/`approval`-Nodes muessen ein `legacyTemplateKey` im Node-Config haben |
| Publish-time Validation | `PostgresWorkflowRepository.WorkflowDefinitionGraphMappingOperations.cs:49` | Existenz-Check via `LegacyTaskTemplateExists` |
| Startup Validation | `LifecycleStartupValidationExtensions.cs:536-549` | Drift-Check beim App-Start |
| Runtime-Aktivierung | `PostgresWorkflowRuntimeRepository.cs:1664-1669` | `LoadActiveTaskTemplateByKey` zieht 11 Felder aus `task_templates` |
| Approval-Matching | `PostgresWorkflowRuntimeRepository.cs:1846-1849` | Vergleicht Approval-Node-`legacyTemplateKey` gegen `workflow_definitions.approval_task_template_key` (Bruecke seit Slice 6.3d-iv) |
| Builder-UI | `WorkflowBuilderStepConfigEditor.tsx:71-86` | Dropdown der TaskTemplates der aktiven Definition |
| Tests | `WorkflowDefinitionValidationServiceTests.cs` (~12 Stellen) | Fixtures setzen `legacyTemplateKey` im Node-Config |

### 1.2 Was `task_templates` ausser dem Per-Node-Lookup noch traegt

Schema (Auszug aus [01_schema.sql:1514](../db/01_schema.sql#L1514)):

```sql
CREATE TABLE task_templates (
    id integer NOT NULL,
    workflow_definition_id integer NOT NULL,         -- FK seit Slice 6.3d-ii
    template_key character varying(120) NOT NULL,    -- UNIQUE GLOBAL (!), nicht pro Definition
    title, description, category, icon_key,
    owning_department_id, default_responsibility_id, -- fachliche Zuordnung
    process_area_label,
    is_department_phase_task, is_required,
    due_in_days, sort_order, is_active
);
```

Plus Satellitentabellen:
- `task_template_conditions` — Sichtbarkeitsregeln auf Form-Antworten (`answer_key`, `operator`, `expected_value_*`, `condition_group`)
- `task_template_dependencies` — Reihenfolge-Constraints (`depends_on_task_template_id`, `required_status`)

Audit-Bezug:
- `workflow_tasks.task_template_id` (FK ON DELETE SET NULL) — jede erzeugte Task verweist auf ihre Vorlage

### 1.3 Der zweite Konsument — der unsichtbare

[PostgresWorkflowTaskGenerationService.cs:39-41](../api/API/Repositories/PostgresWorkflowTaskGenerationService.cs#L39-L41) laedt **alle** `task_templates` einer `workflow_definition_id` — ohne `legacyTemplateKey`-Bezug zum Definition-Graph — und materialisiert daraus die Tasks fuer den `measure_*`-Block. Conditions filtern ueber Form-Antworten, Dependencies bilden eine DAG **innerhalb** des Maßnahmen-Blocks.

Damit ist die Aussage im TODO ("Workflow-Definitionen muessen Task-Spezifikationen direkt besitzen") **fuer beide Achsen** zu loesen, nicht nur fuer `legacyTemplateKey`:

| Achse | Heute | Was LA5 loesen muss |
|---|---|---|
| **Per-Node-Task** (`task`/`approval`) | Node-Config zeigt per `legacyTemplateKey` auf `task_templates` | Spezifikation an den Node |
| **Bulk-Maßnahmen** (`measure_*`) | Generator zieht **alle** `task_templates` der Definition | Spezifikation an den `measure_*`-Block oder eigene Sub-Liste |
| **Conditions** | `task_template_conditions` per Template | Inline am Per-Node-Task **und** Sub-Liste am Block |
| **Dependencies** | `task_template_dependencies` als globale DAG ueber alle Templates der Definition | Sub-DAG **innerhalb** des Maßnahmen-Blocks bzw. Edges fuer Per-Node-Tasks |
| **Conditions evaluieren ueber Form-Antworten** | Form-Antworten kommen aus Form-Nodes des Definition-Graphs | bleibt; Bezug ueber `answer_key` ist orthogonal |

### 1.4 Bestehende Bruecken im Schema, auf denen LA5 aufsetzt

- `workflow_definitions.approval_task_template_key` (varchar 120) — Slice 6.3d-iv. Funktional eine Pre-Computation des per-Definition Approval-`legacyTemplateKey`.
- `workflow_definitions.requires_target_person`, `requires_supervisor_step`, `allows_manager_creation` — fachliche Flags, die direkt an der Definition haengen (Slices 6.2/6.3d).

Das Muster ist klar erkennbar: Pro neuer Sondersituation eine eigene Spalte auf `workflow_definitions`. **Das skaliert fuer LA5 nicht** — man wuerde 10+ Spalten brauchen, um die Per-Template-Felder abzubilden. LA5 muss strukturell loesen, was die Bruecken bisher ad hoc geloest haben.

### 1.5 Admin-UI-Surface

**TaskTemplate-Editor** (eigene Sektion): 6 Komponenten, 7 Hooks. Pflegt fachlich Title/Description, Conditions, Dependencies, Responsibility, Department, Sort-Order. Ist heute **die** Stelle, an der Admin die Bulk-Maßnahmen einer Definition pflegt.

**Builder-UI** (Step-Editor): zeigt fuer `task`/`approval`-Nodes ein Dropdown der TaskTemplates der aktuellen Definition zur Auswahl von `legacyTemplateKey`.

Beide UI-Wege funktionieren — aber sie sind unterschiedlich, weil das Datenmodell zwei Pfade hat.

---

## 2. Optionen

### Option A — "Inline JSON" — Task-Spezifikation komplett im Node-Config

Jeder `task`/`approval`-Node speichert seine vollen Attribute (Title, Description, Responsibility, Due-Days, Conditions, Dependencies) direkt im `workflow_node_configs.config_json`. Der `measure_*`-Block expandiert vor dem Publish in N einzelne Task-Nodes mit JSON-Edges-DAG.

- **Pro:**
  - Schema reduziert sich drastisch — `task_templates`, `task_template_conditions`, `task_template_dependencies` koennen entfallen.
  - Versionierung durch `workflow_definition_versions` automatisch korrekt.
  - Migration linear: pro published Definition-Version Snapshot in JSON ueberfuehren.
- **Contra:**
  - Builder-UX-Regression: der Maßnahmen-Block ist heute **ein** Knoten — expand-on-publish macht die Liste nicht-trivial editierbar.
  - Conditions/Dependencies werden Edge-Pflege im Builder (Form-Editor war eigentlich angetreten, das zu vermeiden — siehe `WorkflowBuilder-FormEditor-Skizze.md`).
  - JSON-im-config-Spalte wird gross; SQL-Querys auf einzelne Felder werden umstaendlich.
- **Risiko:** hoch — der Builder ist gerade stabil mit Form-Editor, expand-on-publish ist fundamentale neue Komplexitaet.

### Option B — "Owned Tables" — Versionierte Spec-Tabellen pro Node

Neue Tabellen analog zum bestehenden Cluster, aber **am Node statt an der Definition**:
- `workflow_node_task_specs` (FK `workflow_node_id`, 0..1 fuer `task`/`approval`, 0..N fuer `measure_*`)
- `workflow_node_task_conditions`
- `workflow_node_task_dependencies` (`depends_on_task_spec_id`, scope-eingeschraenkt auf denselben Block bzw. dieselbe Definition-Version)

`task_templates` und Satelliten verschwinden komplett. Inhalt zieht 1:1 in die neuen Tabellen. Der Generator pruegelt nicht mehr auf `task_templates`, sondern auf `workflow_node_task_specs` ueber den aktiven `measure_*`-Node-Instance.

- **Pro:**
  - Versionierung pro Definition-Version sauber, weil Specs ueber `workflow_node_id` an die Version haengen.
  - Bulk-Block bleibt **ein** Knoten im Graph — interne Sub-Liste mit eigener Sub-DAG. Builder-UX bleibt nahe am Status quo.
  - Migration ist additivem Schema-Refactor — Daten 1:1 kopieren, FKs umlenken, alte Tabelle droppen. Keine semantische Aenderung.
  - Admin-Editor bleibt strukturell gleich, nur die FKs tauschen.
- **Contra:**
  - Mehr Tabellen — fachlich aber gerechtfertigt, weil das Datenmodell der Realitaet folgt.
  - Per-Node-Task ist im Schema redundant zum Node selbst (jeder `task`-Node hat 1:1 einen Spec-Eintrag) — koennte man als Inline-Variante machen, aber das mischt die Modelle. Saubrer ist 1:1.
- **Risiko:** mittel — Schema-additives Migrations-Skript, Verhalten 1:1 erhaltbar, Tests passen mit FK-Renames.

### Option C — "Hybrid" — Per-Node inline, Bulk in eigener Tabelle

- **Per-Node-Tasks (`task`/`approval`):** Spec inline ins Node-Config-JSON (Option A nur fuer diese Knoten).
- **Bulk-Tasks (`measure_*`):** Eigene Tabelle `workflow_node_measure_items` mit `node_id` FK + Sub-Tabellen fuer Conditions/Dependencies.
- `task_templates` als globale Stammdaten-Tabelle entfaellt. Jede Spec landet bei ihrem natuerlichen Owner.

- **Pro:**
  - Klarste fachliche Trennung — Per-Node-Task ist 1:1 ein Node, Bulk-Block ist 1:N und braucht Tabelle.
  - Fuer Per-Node-Tasks bleiben keine Phantomtabellen-Eintraege liegen.
  - Versionierung pro Achse: Per-Node ueber Node-Version, Bulk ueber FK an Node.
- **Contra:**
  - Zwei Stellen, an denen "Task-Spezifikation" lebt — Admin-UI muss zwei Editoren pflegen (oder einen Editor mit zwei Code-Pfaden).
  - Wenn spaeter ein neuer Bulk-Knoten dazu kommt, braucht er ggf. seine eigene Sub-Tabelle.
- **Risiko:** mittel — fachlich am saubersten, dafuer mehr Code-Pfade.

---

## 3. Empfehlung — Option B

**Begruendung:**

1. Option B vermeidet die Builder-UX-Regression von Option A komplett. Der Form-Editor (L7-Zyklus) hat gerade Stabilitaet erreicht; den Maßnahmen-Block beim Publish in N Knoten zu expandieren ist genau das, was der Form-Editor abschaffen wollte.
2. Option C ist fachlich sauberer, aber doppelter Wartungsaufwand fuer Admin-UI ist nicht durch ein konkretes Problem gerechtfertigt — die heutige `task_templates` traegt beide Achsen seit Jahren ohne Drift.
3. Option B ist **die einzige Option, die rein additiv migrierbar ist** — alle Daten passen 1:1 ins neue Schema, der Generator-Code aendert nur SQL-Quellen, das Verhalten bleibt identisch. Damit ist die Migration testbar gegen Parity-Snapshot.
4. Die bestehenden Bruecken in `workflow_definitions` (`requires_target_person`, `approval_task_template_key`, `requires_supervisor_step`) koennen **bleiben** — sie sind Per-Definition-Flags, nicht Per-Task. Option B kollidiert nicht mit ihnen.
5. Naming-Vorteil: nach LA5 heisst nichts mehr `legacy*`. `workflow_node_task_specs` ist der kanonische Name, `template_key` heisst dann `spec_key` (oder entfaellt, weil Per-Node-Identity ueber Node-Key+Version laeuft).

**Was Option B konkret bedeutet (high-level Slicing fuer die Implementierungs-Session):**

| Slice | Inhalt | Schaetzung |
|---|---|---|
| LA5-A | Schema-Skizze final + Migration-Skript-Entwurf (rein DB) | 0,5 d |
| LA5-B | Neue Tabellen anlegen + Daten kopieren + Parity-Test (read-only Doppellauf gegen alt+neu) | 1,5 d |
| LA5-C | Generator + Runtime-Resolver auf neue Tabellen umstellen (lesend) | 1 d |
| LA5-D | Admin-Editor + Builder-Step-Editor auf neue Tabellen umstellen (Bezug `legacyTemplateKey` ersetzen durch `nodeKey` oder `specId`) | 1 d |
| LA5-E | Schreibpfade (Admin-UI Save) auf neue Tabellen umstellen | 1 d |
| LA5-F | Alte Tabellen droppen + `legacyTemplateKey` entfernen + Tests bereinigen | 0,5 d |
| LA5-G | Doku (`Zielarchitektur.md`, `Migrationspfad.md`, `Code-Review-Status.md`, `MEMORY.md`) | 0,5 d |

Gesamt **~6 d**, im Rahmen der TODO-Schaetzung "> 5 d".

---

## 4. Was diese Skizze NICHT festlegt (offene Detailpunkte fuer den naechsten Schritt)

- **Genaue Spaltennamen + Tabellen-Layout** — kommt in LA5-A.
- **Ob `template_key` als externes Identitaetsmerkmal erhalten bleibt** — Workflow-Tasks haben heute `task_template_id` als Audit-Bezug; nach Migration wird das `task_spec_id`. Die Audit-Lesbarkeit (`workflow_tasks.task_template_id` ON DELETE SET NULL) muss beim Drop der alten Tabelle erhalten bleiben — entweder per Backfill der neuen ID oder per separatem Audit-Snapshot.
- **Conditions-Modell:** Bleiben Conditions Per-Spec (heute) oder ziehen sie in den Decision-Node-Mechanismus um? Empfehlung: **bleiben** — Task-Sichtbarkeitsregeln auf Form-Antworten sind orthogonal zum Definition-Graph-Decision-Mechanismus.
- **Dependencies-Scope:** Heute global ueber alle Templates der Definition, praktisch genutzt nur innerhalb des Maßnahmen-Blocks. Nach Migration: Constraint, dass `depends_on_task_spec_id` im selben `node_id` liegt? Empfehlung **ja** — explizit als CHECK-Constraint, weil das die heutige fachliche Realitaet ist.
- **DB-Inventur** vor Beginn: gibt es in der Prod-DB `task_templates`-Eintraege mit `is_active = false`, `condition_group > 1`, oder `dependencies` mit `required_status != 'done'`? Falls ja: Migration muss diese explizit behandeln. (Analog LA2/LA4-Inventur.)

---

## 5. DB-Inventur (Dev-Snapshot 2026-05-03)

System hat keinen produktiven Datenstand — Dev-Snapshot ist die einzige reale Datenquelle. Inventur-Queries siehe `scripts/`-freier Inline-Run im Session-Log.

### Globale Counts

| Metrik | Wert | Bedeutung fuer LA5 |
|---|---|---|
| `task_templates` total | **76** | Alle aus dem Seed |
| `task_template_conditions` | **64** | Sichtbarkeitsregeln |
| `task_template_dependencies` | **65** | Reihenfolge-Constraints |
| `is_active = false` | **0** | `is_active`-Flag ist dead column im aktuellen Stand |
| `condition_group > 1` | **0** | Keine OR-Gruppen in real data — Schema kann vereinfachen |
| `required_status <> 'done'` | **0** | Dependency-Status-Variabilitaet ist dead column |
| `owning_department_id IS NOT NULL` | **0** | Spalte ungenutzt — kann Slot bleiben oder weg |
| `default_responsibility_id IS NOT NULL` | **75/76** | Pflicht-Daten (eine Ausnahme) |
| `process_area_label IS NOT NULL` | **76/76** | Pflicht-Daten |
| `due_in_days IS NOT NULL` | **76/76** | Pflicht-Daten |
| `template_key` global eindeutig | **true** | Heutige UNIQUE-Constraint passt zur Realitaet |
| `template_key`-Dupes ueber Definitionen | **0** | Option B kann Constraint ohne Datenverlust auf Per-Node-Scope umstellen |

### Audit-Impact

| Metrik | Wert | Bedeutung |
|---|---|---|
| `workflow_tasks` total | **0** | System ist nicht produktiv; keine Audit-Migration noetig |
| `workflow_tasks.task_template_id IS NOT NULL` | **0** | FK-Drop beim Tabellen-Drop unkritisch |

### Per-Definition-Verteilung

| Definition | Templates | Required | Active |
|---|---|---|---|
| `onboarding` | 20 | 20 | 20 |
| `offboarding` | 14 | 14 | 14 |
| `position_change` | 13 | 13 | 13 |
| `department_change` | 12 | 12 | 12 |
| `role_change` | 12 | 12 | 12 |
| `name_change` | 5 | 5 | 5 |

Maximum 20 Specs pro Maßnahmenblock — Sub-DAG-Pflege im Builder muss diese Groessenordnung verkraften.

### Definition-Graph-Realitaet

| Node-Typ | Count | Hat Config |
|---|---|---|
| `start` | 8 | 0 |
| `form` | 8 | 8 |
| `measure_provision` | 2 | 0 |
| `measure_deprovision` | 1 | 0 |
| `measure_change` | 4 | 0 |
| `measure_rename` | 1 | 0 |
| `end` | 8 | 0 |
| `task` | **0** | — |
| `approval` | **0** | — |

**Befund:** In den 6 published Definitions + 2 Drafts gibt es **keinen einzigen** `task`- oder `approval`-Node. Der `legacyTemplateKey`-Pfad in Node-Configs ist heute komplett **unbenutzt** in echten Daten — er existiert nur in Test-Fixtures (`WorkflowDefinitionValidationServiceTests.cs`).

`measure_*`-Nodes haben kein Config — ihre Bindung an `task_templates` erfolgt **implizit** ueber `workflow_definition_id` der parent Definition. Das ist genau der Punkt, den LA5 explizit machen muss.

### Versionen

| Metrik | Wert |
|---|---|
| Published Versions | 6 |
| Draft Versions | 2 |
| Max Versions pro Definition | 2 |

Niedrige Versionsdichte — Migration laeuft pro Definition gegen 1–2 Versionen.

### Konsequenzen fuer Option B

1. **Kein Parity-Snapshot noetig.** Audit-Tabellen sind leer; Daten-Migration ist 1:1-Kopie ohne historische Pruefung.
2. **`task`/`approval`-Pfad ist greenfield.** Per-Node-Specs muessen das Datenmodell sauber unterstuetzen, aber es gibt keine produktiven Eintraege zum Migrieren — Tests + Builder-UI sind die einzigen Konsumenten.
3. **`measure_*`-Block ist die eigentliche Migration.** 76 Template-Eintraege ziehen in `workflow_node_task_specs` mit FK an den jeweiligen `measure_*`-Node der published Version. Pro Definition gibt es 1 measure-Node und N Specs.
4. **Schema-Vereinfachungen moeglich:**
   - `condition_group` Spalte kann entfallen (immer 1 in real data)
   - `task_template_dependencies.required_status` kann auf Default `done` fixiert werden (Spalte raus)
   - `is_active` Spalte muss neu bewertet werden — wenn keine Soft-Deletes geplant sind, weg
   - `owning_department_id` Spalte kann weg, falls nicht in Roadmap
   - `template_key` UNIQUE-Constraint kann von global auf `(node_id, spec_key)` scope-eingegrenzt werden
5. **Cleaner cut moeglich.** Statt `legacyTemplateKey` als String-Bruecke beizubehalten, kann der Per-Node-Spec direkt ueber `workflow_node_task_specs.workflow_node_id` referenziert werden — kein String-Lookup mehr.

### Offene Inventur-Frage

Falls es zwischen jetzt und Implementierung produktive Daten gibt: dieselben Queries gegen Prod-DB laufen lassen. Sonst gilt Dev-Snapshot als verbindliche Basis.

---

## 6. LA5-A — Schema-Entwurf + Migrationsreihenfolge

Konkretisierung von Option B nach DB-Inventur-Ergebnissen. Pre-Prod-Kontext erlaubt **clean cut** statt Dual-Write-Parity.

### 6.1 Neue Tabellen

```sql
-- Per-Node-Task-Spezifikation (ersetzt task_templates)
-- Anker: workflow_nodes (haengt damit an einer Definition-Version, nicht an einer Definition)
-- 1 Spec pro task/approval-Node, N Specs pro measure_*-Node
CREATE TABLE workflow_node_task_specs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_node_id bigint NOT NULL
        REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    spec_key varchar(120) NOT NULL,
    title varchar(220) NOT NULL,
    description text NOT NULL,
    category varchar(80) NOT NULL DEFAULT 'general',
    icon_key varchar(80) NOT NULL DEFAULT 'berechtigungen',
    default_responsibility_id integer
        REFERENCES app_responsibilities(id) ON DELETE SET NULL,
    process_area_label varchar(80),
    is_department_phase_task boolean NOT NULL DEFAULT TRUE,
    is_required boolean NOT NULL DEFAULT TRUE,
    due_in_days integer,
    sort_order integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE (workflow_node_id, spec_key)
);

CREATE INDEX idx_wf_node_task_specs_node
    ON workflow_node_task_specs (workflow_node_id, sort_order, id);

-- UNIQUE composite (id, workflow_node_id) ist via PK + NOT NULL implizit eindeutig,
-- muss aber explizit als CONSTRAINT angelegt werden, damit composite-FKs darauf zeigen koennen
ALTER TABLE workflow_node_task_specs
    ADD CONSTRAINT workflow_node_task_specs_id_node_uk
    UNIQUE (id, workflow_node_id);
```

```sql
-- Sichtbarkeitsregeln auf Form-Antworten (ersetzt task_template_conditions)
-- condition_group entfaellt (immer 1 in real data)
CREATE TABLE workflow_node_task_spec_conditions (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_node_task_spec_id bigint NOT NULL
        REFERENCES workflow_node_task_specs(id) ON DELETE CASCADE,
    answer_key varchar(120) NOT NULL,
    operator varchar(32) NOT NULL
        CHECK (operator IN ('eq','neq','is_true','is_false','is_null','is_not_null')),
    expected_value_text text,
    expected_value_boolean boolean,
    expected_value_number numeric(12,2),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX idx_wf_node_task_spec_conditions_spec
    ON workflow_node_task_spec_conditions (workflow_node_task_spec_id);
```

```sql
-- Reihenfolge-DAG innerhalb eines Nodes (ersetzt task_template_dependencies)
-- required_status entfaellt (immer 'done' in real data)
-- Composite-FK erzwingt: dependent + depends_on liegen am SELBEN Node
CREATE TABLE workflow_node_task_spec_dependencies (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_node_task_spec_id bigint NOT NULL,
    depends_on_workflow_node_task_spec_id bigint NOT NULL,
    workflow_node_id bigint NOT NULL,
    UNIQUE (workflow_node_task_spec_id, depends_on_workflow_node_task_spec_id),
    CHECK (workflow_node_task_spec_id <> depends_on_workflow_node_task_spec_id),
    CONSTRAINT wf_node_task_spec_deps_dependent_fkey
        FOREIGN KEY (workflow_node_task_spec_id, workflow_node_id)
        REFERENCES workflow_node_task_specs(id, workflow_node_id) ON DELETE CASCADE,
    CONSTRAINT wf_node_task_spec_deps_depends_on_fkey
        FOREIGN KEY (depends_on_workflow_node_task_spec_id, workflow_node_id)
        REFERENCES workflow_node_task_specs(id, workflow_node_id) ON DELETE CASCADE
);

CREATE INDEX idx_wf_node_task_spec_deps_dependent
    ON workflow_node_task_spec_dependencies (workflow_node_task_spec_id);
CREATE INDEX idx_wf_node_task_spec_deps_depends_on
    ON workflow_node_task_spec_dependencies (depends_on_workflow_node_task_spec_id);
```

**Begruendung der Spaltenwahl (basiert auf §5 Inventur):**

| Spalte | Beibehalten | Begruendung |
|---|---|---|
| `template_key` → `spec_key` | ja | Stable Identitaet pro Node fuer `legacyTemplateKey`-Ersatz + Frontend-Reference |
| `default_responsibility_id` | ja | 75/76 belegt — fachlich Pflichtfeld |
| `process_area_label` | ja | 76/76 belegt |
| `due_in_days` | ja | 76/76 belegt |
| `is_required` | ja | Belegt, fachlich relevant trotz aktuell 100% true |
| `is_department_phase_task` | ja | UI-Logik nutzt es |
| `category`, `icon_key` | ja | UI-Logik |
| `is_active` | **weg** | Immer true — keine Soft-Delete-Semantik in real data |
| `owning_department_id` | **weg** | Immer NULL — keine produktive Nutzung |
| `workflow_definition_id` | **weg** | Indirekt via `workflow_node_id → workflow_definition_version_id → workflow_definition_id`. Lookup-Helper `LoadWorkflowDefinitionForNode` |
| `condition_group` | **weg** | Immer 1 — OR-Gruppen nicht in Roadmap, additiv re-addable falls noetig |
| `dependencies.required_status` | **weg** | Immer 'done' |

### 6.2 Aenderungen an `workflow_tasks`

```sql
-- Audit-Link auf Spec statt Template
-- Inventur: 0 Zeilen, kein Daten-Backfill noetig
ALTER TABLE workflow_tasks
    DROP CONSTRAINT workflow_tasks_task_template_id_fkey,
    DROP COLUMN task_template_id,
    ADD COLUMN workflow_node_task_spec_id bigint
        REFERENCES workflow_node_task_specs(id) ON DELETE SET NULL;
```

### 6.3 Daten-Migration (clean cut)

Pre-Prod-Kontext: keine produktiven `workflow_tasks` (Inventur: 0). Migration ist 1:1-Kopie der 76 Templates + 64 Conditions + 65 Dependencies in die neuen Tabellen, anchored an die `measure_*`-Nodes der published Versions.

```sql
-- Schritt 1: Specs aus task_templates kopieren, FK an measure_*-Node der published Version
-- Annahme aus Inventur: pro Definition gibt es genau 1 measure_*-Node in der published Version,
-- und task_templates der Definition gehoeren konzeptionell ALLE zu diesem Block
INSERT INTO workflow_node_task_specs (
    workflow_node_id, spec_key, title, description, category, icon_key,
    default_responsibility_id, process_area_label,
    is_department_phase_task, is_required, due_in_days, sort_order
)
SELECT
    measure_node.id,
    tt.template_key,
    tt.title, tt.description, tt.category, tt.icon_key,
    tt.default_responsibility_id, tt.process_area_label,
    tt.is_department_phase_task, tt.is_required, tt.due_in_days, tt.sort_order
FROM task_templates tt
JOIN workflow_definition_versions v
    ON v.workflow_definition_id = tt.workflow_definition_id
    AND v.published_at IS NOT NULL
JOIN workflow_nodes measure_node
    ON measure_node.workflow_definition_version_id = v.id
    AND measure_node.node_type LIKE 'measure_%'
WHERE tt.is_active = TRUE;
-- Erwartete Zeilen: 76

-- Schritt 2: Conditions analog ueber Spec-Lookup per (node_id, spec_key)
INSERT INTO workflow_node_task_spec_conditions (
    workflow_node_task_spec_id, answer_key, operator,
    expected_value_text, expected_value_boolean, expected_value_number
)
SELECT
    s.id, c.answer_key, c.operator,
    c.expected_value_text, c.expected_value_boolean, c.expected_value_number
FROM task_template_conditions c
JOIN task_templates tt ON tt.id = c.task_template_id
JOIN workflow_node_task_specs s
    ON s.spec_key = tt.template_key
    -- via workflow_definition_id → workflow_node
    AND s.workflow_node_id IN (
        SELECT n.id FROM workflow_nodes n
        JOIN workflow_definition_versions v ON v.id = n.workflow_definition_version_id
        WHERE v.workflow_definition_id = tt.workflow_definition_id
    );
-- Erwartete Zeilen: 64

-- Schritt 3: Dependencies analog
INSERT INTO workflow_node_task_spec_dependencies (
    workflow_node_task_spec_id, depends_on_workflow_node_task_spec_id, workflow_node_id
)
SELECT
    s_dep.id, s_on.id, s_dep.workflow_node_id
FROM task_template_dependencies d
JOIN task_templates tt_dep ON tt_dep.id = d.task_template_id
JOIN task_templates tt_on ON tt_on.id = d.depends_on_task_template_id
JOIN workflow_node_task_specs s_dep
    ON s_dep.spec_key = tt_dep.template_key
JOIN workflow_node_task_specs s_on
    ON s_on.spec_key = tt_on.template_key
    AND s_on.workflow_node_id = s_dep.workflow_node_id;
-- Erwartete Zeilen: 65, alle SAME-NODE
-- Falls eine Dependency cross-node waere, wuerde der Composite-FK in
-- workflow_node_task_spec_dependencies sie ablehnen. Das ist der Sicherheitsanker.
```

### 6.4 Slice-Reihenfolge (konkret)

**LA5-A** ✓ erledigt — dieses Dokument.

**LA5-B (1,5 d) — Schema + Daten-Kopie**
1. `01_schema.sql` um die 3 neuen Tabellen + Index ergaenzen.
2. `02_bootstrap.sql` + `02_dev_seed.sql`: nach `task_templates`-INSERT-Bloecken die 3 neuen Tabellen befuellen via dem Migration-SQL aus §6.3 (oder als statische INSERTs nach Manual-Lauf gegen Dev-Seed).
3. `workflow_tasks.task_template_id` → `workflow_node_task_spec_id` umbenennen + FK umlenken.
4. **Alte Tabellen erst in LA5-F droppen** — bleiben fuer Read-Pfade in LA5-C als Fallback erhalten.
5. Smoke-Test: Build clean, alle 330+ Backend-Tests gruen.

**LA5-C (1 d) — Read-Pfade umstellen**
1. `PostgresWorkflowRuntimeRepository.LoadActiveTaskTemplateByKey` → `LoadActiveTaskSpecByNodeId` (sucht Spec via `workflow_node_id` der Approval/Task-Node-Instance, nicht via String-Key). Fallback noetig fuer measure_*-Pfad nicht: dort ist der Generator selbst Konsument, nicht ein Per-Node-Resolver.
2. `PostgresWorkflowTaskGenerationService.LoadTaskTemplatesAsync` → `LoadTaskSpecsForNodeAsync(workflow_node_id)` — bekommt jetzt explizit den Maßnahmen-Node-Identifier statt der Definition-Id.
3. `LoadTaskTemplateConditionsAsync`, `LoadTaskTemplateDependenciesAsync` analog auf Node-Id-Filter.
4. `WorkflowDefinitionGraphMappingOperations.LegacyTaskTemplateExists`-Validation entfaellt — `legacyTemplateKey` existiert nicht mehr; statt dessen Validation, dass `task`/`approval`-Nodes ein `workflow_node_task_specs`-Eintrag haben.
5. `LifecycleStartupValidationExtensions` analog.
6. `WorkflowDefinitionValidationService.ValidateRequiredStringConfig(node, "legacyTemplateKey")` entfaellt — Spec-Existenz wird DB-seitig validiert.

**LA5-D (1 d) — Builder + Admin-UI lesend umstellen**
1. `WorkflowBuilderStepConfigEditor.tsx`: kein `legacyTemplateKey`-Dropdown mehr — fuer `task`/`approval`-Nodes wird die Spec inline editiert (1:1 zum Node).
2. Admin-`AdminTaskTemplate*`-Editor bleibt bestehen, aber lest aus `workflow_node_task_specs` ueber den Maßnahmen-Node der published Version statt aus `task_templates`.
3. Frontend-Types: `AdminTaskTemplate*` → `AdminTaskSpec*` umbenennen, `templateKey` → `specKey`.
4. Pruefen: 22 Frontend-Files mit `task-template`-Refs (Grep-Treffer). Mechanisches Rename ist Großteil.

**LA5-E (1 d) — Schreibpfade umstellen**
1. Admin-UI Save-Path: `PUT /admin/config/process-types/.../task-templates/...` Endpoints + Service-Methods auf neue Tabellen umstellen. Endpoint-Pfade ggf. umbenennen oder als Alias halten — Entscheidung: **direkt umbenennen** auf `/admin/workflow-definitions/.../task-specs/...`, weil System nicht produktiv und kein Konsument stoert.
2. `ResolveProcessTypeId`/`NormalizeWorkflowDefinitionId`-Bridges in `MasterDataOperations` entkoppeln — kein FK mehr auf process_type-Aera.

**LA5-F (0,5 d) — Drop**
1. `task_templates`, `task_template_conditions`, `task_template_dependencies` droppen (Schema + Seed).
2. `legacyTemplateKey` aus allen Test-Fixtures + Validation-Pfaden raus (nicht mehr verwendet).
3. `LegacyTaskTemplateExists`, `LoadActiveTaskTemplateByKey`, `LoadTaskTemplatesAsync` etc. loeschen.
4. Tests aufraeumen.

**LA5-G (0,5 d) — Doku**
1. `KauthWorkflow/Architektur/Zielarchitektur.md`: Task-System-Beschreibung um neuen Spec-Anker erweitern.
2. `KauthWorkflow/Architektur/Migrationspfad.md`: Parallelzustand-Tabelle bereinigen (Eintrag `legacyTemplateKey` ist weg).
3. `KauthWorkflow/Stand/Legacy-Abbau-Plan.md`: Schritt 9 als ✓ markieren mit Zusammenfassung.
4. `MEMORY.md`: aufraeumen.
5. `TODO.md`: LA5 → ✓ done.

### 6.5 Risiken + Mitigationen

| Risiko | Wahrscheinlichkeit | Mitigation |
|---|---|---|
| measure_*-Node der published Version nicht eindeutig (mehrere measure-Nodes pro Definition) | **niedrig** — Inventur zeigt 1 measure-Node pro Definition | Migration-SQL bricht via UNIQUE-Constraint, falls 2 measure-Nodes auftauchen — explizite Pre-Check-Query in LA5-B |
| Cross-Node-Dependency in real data | **null** — Inventur G3=0 | Composite-FK lehnt es ab; wir wuerden es im Migration-Lauf sehen |
| Test-Fixtures mit `legacyTemplateKey` brechen | **hoch** — ~12 Test-Stellen | LA5-F/G: parallel zur Code-Migration alle Fixtures auf neue Specs umstellen |
| Builder-UI-Save-Pfad bricht waehrend Slice | **mittel** | LA5-B/C/D rein lesend; LA5-E ist atomic Schreibpfad-Switch |
| Frontend-Code-Surface unterschaetzt (22 Files mit task-template-Refs) | **mittel** | LA5-D enthaelt Rename-Pass mit konkretem Grep-Ziel; sed-script wie in 6.3d-iii |

### 6.6 Pre-Check-Queries fuer LA5-B

Vor Daten-Migration laufen lassen, bricht die Migration ab falls != erwartet:

```sql
-- Pre-Check 1: Genau 1 measure-Node pro published Definition
SELECT v.workflow_definition_id, COUNT(*) AS measure_nodes
FROM workflow_definition_versions v
JOIN workflow_nodes n ON n.workflow_definition_version_id = v.id
WHERE v.published_at IS NOT NULL AND n.node_type LIKE 'measure_%'
GROUP BY v.workflow_definition_id
HAVING COUNT(*) <> 1;
-- Erwartet: 0 Zeilen

-- Pre-Check 2: Alle aktiven task_templates haben eine published Version
SELECT tt.id, tt.template_key, tt.workflow_definition_id
FROM task_templates tt
WHERE tt.is_active = TRUE
  AND NOT EXISTS (
      SELECT 1 FROM workflow_definition_versions v
      WHERE v.workflow_definition_id = tt.workflow_definition_id
        AND v.published_at IS NOT NULL
  );
-- Erwartet: 0 Zeilen

-- Pre-Check 3: Dependencies sind cross-template OK, aber alle Templates muessen
-- in der gleichen Definition liegen (sonst kann Composite-FK nicht greifen)
SELECT d.id, tt_dep.workflow_definition_id AS dep_def, tt_on.workflow_definition_id AS on_def
FROM task_template_dependencies d
JOIN task_templates tt_dep ON tt_dep.id = d.task_template_id
JOIN task_templates tt_on ON tt_on.id = d.depends_on_task_template_id
WHERE tt_dep.workflow_definition_id <> tt_on.workflow_definition_id;
-- Erwartet: 0 Zeilen
```

---

## 7. Verwandte Dokumente

- [[Legacy-Abbau-Plan]] — Schritt 9 ist LA5
- [[Zielarchitektur]] — Workflow Definition Layer + Task-System
- [[Entscheidungen]] — "Workflow-Definition ist eigener Kern" + "Versionierung ist Pflicht"
- [[Migrationspfad]] — Parallelzustand-Tabelle, in der LA5 nach Abschluss eingetragen wird
- [[WorkflowBuilder-FormEditor-Skizze]] — Builder-UX, mit der LA5 kompatibel bleiben muss
