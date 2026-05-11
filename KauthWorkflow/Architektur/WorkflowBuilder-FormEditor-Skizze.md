# WorkflowBuilder Form-Editor — Layout-Skizze

**Status:** Entwurf zur Abnahme — Phase 1 von L7 (CODE_REVIEW.md)
**Datum:** 2026-05-02
**Hintergrund:** Der heutige Canvas-Editor (React Flow + Drag&Drop, ~5.164 Zeilen Frontend) wird durch einen strukturierten Form-Editor ersetzt. Ziele: verständlich, nicht überladen, gut geordnet — auch für nachfolgende Admins / HR ohne Tech-Hintergrund nutzbar.

Dieses Dokument legt das Layout fest, **bevor** Code geschrieben wird. Phase 2 (Implementierung) startet erst nach Freigabe.

---

## Leitprinzipien

1. **Lineare Lese-Reihenfolge.** Was zusammengehört steht untereinander, nicht in 3 Spalten verteilt. Kein Hin-und-Her zwischen Canvas + Inspector + Palette.
2. **Eine Sache pro Sektion.** Jede Sektion beantwortet *eine* Frage: „Was ist das?" / „Welche Schritte?" / „Wie hängen sie zusammen?" / „Wann wird was erzeugt?".
3. **Tech-Begriffe sind versteckt aber zugänglich.** `nodeKey`, `configJson`, `inputMappingJson` etc. nur unter „Technische Details ►" Aufklapp-Block, nicht im Hauptfluss.
4. **Reihenfolge sichtbar machen ohne Pfeile.** Statt Edges visuell zu verkabeln: nummerierte Reihenfolge + bei Verzweigung explizite „Folge-Schritt"-Dropdowns. Edges sind ein Tabellen-Editor, nicht ein Canvas-Konzept.
5. **Sofort-Feedback statt Save-Round-Trips.** Lokale Validierungs-Hinweise inline (Pflichtfeld leer, doppelter Schlüssel etc.).

---

## Seitenaufbau

```
┌─────────────────────────────────────────────────────────────────┐
│  Header: Workflow-Definition / Version-Auswahl + Status-Pille   │
├─────────────────────────────────────────────────────────────────┤
│  1. Stammdaten                                                  │
├─────────────────────────────────────────────────────────────────┤
│  2. Schritte (Nodes)                                            │
│     [Liste, vertikal, mit Inline-Eigenschaften pro Schritt]     │
├─────────────────────────────────────────────────────────────────┤
│  3. Übergänge (Edges)                                           │
│     [Tabelle: von → zu + optionale Bedingung + Reihenfolge]     │
├─────────────────────────────────────────────────────────────────┤
│  4. Validierung & Vorschau                                      │
│     [Issue-Liste + Diagramm-Vorschau (read-only)]               │
├─────────────────────────────────────────────────────────────────┤
│  Footer-Aktionen: Speichern · Veröffentlichen · Verwerfen       │
└─────────────────────────────────────────────────────────────────┘
```

Kein Drei-Spalten-Layout mehr. Linearer Lesefluss von oben nach unten. Sticky-Footer mit den Aktionen. Bei langen Definitionen scrollbarer Mittelteil.

---

## Sektion 1 — Stammdaten

**Frage:** Was ist diese Workflow-Definition?

| Feld                    | Typ                | Pflicht | Hinweis                              |
|-------------------------|--------------------|---------|--------------------------------------|
| Name                    | Text               | Ja      | „Onboarding 2026 Q2"                 |
| Beschreibung            | Mehrzeilig (3-5)   | Nein    | Zweck, Owner, Anlass                 |
| Prozessbezug            | Dropdown           | Ja      | Onboarding/Offboarding/Wechsel/...   |
| Definition-Schlüssel    | Auto, read-only    | —       | wird vom Namen abgeleitet            |
| Version                 | Auto, read-only    | —       | „Version 3 (Entwurf)"                |

**Aufklappbar „Technische Details ►":** `definition_key` (manuell überschreibbar), `workflowDefinitionKey`.

---

## Sektion 2 — Schritte (Nodes)

**Frage:** Welche Schritte hat der Workflow?

