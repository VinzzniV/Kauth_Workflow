# Einheit 7 — Einarbeitungsplan im echten Repo

> Ziel: Eine Person mit etwas Java-Erfahrung, Grundlagen in C# und Python und gutem Architekturverständnis soll strukturiert in `kauth_workflow` hineinwachsen, ohne zu früh in die falschen Stellen zu schneiden.

## Vorbemerkung

Für dieses Projekt ist nicht die Sprache die größte Hürde, sondern:

- das Zusammenspiel von Altlogik und Zielarchitektur
- die Trennung von fachlicher Domäne und technischer Umsetzung
- die Doku-Hierarchie
- die Disziplin, Änderungen nur in klaren Slices zu machen

Die Person muss also nicht zuerst "perfekt C#" können.
Sie muss zuerst verstehen:

1. Was das Produkt werden soll
2. Wo die aktuelle Codebase noch historisch gewachsen ist
3. Welche Dokumente die Wahrheit tragen
4. Wie ein sicherer kleiner Änderungsschnitt aussieht

---

## Realistische Erwartung

### Nach 2 bis 3 Tagen

Machbar:
- Repo-Struktur verstehen
- Entwicklungsumgebung lokal starten
- einfache Endpunkte und Datenflüsse lesen
- kleine Doku- oder Teständerungen verstehen

Noch nicht realistisch:
- sichere Änderungen an Runtime, Rotation, Auth oder Workflow-Validierung ohne Führung

### Nach 1 bis 2 Wochen

Machbar:
- kleinere Backend-Tasks
- klar abgegrenzte Testanpassungen
- kleinere Frontend- oder Endpoint-Änderungen
- Review-Befunde im Code wiederfinden

### Nach 3 bis 6 Wochen

Erst dann realistisch:
- eigenständige Arbeit an sensiblen Architekturstellen
- sichere Refactors in Runtime-/Lifecycle-/Repository-Bereichen
- gute Entscheidungen über Migrationsschnitte

---

## 14-Tage-Plan

## Phase 1 — Landkarte und Sprache (Tag 1 bis 3)

### Tag 1

Lesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `PROJECT_STRUCTURE.md`
- `KauthWorkflow/Lernen/01-Die-grosse-Landkarte.md`
- `KauthWorkflow/Lernen/03-Csharp-Syntax.md`

Ziel:
- verstehen, wie Frontend, Backend und DB zusammenhängen
- die Begriffe Endpoint, Service, Repository sauber unterscheiden
- die Root-Dokumente als Navigationssystem verstehen

Erfolgskriterium:
- die Person kann den Request-Fluss vom Browser bis zur DB in eigenen Worten erklären

### Tag 2

Lesen:
- `KauthWorkflow/Architektur/Zielarchitektur.md`
- `KauthWorkflow/Architektur/Entscheidungen.md`
- `KauthWorkflow/Domäne/Workflow.md`
- `KauthWorkflow/Domäne/Identity.md`
- `KauthWorkflow/Domäne/Rotation.md`

Ziel:
- verstehen, warum das Projekt von einem Onboarding-Tool zu einer Workflow-Plattform migriert
- Rollen, Responsibilities, Workflow-Definition, Runtime und Automation sauber auseinanderhalten

Erfolgskriterium:
- die Person kann erklären, warum "Onboarding ist nur ein Workflow" eine Kernregel ist

### Tag 3

Praktisch:
- Backend starten
- Frontend starten
- DB-Verbindung nachvollziehen
- 2 bis 3 echte Flows in der Anwendung klicken

Lesen:
- `KauthWorkflow/Betrieb/Setup.md`
- `web/README.md`

Ziel:
- lokale Entwicklungsumgebung sicher starten koennen
- Frontend- und Backend-Einstiegspunkte kennen

Erfolgskriterium:
- die Person bekommt lokal einen funktionierenden End-to-End-Flow hoch

---

## Phase 2 — Lesen statt sofort ändern (Tag 4 bis 6)

### Tag 4

Lesen:
- `CODE_REVIEW.md`
- `TODO.md`
- `KauthWorkflow/Stand/Code-Review-Status.md`

Ziel:
- verstehen, welche Probleme fachlich wichtig sind und welche nur technisch unschön
- sehen, wie Review-Befunde in konkrete Slices übersetzt werden

Erfolgskriterium:
- die Person kann 3 aktuelle technische Risiken benennen und einordnen

### Tag 5

