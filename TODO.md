# TODO.md

Diese Datei steuert nur die Reihenfolge der Umsetzung.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in:

- `CODE_REVIEW.md`

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen.

Mindestens immer lesen:
- `## 6. Priorisierte Probleme`
- `## 7. Konkrete Verbesserungsvorschläge`
- die betroffene Analyse aus `## 1` bis `## 5`

Zusätzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz pruefen und festhalten, welche dieser Dokus bei der Aufgabe mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Relevante Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Wenn eine Aufgabe abgeschlossen wurde, muss ihr Status in `TODO.md` im selben Arbeitsgang auf `done` gesetzt werden.

Wichtig:
- `TODO.md` enthält bewusst keine Detailanweisungen.
- Die KI darf die Aufgabe nicht allein aus `TODO.md` ableiten.
- Die KI muss fuer jede Aufgabe `CODE_REVIEW.md` als aktuelle Primaerquelle verwenden.

---

## Prioritaeten

- `CRITICAL` = blockiert Nutzung, kann inkonsistente Datenstaende erzeugen oder laesst Berechtigungen aktiv
- `HIGH` = strukturell wichtig fuer Wartbarkeit, Validierung, Testbarkeit und Betrieb
- `LOW` = sinnvolle Haertung oder Bereinigung ohne unmittelbaren Blocker

---

## Umsetzungssteuerung

Alles was an Mails versendet wird soll zentral über Administration -> System -> Konfigration gepflegt werden können. das man die Texte sieht die die leute bekommen und man die bei bedarf ändern kann. In den Logs soll man auch sehen können wann welche Mail versendet wurde und was der Inhalt war

Aktuelle Review-Nacharbeit:
- [done] Prüfen, warum manche Personen in der Fachabteilung entweder Aufgaben anderer Personen sehen bzw. bekommen oder gar keine Aufgaben erhalten.

---

## Aufgabenteilung: Codex vs. Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

### Codex-Aufgaben (Routine-Implementierung nach vorgegebenem Fix)

Codex arbeitet nach den Fix-Code-Snippets in `CODE_REVIEW.md ## 7`.
Detailanweisungen kommen von dort. Diese Datei gibt nur Reihenfolge und Rahmendaten vor.

| ID | Aufgabe | Prio | Modell | Reasoning |
|----|---------|------|--------|-----------|
| COD-1 | C1: Transaktionsgrenzen in `SynchronizeRotationGeneratedTasks` einfuehren | CRITICAL | o4-mini | low — klares Muster, Fix-Code liegt vor |
| COD-2 | H2: Stations-Ueberschneidungspruefung in `RotationPlanningService.CreateRotationStation()` | HIGH | o4-mini | low — Logik und Code-Snippet aus Review verwenden |
| COD-3 | H3: Automation-Key gegen `WorkflowAutomationHandlerRegistry` validieren beim Template-Speichern | HIGH | o4-mini | low — Snippet liegt vor, Einstiegspunkt ist `AdminRotationConfigEndpoints` |
| COD-4 | H4: Notification-Template-Platzhalter beim Speichern gegen erlaubten Katalog pruefen | HIGH | o4-mini | medium — Regex-Pattern und Katalog muessen definiert werden |
| COD-5 | L4: Status-Enums und SystemKeys als zentrale Konstanten-Klassen anlegen und ueberall einsetzen | LOW | o4-mini | low — mechanische Suche-und-Ersetz-Arbeit |
| COD-6 | H7: Positionale Reader-Indizes (`GetString(0)`) durch `GetOrdinal(name)` ersetzen | LOW | o4-mini | low — stupide Umbauarbeit, kein Logik-Risiko |

- [open] COD-1
- [open] COD-2
- [open] COD-3
- [open] COD-4
- [open] COD-5
- [open] COD-6

### Claude-Aufgaben (Architektur, Sicherheit, SQL-Sichtbarkeit)

Claude uebernimmt Aufgaben mit Nebenwirkungen auf Auth, SQL-Logik oder Architekturschnitte.

| ID | Aufgabe | Prio | Modell | Reasoning |
|----|---------|------|--------|-----------|
| CLA-1 | C4: Task-Sichtbarkeitsfilter aus Memory in SQL-WHERE verlagern — mit Permission-Pruefung | CRITICAL | claude-sonnet-4-6 | high — SQL muss Berechtigungslogik korrekt abbilden, kein falsches Ergebnis tolerierbar |
| CLA-2 | C3: Entra-geloeschte User lokal deaktivieren (soft-delete + Audit) in `EntraDirectorySyncService` | CRITICAL | claude-sonnet-4-6 | high — Sicherheitsrelevant, Fehlverhalten hat direkte Auswirkung auf Zugriffsrechte |
| CLA-3 | H1/H6: `RotationRepository` und `RotationTaskRegenerationEngine` aus monolithischem Repository herausschneiden | HIGH | claude-opus-4-7 | high — Architekturschnitt mit vielen Abhaengigkeiten, falscher Schnitt erzeugt mehr Schulden |
| CLA-4 | H5: DAG-Vollstaendigkeitspruefung vor Workflow-Definition-Publish in `WorkflowDefinitionValidationService` | HIGH | claude-sonnet-4-6 | medium — Algorithmus ueberschaubar, aber Definition-Layer-Semantik muss verstanden werden |

- [open] CLA-1
- [open] CLA-2
- [open] CLA-3
- [open] CLA-4

---

## Abschlussregel für jede KI-Aufgabe

Nach jedem größeren Schritt muss berichtet werden:
1. Welche Dateien wurden geändert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Änderung zur Zielarchitektur?
4. Welche Risiken oder Lücken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
