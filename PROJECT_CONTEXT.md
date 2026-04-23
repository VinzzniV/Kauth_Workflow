# PROJECT_CONTEXT.md

## Ziel

Das Projekt entwickelt sich von einem konfigurierbaren Employee-Lifecycle-Tool zu einer versionierten internen Workflow-Plattform.

Kurzfristig wichtig:
- bestehende Workflows stabil halten
- Produktkern von onboarding-spezifischen Annahmen entkoppeln
- Definition Layer und Runtime parallel zur Altwelt einfuehren

Langfristig wichtig:
- neue Workflows ohne Quellcodeaenderung aus sicheren Bausteinen konfigurieren
- Human Tasks, Entscheidungen, Benachrichtigungen und kontrollierte Automationen in einem Plattformkern abbilden

---

## Aktueller Architekturstand

Bereits vorhanden:
- Workflow-Instanzen
- Tasks, Assignments, Kommentare, Deadlines, Audit
- formular- und antwortgetriebene Konfiguration
- Conditions, Dependencies, Task Templates
- mehrere Prozessarten
- Admin-Konfiguration, Directory-Sync, Rollen-/Responsibility-Modell

Noch nicht im Zielbild:
- versionierte Workflow-Definitionen als eigener Kern
- eigenstaendige Runtime-/Orchestrierungsschicht
- kontrollierter Automation Layer als eigenes Subsystem
- generischer Builder fuer Workflow-Definitionen
- explizit transaktionale und skalierbare Rotation-Task-Generierung

Wichtige Diagnose:
- Das Problem ist nicht nur "zu viel hardcoded".
- Das aktuelle Kernmodell ist fuer das Produktziel zu eng und noch zu task-generator-lastig.
- Die aktuelle CodeReview benennt zusaetzlich das monolithische Repository, fehlende Transaktionsgrenzen, in-memory Task-Filter und unvollstaendige Validierungen als wichtigste technische Risiken.

---

## Zielkern

Die Zielarchitektur besteht aus diesen Schichten:

- Fachobjekte: Person, technische Identity, Department, Responsibility, Workflow Instance, Task, Kommentar, Notification, Audit
- Workflow Definition Layer: Definitionen, Versionen, Nodes, Edges, Node-Configs
- Runtime Layer: aktiviert Nodes, fuehrt Definitionen aus, erzeugt Laufzeiteffekte, behandelt Fehlerpfade
- Task-System: Human Tasks, Kommentare, Fristen, Status, Eskalation, Zuweisung
- Automation Layer: Action Definitions, Jobs, Worker, Logging, Retry, Idempotenz
- Admin / Builder Layer: Definition anlegen, konfigurieren, validieren, veroeffentlichen

---

## Kritische Regeln

### Backend ist Source of Truth
- Geschaeftslogik muss im Backend korrekt sein.
- Das Frontend darf Regeln nicht neu definieren.

### Rollen und Verantwortlichkeiten bleiben getrennt
- Rollen = Zugriff
- Responsibilities = fachliche Ownership
- Niemals vermischen

### Assignment bleibt strikt
- `user` = genau dieser Benutzer
- `responsibility` = geteilte fachliche Zustaendigkeit
- Keine impliziten Abkuerzungen

### Workflow-Definition ist nicht gleich Task-Generierung
- Tasks sind nur eine moegliche Laufzeitwirkung.
- Die Engine darf nicht weiter nur ein Task-Generator sein.

### Fachliche Hauptfluesse bleiben phasenbasiert
- Reale Lifecycle-Workflows werden im sichtbaren Hauptfluss als Phasen modelliert, nicht als Kette einzelner Boolean-Entscheidungen.
- In Phase A werden fuer `onboarding`, `offboarding` und `department_change` fachliche Maßnahmen-Nodes genutzt: `measure_provision`, `measure_deprovision`, `measure_change`.
- In Phase C nutzen auch `name_change`, `position_change` und `role_change` fachliche Maßnahmen-Nodes: `measure_rename` fuer Namensaenderung sowie `measure_change` fuer Positions- und Rollenwechsel.
- Der sichtbare Hauptpfad dieser Measure-Flows bleibt auf `start -> form -> optionale approval -> maßnahmen-block -> end` begrenzt.
- `setup` bleibt nur als expliziter Legacy-Alias kompatibel und darf nicht mehr als neuer Standard-Baustein angeboten werden.
- Sichtbare Builder-Phasen muessen fuer Fachanwender lesbar bleiben; technische Bedingungen, `answerKey`s und Operatoren gehoeren nicht in die Hauptdarstellung.
- In Phase B ist der Inspector fuer Measure-Flows ein Builder-lokaler Kontext-Hub: direkt konfigurierbare Node-Felder bleiben editierbar, abgeleitete Templates/Bedingungen/Abhaengigkeiten werden vollstaendig im Builder gezeigt und gezielt im selben Sidebar-Kontext bearbeitet.
- Quellenbearbeitung fuer Prozessart, Antwortfelder, Aufgabenvorlagen, Bedingungen und Abhaengigkeiten darf den Builder-Kontext nicht verlassen; keine Bereichswechsel nur zum Verstehen oder Nachpflegen einer referenzierten Quelle.
- Gleicher Maßnahmen-Typ bedeutet nicht automatisch gleiche Fachbedeutung: `measure_change` muss im Builder fuer `position_change` und `role_change` prozessspezifisch erklaert werden.

