# TODO.md

Diese Datei steuert die Reihenfolge der Umsetzung aktiver Review-Zyklen.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in `CODE_REVIEW.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen (Priorisierungs- und Analyseabschnitte).

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz pruefen und festhalten, welche dieser Dokus bei der Aufgabe mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Relevante Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Wenn eine Aufgabe abgeschlossen wurde, muss ihr Status in `TODO.md` im selben Arbeitsgang auf `done` gesetzt werden.

Wichtig:
- `TODO.md` enthaelt bewusst keine Detailanweisungen.
- Die KI darf die Aufgabe nicht allein aus `TODO.md` ableiten.
- Die KI muss fuer jede Aufgabe `CODE_REVIEW.md` als aktuelle Primaerquelle verwenden.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus TODO.md)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

Format-Beispiel: *„Naechster Schritt: HQ1 — Decision-Condition-Migration. Reasoning: high. Modell: opus."*

Damit kann der Nutzer entscheiden, ob er das Modell wechseln, die Aufgabe verschieben oder den Scope anpassen will, **bevor** Tokens fliessen.

---

## Prioritaeten

- `CRITICAL` = blockiert Nutzung, kann inkonsistente Datenstaende erzeugen oder laesst Berechtigungen aktiv
- `HIGH` = strukturell wichtig fuer Wartbarkeit, Validierung, Testbarkeit und Betrieb
- `LOW` = sinnvolle Haertung oder Bereinigung ohne unmittelbaren Blocker

---

## Aufgabenteilung: Codex vs. Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

---

## Aktiver Zyklus: Legacy-Abbau (Zyklus 5, 2026-05-02)

Detailanalyse in `KauthWorkflow/Stand/Legacy-Abbau-Plan.md`. Empfohlene Reihenfolge entspricht Abhaengigkeiten: erst die risikoarmen, voraussetzungsfreien Schritte, dann die abhaengigen.

| # | Aufgabe | Prio | Aufwand | Reasoning Effort | Modell | Status |
|---|---------|------|---------|------------------|--------|--------|
| LA1 | **`LegacyWorkflowStatus` entfernen** (= Abbau-Plan Schritt 1). DTO-Feld `WorkflowRuntimeDtos.cs:28` raus, `WorkflowStatusRules.OpenLegacy` + `ToLegacyStatus()` raus, Frontend-Mapper `toWorkflowLegacyStatus()` + `normalizeWorkflowLegacyStatus()` + 3 Aufrufstellen in `mappers.ts` raus. Tests anpassen. | LOW | 0,5 d | low | sonnet | ✓ done (2026-05-02) |
| LA2 | **`setup`-Node-Type ablegen** (= Abbau-Plan Schritt 2). Zuerst DB-Inventur: gibt es noch `type = 'setup'`-Zeilen in `workflow_nodes` fuer published Versions? Falls ja, SQL-Datenmigration. Dann Code: `LEGACY_SETUP_TITLES` + alle `nodeType === "setup"`-Sonderfaelle in Builder-Komponenten entfernen, `setup` aus Node-Type-Union raus. | LOW | 1–2 d | medium | sonnet | ✓ done (2026-05-03) — DB-Inventur Prod ausstehend (siehe CODEX_SYNC) |
| LA3 | **Filter-Params auf `definition_key` umstellen** (= Abbau-Plan Schritte 3+4). Voraussetzung: Definition-Coverage-Check (jeder genutzte `process_type` hat eine published `workflow_definition`). Dann: `legacyProcessTypeKey`-Filter-Param aus Endpoints + Frontend entfernen, Workflow-Liste + Dashboard filtern nach `workflowDefinitionKey`. | HIGH | 1,5 d | medium | sonnet | ✓ done (2026-05-03) |
| LA4 | **`HasLegacyRolePermission()` entfernen** (`AuthorizationPolicyService.cs:392`). Voraussetzung: pruefen ob noch offene Tasks ohne `node_instance_id` in der Prod-DB existieren. Falls nein: Branch + Methode loeschen, Kommentar bei Aufrufstelle raus. | LOW | 0,5 d | low | sonnet | ✓ done (2026-05-03) |
| LA5 | **`legacyTemplateKey` aus Node-Configs loesen** (= Abbau-Plan Schritt 9). Workflow-Definitionen besitzen Task-Spezifikationen direkt am Node statt per Legacy-Key auf `task_templates` zu zeigen. | HIGH | > 5 d | high | opus | ✓ done (2026-05-03) — Option B, siehe `KauthWorkflow/Architektur/LA5-TaskSpezifikation-Skizze.md` |

**Reihenfolge-Empfehlung:** LA1 (keine Abhaengigkeit) → LA2 (nach DB-Inventur) → LA4 (nach DB-Inventur) → LA3 (nach Coverage-Check) → LA5 (eigene Session-Planung).

**Watch-Items (unveraendert):**
- **LQ2-Z3** `EntraDirectorySyncService` (2222 Z.) — defer ohne Trigger.
- **LQ4-Z3** `AdminConfigPage` (423 Z., 7 Hooks) — beobachten; bei 8. Hook splitten.
- **LQ3-Z4** `GetOrCreate…WorkingDraft` → `Ensure…` — defer bis Beruehrungs-Anlass.
- **LQ4-Z4** `MatchesTaskAssignment`-Predicate-Rename — defer bis Beruehrungs-Anlass.

---

## Abgeschlossene Zyklen

### Zyklus 4: Naming + kleine Haertungen (abgeschlossen 2026-05-02)

| ID | Aufgabe | Status |
|----|---------|--------|
| HQ1-Z4 | `ProcessTypeKey` → `LegacyProcessTypeKey` (DTOs + 4 Endpoints + Frontend + Compat-Doku) | ✓ done |
| HQ2-Z4 | `responsibilityIds` → `effectiveResponsibilityIds` (Methoden-Signaturen + Struct-Field) | ✓ done |
| LQ1-Z4 | `ParseDecisionCondition`-Exception mit Edge-Kontext anreichern | ✓ done |
| LQ2-Z4 | Sub-Section-Error-Boundaries in `AdminConfigPage` + `WorkflowDetailPage` | ✓ done |
| LQ3-Z4 | `GetOrCreate…WorkingDraft` → `Ensure…` | defer |
| LQ4-Z4 | `MatchesTaskAssignment`-Predicate-Rename | defer |

---

## Abgeschlossene Zyklen

### Zyklus 3 (Test-Coverage + Wartbarkeits-Split, 2026-05-02)

Alle 5 Tasks done (2 davon partial mit dokumentiertem Defer-Grund). 1 Watch-Item, 1 Defer-Item.

| ID | Aufgabe | Status |
|----|---------|--------|
| LQ3-Z3 | Doku-Drift `Code-Review-Status.md` | ✓ done |
| HQ1-Z3 | Tests fuer HQ5-Hooks (+32 Tests, gesamt 172) | ✓ done |
| LQ1-Z3 | RotationNotification 2-h-Timeout-Wrapper | ✓ done |
| HQ3-Z3 | Repository-Partials splitten (TaskTemplate 3-fach, WorkflowDefinition GraphMapping ausgelagert) | ✓ done partial — Versions-Split deferred wegen Tx-Kopplung |
| HQ2-Z3 | `useAdminWorkflowBuilder` Sub-Hook-Zerlegung (Reference-Data extrahiert, 865→761) | ✓ done partial — 3 weitere Sub-Hook-Splits deferred |
| LQ2-Z3 | EntraSync 2222 Z. Split | deferred — kein Anlass |
| LQ4-Z3 | AdminConfigPage Watch | watch |

### Zyklus 2 (Hardening + Testbarkeit, 2026-05-02)

Alle HIGH (HQ1-HQ5) + alle LOW (LQ1-LQ7) abgeschlossen — 12 Tasks. Backend Architektur B-→B+, Frontend C+→B, Testbarkeit D→C+, Skalierbarkeit C→B-.

Highlights:
- HQ1: Decision-Condition-Migration + Save-Validation
- HQ2: In-Memory-Task-Filter via SQL-EXISTS-Narrowing
- HQ3: `person_match_audit_log` Tabelle + CTE-INSERT in Sync-Pfaden
- HQ4: Testcontainers-Setup → Integration-Tests laufen out-of-the-box
- HQ5: Rotation-Pages refactored (35-49% kleiner)
- LQ-Bundle: Property-Catalog-Endpoint, Sim-Login-Rollen, Notification-Linter (war schon da), Hook-Tot-Code raus, Vault-Doku

---

### Zyklus 1: Code-Review 2026-04-23 + Folge-Hardening (abgeschlossen 2026-05-02)

CRITICAL- und HIGH-Punkte (C1-C4, H1-H7) komplett erledigt. Folgepunkte H6, L1, L3, L5, L6 ebenfalls. L2 deferred. Repository-Monolith in 8 Subsystem-Klassen aufgespalten. RotationTaskRegenerationEngine als pure Domain-Engine extrahiert.

### Zyklus „WorkflowBuilder Form-Editor" (L7, abgeschlossen 2026-05-02)

Phase 1-3 + Restposten R1-R7+R9. Form-Editor mit 12 Schritt-Typen, Bundle 449→228 kB, React-Flow + dagre raus, Hook-API 47→39 Properties. Decision-Bedingungs-Bug entdeckt + gefixt. Restposten R8 + R10 offen (siehe unten).

Detail in `KauthWorkflow/Architektur/WorkflowBuilder-FormEditor-Skizze.md`.

---

## Offene Restposten (zyklusuebergreifend)

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen durchklicken) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