Lesen im Code:
- ein einfacher Endpoint
- der zugehörige Service
- das passende Repository
- die dazugehörigen Tests

Empfohlene Lernfrage:
- Wo wird nur transportiert?
- Wo steckt Fachlogik?
- Wo passiert SQL?

Ziel:
- den Architekturpfad im echten Code wiedererkennen

### Tag 6

Lesen:
- `KauthWorkflow/Lernen/02-Anfrage-durch-den-Code.md`
- `KauthWorkflow/Lernen/02a-Lambda-Interface-DI.md`
- `KauthWorkflow/Lernen/04-Frontend-Tour.md`

Ziel:
- DI, Endpoint-Mapping und grundlegenden Frontend-Datenfluss verstehen

Erfolgskriterium:
- die Person kann einen Endpunkt und seinen Verdrahtungsweg im Code verfolgen

---

## Phase 3 — Sicher üben außerhalb des Hauptprojekts (Tag 7 bis 8)

### Tag 7 bis 8

Arbeiten mit:
- `KauthWorkflow/Lernen/06-Mini-Projekt-Todo-Board.md`

Ziel:
- dieselbe Schichtenlogik in klein einmal selbst ändern
- nicht nur lesen, sondern bewusst Endpoint → Service → Repository nachvollziehen

Konkrete Übung:
- `Todo als erledigt markieren`

Erfolgskriterium:
- die Person baut den kleinen Flow selbst und kann jede Schicht erklären

---

## Phase 4 — Erste echte Aufgaben im Repo (Tag 9 bis 11)

### Geeignete erste Aufgaben

- Doku-Korrekturen
- kleine Testanpassungen
- kleine Read-Only-Endpunkte verstehen oder leicht erweitern
- harmlose UI-Text-/Anzeigeänderungen
- kleine Refactors ohne Architekturwirkung

### Noch vermeiden

- Runtime-/Lifecycle-Logik
- Rotation-Task-Routing
- Auth-/Permission-Entscheidungen
- Repository-Großumbauten
- Migrations- oder Validierungs-Refactors ohne enge Führung

Ziel:
- erste echte Änderungen im Hauptrepo machen, aber nur in Bereichen mit kleinem Blast Radius

---

## Phase 5 — Geführte Backend-Arbeit (Tag 12 bis 14)

Jetzt erst:
- eine kleine Backend-Aufgabe mit Review-Befund wählen
- betroffene Doku vorher lesen
- Änderung klein halten
- Tests selbst ausführen
- erklären, warum der Schnitt klein genug war

Gute Zielaufgaben:
- Testlücke schließen
- kleine Validierungsregel ergänzen
- harmlosen Service-Split vorbereiten
- kleine Query-/Read-Verbesserung

Ziel:
- zeigen, dass nicht nur Code geschrieben, sondern sicher im Projektkontext gearbeitet wird

---

## Was die Person am Ende können sollte

Nach diesem Pfad sollte die Person:

- den Projektzweck erklären können
- die Doku-Hierarchie kennen
- Frontend, Backend und DB im Repo finden
- den Unterschied zwischen Zielarchitektur und Altlogik verstehen
- kleine Änderungen mit Tests und Doku sauber ausführen können
- wissen, welche Bereiche noch zu gefährlich für Solo-Arbeit sind

---

## Warnsignale in der Einarbeitung

Wenn eine Person früh solche Muster zeigt, ist sie noch nicht sicher genug für kritische Bereiche:

- sie liest `CODE_REVIEW.md` und `DOCS_CONTROL.md` nicht mit
- sie behandelt Repositories als Ort für beliebige Fachlogik
- sie macht große Refactors ohne klaren Slice
- sie ändert Runtime- oder Auth-Bereiche, ohne Tests mitzuziehen
- sie ignoriert die Zielarchitektur und erweitert Altlogik einfach weiter

---

## Empfohlene erste Verantwortungsstufe

Zuerst gut geeignet:
- Doku
- Tests
- kleine Services
- klar abgegrenzte Endpunkte
- harmlose Frontend-Anpassungen

Erst später:
- Runtime
- Rotation
- Workflow-Validierung
- Auth / Permissions
- große Repository-Splits

---

## Verwandte Einheiten

- [[01-Die-grosse-Landkarte]]
- [[02-Anfrage-durch-den-Code]]
- [[03-Csharp-Syntax]]
- [[04-Frontend-Tour]]
- [[06-Mini-Projekt-Todo-Board]]
