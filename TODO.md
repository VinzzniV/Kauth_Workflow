# TODO - Produktreife

## Zweck

Aktiver Produkt- und Umsetzungsplan. Nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie und erledigte Slices liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung").

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Stand 2026-05-12

Z21-S1..S3 + S5..S10 + S6b sind 2026-05-12 abgeschlossen. Verifizierter Ist-Stand (per `scripts/verify-prod-ready.sh`):
- API Release-Build: ✅ gruen
- API-Test-Build: ✅ 7 Errors (Baseline aus frueheren Refactorings; keine neuen seit 2026-05-12)
- FE-Build: ✅ gruen
- FE-Tests: ✅ 323 passed
- `start-vm.sh`-Syntax: ✅ gruen

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8), Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials.

## Aktive TODOs

| ID | Aufgabe | Prio | Status | Nutzen |
| --- | --- | --- | --- | --- |
| Z21-S4 | Realen Automation-Pfad aus der Hybrid-AD-Entscheidung ableiten: Handler-Vertrag, Ausfuehrungsort, Credentials, Secret-Rotation, Retry/Idempotenz, Audit und Rollback-Grenzen. | HIGH | offen — blockiert bis Migrationspfad-Etappe 9a Schritt 1 entschieden (Worker-Deployment, Transport, AD-Schreibmechanik, Auth, Audit-Rueckkanal) | Macht aus vorbereiteter Automation einen implementierbaren Produktionspfad. |

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
