# MEMORY.md

## Zweck

- kurzfristiges Arbeitsgedaechtnis fuer die naechste Session
- nur aktueller Fokus, aktive Risiken und wenige echte Stolperfallen

## Primaerquelle fuer

- naechsten konkreten Arbeitskontext
- kurzfristige Watchouts

## Nicht verwenden fuer

- Backlog
- Changelog
- zweite Architektur- oder Review-Doku

## Wann aktualisieren

- wenn sich aktiver Zyklus oder naechster Schritt aendert
- wenn eine Stolperfalle nicht mehr aktuell ist

## Verwandte Dateien

- `CODE_REVIEW.md`
- `TODO.md`
- `CODEX_SYNC.md`
- `DOCS_CONTROL.md`

---

## Current Focus

- **Z21 vollstaendig abgearbeitet** am 2026-05-12 bis auf den blockierten HIGH-Slice. Detail-Belege in `CODE_REVIEW_ARCHIVE.md` § „Zyklus 21". Verifikation via `./scripts/verify-prod-ready.sh` (7 Checkpoints: API/FE/start-vm + Worker.Core-Build + Worker-Tests, jetzt 38 statt 20).
- **Einziger aktiver offener Slice** in `TODO.md`: **Z21-S4 Realer Automation-Pfad**. Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 + 6 + 7 ✓ 2026-05-13. Schritt 7 hat **CreateMailboxGraph** geliefert: (a) Per-Action-Retry-Override-Spalten (`max_attempts_override`, `subsequent_retry_delay_seconds_override`) auf `action_definitions` — einzelne Handler koennen ein laengeres Retry-Budget bekommen, ohne dass alle anderen mit-laufen. CreateMailboxGraph-Seed setzt 10/300 = ~41 min Wartezeit-Budget fuer den Entra-Connect-Sync. (b) `IGraphMailboxProvisioner` + `GraphMailboxProvisioner` mit SMTP-Strictness: liefert nur `Provisioned`-Outcome bei echter beobachteter SMTP aus `proxyAddresses`/`mail` — KEIN UPN-Fallback. Vier weitere Outcomes: `UserNotInDirectoryYet` (Entra-Sync-Lag), `MailboxProvisioningInProgress` (SMTP noch nicht publiziert), Permanent/TransientFailure. (c) `CreateMailboxGraphHandler` (Action ID 10) mappt Outcomes auf `failure_kind`; eigene Info-Log-Marker fuer operative Diagnose. (d) `created_ad_user`-Whitelist erweitert um `userPrincipalName`; neue enge `created_mailbox`-Source mit Property-Whitelist nur `primarySmtpAddress`. Pflicht-Permissions: `User.Read.All` (GET) + `LicenseAssignment.ReadWrite.All` (assignLicense) als least-privileged Application-Permissions. Naechster Code-Slice: **CreateErpEmployee** als eigener Backend-Architektur-Slice (ERP-System-Auswahl mit Stakeholder).
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **DB-Drift-Pfad:** schemaaendernde Slices erzeugen weiterhin manuelle SQL-Helfer in `db/manual/`. Seit Z19-S3 (2026-05-11) sichert `db/manual/manifest.json` + `SchemaParityTests.cs` ab, dass jede `*.sql`-Datei dort ihren Soll-Endstand in `db/01_schema.sql` widerspiegelt; jede neue Migration braucht zusaetzlich einen Manifest-Eintrag mit `expect_in_schema` (+ optional `forbid_in_schema`), sonst wird der Test rot.
- **CancellationToken-Resthebel nach Z19-S4:** tiefe statische Repo-Helfer (`PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal`, `CreateWorkflowNodeInstance`, `InsertWorkflowRuntimeEvent`, `LoadPublishedWorkflowDefinitionVersion`, einige `PostgresRepositorySharedHelpers.*`) tragen den Token nicht. Praktisch: Connection und Transaktion brechen am Commit-Punkt, statische Sub-Queries laufen bis zur naechsten `CommandText`-Grenze weiter. Nicht jetzt — eigener Folgeslice nur bei konkretem Lock-/Latenz-Befund.
- **Person ≠ Identity:** `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- `start.ps1` kann die Dev-DB jetzt auf einen Ausweichport starten, wenn `26432` unter Windows durch einen Exclusion-Range blockiert ist. DB-gebundene Tests nutzen weiter standardmaessig `127.0.0.1:26432`; fuer abweichende Ports `ONBOARDING_TEST_CONNECTION_STRING` setzen.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
