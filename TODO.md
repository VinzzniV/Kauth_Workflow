# TODO.md

Diese Datei steuert nur noch die Reihenfolge der Umsetzung.
Die Details, Regeln, Architekturgrenzen und Deliverables stehen zentral in `Workflow_Plattform_Implementation_Plan.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `Workflow_Plattform_Implementation_Plan.md` lesen.

Mindestens immer lesen:
- `# 1. Zielbild`
- `# 2. Wichtigster neuer Architekturpunkt`
- `# 3. NEUER Pflichtbereich: Builder Architecture`
- `# 4. Erweiterte Zielarchitektur`
- `# 5. Zentrale neue Prinzipien`
- `# 9. NEUE harte Verbote`
- `# 10. NEUE Prioritaeten`
- `# 11. Schlussanweisung (aktualisiert)`

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Zusaetzlich muss pro Aufgabe immer der unten referenzierte Phasenabschnitt gelesen werden.

Wichtig:
- `TODO.md` enthaelt bewusst keine Detailanweisungen mehr.
- Die KI soll die Aufgabe nicht aus `TODO.md` allein ableiten.
- Die KI soll fuer jede Aufgabe den Implementierungsplan als Primaerquelle verwenden.

---

## Prioritaeten

- `P0` = Builder-Kernblocker fuer Canvas, Edges und visuelle Flow-Verstaendlichkeit
- `P1` = sichtbare Konfiguration, Actions und Builder-Bedienung
- `P2` = UI-Cleanup und UX-Verbesserung in Richtung Produkt statt Admin-CRUD
- `P3` = Folgeausbau nach stabilem Builder-Core

---

## Umsetzungssteuerung

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `Workflow_Plattform_Implementation_Plan.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Builder-Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
