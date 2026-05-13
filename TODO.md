# TODO - Produktreife

## Zweck

Aktiver Produkt- und Umsetzungsplan. Nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie und erledigte Slices liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung"). Etappe 9a Schritt 6 (Temporary-Credentials-Vault, 2026-05-13) ist abgeschlossen; Belege siehe Commits `ee77538`/`1d5328a`/`a422c81` und Doku-Commit dieses Slices.

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Stand 2026-05-13

Z21-S1..S3 + S5..S10 + S6b sind 2026-05-12 abgeschlossen. Migrationspfad-Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 + 6 alle durch. Verifizierter Ist-Stand (per `scripts/verify-prod-ready.sh`):
- API Release-Build: ✅ gruen
- API-Test-Build: ✅ 7 Errors (Baseline aus frueheren Refactorings; keine neuen seit 2026-05-12)
- FE-Build: ✅ gruen
- FE-Tests: ✅ 323 passed
- `start-vm.sh`-Syntax: ✅ gruen
- Worker.Core + Worker.Tests Build: ✅ gruen (deterministisch, Linux)
- Worker Core tests: ✅ 57 passed (48 vor Schritt 6 + 9 VaultKeyLoaderTests)

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8), Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials, Worker-Host-Build (net8.0-windows) auf einer Windows-Maschine, E2E `CreateAdUserLdaps` gegen Test-DC + Vault-Roundtrip, E2E `SendWelcomeMailGraph` mit Vault-Mapping und echtem Passwort im Mail-Body.

## Aktive TODOs

| ID | Aufgabe | Prio | Status | Nutzen |
| --- | --- | --- | --- | --- |
| Z21-S4 | Realen Automation-Pfad aus der Hybrid-AD-Entscheidung ableiten. | HIGH | Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 + 6 ✓ 2026-05-13. Schritt 6 hat den Temporary-Credentials-Vault produktiv gemacht: `temporary_credentials`-Tabelle (pgcrypto symmetric), atomare Vault-Tx im `PostgresWorkerJobStore` (Vault-Insert + Attempt + Job in einer Tx; ON CONFLICT-Recovery), `CreateAdUserLdapsHandler` liefert `PendingVaultWrite` und schreibt kein Plain-Passwort mehr ins Output, `created_ad_user`-Whitelist um `credentialVaultId` erweitert (UUID, kein Geheimnis; Null-Pfad durchgereicht), `SendWelcomeMailGraphHandler` decryptet zur Run-time via `ITemporaryCredentialRepository.ReadAdInitialPasswordByVaultIdAsync`, `{{temporary_password}}`-Placeholder im Catalog-Template. Vault-Key auf Worker via DPAPI-Pfad (`vault.config.dpapi` analog `db.config.dpapi`); API via `KAUTH_VAULT_KEY` Env-Var. **Naechste Code-Slices (eigene Plan-Mode-Slices):** `CreateMailbox` (Graph-Mail-Provisioning) und `CreateErpEmployee` (ERP-System-Auswahl mit Stakeholder) jeweils als eigene Backend-Architektur-Slices. Vor Live-Inbetriebnahme: gMSA in der Domaene anlegen + `Install-ADServiceAccount` auf der Worker-VM; Postgres-User `kauth_worker` mit GRANTs (jetzt inkl. INSERT auf `temporary_credentials`) + `kauth_api` mit SELECT+UPDATE auf `temporary_credentials`; `install-db-config.ps1` DPAPI-Lauf; `install-vault-key.ps1` DPAPI-Lauf (gleicher Key auf Linux-API als `KAUTH_VAULT_KEY`-Env-Var); Delegated-Rechte (Create Child + Reset Password + Modify Member auf Groups) auf der Ziel-OU; Graph App-only-Credential. | Macht aus vorbereiteter Automation einen implementierbaren Produktionspfad — End-to-End Onboarding mit echtem Passwort in der Welcome-Mail ist jetzt produktionsreif. |

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
