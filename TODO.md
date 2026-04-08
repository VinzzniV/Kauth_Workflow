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

### [x] T0. Fundament bis Definition Layer, Runtime, Automation und Guided Builder steht
**Prioritaet:** P3
**Reasoning Effort:** High
**Plan Mode:** AUS

Lese zuerst:
- `MEMORY.md`
- Bereich `## Current Focus`

### [x] T1. Builder Core: Canvas einfuehren
**Prioritaet:** P0
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9A -- Builder von CRUD -> Flow-System umbauen`
- Teil `#### 1. Canvas einfuehren`

### [x] T2. Builder Core: Edges sichtbar und editierbar machen
**Prioritaet:** P0
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9A -- Builder von CRUD -> Flow-System umbauen`
- Teil `#### 2. Edges UI bauen`
- Abschnitt `## Prinzip 8: Edges sind Pflicht`

### [x] T3. Builder Core: Node Rendering von Datensatzfeldern auf Builder-Cards umstellen
**Prioritaet:** P0
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9A -- Builder von CRUD -> Flow-System umbauen`
- Teil `#### 3. Node Rendering`
- Abschnitt `## Prinzip 7: Nodes sind Bausteine, keine Datensaetze`

### [x] T4. Builder Core: Sidebar fuer Node-Konfiguration, Bedingungen und Actions einfuehren
**Prioritaet:** P1
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9A -- Builder von CRUD -> Flow-System umbauen`
- Teil `#### 4. Sidebar einfuehren`
- Abschnitt `## Prinzip 9: Actions sind sichtbar im Builder`

### [x] T5. Builder Core: Toolbar fuer Add Node, Delete Node, Validate und Publish bauen
**Prioritaet:** P1
**Reasoning Effort:** Medium
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9A -- Builder von CRUD -> Flow-System umbauen`
- Teil `#### 5. Toolbar`

### [x] T6. Action Layer im UI als eigener Builder-Bestandteil sichtbar machen
**Prioritaet:** P1
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Prinzip 9: Actions sind sichtbar im Builder`
- Abschnitt `## Top 3 Aufgaben jetzt`

### [x] T7. UI-Fixes sofort umsetzen: Buttons, Builder-Ablauf und Feldreduktion
**Prioritaet:** P2
**Reasoning Effort:** Low
**Plan Mode:** AUS

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `# 7. UI Fixes (sofort umsetzen)`

### [x] T8. Phase 9B: Builder-UX auf Produktniveau anheben
**Prioritaet:** P2
**Reasoning Effort:** Medium
**Plan Mode:** AUS

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9B -- UX Verbesserung`

### [x] T9. Label-Strategie und Umlaut-Regel im Builder hartziehen
**Prioritaet:** P2
**Reasoning Effort:** Low
**Plan Mode:** AUS

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Prinzip 10: Keine Umlaute im Systemkern`
- Abschnitt `# 7. UI Fixes (sofort umsetzen)`

---

## Empfohlene Reihenfolge

1. T9 Label-Strategie und Umlaut-Regel hartziehen

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `Workflow_Plattform_Implementation_Plan.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Builder-Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
