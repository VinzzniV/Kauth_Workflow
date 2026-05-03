# Code Review Status

#stand #review

Aktueller Stand der Code Review. Was offen ist.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

---

## Gesamtbewertung (Stand 2026-05-03 — nach Zyklus 5)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | A- | Repository-Monolith in 8 Subsystem-Klassen + TaskTemplate 3-fach + GraphMapping ausgelagert |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B+ | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | B+ | Engine als Domain-Service + HQ5-Hooks mit Test-Coverage; Sweep-Timeout |
| Frontend-Architektur | B+ | WorkflowBuilder Form-Editor + Rotation-Pages refactored; AdminConfigPage Bundle-Refactor |
| Testbarkeit | B | Testcontainers + Integration-Tests + Hook-Tests (172 Frontend, 384 Backend) |
| Skalierbarkeit | B- | Workflow-Task-Filter SQL-pre-narrowed |
| Sicherheit | B+ | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |
| Lesbarkeit | B+ | Konventionen durchgaengig |

---

## Zyklus-Historie

Alle Zyklen 1–5 abgeschlossen. Detail via `git log` und `CODEX_SYNC.md`-History.

| Zyklus | Datum | Stichwort |
|--------|-------|-----------|
| 1 | 2026-04-23 | Code-Review + Hardening: C1–C4, H1–H7, L1/L3/L5/L6 |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, Task-Filter SQL-Push, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: HQ1-Z3 (Hook-Tests), HQ3-Z3 (Repo-Splits), HQ2-Z3 (Hook-Zerlegung) |
| 4 | 2026-05-02 | Naming + Härtungen: HQ1-Z4 LegacyProcessTypeKey, HQ2-Z4 effectiveResponsibilityIds, LQ1-Z4..LQ4-Z4 |
| 5 | 2026-05-02..03 | Legacy-Abbau: LA1 (LegacyWorkflowStatus), LA2 (setup-Node), LA3 (definition_key Filter), LA4 (HasLegacyRolePermission), LA5 (Specs am Node) |

---

## Aktiver Zyklus 6 (Runtime-Lifecycle, 2026-05-03)

Migrationspfad-Schritt 7: Task-System an Node-Runtime anbinden. Detail-Skizze + Slice-Plan in `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.

| Slice | Status |
|-------|--------|
| Slice 0 — Inventur | ✓ done |
| Q6 — rekursiver Loop-Pfad | ✓ done — Option (a) Apply-seitige Iteration |
| Slice 1 — Engine-Extraktion | gated auf lokale Postgres-DB |
| Slice 2 — Service-Brücke (Lifecycle-Service + Connection-Scope) | gated auf Slice 1 |

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` (2222 Z.) Split | deferred — kein Trigger |

---

## Verwandte Notizen

- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Schritt7-Runtime-TaskSystem-Skizze]] — Aktive Architekturarbeit
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