**Layout:** Vertikale Liste. Jeder Schritt ist eine Karte mit Inline-Editor. Reihenfolge entspricht der natürlichen Lese-Reihenfolge (topologisch sortiert anhand der Edges, fallback `sortOrder`). Add-Button am Ende der Liste.

```
[#1] ─ Start                                          [↑] [↓] [✕]
       Schritt-Name: Workflow-Start
       ► Technische Details

[#2] ─ Formular                                       [↑] [↓] [✕]
       Schritt-Name: Vorgesetzten-Bestätigung
       Prozess-Vorlage: HR-Eintritts-Anforderungen ▼
       ► Technische Details (nodeKey, configJson)

[#3] ─ Bereitstellung                                 [↑] [↓] [✕]
       Schritt-Name: IT-Maßnahmen erzeugen
       (für diesen Prozess automatisch ausgewählt)
       ► Technische Details

[#4] ─ Aufgabe                                        [↑] [↓] [✕]
       Schritt-Name: Hardware-Übergabe
       Vorlage: Hardware-Setup ▼
       Verantwortlich: IT-Abteilung ▼
       ► Technische Details

[#5] ─ Automatisierung                                [↑] [↓] [✕]
       Schritt-Name: AD-User anlegen
       Aktionen:
         1. CreateAdUser           [Eingabe-Mapping bearbeiten ►] [✕]
         2. AssignGroups           [Eingabe-Mapping bearbeiten ►] [✕]
         + Aktion hinzufügen ▼
       Bei Fehler: Workflow abbrechen (fixiert)
       ► Technische Details

[#6] ─ Entscheidung                                   [↑] [↓] [✕]
       Schritt-Name: Hat Hardware-Übernahme?
       Bedingungen werden in Sektion 3 definiert.

[#7] ─ Ende                                           [↑] [↓] [✕]

  [+ Schritt hinzufügen ▼]   ← Dropdown: Formular / Aufgabe /
                                Freigabe / Bereitstellung / ... / Ende
```

### Pro Schritt-Typ angezeigte Felder

Inspector-Logik aus heutigem `BuilderInspectorPanel.tsx` bleibt grundsätzlich erhalten — nur als Inline-Block statt Seiten-Panel. Eigenschaften pro Typ (gekürzt):

- **Start / Ende:** nur Schritt-Name
- **Formular:** Name, Prozess-Vorlage (= Anforderungs-Set), Aufklappbar Vorschau der Anforderungen
- **Freigabe:** Name, Approver-Zuständigkeit, Aufgaben-Vorlage (Approval-Task)
- **Aufgabe:** Name, Aufgaben-Vorlage, Verantwortlich
- **Bereitstellung / Entzug / Änderung / Umbenennung:** Name, automatische Maßnahmen-Vorschau (read-only Liste mit aufklappbarem Detail pro Maßnahme — siehe heutiges `BuilderInspectorFocusPanel` für `measure_*`-Nodes)
- **Automatisierung:** Name, Aktions-Liste mit Reihenfolge, pro Aktion ein „Eingabe-Mapping bearbeiten"-Modal mit JSON-Editor (für Tech-Affine)
- **Entscheidung:** Name, Hinweis „Bedingungen siehe Übergänge (Sektion 3)"
- **Parallel-Split / Parallel-Join:** Name, sonst nichts

### Maßnahmen-Nodes (Sonderbehandlung)

Die `measure_*`-Nodes haben heute eine eigene Detail-Ansicht (`BuilderInspectorFocusPanel`, 470 Zeilen) die zeigt *welche Task-Vorlagen* für diesen Prozess automatisch erzeugt werden, mit ihren Bedingungen und Abhängigkeiten. Das bleibt — als aufklappbarer Block innerhalb der Schritt-Karte:

```
[#3] ─ Bereitstellung                                 [↑] [↓] [✕]
       Schritt-Name: IT-Maßnahmen erzeugen
       ▼ Geplante Maßnahmen (12)
         ├─ Hardware-Bestellung                  Pflicht
         │  └─ Bedingungen: HardwareTakeover = false
         ├─ AD-Account anlegen                   Pflicht
         ├─ E-Mail-Adresse anlegen               Pflicht
         │  └─ Hängt ab von: AD-Account anlegen
         └─ ... (9 weitere)                      [Alle ausklappen]
       ► Technische Details
```

