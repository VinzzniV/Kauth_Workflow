# Code Review Status

#stand #review

Aktueller Stand der Code Review vom 2026-04-23. Was erledigt ist, was noch aussteht.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

---

## Gesamtbewertung (April 2026)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | B- | Gute Service-Trennung, aber monolithisches Repository |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B | Durchdacht, Kantenfälle (gelöschte User, leere Zuständigkeiten) jetzt behandelt |
| Rotation-Feature | B- | Funktioniert, Kernlogik im falschen Layer |
| Frontend-Architektur | C+ | Seiten zu groß, fehlende Error-Boundaries |
| Testbarkeit | D | Monolithisches Repository verhindert Unit-Tests |
| Skalierbarkeit | C | In-Memory-Filter strukturell problematisch (Rotation behoben) |
| Sicherheit | B | Auth konsistent; `/client/log-events` ohne Rate-Limiting |

---

## Kritische Punkte

| ID | Problem | Status |
|----|---------|--------|
| C1 | Transaktionsgrenzen Rotation-Regenerierung | ✓ erledigt (COD-1, 2026-04-23) |
| C2 | Template ohne Zuständigkeit → Aufgaben unsichtbar | ✓ erledigt (2026-04-24) — Pflichtfeld + SQL-Spaltenbug gefixt |
| C3 | Entra-gelöschte User nicht deaktiviert | ✓ erledigt (CLA-2, 2026-04-23) |
| C4 | Task-Filter in-memory statt SQL (Rotation) | ✓ erledigt (CLA-1, 2026-04-23) |

---

## Hohe Priorität

| ID | Problem | Status |
|----|---------|--------|
| H1 | Monolithisches Repository (23.758 Zeilen) | ✓ teilweise erledigt (CLA-3, 2026-04-23) — Rotation-Slice herausgeschnitten |
| H2 | Stations-Überschneidungsprüfung fehlte | ✓ erledigt (COD-2, 2026-04-23) |
| H3 | automation_key nicht validiert beim Speichern | ✓ erledigt (COD-3, 2026-04-23) |
| H4 | Notification-Platzhalter nicht validiert | ✓ erledigt (COD-4, 2026-04-23) |
| H5 | Keine DAG-Vollständigkeitsprüfung vor Publish | ✓ erledigt (CLA-4, 2026-04-24) |
| H6 | RotationTaskRegenerationEngine noch im Repository | **offen** — Folgearbeit zu CLA-3 |
| H7 | Positionale Reader-Indizes im SQL (GetString(0)) | ✓ erledigt (COD-6, Rotation-Slice) |

---

## Niedrige Priorität (offen)

| ID | Problem |
|----|---------|
| L1 | Retry-Delays hardcodiert |
| L2 | Keine Datenbereinigung für Drafts/abgebrochene Pläne |
| L3 | `/client/log-events` ohne Rate-Limiting |
| L6 | AUTH_MODE=dev-sim kein Guard gegen Prod-Aktivierung |

---

## Offene Folgearbeit

| ID | Aufgabe | Prio |
|----|---------|------|
| H6 | `RotationTaskRegenerationEngine` als Domain-Service extrahieren (Unit-Tests ohne DB) | HIGH |

---

## Bekanntes Dauerproblem: Test-Fixture

`dotnet test` schlägt in der DB-Fixture fehl mit:
```
Duplicate-Key auf uq_workflow_answer_visibility_rules
```
→ Test-Isolation-Problem in der DB-Fixture, unabhängig von den Codex-Änderungen.

---

## Verwandte Notizen

- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
