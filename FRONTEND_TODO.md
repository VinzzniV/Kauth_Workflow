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

## Offene Items

| ID | Aufgabe | Prio | Reasoning | Modell | Abhaengigkeit |
| --- | --- | --- | --- | --- | --- |
| FE-Z21-8 | Navigationsbegriffe pruefen: `Laufende Vorgänge`, `Meine Aufgaben`, `Wechsel & Aufgaben` und Persona-Wechsel so formulieren, dass Rolle und Zweck sofort klar sind. | MEDIUM | low | Sonnet | keine |
| FE-Z21-10 | Frontend-Chunks aufteilen, falls reale Ladezeiten problematisch sind. | LOW | low | Sonnet | ≡ TODO Z21-N1 |

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