---

## Sektion 3 — Übergänge (Edges)

**Frage:** Wie hängen die Schritte zusammen?

**Layout:** Tabelle. Eine Zeile pro Übergang. Sortierung primär nach Quell-Schritt (in Schritt-Reihenfolge), sekundär nach Pfad-Reihenfolge.

| Von Schritt           | →   | Zu Schritt              | Pfad-Reihenfolge | Bedingung                    |        |
|-----------------------|-----|-------------------------|------------------|------------------------------|--------|
| #1 Start              | →   | #2 Vorgesetzten-Best.   | 1                | —                            | [✕]    |
| #2 Vorgesetzten-Best. | →   | #3 IT-Maßnahmen         | 1                | —                            | [✕]    |
| #3 IT-Maßnahmen       | →   | #4 Hardware-Übergabe    | 1                | —                            | [✕]    |
| #4 Hardware-Übergabe  | →   | #6 Hat Hardware-Übern.? | 1                | —                            | [✕]    |
| #6 Hat Hardware-Üb.?  | →   | #5 AD-User anlegen      | 1                | `hardwareTakeover == true`   | [✕]    |
| #6 Hat Hardware-Üb.?  | →   | #7 Ende                 | 2                | `hardwareTakeover == false`  | [✕]    |
| #5 AD-User anlegen    | →   | #7 Ende                 | 1                | —                            | [✕]    |

**+ Übergang hinzufügen** öffnet eine Zeile mit zwei Dropdowns (von / zu, befüllt mit allen vorhandenen Schritt-Namen) und zwei Eingabefeldern (Reihenfolge / Bedingung). Bedingungen sind nur bei Entscheidung-Quellen sinnvoll → wenn Quelle keine Entscheidung ist, wird das Bedingungs-Feld read-only mit Hinweis „nur bei Entscheidung-Schritten".

---

## Sektion 4 — Validierung & Vorschau

**Frage:** Funktioniert das, was ich gerade gebaut habe?

**Zwei nebeneinander angeordnete Blöcke** (auf schmalen Screens untereinander):

### 4a) Validierungs-Issues
Liste aller Probleme die ein Veröffentlichen blockieren würden. Inline pro Issue: Klick springt zu der Sektion / dem Feld, wo das Problem ist. Beispiele:
- „Schritt #5 'AD-User anlegen' hat keine Aktionen definiert."
- „Übergang von #6 'Hat Hardware-Übern.?' hat doppelte Pfad-Reihenfolge 1."
- „Schritt-Schlüssel `step_form_2` ist doppelt vergeben."

Wenn keine Issues → grüner „Bereit zum Veröffentlichen"-Hinweis.

~~### 4b) Diagramm-Vorschau (read-only)~~
**Entschieden 2026-05-02: Sektion 4b entfällt komplett.** React Flow wird als Dependency entfernt. Sektion 4 enthält nur noch die Validierungs-Issues.

---

## Footer

Sticky am unteren Bildschirmrand. Drei Aktionen:

| Aktion                | Sichtbar wenn        | Effekt                                        |
|-----------------------|----------------------|-----------------------------------------------|
| **Verwerfen**         | Lokale Änderungen    | Setzt Draft auf Server-Stand zurück           |
| **Speichern (Draft)** | Lokale Änderungen    | Speichert Draft, bleibt im Edit-Modus         |
| **Veröffentlichen**   | Keine Issues         | Speichert + Publish; Version → `published`    |

Daneben ein Aufklapp-Menü „Weitere Aktionen": Version-Historie ansehen, neue Version aus dieser ableiten, Version löschen (nur Draft).

---

## Was dabei *nicht* mehr existiert

- **Drag&Drop-Canvas zum Verkabeln.** Edges sind Tabelle, kein gezogener Pfeil.
- **Drei-Spalten-Layout** (Palette links, Canvas Mitte, Inspector rechts). Stattdessen lineare Sektionen.
- **Auswahl-State pro Node.** Eigenschaften sind immer inline sichtbar, keine „bitte zuerst auswählen"-Hinweise.
- **`BuilderPalettePanel`** als separate Komponente. Ersetzt durch „Schritt hinzufügen ▼"-Dropdown direkt in Sektion 2.

