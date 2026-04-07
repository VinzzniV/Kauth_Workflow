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

Wichtige Diagnose:
- Das Problem ist nicht nur "zu viel hardcoded".
- Das aktuelle Kernmodell ist fuer das Produktziel zu eng und noch zu task-generator-lastig.

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
- `Workflow_Plattform_Implementation_Plan.md` = zentrale Umsetzungsanweisung fuer die Migration
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` = stabiles Sollbild der Plattform
- `DECISIONS.md` = langfristige Architekturentscheidungen
- `MEMORY.md` = kurzfristiger Session-Kontext
- `TODO.md` = priorisierte Arbeitspakete entlang der Migrationsphasen
