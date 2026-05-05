# CLAUDE_CONTROL.md

## Zweck

- operative Steuerungsdatei fuer Claude-Arbeit unter Codex-Orchestrierung
- klare Rollen, Slice-Regeln und Doku-Pflichten

## Primaerquelle fuer

- Arbeitsmodus Claude unter Codex-Orchestrierung
- Commit-Regel pro abgeschlossenem Slice
- Verhalten bei zu grossen oder unsauberen Aufgaben

## Nicht verwenden fuer

- Architekturentscheidungen
- Produktregeln
- allgemeine Repo-Struktur

## Wann aktualisieren

- wenn sich der Orchestrierungsworkflow zwischen Codex und Claude aendert
- wenn neue verbindliche Arbeitsregeln fuer Claude dazukommen

## Verwandte Dateien

- `DOCS_CONTROL.md`
- `CLAUDE.md`
- `CODE_REVIEW.md`
- `TODO.md`
- `CODEX_SYNC.md`
- `KauthWorkflow/Arbeit/KI-Workflow.md`

---

Diese Datei definiert, wie Claude in diesem Repo arbeiten soll, wenn Codex als Orchestrator vorgeschaltet ist.

Sie ist keine Architektur- oder Produktdoku, sondern die operative Steuerungsdatei fuer Claude-Arbeit.

---

## Zielbild

- **Codex** fuehrt den Prozess, priorisiert den naechsten Schritt, prueft Doku-Konsistenz und steuert die Reihenfolge.
- **Claude** arbeitet die von Codex vorgegebenen Arbeitspakete ab.
- Claude soll nicht frei mehrere Baustellen parallel aufmachen, sondern klar abgegrenzte Slices abschliessen.

## Rollen

### Codex als Orchestrator

Codex ist zustaendig fuer:
- Lesen und Abgleichen von `DOCS_CONTROL.md`, `CODE_REVIEW.md`, `TODO.md`, `MEMORY.md`, `CODEX_SYNC.md`
- Auswahl des naechsten sinnvollen Schritts
- Auswahl und **explizite CLI-Erzwingung** von Claude-`--model` und `--effort` pro Slice
- Pruefen, ob Claude zu breit oder in den falschen Bereich arbeitet
- Einfordern von Doku-Konsistenz ueber mehrere Dateien
- Entscheidung, ob ein grosser Block weiter geschnitten werden muss
- Review des von Claude erreichten Zwischenstands

### Claude als Worker

Claude ist zustaendig fuer:
- Architektur- und Refactor-Slices innerhalb des von Codex gesetzten Umfangs
- konkrete Code-Aenderungen im vorgegebenen Slice
- Testanpassungen fuer genau diesen Slice
- Doku-Updates im selben Arbeitsgang
- Commit nach jedem abgeschlossenen Slice

## Grundregeln fuer Claude

1. Claude arbeitet immer auf einem **explizit benannten Slice** oder Arbeitspaket.
2. Claude liest vor nicht-trivialer Arbeit mindestens:
   - `DOCS_CONTROL.md`
   - `PROJECT_CONTEXT.md`
   - `MEMORY.md`
   - `CODEX_SYNC.md`
3. Bei Review-, Runtime-, Rotations- oder Nacharbeits-Themen liest Claude zusaetzlich:
   - `CODE_REVIEW.md`
   - `TODO.md`
4. Wenn Codex auf weitere Dokus verweist, sind diese fuer den aktuellen Durchgang Pflicht.
5. Claude macht **keine stillen Nebenbei-Refactors** ausserhalb des beauftragten Slices.
6. Claude zieht notwendige Doku im selben Arbeitsgang mit.
7. Claude benennt offen, wenn ein Slice zu gross ist, und schlaegt einen kleineren Zuschnitt vor.
8. Claude committet **nach jedem abgeschlossenen Slice**.

## Standard-Workflow

### 1. Auftrag durch Codex

Codex gibt Claude:
- den aktuellen Fokus
- den exakten Slice oder Block
- das zu verwendende Claude-`--model`
- das zu verwendende Claude-`--effort`
- Leitplanken
- Pflichtdokus
- Testerwartung
- Commit-Regel

**Verbindliche Regel:** Wenn Codex Claude ueber die CLI startet, muessen `--model` und `--effort` immer explizit gesetzt werden. Es reicht nicht, Modell oder Reasoning nur im Prompttext oder indirekt ueber `TODO.md` zu empfehlen. Die Auswahl aus `TODO.md`/`CODE_REVIEW.md` ist technisch per CLI-Flag zu erzwingen.

### 2. Analyse durch Claude

Claude:
- liest die Pflichtdokus
- prueft den Working Tree
- benennt, welche Dokus im selben Pass mitgezogen werden muessen
- entscheidet nur dann ueber einen Split, wenn der Umfang fuer einen sicheren Schritt zu gross ist

### 3. Umsetzung

Claude:
- arbeitet nur den beauftragten Slice ab
- passt Tests an
- aktualisiert die betroffenen Dokus
- bereinigt veraltete Hinweise direkt mit

### 4. Verifikation

Claude:
- fuehrt die relevanten Tests oder Builds aus
- nennt die exakten Befehle
- nennt das echte Ergebnis ohne Beschoenigung

### 5. Commit

Nach abgeschlossenem Slice:
- `git status` pruefen
- nur den aktuellen abgeschlossenen Stand committen
- klare Commit-Message verwenden
- Commit-Hash an Codex zurueckmelden

## Pflichtausgabe von Claude nach jedem Slice

Claude liefert am Ende mindestens:
- welche Dateien geaendert wurden
- was der Slice konkret erledigt hat
- welche Testbefehle gelaufen sind und mit welchem Resultat
- welcher Commit erstellt wurde
- welcher naechste offene Schritt folgt

Codex dokumentiert im Auftrag intern immer auch, welches `--model` und welches `--effort` fuer diesen Slice erzwungen wurden.

## Verhalten bei zu grossen oder unsauberen Slices

Wenn der angeforderte Block zu gross ist, soll Claude:
- **nicht** halb implementieren und den Tree chaotisch hinterlassen
- den Restumfang klar beschreiben
- einen sinnvollen Slice-Plan vorschlagen
- nach Entscheidung durch Codex den ersten sicheren Slice umsetzen

Wenn Claude auf Inkonsistenzen im Tree oder in der Doku stoesst, soll Claude:
- die Inkonsistenz offen benennen
- sie nach Moeglichkeit im selben Pass bereinigen
- andernfalls Codex den Konflikt klar melden

## Doku-Pflicht fuer Claude

Wenn sich durch einen Slice Priorisierung, Status oder Architekturzuschnitt aendern, muss Claude die passenden Dateien mitziehen.

Typische Dateien:
- `TODO.md`
- `CODE_REVIEW.md`
- `MEMORY.md`
- `CODEX_SYNC.md`
- `KauthWorkflow/Stand/Code-Review-Status.md`
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`
- weitere Architektur- oder Strukturdateien, falls der Slice das rechtfertigt

`MEMORY.md` bleibt kurzlebig. Veraltete Zwischenstaende muessen entfernt werden.

## Nicht erlaubt fuer Claude

- ohne Auftrag mehrere unabhaengige Baustellen gleichzeitig anfangen
- abgeschlossene Slices uncommitted liegen lassen
- TODO-/Review-Status nicht nachziehen, obwohl der Slice abgeschlossen ist
- breit streuende Refactors unter einem kleinen Auftrag verstecken
- offene Risiken verschweigen
