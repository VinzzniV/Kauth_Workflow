# TODO - Produktreife

## Zweck

Aktiver Produkt- und Umsetzungsplan. Nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie und erledigte Slices liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung" + „Migrationspfad-Etappe 9a" + „Admin-Gated-Automation Slices 1-6"). Migrationspfad-Etappe 9a Schritt 1..8 (2026-05-12..13) und Admin-Gated-Automation Slices 1..6 (2026-05-13..15) sind komplett abgeschlossen.

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Stand 2026-05-15

Z21-S1..S3 + S5..S10 + S6b (2026-05-12), Migrationspfad-Etappe 9a Schritt 1..8 (2026-05-12..13) und Admin-Gated-Automation Slices 1..6 (2026-05-13..15) alle abgeschlossen. Details in `CODE_REVIEW_ARCHIVE.md`.

Verifizierter Ist-Stand (per `scripts/verify-prod-ready.sh` und FE-Build/Vitest am 2026-05-15):
- API Release-Build: ✅ gruen
- API-Test-Build: ✅ 7 Errors (Baseline aus frueheren Refactorings; unveraendert seit 2026-05-12)
- FE-Build: ✅ gruen
- FE-Tests: ✅ 323 passed
- FE-Lint: ✅ Baseline (7 errors / 7 warnings; unveraendert seit Slice 5)
- `start-vm.sh`-Syntax: ✅ gruen
- Worker.Core + Worker.Tests Build: ✅ gruen
- Worker Core tests: ✅ 57 passed

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8) + Approval-Dialog, Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials, Worker-Host-Build (net8.0-windows) auf Windows-Maschine, E2E `CreateAdUserLdaps` gegen Test-DC + Vault-Roundtrip, E2E `SendWelcomeMailGraph` mit echtem Passwort im Mail-Body.

## Aktive TODOs

Kein aktiver, code-arbeitsfaehiger Slice. Die produktive Onboarding-Pipeline und der Admin-Approval-Flow sind nutzbar; die Resthebel sind alle entweder stakeholder-blockiert (`CreateErpEmployee`) oder bewusst out-of-scope (siehe „Nachgelagert").

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |
| AGA-N1 | `CreateErpEmployee` (InforLN) als eigener Backend-Architektur-Slice. | HIGH | stakeholder-blockiert | Wartet auf Stakeholder-Entscheidung; eigener Plan-Mode noetig. |
| AGA-N2 | Echtes Entra-Re-Auth (MSAL `prompt: 'login'` + `auth_time`-Check) statt heutiger Soft-Bestaetigung. | MED | offen | Sicherheits-Slice; Soft-Bestaetigung ist bewusste Slice-5-Grenze. |
| AGA-N3 | Live-Log mit per-Action-Granularitaet im Approval-Dialog. | MED | offen | Heute kennt der Dialog nur succeeded/background; per-Action-Failure-Detection braucht neuen Read-Pfad. |
| AGA-N4 | Post-Execution 360°-Karte-Aggregator unter `/people/:personId`. | MED | offen | Eigene Read-only-Sicht, kein Architekturrisiko; baut auf bestehenden `automation_job_attempts` + Vault auf. |
| AGA-N5 | Referenzuser-Mapping-Source. | LOW | offen | Eigener Slice nach vorhandenem Muster; Whitelist-Entscheidung in Plan-Mode. |
| AGA-N6 | Builder-UI fuer `automation_output`-Bedingungen (Source-Dropdown + Property-Filter). | LOW | offen | Heute nur per JSON-API / Dev-Seed konfigurierbar. |
| AGA-N7 | `RemoveMailboxLicense` fuer User-Deprovisionierung; Vault-Cleanup-Sweeper; Key-Rotation; Connection-Pooling LDAPS. | LOW | offen | Operative Resthebel ohne aktuellen Blocker. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
