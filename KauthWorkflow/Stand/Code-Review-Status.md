# Code Review Status

#stand #review

Aktueller Stand der Code Review. Was erledigt ist, was noch aussteht.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

---

## Gesamtbewertung (Stand 2026-05-02 — Zyklus 4)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | A- | Repository-Monolith in 8 Subsystem-Klassen + TaskTemplate 3-fach + GraphMapping ausgelagert |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B+ | Permission-Audit hat Reason-Feld; Person-Matching-Audit live; HQ2-Z4 Naming-Verbesserung empfohlen |
| Rotation-Feature | B+ | Engine als Domain-Service + HQ5-Hooks mit Test-Coverage; Sweep-Timeout (LQ1-Z3) |
| Frontend-Architektur | B+ | WorkflowBuilder Form-Editor + Rotation-Pages refactored; `useAdminWorkflowBuilder` 865→761 nach HQ2-Z3 |
| Testbarkeit | B | Testcontainers + Integration-Tests + HQ5-Hooks-Tests (172 Frontend, 384 Backend) |
| Skalierbarkeit | B- | Workflow-Task-Filter SQL-pre-narrowed (HQ2) |
| Sicherheit | B+ | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |
| **Lesbarkeit (Z4)** | **B+** | Konventionen durchgaengig; HQ1-Z4 + HQ2-Z4 als gezielte Naming-Verbesserungen empfohlen |

---

## Zyklus 1 (Code-Review 2026-04-23) — abgeschlossen

CRITICAL- und HIGH-Punkte (C1–C4, H1–H7) komplett erledigt. Folgepunkte H6, L1, L3, L5, L6 ebenfalls. L2 deferred.

| ID | Problem | Status |
|----|---------|--------|
| C1 | Transaktionsgrenzen Rotation-Regenerierung | ✓ erledigt (COD-1, 2026-04-23) |
| C2 | Template ohne Zuständigkeit → Aufgaben unsichtbar | ✓ erledigt (2026-04-24) |
| C3 | Entra-gelöschte User nicht deaktiviert | ✓ erledigt (CLA-2, 2026-04-23) |
| C4 | Task-Filter in-memory statt SQL (Rotation) | ✓ erledigt (CLA-1, 2026-04-23) |
| H1 | Monolithisches Repository (23.758 Zeilen) | ✓ erledigt (Phase 8, 2026-05-02) |
| H2–H7 | Diverse | ✓ alle erledigt |
| L1, L3, L5, L6 | Hardening | ✓ alle erledigt |
| L2 | Datenbereinigung Drafts | deferred — wartet auf Produkt-Entscheidung |
| L7 | WorkflowBuilder Canvas-UX | ✓ erledigt (eigener Zyklus, 2026-05-02 — Form-Editor live) |

---

## Zyklus 2 (Hardening + Testbarkeit, 2026-05-02) — abgeschlossen

| ID | Problem | Status |
|----|---------|--------|
| HQ1 | Decision-Condition-Migration + Save-Validation | ✓ done (2026-05-02) |
| HQ2 | In-Memory-Task-Filter nach SQL ziehen | ✓ done (2026-05-02) |
| HQ3 | Personen-Matching-Audit-Trail | ✓ done (2026-05-02) — Tabelle `person_match_audit_log` |
| HQ4 | Test-DB-Setup via Testcontainers | ✓ done (2026-05-02) |
| HQ5 | Frontend-Page-Refactor (Rotation-Pages) | ✓ done (2026-05-02) — 35–49% kleiner |
| LQ1 | Mapping-Property-Catalog-Endpoint | ✓ done (2026-05-02) |
| LQ2 | Mapping-Editor flat-only — als Architektur-Entscheidung dokumentiert | ✓ done (2026-05-02) |
| LQ3 | Sim-Login-Untertitel mit Rollen | ✓ done (2026-05-02) |
| LQ4 | Notification-Templates-Linter | ✓ war bereits implementiert |
| LQ5 | Frontend-Error-Boundaries | ✓ done (2026-05-02) |
| LQ6 | WorkflowBuilder-Tests (Roundtrips) | ✓ done (2026-05-02) — 32 neue Tests |
| LQ7 | Hook-Tot-Code-Cleanup | ✓ done (2026-05-02) |

---

## Zyklus 3 (Test-Coverage + Wartbarkeits-Split, 2026-05-02) — abgeschlossen

| ID | Problem | Prio | Status |
|----|---------|------|--------|
| LQ3-Z3 | Doku-Drift `Code-Review-Status.md` aktualisieren | LOW | ✓ done |
| HQ1-Z3 | Tests fuer `useRotationOperationsView` + `useRotationStationForm` | HIGH | ✓ done — +32 Tests, gesamt 172 |
| LQ1-Z3 | `RotationNotificationHostedService` Sweep-Timeout-Wrapper | LOW | ✓ done — 2-h CancelAfter + Catch |
| HQ3-Z3 | Repository-Partials splitten | HIGH | ✓ done partial — TaskTemplate 3-fach (1017→391+251+391); WorkflowDefinition GraphMapping ausgelagert (1430→1103+350); Versions-Split deferred (Tx-Kopplung) |
| HQ2-Z3 | `useAdminWorkflowBuilder` Sub-Hook-Zerlegung | HIGH | ✓ done partial — Reference-Data ausgelagert (140 Z.), Main-Hook 865→761; 3 weitere Splits deferred (State-Verzahnung) |
| LQ2-Z3 | `EntraDirectorySyncService` (2222) Split | LOW | deferred |
| LQ4-Z3 | `AdminConfigPage.tsx` (423) Watch | LOW | watch |

## Zyklus 4 (Naming + kleine Haertungen, 2026-05-02) — abgeschlossen

Frischer Sweep mit explizitem Naming-/Lesbarkeits-Audit. CRITICAL-Liste leer. Konventionen durchgaengig — Lesbarkeit insgesamt B+.

| ID | Problem | Prio | Status |
|----|---------|------|--------|
| HQ1-Z4 | `ProcessTypeKey` → `LegacyProcessTypeKey` (DTO + 4 Endpoints + Frontend + Compat-Doku) | HIGH | ✓ done (2026-05-02) |
| HQ2-Z4 | `responsibilityIds` → `effectiveResponsibilityIds` (Methoden-Signaturen + Struct-Field) | HIGH | ✓ done (2026-05-02) |
| LQ1-Z4 | `ParseDecisionCondition`-Exception mit Workflow-/Edge-Kontext anreichern | LOW | ✓ done (2026-05-02) |
| LQ2-Z4 | Sub-Section-Error-Boundaries in `AdminConfigPage` + `WorkflowDetailPage` | LOW | ✓ done (2026-05-02) |
| LQ3-Z4 | `GetOrCreate…WorkingDraft` → `Ensure…WorkingDraft` (.NET-Konvention) | LOW | defer |
| LQ4-Z4 | `MatchesTaskAssignment` Predicate-Rename | LOW | defer |

**Drei Agent-Falsch-Positive verworfen** (siehe `CODE_REVIEW.md` Zyklus 4): `tt`/`pt`/`wta` sind SQL-Aliase (kein C#); `GetAdminWorkflowDefinitionVersionDetailById` ist read-only (nur der Wrapper mutiert); `NormalizeNullableText` ist im Vokabular etabliert.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |

---

## Verwandte Notizen

- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
