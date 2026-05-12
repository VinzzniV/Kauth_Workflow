# TODO - Produktreife

## Zweck

Diese Datei ist der aktive Produkt- und Umsetzungsplan. Sie enthält nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie, erledigte Review-Zyklen und Detailnotizen gehoeren nicht hierher, sondern in `CODE_REVIEW.md`, `MEMORY.md`, `CODEX_SYNC.md` oder die Archivdateien.

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Aktueller Zielzustand

Ziel ist ein Stand, der fuer normale Endbenutzer verstaendlich ist und fuer einen kontrollierten produktiven Einsatz verantwortbar vorbereitet ist.

Nicht Ziel dieses Plans:
- allgemeines Refactoring ohne Produktnutzen
- reine Code-Stil-Themen
- kosmetische UI-Experimente ohne Bedienverbesserung
- alte erledigte Review-Punkte

Zuletzt verifiziert:
- Frontend Build: `npm run build` erfolgreich, aber groesse Warnung fuer grosse Chunks.
- Frontend Tests: `npm test` erfolgreich.
- API Build: `dotnet build api/API/API.csproj -c Release` erfolgreich.
- Backend Integrationstests: lokal nicht voll verifizierbar, weil Docker/Testcontainers nicht verfuegbar war.

## Zielplan zum produktionsfaehigen Stand

| Phase | Ziel | Ergebnis |
| --- | --- | --- |
| 1 | Keine falschen Erwartungen an Automation | Simulierte Handler sind ueberall klar sichtbar; niemand kann Dev-Automation mit produktiver AD-Automation verwechseln. |
| 2 | Workflow-Lebenszyklus vervollstaendigen | Laufende Vorgänge koennen kontrolliert storniert werden; Folgeaufgaben und Audit sind konsistent. |
| 3 | Hybrid-AD-Entscheidung treffen | Architekturentscheidung: Entra/Graph-first oder on-prem AD Worker/Bridge. |
| 4 | Reale Automation anschlussfaehig machen | Konkreter technischer Pfad fuer produktive Handler, Secrets, Berechtigungen, Fehlerbehandlung und Betrieb. |
| 5 | Betriebsfaehigkeit herstellen | Admins sehen fehlgeschlagene Automation, blockierte Mailausgaenge und kritische Runtime-Zustaende ohne Logsuche. |
| 6 | Endbenutzer-UX haerten | Builder, Detailseiten, Mitarbeiter, Durchlaufplanung und Navigation sind fachlich klar und fehlertolerant. |
| 7 | Produktionscheck durchfuehren | Build, Tests, Browser-Smoke, Docker-Integrationstests und Betriebsdoku sind reproduzierbar gruen. |

## Aktive TODOs

Z21-S1 (Simulation kennzeichnen), Z21-S2 (Storno) und Z21-S3 (Hybrid-AD-Entscheidung) sind 2026-05-12 abgeschlossen (Detail in `CODE_REVIEW_ARCHIVE.md`, Abschnitt „Zyklus 21 (Done-Findings)"). Z21-S6 ist im UX-Teil 2026-05-12 done; Rest steckt in `Z21-S6b`. Aktive offene Punkte:

| ID | Aufgabe | Prio | Status | Nutzen |
| --- | --- | --- | --- | --- |
| Z21-S4 | Realen Automation-Pfad aus Z21-S3 ableiten: Handler-Vertrag, Ausfuehrungsort, Credentials, Secret-Rotation, Retry/Idempotenz, Audit und Rollback-Grenzen. | HIGH | offen — blockiert bis Migrationspfad-Etappe 9a Schritt 1 entschieden | Macht aus vorbereiteter Automation einen implementierbaren Produktionspfad. |
| Z21-S5 | Runtime-/Admin-Sicht fuer fehlgeschlagene Automation und Mailversand: failed/blocked Jobs, letzte Fehler, Retry-Status und Konfigurationsprobleme sichtbar machen. | HIGH | **done 2026-05-12**: `AdminRuntimeHealthDto.automationFailures` + `notificationFailures` (24h-Fenster, COUNT + 5 juengste mit Label + gekuerztem Error). Im `DashboardAdminRuntimeHealthBlock` als Stat-Tiles + collapsible Liste. `overallSeverity` aggregiert Failures mit. | Betrieb erkennt Ausfaelle im Produkt selbst, ohne DB/Logs durchsuchen zu muessen. |
| Z21-S6b | Decision-Conditions: AND/OR-Mehrbedingungen am Edge — `WorkflowRuntimeEngine.ParseDecisionCondition` + Schema + FE-Editor. Rueckwaertskompat fuer Single-Condition. | MEDIUM | offen | Heute akzeptiert die Runtime nur eine einzelne Bedingung pro Verbindung; AND/OR-Faelle muessen heute ueber mehrere Decision-Schritte modelliert werden. |
| Z21-S7 | Durchlaufplanung absichern: aktive Plaene ohne Stationen verhindern oder eindeutig als nicht produktiv/leer markieren. | MEDIUM | offen | Verhindert Planungszustaende, die fuer Nutzer aktiv wirken, aber keine Aufgaben erzeugen. |
| Z21-S8 | Workflow-Detail und Listen ergonomischer machen: Status, Aufgaben, Anforderungen, Benachrichtigungen, Audit und Verwaltungsaktionen klarer trennen. | MEDIUM | offen | Laufende Vorgaenge werden fuer Bearbeiter und Admins schneller nachvollziehbar. |
| Z21-S9 | Mitarbeiterbereich fachlich trennen: Mitarbeiterakten, importierte Directory-Identitaeten und Import-/Link-Aktionen deutlicher unterscheiden. | MEDIUM | offen | Verhindert Missverstaendnisse zwischen HR-Personen, Entra-Identitaeten und noch nicht verknuepften Accounts. |
| Z21-S10 | Produktions-Verifikationslauf definieren und ausfuehren: Docker/Testcontainers, API Build, Frontend Build, Frontend Tests, Browser-Smoke und Graph-/Mail-Konfigurationscheck. | MEDIUM | offen | Ersetzt lokale Teilverifikation durch einen belastbaren Freigabecheck. |

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance ist relevant, aber weniger kritisch als Automation, Storno und Betriebsfaehigkeit. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop; mobile Optimierung erst nach fachlicher Stabilisierung. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend fuer die fachliche Architekturentscheidung. |

## Bewusst entfernt aus dem aktiven Backlog

- Bereits erledigte Builder-Fixes, Mail-Konfigurations-Fixes und FE-Review-Zyklen wurden aus der aktiven TODO-Liste entfernt.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife wurden nicht uebernommen.
- Alte Review-Historie bleibt in den Review-/Sync-/Archivdateien nachvollziehbar, blockiert aber nicht mehr den aktuellen Plan.
