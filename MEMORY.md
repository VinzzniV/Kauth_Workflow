# MEMORY.md

## Purpose

This file is the short-lived working memory for the project.
Use it for things that are useful in the next session but are not stable enough for `PROJECT_CONTEXT.md` or `DECISIONS.md`.

Use this file for:
- current focus
- latest findings
- active risks or traps
- next sensible steps
- temporary implementation notes worth keeping across sessions

Do not use this file for:
- stable architecture truth
- long-term decisions
- full setup instructions
- the complete backlog

---

## Current Focus

- P2.1 ist abgeschlossen; naechster sinnvoller Fokus ist P2.2 oder P2.3.

## Latest Findings

- Graph-, Directory-Sync- und Mail-Secrets laufen jetzt runtime-only ueber `ENTRA_*` plus optional `GRAPH_CLIENT_SECRET`.
- Die Graph-Admin-Ansicht ist read-only; der Patch-Endpoint fuer Graph-Konfiguration wurde entfernt.
- `npm run lint` im Frontend laeuft wieder gruen; die verbliebenen State-Synchronisationsfehler wurden auf abgeleitete Defaults, eventgetriebene Updates und getrennte Hook-/Provider-Module umgestellt.
- `npm test` im Frontend laeuft wieder gruen; die Testsuite nutzt jetzt die echten Service-Importgrenzen und aktualisierte UI-/A11y-Vertraege.
- Swagger wird nur noch ausserhalb von Production aktiviert; `SWAGGER_ENABLED=true` ist in Production jetzt ein harter Startup-Fehler.
- Die minimale GitHub-Actions-Pipeline validiert jetzt Backend Build/Tests sowie Frontend Lint/Tests/Build.
- Der volle `api/API.Tests`-Lauf ist wieder gruen; der `42P08`-Cluster in den Admin-Config-Integrations-Tests war ein Nullable-Parameter-Typfehler in zwei Repository-Queries.
- Health-Probes sind jetzt getrennt: `/health/live` fuer Liveness, `/health/ready` fuer DB-Readiness und `/health` als Deep-Health ohne Restart-Semantik bei externer Entra-Stoerung.
- `compose.prod.yml` nutzt fuer API- und Proxy-Healthchecks jetzt `/health/ready` statt Deep-Health.
- Production-Startup erzwingt jetzt `https://` fuer `PUBLIC_BASE_URL` und CORS-Origins.
- Der deployte Web-Container nutzt keine alten `demo`-Defaults mehr; `app-config.js` akzeptiert nur `dev-sim` oder `entra`, und `entra` braucht explizite Runtime-Werte inklusive Redirect-URI.
- `scripts/Prepare-Handoff.ps1` erzeugt jetzt ein bereinigtes Handoff-ZIP und validiert nach dem Packen, dass keine `.git`-, `node_modules`-, `dist`-, `bin/obj`-, `TestResults`-, Log- oder lokale Env-Artefakte enthalten sind.
- P2.1 Phase 1 hat die groessten Frontend-State-Hotspots reduziert:
  - `useAdminNotificationEmailConfiguration` nutzt einen gemeinsamen Draft-Container statt 8 Einzelstates
  - `useAdminUserManagement` buendelt Edit-/New-Drafts und resynchronisiert nur noch bei echten User-/Group-Refreshes
  - `useRequirementEditor` resettet Drafts nur noch bei Workflow-UID-Wechseln
  - `WorkflowLinksPanel` haengt jetzt an `useRelatedWorkflows(uid)` in der React-Query-Schicht
- `useWorkflowCreation` nutzt jetzt Query-State fuer `completedOnboardings` und `workflowConfig`; die Auswahl der Zielperson wird als Snapshot gehalten und ohne Sync-Effect gegen aktuelle Query-Daten abgeleitet.
- `useAdminTaskTemplateManagement` fasst die verstreuten Operation-Flags in einem gemeinsamen `OperationState` zusammen, ohne die Consumer-Schnittstelle von `AdminTaskTemplateSection` zu aendern.
- `useAdminAnswerDefinitionManagement` und `useAdminRoleAnswerDefaults` haben jetzt ebenfalls konsolidierte Operation-States statt einzelner Loading-/Saving-Flags.
- `useAdminOrganizationManagement` wurde bewusst nicht umgebaut; die bestehenden Draft-Sync-Effects wurden als legitimes Pattern fuer editierbare Server-Drafts bewertet.
- Die Frontend-Testumgebung setzt in `web/tests/setup.ts` jetzt explizit `authMode: "dev-sim"`, damit lokale `.env`-Werte die Suite nicht verfälschen.
- Frontend-Checks sind nach P2.1 Phase 1 gruen: `npm run lint`, `npm test`, `npm run build`.
- Frontend-Checks sind nach dem naechsten P2.1-Block weiter gruen: `npm run lint`, `npm test`, `npm run build`.
- P2.1 ist insgesamt gruen verifiziert: `npm run lint`, `npm test`, `npm run build`.

## Active Risks / Watchouts

- Eine lokal laufende Debug-API sperrt `api/API/bin/Debug/net8.0/API.dll` und stoert Debug-Testlaeufe.
- Lokale Release- oder Debug-Testlaeufe koennen durch parallel laufendes `dotnet run` bzw. `dotnet watch` an gesperrten Build-Artefakten scheitern.

## Next Steps

- P2.2 angehen: Test-Helfer zentralisieren und Barrel-/Importgrenzen in den Tests bereinigen.
- Danach P2.3: Manager-/Supervisor-Dashboard serverseitig härten.

## Cleanup Candidates

- Remove or move entries once they become stable, obsolete or properly documented elsewhere.
