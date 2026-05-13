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
- **Einziger aktiver offener Slice** in `TODO.md`: **Z21-S4 Realer Automation-Pfad**. Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 ✓ 2026-05-12. Schritt 5 hat drei Vertrags-Bausteine + zwei reale Handler geliefert: (a) enge `created_ad_user`-Mapping-Source mit Property-Whitelist (nur `distinguishedName`) und `nodeKey`-Referenz — Vault-Grenze ist im Code, sensible Felder unreichbar. (b) Worker-Failure-Pfad traegt jetzt Output (`MarkJobFailedAsync` schreibt `output_json` im Failure). (c) Linux-Handler-Vertrag strukturiert (`WorkflowAutomationHandlerResult` mit IsSuccess/ErrorMessage/FailureKind; Service discriminiert Result-Failure vs Exception). (d) `AssignGroupsLdaps` als zweiter LDAPS-Handler. (e) `SendWelcomeMailGraph` als erster echter Linux-side-Handler via Graph App-only. Handler-Registry von Singleton auf Scoped umgestellt. Naechster Code-Slice: **Etappe 9a Schritt 6** — Temporary-Credentials-Vault als eigene Architektur-Entscheidung; CreateMailbox/CreateErpEmployee als eigene Backend-Slices. Eigener Plan-Mode-Slice vor Start.
- **Schreibregel (verbindlich):** jedes Review-Finding und jeder Slice muss zusaetzlich zur Technik kurz erklaeren, was es praktisch bedeutet, warum es sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird. Verankert in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

## Active Risks / Watchouts

- **DB-Drift-Pfad:** schemaaendernde Slices erzeugen weiterhin manuelle SQL-Helfer in `db/manual/`. Seit Z19-S3 (2026-05-11) sichert `db/manual/manifest.json` + `SchemaParityTests.cs` ab, dass jede `*.sql`-Datei dort ihren Soll-Endstand in `db/01_schema.sql` widerspiegelt; jede neue Migration braucht zusaetzlich einen Manifest-Eintrag mit `expect_in_schema` (+ optional `forbid_in_schema`), sonst wird der Test rot.
- **CancellationToken-Resthebel nach Z19-S4:** tiefe statische Repo-Helfer (`PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal`, `CreateWorkflowNodeInstance`, `InsertWorkflowRuntimeEvent`, `LoadPublishedWorkflowDefinitionVersion`, einige `PostgresRepositorySharedHelpers.*`) tragen den Token nicht. Praktisch: Connection und Transaktion brechen am Commit-Punkt, statische Sub-Queries laufen bis zur naechsten `CommandText`-Grenze weiter. Nicht jetzt — eigener Folgeslice nur bei konkretem Lock-/Latenz-Befund.
- **Person ≠ Identity:** `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker — nie vermischen (in `KauthWorkflow/Domäne/Identity.md` verankert).
- `WorkflowLifecycleService` bleibt Commit-Grenze fuer Create/Form/Approval/Task — nicht aufweichen.
- `start.ps1` kann die Dev-DB jetzt auf einen Ausweichport starten, wenn `26432` unter Windows durch einen Exclusion-Range blockiert ist. DB-gebundene Tests nutzen weiter standardmaessig `127.0.0.1:26432`; fuer abweichende Ports `ONBOARDING_TEST_CONNECTION_STRING` setzen.
- P2-Cursor (Z11-F2) opaque — FE nie zerlegen.
