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

Z21-S1..S3 + S5..S10 + S6b sind 2026-05-12 abgeschlossen. Migrationspfad-Etappe 9a Schritt 1 + 2 + 3 + 4 ebenfalls am 2026-05-12 durch. Verifizierter Ist-Stand (per `scripts/verify-prod-ready.sh`):
- API Release-Build: ✅ gruen
- API-Test-Build: ✅ 7 Errors (Baseline aus frueheren Refactorings; keine neuen seit 2026-05-12)
- FE-Build: ✅ gruen
- FE-Tests: ✅ 323 passed
- `start-vm.sh`-Syntax: ✅ gruen
- Worker.Core + Worker.Tests Build: ✅ gruen (deterministisch, Linux)
- Worker Core tests: ✅ 38 passed (20 Skeleton + 3 DPAPI-Loader + 5 PasswordGenerator + 10 CreateAdUserLdapsHandler)

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8), Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials, Worker-Host-Build (net8.0-windows) auf einer Windows-Maschine, Skeleton-E2E mit Worker + Sweeper + Stale-Claim-Test, E2E `CreateAdUserLdaps` gegen Test-DC + DPAPI-Setup unter gMSA.

## Aktive TODOs

| ID | Aufgabe | Prio | Status | Nutzen |
| --- | --- | --- | --- | --- |
| Z21-S4 | Realen Automation-Pfad aus der Hybrid-AD-Entscheidung ableiten. | HIGH | Etappe 9a Schritt 1 + 2 + 3 + 4 ✓ 2026-05-12. Schritt 4 hat die zwei in Schritt 3 offen gelassenen Trade-offs geschlossen: failure_kind-Klassifikation (Worker→Linux) und zentraler Stale-Worker-Claim-Sweep als HostedService. **Naechster Code-Slice: Etappe 9a Schritt 5** — Temporary-Credentials-Vault statt Klartext-Passwort, `AssignGroups`-LDAPS-Handler (Wiederverwendung 3c-Layering), `SendWelcomeMail` real (Linux-side via GraphWorkflowEmailNotificationSender). `CreateMailbox`+`CreateErpEmployee` brauchen eigene Backend-Architektur-Slices. Eigener Plan-Mode-Slice vor Start. Vor Live-Inbetriebnahme (nicht vor Slice-Beginn): gMSA in der Domaene anlegen + `Install-ADServiceAccount` auf der Worker-VM; Postgres-User `kauth_worker` mit den GRANTs aus dem Skeleton-Plan; `install-db-config.ps1` DPAPI-Lauf; Delegated-Rechte (Create Child + Reset Password) auf der Ziel-OU. | Macht aus vorbereiteter Automation einen implementierbaren Produktionspfad. |

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
