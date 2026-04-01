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

- P0.3 bis P0.7 sind umgesetzt; naechster sinnvoller Fokus sind P1.1 oder P1.2.

## Latest Findings

- Graph-, Directory-Sync- und Mail-Secrets laufen jetzt runtime-only ueber `ENTRA_*` plus optional `GRAPH_CLIENT_SECRET`.
- Die Graph-Admin-Ansicht ist read-only; der Patch-Endpoint fuer Graph-Konfiguration wurde entfernt.
- `npm run lint` im Frontend laeuft wieder gruen; die verbliebenen State-Synchronisationsfehler wurden auf abgeleitete Defaults, eventgetriebene Updates und getrennte Hook-/Provider-Module umgestellt.
- `npm test` im Frontend laeuft wieder gruen; die Testsuite nutzt jetzt die echten Service-Importgrenzen und aktualisierte UI-/A11y-Vertraege.
- Swagger wird nur noch ausserhalb von Production aktiviert; `SWAGGER_ENABLED=true` ist in Production jetzt ein harter Startup-Fehler.
- Die minimale GitHub-Actions-Pipeline validiert jetzt Backend Build/Tests sowie Frontend Lint/Tests/Build.
- Der volle `api/API.Tests`-Lauf ist wieder gruen; der `42P08`-Cluster in den Admin-Config-Integrations-Tests war ein Nullable-Parameter-Typfehler in zwei Repository-Queries.

## Active Risks / Watchouts

- Eine lokal laufende Debug-API sperrt `api/API/bin/Debug/net8.0/API.dll` und stoert Debug-Testlaeufe.
- Das Backend-Testsuite ist insgesamt noch nicht vollstaendig gruen; fuer P0.3 sollten gezielte oder Release-basierte Testlaeufe genutzt werden, bis der Admin-Config-Cluster separat behoben ist.

## Next Steps

- P1.1 Health-Checks sauber in Liveness / Readiness / Deep Health trennen.
- P1.2 CORS-, Auth- und Redirect-Konfiguration pro Umgebung weiter haerten.

## Cleanup Candidates

- Remove or move entries once they become stable, obsolete or properly documented elsewhere.
