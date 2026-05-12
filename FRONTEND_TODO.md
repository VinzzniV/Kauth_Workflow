# FRONTEND TODO

## Zweck

Aktiver Frontend- und UX-Backlog fuer `kauth_workflow`. Diese Datei enthaelt nur offene Arbeiten, die Endbenutzer-Verstaendnis, Bedienbarkeit, fachliche Sicherheit oder Produktionsreife verbessern.

Historie, erledigte Review-Zyklen und Detailnotizen gehoeren in `CODE_REVIEW.md`, `MEMORY.md`, `CODEX_SYNC.md` oder die Archivdateien.

## Regeln fuer Frontend-Arbeiten

1. Vor UI-Arbeiten `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `TODO.md`, `CODE_REVIEW.md` und diese Datei lesen.
2. UI-Aenderungen sollen fachliche Klarheit verbessern, nicht nur kosmetisch wirken.
3. Technische Begriffe nur zeigen, wenn sie fuer Admins wirklich notwendig sind.
4. Keine langen Erklaertexte als Ersatz fuer klare Struktur, Labels und Statuszustaende.
5. Nach Umsetzung Build und Tests ausfuehren, mindestens `npm run build` und `npm test`.

## Naechster sinnvoller Schritt

Frontend-seitig zuerst `FE-Z21-1` umsetzen, sobald klar ist, ob die Simulationserkennung rein aus `handler_type` erfolgt oder ueber ein explizites API-Feld kommt.

Wenn parallel Backend gearbeitet wird, zuerst `TODO.md` Punkt `Z21-S1` klaeren. Danach kann das Frontend die Simulation konsistent in Builder, Katalog und Admin-Sicht anzeigen.

## Offene Items

| ID | Aufgabe | Prio | Reasoning | Modell | Abhaengigkeit |
| --- | --- | --- | --- | --- | --- |
| FE-Z21-1 | Simulierte Automation sichtbar machen: Action-Katalog, Builder-Aktion, Workflow-Zusammenfassung und Admin-Status muessen klar anzeigen, wenn ein Handler nur simuliert. | HIGH | medium | Sonnet | Z21-S1 |
| FE-Z21-2 | Storno-UI fuer laufende Workflows bauen: Aktion, Dialog, klare Folgen, Lade-/Fehlerzustand und Refresh der Listen/Details. | HIGH | medium | Sonnet | Z21-S2 |
| FE-Z21-3 | Builder-Mapping und Bedingungen fachlicher formulieren: rohe Property-Keys, Answer-Keys und Handler-Parameter in normale Labels uebersetzen; technische Details nur im Expertenkontext zeigen. | HIGH | high | Opus | Z21-S6 |
| FE-Z21-4 | Admin-/Runtime-Sicht fuer Automation und Mailversand erweitern: failed/blocked Jobs, letzte Fehler, Retry-Status und Konfigurationsprobleme sichtbar machen. | HIGH | high | Opus | Z21-S5 |
| FE-Z21-5 | Workflow-Detailseite strukturieren: Status & Aufgaben, Anforderungen, Benachrichtigungen & Audit, Links & Verwaltung klar trennen. | MEDIUM | medium | Sonnet | Z21-S8 |
| FE-Z21-6 | Mitarbeiterbereich klarer trennen: Mitarbeiterakten, Directory-Importe und nicht verknuepfte Identitaeten in Navigation, Labels und Aktionen eindeutiger darstellen. | MEDIUM | medium | Sonnet | Z21-S9 |
| FE-Z21-7 | Durchlaufplanung absichern: aktive leere Plaene im UI verhindern oder sehr deutlich als wirkungslos kennzeichnen. | MEDIUM | medium | Sonnet | Z21-S7 |
| FE-Z21-8 | Navigationsbegriffe pruefen: `Laufende Vorgänge`, `Meine Aufgaben`, `Wechsel & Aufgaben` und Persona-Wechsel so formulieren, dass Rolle und Zweck sofort klar sind. | MEDIUM | low | Sonnet | Z21-S8 |
| FE-Z21-9 | Persona-Wechsler entschärfen: klarstellen, dass die Ansicht gewechselt wird und keine echte Berechtigungsänderung stattfindet. | LOW | low | Sonnet | keine |
| FE-Z21-10 | Frontend-Chunks aufteilen, falls reale Ladezeiten problematisch sind. | LOW | low | Sonnet | Z21-N1 |

## Bewusst nicht aktiv

- Tailwind-/Konfigurationswarnungen ohne sichtbaren Produktfehler bleiben nachrangig.
- Reine Kosmetik ohne messbaren Bediennutzen wird nicht in den aktiven Backlog aufgenommen.
- Alte erledigte FE-Zyklen wurden aus dieser Datei entfernt, damit die offenen Aufgaben klar priorisiert bleiben.

## Abschlusskriterien fuer UI-Punkte

Ein Frontend-Punkt gilt erst als erledigt, wenn:
- der betroffene Endbenutzerzustand im UI klar sichtbar ist,
- Lade-, Fehler- und Leerzustaende sinnvoll behandelt sind,
- keine neue fachliche Verwechslung entsteht,
- `npm run build` erfolgreich war,
- `npm test` erfolgreich war oder eine nicht ausgefuehrte Verifikation explizit dokumentiert ist.
