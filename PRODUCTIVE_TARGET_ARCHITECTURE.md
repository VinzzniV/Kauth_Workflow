# Produktive Zielarchitektur

Dieses Dokument beschreibt das stabile Sollbild fuer den Ausbau zur internen Workflow-Plattform.
Die operative Umsetzungsreihenfolge steht in `Workflow_Plattform_Implementation_Plan.md`.

## Statusbild April 2026

Bereits sichtbar:
- Backend, Frontend und PostgreSQL als tragfaehige Produktbasis
- Workflow-Instanzen, Tasks, Kommentare, Deadlines, Audit und Admin-Konfiguration
- Directory-/Identity-Richtung mit Entra, Gruppen-Mapping und lokaler Responsibility-Logik
- mehrere Prozessarten und konfigurierbare Formular-/Task-Bausteine
- erste business-phasenbasierte Definitionen fuer `onboarding`, `offboarding` und `department_change`

Noch nicht im Zielbild:
- expliziter Definition Layer mit Versionierung
- eigenstaendige Runtime-/Orchestrierungsschicht
- Automation Layer mit kontrollierten Actions und Jobs
- Guided Builder fuer neue Workflow-Definitionen

---

## 1. Produktziel

Die Anwendung soll produktiv als versionierte, kontrollierte Workflow-Plattform betrieben werden.

Beispiele fuer Workflows:
- Onboarding
- Offboarding
- Department Change
- Rollenwechsel
- Namensaenderung
- weitere interne Genehmigungs- und Lifecycle-Prozesse

Ein Nicht-Entwickler soll fachliche Workflows aus sicheren Bausteinen konfigurieren koennen, ohne Quellcode zu aendern.

---

## 2. Architektur-Schichten

### Fachobjekte

Relativ stabile Kernobjekte:
- Person
- technische Identity
- Department
- Responsibility
- Workflow Instance
- Task
- Kommentar
- Notification
- Audit

### Workflow Definition Layer

Neue fachliche Quelle des Ablaufs:
- `workflow_definitions`
- `workflow_definition_versions`
- `workflow_nodes`
- `workflow_edges`
- `workflow_node_configs`

### Runtime / Orchestrierung

Fuehrt Definitionen aus:
- aktiviert Nodes
- wertet Entscheidungen aus
- startet Tasks, Benachrichtigungen und Automationen
- protokolliert Runtime-Ereignisse
- behandelt Fehler- und Abbruchpfade

Fachliche Guardrails im sichtbaren Flow:
- Hauptfluesse bleiben auf fachliche Phasen verdichtet
- requirement-/approval-Gates kommen vor jeder Fachaufgabengenerierung
- `setup`-Phasen koennen interne bedingte Legacy-Tasklogik kapseln, ohne die Hauptdarstellung technisch aufzubrechen

### Task-System

Bleibt eigenstaendig:
- Human Tasks
- Kommentare
- Deadlines
- Status
- Eskalation
- Zuweisung

### Automation Layer

Strikt kontrolliertes Integrationssystem:
- `action_definitions`
- `workflow_node_actions`
- `automation_jobs`
- `automation_job_attempts`
- `automation_job_logs`

### Admin / Builder Layer

Zunaechst als Guided Builder:
- Definition anlegen
- Version erstellen
- Nodes und Edges pflegen
- Konfiguration validieren
- veroeffentlichen

---

## 3. Zentrale Prinzipien

- Workflow-Definition ist nicht gleich Task-Generierung.
- Versionierung ist Pflicht.
- Migration erfolgt parallel zur Altwelt.
- Actions sind Produktelemente, keine losen Skripte.
- Nicht-Entwickler konfigurieren fachlich, nicht technisch frei.
- Onboarding darf kein versteckter Produktkern bleiben.

---

## 4. Runtime-Zielbild

`workflow_instances` referenzieren eine Definition-Version.
Neue Runtime-Objekte:
- `workflow_node_instances`
- `workflow_runtime_events`

Minimal zu tragende Node-Typen:
- `start`
- `form`
- `approval`
- `setup`
- `task`
- `decision`
- `parallel_split`
- `parallel_join`
- `automation`
- `end`

Spaeter ausbaubar um:
- `wait`
- `notification`
- `subworkflow`

Business-Phase-Muster fuer Employee-Lifecycle-Prozesse:
- `start`
- `form` zum Erfassen der Anforderungen
- optionale `approval` als fachlicher Gatekeeper
- `setup` als Sammelblock fuer parallel erzeugte Fachaufgaben
- `end` erst nach Abschluss aller Pflichtaufgaben

---

## 5. Identity- und Betriebsmodell

Diese Richtung bleibt bestehen:
- AD bzw. Entra liefert technische Identitaet
- das Backend validiert Auth und bleibt Source of Truth
- Rollen = Zugriff, Responsibilities = fachliche Ownership
- Person und technische Identity bleiben getrennte Konzepte
- produktive Secrets und Runtime-Konfiguration kommen aus sicheren Laufzeitmechanismen

---

## 6. Migrationspfad

1. Artefakt- und Secret-Hygiene
2. Produktkern ent-onboarden
3. Zielarchitektur dokumentieren und Begriffe harmonisieren
4. Definition Layer einfuehren
5. Runtime parallel einfuehren
6. bestehende Workflows mappen
7. Task-System an Node-Runtime anbinden
8. generische Validierung einfuehren
9. Automation Layer bauen
10. Guided Builder ausbauen
11. Altwelt gezielt zurueckbauen

---

## 7. Klare Entscheidungen

- Die App entwickelt sich zu einer Workflow-Plattform, nicht zu einem groesseren Spezial-Onboarding-Tool.
- Das bestehende Fachwissen in Templates, Conditions und Dependencies wird migriert, nicht weggeworfen.
- Der Plattformkern wird versioniert, kontrolliert und validierbar.
- Sicherheit und Kontrollierbarkeit gehen vor maximaler Flexibilitaet.