### Anforderungen sind der Gatekeeper fuer Fachaufgaben
- Vor dem Erfassen der Anforderungen duerfen keine Fachbereichsaufgaben entstehen.
- Die Aufgabengenerierung startet erst nach dem Gatekeeper-Schritt und optionaler Freigabe.
- Fachaufgaben duerfen intern weiter bedingungsgetrieben entstehen; im Builder werden sie hinter einem fachlichen Maßnahmen-Baustein zusammengefasst.

### Aufgabenstatus steuern den Workflow-Fortschritt
- Jede erzeugte Aufgabe braucht eine klare Zuweisung (`user` oder `responsibility`) und einen Status.
- Der Gesamtstatus eines Workflows wird aus den relevanten Aufgaben abgeleitet, nicht nur aus sichtbaren Canvas-Nodes.
- Der aktuelle Verantwortliche und der Fortschritt muessen aus Aufgaben und ihren offenen Bereichen bestimmbar sein.

### Abteilungsleiter-Sicht bleibt fachlich abteilungsgebunden
- `auth_manager` oder globale `workflows.view_department`-/`users.view_department`-Wirkung duerfen Workflow-Sicht nicht abteilungsuebergreifend oeffnen.
- Fuer Workflow-Listen, Details, verknuepfte Workflows und verwandte Suchpfade zaehlen fuer Abteilungsleitungen nur explizit aufgeloeste beobachtbare Abteilungen.
- Diese beobachtbaren Abteilungen kommen aus `department_settings`, `department_lead`-Responsibilities und ggf. department-scoped Permission Grants, nicht aus einer bloss globalen Manager-Rolle.

### Entra fuehrt die Abteilungsleitung in `department_settings`
- Bei jedem Directory-Sync wird pro Abteilung aus Entra-synchronisierten `auth_manager`-Benutzern mit synchronisierter Abteilung genau eine eindeutige Abteilungsleitung gesucht.
- Genau ein Treffer setzt `department_lead_person_id` und `requirement_approver_person_id` auf dieselbe Person.
- Kein Treffer oder mehrere Treffer leeren beide Felder; Konflikte und automatische Aenderungen werden im Directory-Audit protokolliert.
- Die Admin-Ansicht fuer Abteilungen zeigt dazu Quelle, Sync-Status und Detailhinweise (`entra_managed` / `manual`, `resolved` / `missing` / `conflict`).

### Versionierung ist Pflicht
- Laufende Instanzen muessen auf einer festen Definition-Version weiterlaufen.
- Admin-Aenderungen duerfen laufende Instanzen nicht zerstoeren.

### Automationen bleiben kontrolliert
- Keine freie PowerShell
- Kein freies SQL
- Keine beliebigen HTTP-Requests mit Secrets aus dem Admin-UI
- Nur freigegebene, validierte Actions

### Migration statt Big Bang
- Bestehende Prozesse bleiben zunaechst lauffaehig.
- Neue Architektur wird parallel eingefuehrt.
- Altlogik erst nach Paritaet zurueckbauen.

---

## Richtung

Wir bewegen uns in diese Richtung:
- weniger implizite Prozesslogik
- mehr versionierte Definitionen
- generische Runtime statt Prozessart-Spezialfaelle
- UI-gestuetzte fachliche Konfiguration mit Guardrails
- kontrollierte technische Integrationen
- mehrere Workflows auf einem gemeinsamen Plattformkern

---

## Nicht tun

- neue Prozessarten primaer per Seed-SQL weiterbauen
- onboarding-spezifische Kernlogik weiter aufblasen
- freie technische Aktionen fuer Admins anbieten
- Business-Regeln ins Frontend verschieben
- Altlogik voreilig loeschen
- grosse Refactors ohne klaren Migrationsschnitt machen

---

## Arbeitsdokumente

- `DOCS_CONTROL.md` = Lesereihenfolge, Schreibziele, Doku-Hygiene
- `CODE_REVIEW.md` = aktuelle CodeReview und priorisierte Nacharbeit
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` = stabiles Sollbild der Plattform
- `DECISIONS.md` = langfristige Architekturentscheidungen
- `MEMORY.md` = kurzfristiger Session-Kontext
- `TODO.md` = priorisierte Arbeitspakete fuer das Rotations-/Durchlauf-Feature
