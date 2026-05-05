# Code Review Status

#stand #review

Aktueller Stand der Code Review. Was offen ist.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

---

## Gesamtbewertung (Stand 2026-05-05 — nach Zyklus 6, Zyklus 7 aktiv)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | A- | Repo-Monolith aufgespalten + Lifecycle-Service nach S7 wirksam, aber als Commit-Grenze noch nicht voll konsolidiert |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B+ | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | B+ | Engine als Domain-Service + HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | B+ | Builder + Listen-Workspaces refactored; Split-Views + Karten/Tabelle; Personenakte 360° |
| Testbarkeit | B | Testcontainers + Integration-Tests; Lifecycle-Service hat jetzt einen eigenen Service-Test fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | B- | Workflow-Task-Filter SQL-pre-narrowed |
| Sicherheit | B+ | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |
| Lesbarkeit | B+ | Konventionen durchgaengig; Validation-Service als groesster Monolith offen |

---

## Zyklus-Historie

| Zyklus | Datum | Stichwort |
|--------|-------|-----------|
| 1 | 2026-04-23 | Code-Review + Hardening: C1–C4, H1–H7, L1/L3/L5/L6 |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, Task-Filter SQL-Push, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: HQ1-Z3 (Hook-Tests), HQ3-Z3 (Repo-Splits), HQ2-Z3 (Hook-Zerlegung) |
| 4 | 2026-05-02 | Naming + Härtungen: HQ1-Z4 LegacyProcessTypeKey, HQ2-Z4 effectiveResponsibilityIds, LQ1-Z4..LQ4-Z4 |
| 5 | 2026-05-02..03 | Legacy-Abbau: LA1 (LegacyWorkflowStatus), LA2 (setup-Node), LA3 (definition_key Filter), LA4 (HasLegacyRolePermission), LA5 (Specs am Node) |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope; 409 Tests gruen |

---

## Aktiver Zyklus 7 — Lifecycle-Service-Konsolidierung (2026-05-05)

Folgearbeit aus Zyklus 6. Lifecycle-Service nach Schritt 7 mid-state: vier Pass-Through-Methoden ohne Conn+Tx, drei statische Repo-Aufrufe als Bridge, doppelte Aufrufstelle in `PostgresWorkflowRepository.TaskOperations`. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 7".

| Befund | Prio | Status |
|--------|------|--------|
| Z7-1 — Lifecycle-Service vollstaendig zur Commit-Grenze ausbauen (Slice-Plan) | HIGH | done am 2026-05-05 — Z7-1.1, Z7-2, Z7-1.2, Z7-1.3, Z7-1.4, Z7-1.5a, Z7-1.5b.i–v erledigt; Scoped-Vertrag `IWorkflowDefinitionRuntimeScopedRepository` aufgeloest |
| Z7-2 — `WorkflowLifecycleService` Test-Coverage | HIGH | done am 2026-05-05 |
| Z7-3 — `WorkflowDefinitionValidationService` (2131 Z.) splitten | MEDIUM | in Arbeit (4 Sub-Slices, Z7-3.1 Catalog + Z7-3.2 Helpers + Z7-3.3 SnapshotValidator done am 2026-05-05) |

Naechster Schritt: Z7-3.4 (`WorkflowDefinitionDraftValidator` extrahieren, sonnet, medium). Z7-3.1 (Catalog) + Z7-3.2 (Helpers) + Z7-3.3 (SnapshotValidator) am 2026-05-05 erledigt.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` (2485 Z.) Split | deferred — kein Trigger |
| FE-8 | `approval_task_template_key` → `approval_spec_key` Rename | defer ohne Trigger |

---

## Verwandte Notizen

- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Schritt7-Runtime-TaskSystem-Skizze]] — Architekturarbeit aus Zyklus 6
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