## Was bleibt unverändert

- Datenmodell + State-Hooks (`useAdminWorkflowBuilder`, `adminWorkflowBuilderModel`) → zu ~80% recyclebar
- Backend-API (Endpunkte, DTOs, Validierungs-Snapshot) → kein Backend-Change
- Inspector-Detail-Logik für `measure_*`-Nodes (was wird erzeugt) → wird zum aufklappbaren Inline-Block
- Action-Registry + Aktions-Definitionen (nur die Auswahl-UI ändert sich)

---

## Entscheidungen (2026-05-02)

1. **Diagramm-Vorschau (Sektion 4b): RAUS.** Sektion 4 enthält nur noch die Validierungs-Issues. React-Flow-Dependency kann komplett entfernt werden. Vereinfacht den Bundle und reduziert Wartung.

2. **Eingabe-Mapping für Automation-Aktionen (Sektion 2 / Automation): Form-Builder (Option B).** Pro Parameter ein Formular-Block mit Quellen-Dropdown (`workflow` / `static` / `answer` / ggf. weitere) + dynamischen Feldern abhängig von der gewählten Quelle. Hinter den Kulissen wird daraus das bekannte JSON-Mapping erzeugt. Begründung: nachfolgende HR-Admins sollen Automatisierungs-Schritte bedienen können ohne JSON zu lernen. Zusätzlicher Aufwand 3-5 Tage gegenüber Roh-JSON-Editor — wird in Phase 2 als eigener Block geplant.

3. **Bedingungs-Editor (Sektion 3 / Decision-Edges): Freitext-Expression** (wie heute, `hardwareTakeover == true`). Strukturierter Builder als optionaler Folge-Schritt notiert — siehe Backlog unten.

4. **Reihenfolge der Schritte (Sektion 2): Topologisch berechnet** anhand der Edges. Bei mehrdeutiger Reihenfolge (parallele Pfade) Fallback auf `sortOrder`. Kein manueller Override per Pfeil-Buttons.

5. **Mobile / schmale Bildschirme:** Fokus auf PC/Laptop/Bildschirm. Layout darf annehmen ≥1024px Breite. Handy-Modus später optional, kein Constraint für Phase 2.

## Backlog (für später)

- **Strukturierter Bedingungs-Builder** für Decision-Edges (Dropdown Antwort + Operator + Wert statt Freitext-DSL).
- **Form-Builder für Eingabe-Mapping** falls Phase-2-Entscheidung auf Roh-JSON fällt und sich später als zu unfreundlich erweist.
- **Handy/Tablet-Layout** für den Editor.

---

## Phase-2-Plan (NACH Abnahme dieser Skizze)

1. Neue Datei `web/src/components/admin-config/AdminWorkflowBuilderFormSection.tsx`
2. Neue Datei `web/src/components/admin-config/WorkflowBuilderStepCard.tsx` (eine Karte aus Sektion 2)
3. Neue Datei `web/src/components/admin-config/WorkflowBuilderEdgeTable.tsx` (Sektion 3)
4. Anpassen: `web/src/pages/WorkflowBuilderPage.tsx` — Routing-Switch zwischen alter Canvas-Variante und neuer Form-Variante via Feature-Flag oder URL-Query
5. State-Hook `useAdminWorkflowBuilder` bleibt; ggf. einzelne Selektoren ergänzen
6. Nach Verifikation in Dev: Canvas-Komponenten löschen (`AdminWorkflowBuilderSection.tsx`, `WorkflowBuilderCanvasNode.tsx`, `BuilderPalettePanel.tsx`, `BuilderInspectorPanel.tsx`, `BuilderInspectorFocusPanel.tsx`, `WorkflowBuilderSidebar.tsx` — `BuilderInspectorFocusPanel` wird teilweise wiederverwertet)
7. React Flow Dependency aus `package.json` entfernen (Diagramm-Vorschau raus, siehe Entscheidung 1)

Realistischer Aufwand: **2-3 Tage konzentrierte Arbeit** wenn die offenen Fragen geklärt sind.
