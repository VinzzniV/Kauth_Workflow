# TODO - Produktreife

## Zweck

Aktiver Produkt- und Umsetzungsplan. Nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie und erledigte Slices liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung" + „Migrationspfad-Etappe 9a" + „Admin-Gated-Automation Slices 1-7"). Migrationspfad-Etappe 9a Schritt 1..8 (2026-05-12..13), Admin-Gated-Automation Slices 1..7 (2026-05-13..18) und AGA-N2 echtes Entra-Re-Auth (2026-05-19) sind komplett abgeschlossen.

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Stand 2026-05-19

Z21-S1..S3 + S5..S10 + S6b (2026-05-12), Migrationspfad-Etappe 9a Schritt 1..8 (2026-05-12..13), Admin-Gated-Automation Slices 1..7 (2026-05-13..18) und AGA-N2 (2026-05-19) alle abgeschlossen. Details in `CODE_REVIEW_ARCHIVE.md`.

Verifizierter Ist-Stand (AGA-N2, 2026-05-19):
- API Release-Build: ✅ gruen
- FE-Typecheck (`tsc --noEmit`): ✅ gruen
- FE-Tests Automation-Re-Auth-Mutation: ✅ 6/6 passed
- FE-Tests Gesamt: 325/329 (4 pre-existing `PersonaSwitcher`-Failures, vor/nach AGA-N2 unveraendert)
- FE-Lint: ✅ 0 Errors / 5 Warnings (baseline)
- API-Tests `AutomationReauthFreshnessGate`: ✅ 8/8 passed; alle Automation-Tests: ✅ 102/102 passed
- API-Tests Gesamt: pre-existing Infrastruktur-Fail in `WorkflowEndpointsTests` (Body-Inferred bei `RequestDelegateFactory`, unabhaengig von AGA-N2; identische Failures auch ohne diesen Slice)

Externe Voraussetzung fuer Prod (Nutzer-Aufgabe): in der API-App-Registration im Entra-Manifest `auth_time` als optional claim fuer Access-Tokens konfigurieren. Ohne diesen Claim liefert das Backend strukturiert `422 reauth_unconfigured`; die UI zeigt einen klaren Admin-Hinweis statt unklarem Soft-Erfolg.

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8) + Approval-Dialog mit echtem Entra-Popup (`prompt=login`), Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials, Worker-Host-Build (net8.0-windows) auf Windows-Maschine, E2E `CreateAdUserLdaps` gegen Test-DC + Vault-Roundtrip, E2E `SendWelcomeMailGraph` mit echtem Passwort im Mail-Body.

## Aktive TODOs

_keine — Change-Handler-Slice (MoveAdUserOuLdaps + UpdateAdUserAttributesLdaps) abgeschlossen._

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |
| AGA-N1 | `CreateErpEmployee` (InforLN) als eigener Backend-Architektur-Slice. | HIGH | stakeholder-blockiert | Wartet auf Stakeholder-Entscheidung; eigener Plan-Mode noetig. |
| AGA-N5 | `RenameAdUserLdaps` (UPN + sAMAccountName + CN + displayName) als eigener Slice. | HIGH | offen | UPN-Wechsel hat Folgewirkungen in Entra/Exchange (alter UPN als Alias?). Opus + Zwingend Plan-Mode vor Code. |
| AGA-N6 | Builder-UI fuer `automation_output`-Bedingungen (Source-Dropdown + Property-Filter). | LOW | offen | Heute nur per JSON-API / Dev-Seed konfigurierbar. |
| AGA-N7 | Vault-Cleanup-Sweeper; Key-Rotation; Connection-Pooling LDAPS. (Alle Offboarding-Handler + Change-Handler MoveAdUserOuLdaps + UpdateAdUserAttributesLdaps sind als Handler + Seed/Bootstrap implementiert.) | LOW | offen | Operative Resthebel ohne aktuellen Blocker. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
