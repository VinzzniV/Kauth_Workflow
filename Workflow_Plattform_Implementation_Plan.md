# Workflow Platform Migration Plan (UPDATED)

## Zweck dieser Datei

Diese Datei ist die **zentrale Umsetzungsanweisung für eine KI in der IDE** (z. B. Codex oder Claude), damit aus dem bestehenden Projekt eine **konfigurierbare interne Workflow-Plattform** entsteht.

WICHTIG (neu):
Dieses Dokument berücksichtigt den aktuellen Stand:

* Definition Layer ist bereits teilweise vorhanden
* UI existiert, ist aber aktuell **kein echter Workflow Builder**, sondern Stammdatenpflege

Ziel ist jetzt:

> Von "Workflow-Daten verwalten" → zu "Workflow bauen und verstehen"

---

# 1. Zielbild (unverändert, aber präzisiert)

## Produktziel

Eine Plattform, mit der ein Unternehmen **komplexe interne Prozesse visuell und konfigurierbar bauen kann**.

## Kritischer Zusatz (neu)

Ein Workflow ist kein Formular.
Ein Workflow ist:

* Struktur
* Reihenfolge
* Verbindungen
* Entscheidungen
* Aktionen

Die UI MUSS das widerspiegeln.

---

# 2. Wichtigster neuer Architekturpunkt

## Aktuelles Problem (präzisiert)

Das System hat:

* Nodes ✔️
* Definitionen ✔️
* Versionen ✔️

Aber:

❌ Kein Flow
❌ Keine Edges sichtbar
❌ Keine echte Orchestrierung im UI
❌ Keine Actions sichtbar

👉 Das System ist aktuell:

> "Workflow-Datenmodell mit CRUD UI"

👉 Ziel ist:

> "Visueller Workflow Builder mit Runtime"

---

# 3. NEUER Pflichtbereich: Builder Architecture

## Builder ist KEIN Formular

Der Builder muss bestehen aus:

### 1. Canvas (zentral)

* Nodes als visuelle Blöcke
* Verbindungen (Edges)
* Drag & Drop
* Zoom / Pan

### 2. Sidebar (rechts)

* Node konfigurieren
* Parameter
* Actions
* Bedingungen

### 3. Toolbar (oben)

* Node hinzufügen
* Publish
* Validieren

---

# 4. Erweiterte Zielarchitektur

## G. Builder Layer (NEU – kritisch)

Der Builder ist ein eigener Kernbereich.

Er enthält:

* Canvas Engine
* Node Rendering
* Edge Rendering
* Selection State
* Drag & Drop
* Node Config Panel

---

# 5. Zentrale neue Prinzipien (UI + Architektur)

## Prinzip 6: Workflow muss visuell verständlich sein

Ein User muss sofort sehen:

* Wo startet der Prozess?
* Welche Schritte folgen?
* Wo gibt es Entscheidungen?
* Wo enden Pfade?

Wenn das nicht sichtbar ist → System ist falsch.

---

## Prinzip 7: Nodes sind Bausteine, keine Datensätze

Node UI darf NICHT sein:

* Node Key
* Node Type
* Sort Order

Node UI MUSS sein:

* Titel
* Verantwortlicher
* Aktion
* Verhalten

---

## Prinzip 8: Edges sind Pflicht

Ein Workflow ohne Edges ist kein Workflow.

Du brauchst:

* Verbindung A → B
* Bedingungen:

  * success
  * reject
  * custom condition

---

## Prinzip 9: Actions sind sichtbar im Builder

Actions dürfen NICHT versteckt sein.

Im Builder auswählbar:

* Create AD User
* Create Mailbox
* Assign Groups
* Send Mail
* Create Ticket

---

## Prinzip 10: Keine Umlaute im Systemkern

NEU – verbindlich:

* KEINE Umlaute in:

  * DB
  * Keys
  * API
  * Code

UI optional DE/EN, aber intern:

* department_change
* onboarding
* offboarding

---

# 6. NEUE PHASE (kritisch): Builder Rework

## Phase 9A — Builder von CRUD → Flow-System umbauen

### Ziel

Der Workflow Builder wird zu einem echten Flow Builder.

### Aufgaben

#### 1. Canvas einführen

* React Canvas (z. B. React Flow oder Custom)
* Node Positionierung nutzen (`position_x`, `position_y`)

#### 2. Edges UI bauen

* Verbindung erstellen
* Verbindung löschen
* Bedingungen setzen

#### 3. Node Rendering

Jeder Node als Card:

Beispiel:

Task Node:

* Titel
* Verantwortlicher
* Deadline
* Aktion

Approval Node:

* Rolle
* Kommentar Pflicht

---

#### 4. Sidebar einführen

Wenn Node ausgewählt:

* Konfiguration anzeigen
* Action auswählen
* Bedingungen setzen

---

#### 5. Toolbar

* Add Node
* Delete Node
* Validate
* Publish

---

### Deliverables

* funktionierender visueller Workflow Builder
* Edges sichtbar
* Nodes klickbar
* Sidebar konfigurierbar

### Reasoning effort

**High**

### Plan mode

**AN**

---

# 7. UI Fixes (sofort umsetzen)

## Buttons

Einführen:

* Primary (z. B. blau)
* Secondary
* Danger

---

## Struktur

Statt alles gleichzeitig:

Flow:

1. Definition
2. Version
3. Builder

---

## Entfernen

* Sort Order im UI
* technische Keys im UI
* unnötige Eingabefelder

---

## Labels

Entscheidung treffen:

Option A (empfohlen):

* komplett Englisch

Option B:

* Deutsch UI, aber KEINE Umlaute

---

# 8. Anpassung bestehender Phasen

## Phase 9 (alt) → ersetzt durch:

### Phase 9A — Builder Core

### Phase 9B — UX Verbesserung

---

## Phase 9B — UX Verbesserung

### Ziel

Builder fühlt sich wie Produkt an, nicht wie Admin Tool.

### Aufgaben

* spacing verbessern
* visuelle Hierarchie
* Farben für Node-Typen
* Hover States
* klare CTA Buttons

### Reasoning effort

**Medium**

### Plan mode

**AUS**

---

# 9. NEUE harte Verbote

Die KI darf NICHT:

* neuen Workflow Code als reine Formulare bauen
* Nodes ohne Edges erstellen
* Actions nur im Backend verstecken
* UI als CRUD erweitern statt neu denken

---

# 10. NEUE Prioritäten

## Top 3 Aufgaben jetzt

### 1. Builder neu denken (Canvas + Edges)

* Reasoning: HIGH
* Plan mode: AN

### 2. Action Layer im UI sichtbar machen

* Reasoning: HIGH
* Plan mode: AN

### 3. Umlaute + UI Cleanup

* Reasoning: LOW
* Plan mode: AUS

---

# 11. Schlussanweisung (aktualisiert)

Dieses Projekt ist aktuell:

> ✔️ gutes Workflow-Datenmodell
> ❌ kein echter Workflow Builder

Das Ziel ist:

> ✔️ visuelles, verständliches Workflow-System

Die KI soll **keine weiteren CRUD-Formulare bauen**, sondern:

* Flow sichtbar machen
* Nodes sinnvoll darstellen
* Verbindungen implementieren
* Actions integrieren

Alles andere ist zweitrangig.
