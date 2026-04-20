# TODO.md

Diese Datei steuert nur die Reihenfolge der Umsetzung.
Die fachlichen Details, Architekturregeln, Scope-Grenzen, Datenmodelle, Akzeptanzkriterien und Deliverables stehen zentral in:

- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` lesen.

Mindestens immer lesen:
- `## Kontext`
- `## Produktziel`
- `## Wichtige fachliche Regeln`
- `## MVP-Scope`
- `## Technische Leitplanken`
- `## Gewünschtes Ergebnis für diese Implementierung`
- `## Was Codex zuerst tun soll`
- `## Wichtige Implementierungsregeln für Codex`

Zusätzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Zusätzlich muss pro Aufgabe immer der unten referenzierte Phasenabschnitt gelesen werden.

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz pruefen und festhalten, welche dieser Dokus bei der Aufgabe mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Relevante Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Wenn eine Aufgabe abgeschlossen wurde, muss ihr Status in `TODO.md` im selben Arbeitsgang auf `done` gesetzt werden.

Wichtig:
- `TODO.md` enthält bewusst keine Detailanweisungen.
- Die KI darf die Aufgabe nicht allein aus `TODO.md` ableiten.
- Die KI muss für jede Aufgabe den Implementierungsplan als Primärquelle verwenden.

---

## Prioritäten

- `P0` = Domänenmodell, Persistenz, Validierung, Task-Generierung
- `P1` = Benachrichtigungen und Backend-Ablaufstabilität
- `P2` = HR-UI sowie IT-/Fachbereichs-UI
- `P3` = Audit, Tests, Robustheit, Doku, Vorbereitung für spätere Automatisierung

---

## Umsetzungssteuerung

Alles was an Mails versendet wird soll zentral über Administration -> System -> Konfigration gepflegt werden können. das man die Texte sieht die die leute bekommen und man die bei bedarf ändern kann. In den Logs soll man auch sehen können wann welche Mail versendet wurde und was der Inhalt war
---

## Abschlussregel für jede KI-Aufgabe

Nach jedem größeren Schritt muss berichtet werden:
1. Welche Dateien wurden geändert?
2. Auf welchen Abschnitt in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` wurde gearbeitet?
3. Wie passt die Änderung zur Zielarchitektur?
4. Welche Risiken oder Lücken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
