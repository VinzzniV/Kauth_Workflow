# TODO.md

## Zweck

- aktive Arbeitsplanung fuer Review-Nacharbeit
- nur offene oder unmittelbar relevante Arbeit

## Primaerquelle fuer

- naechsten Arbeitsschritt
- Reihenfolge der offenen Slices

## Nicht verwenden fuer

- lange Historie abgeschlossener Slices
- Architekturargumentation
- Session-Notizen

## Wann aktualisieren

- wenn ein neuer Zyklus startet
- wenn sich Priorisierung aendert
- wenn eine Aufgabe abgeschlossen oder deferred wird
- wenn ein aktiver Zyklus beendet wird; abgeschlossene Detailbloecke danach aus dieser Datei entfernen oder ins Archiv verweisen

## Verwandte Dateien

- `CODE_REVIEW.md`
- `MEMORY.md`
- `CODEX_SYNC.md`
- `FRONTEND_TODO.md`

---

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen.

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Schreibregel: jedes neue Review-Finding / jeder Slice muss neben dem technischen Befund kurz erklaeren, was er praktisch bedeutet, warum es sich lohnt, ihn anzugehen, und was dadurch besser, sicherer, schneller oder wartbarer wird. Detail in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

---

## Aktueller Review-Status

Kein aktiver Review-Zyklus offen.

Z20 ist seit 2026-05-11 vollstaendig abgeschlossen. Die nach Z10/Z11 zurueckgestellten Read-/Listen-Vertraege fuer Admin/Directory/Runtime sind umgesetzt; P3-Lookups sowie die verbleibenden P1-/P2-Huellen sind live. Z19 ist ebenfalls abgeschlossen. Detailhistorie und Abschlussnotizen stehen in `CODE_REVIEW.md` und `CODE_REVIEW_ARCHIVE.md`.

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |
| B-1 | **Responsibility-Aenderungen propagieren nicht**: Wenn nachtraeglich eine fachliche Zustaendigkeit angelegt und mit einer Person belegt wird (z. B. CAD → Person X), aktualisiert sich die zugewiesene Person in laufenden Instanzen/Aufgaben nicht. Offene Tasks muessen nach Responsibility-Aenderung neu aufgeloest werden (re-assign oder zumindest Query-Invalidierung nach Sync-Event). Betrifft `Wechsel & Aufgaben` und `Meine Aufgaben`. Prio: HIGH | Nutzermeldung 2026-05-11 | offen |
| B-2 | **Supervisor-Sichtbarkeit auf Mitarbeiter**: Supervisoren sehen aktuell nicht ihre eigenen Mitarbeiter in der Mitarbeiterliste — nur Admins haben vollen Zugriff. Sichtbarkeitsregel fuer Supervisors definieren (z. B. alle Personen in beobachtbaren Abteilungen) und im Backend durchsetzen. Frontend-Anpassung abhaengig (FE-12 in FRONTEND_TODO.md). Prio: HIGH | Nutzermeldung 2026-05-11 | offen |
