# Code Review — kauth_workflow

**Stand**: 2026-05-03 — nach Abschluss von Zyklus 1–5.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02 Zyklus 2/3/4; 2026-05-02..03 Zyklus 5).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith in 8 Subsystem-Klassen aufgespalten; TaskTemplate-Familie 3-fach gesplittet; WorkflowDefinition GraphMapping ausgelagert |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints, klare Tabellen |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit-Trail live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure-static-Domain-Engine; HQ5-Hooks mit Test-Coverage; Sweep-Timeout |
| Frontend-Architektur | **B+** | WorkflowBuilder Form-Editor (Bundle 449→228 kB); Rotation-Pages 35–49% kleiner; AdminConfigPage Bundle-Refactor (7 Domain-Bundles statt Flat-Spread) |
| Testbarkeit | **B** | Testcontainers laeuft; 172 Frontend + 384 Backend Tests |
| Skalierbarkeit | **B-** | Workflow-Task-Filter via SQL-EXISTS pre-narrowed; In-Memory-Filter raus |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard in Production hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; Naming-Audit (Z4) ohne kritische Befunde |

---

## Offene Befunde

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung (Auto-Delete? Soft-Archive? Cron?) | Zyklus 1 |
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2222 Z.) Split | deferred ohne Trigger — Risiko niedrig (Timer-Pfad, kein User-Pfad). Refactor erst bei Anlass (z. B. Microsoft Graph SDK v6) | Zyklus 3 |

Alles andere aus den Zyklen 1–5 ist abgeschlossen.

---

## Aktiver Zyklus 6 — Runtime-Lifecycle (2026-05-03)

Migrationspfad-Schritt 7: Task-System an Node-Runtime anbinden.

Detail-Skizze + Slice-Plan: `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.

Status:
- Option B (Engine als pure Domain-Service, analog H6) angenommen.
- Q1–Q6 entschieden (siehe Skizze §6).
- Slice 0 (Inventur) ✓ done.
- **Slice 1** (Engine-Extraktion): gated auf lokale Postgres-DB (fuer Verhaltens-Paritaets-Beweis via Bestandstests).
- **Slice 2** (Lifecycle-Service mit Connection-Scope): gated auf Slice 1.

---

## Zyklus-Historie

Detail-Reports zu Zyklus 1–5 sind aus dieser Datei entfernt worden — sie sind in `git log` (Commits 2026-04-23 bis 2026-05-03) erhalten und in `KauthWorkflow/Stand/Code-Review-Status.md` als Tabelle zusammengefasst.

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1 | 2026-04-23 | Code-Review + Hardening (C1–C4, H1–H7, L1/L3/L5/L6) |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, SQL-Task-Filter, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: Hook-Tests, Repo-Splits, Hook-Zerlegung |
| 4 | 2026-05-02 | Naming + Haertungen: LegacyProcessTypeKey, effectiveResponsibilityIds, Error-Boundaries |
| 5 | 2026-05-02..03 | Legacy-Abbau (LA1–LA5): LegacyWorkflowStatus, setup-Node, definition_key, HasLegacyRolePermission, Specs am Node |
| 6 | 2026-05-03 (aktiv) | Runtime-Lifecycle (siehe oben) |

---

## Verwandte Dokumente

- `TODO.md` — aktuelle Priorisierung + Aufgabenstatus
- `FRONTEND_TODO.md` — Frontend-spezifischer Backlog
- `KauthWorkflow/Architektur/Migrationspfad.md` — Gesamtbild der Migration
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md` — Aktive Architekturarbeit
- `KauthWorkflow/Stand/Code-Review-Status.md` — Vault-Spiegel dieses Status
