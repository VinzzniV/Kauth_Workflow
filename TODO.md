# TODO.md

Diese Datei steuert die Reihenfolge der Umsetzung aktiver Review-Zyklen.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in `CODE_REVIEW.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen (Priorisierungs- und Analyseabschnitte).

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz pruefen und festhalten, welche dieser Dokus bei der Aufgabe mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Relevante Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Wenn eine Aufgabe abgeschlossen wurde, muss ihr Status in `TODO.md` im selben Arbeitsgang auf `done` gesetzt werden.

Wichtig:
- `TODO.md` enthaelt bewusst keine Detailanweisungen.
- Die KI darf die Aufgabe nicht allein aus `TODO.md` ableiten.
- Die KI muss fuer jede Aufgabe `CODE_REVIEW.md` als aktuelle Primaerquelle verwenden.

---

## Prioritaeten

- `CRITICAL` = blockiert Nutzung, kann inkonsistente Datenstaende erzeugen oder laesst Berechtigungen aktiv
- `HIGH` = strukturell wichtig fuer Wartbarkeit, Validierung, Testbarkeit und Betrieb
- `LOW` = sinnvolle Haertung oder Bereinigung ohne unmittelbaren Blocker

---

## Aufgabenteilung: Codex vs. Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

Kein aktiver Review-Zyklus. Naechste Aufgaben werden hier eingetragen, sobald ein neues Review vorliegt.

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
