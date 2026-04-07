# Workflow Platform Migration Plan

## Zweck dieser Datei

Diese Datei ist die **zentrale Umsetzungsanweisung für eine KI in der IDE** (z. B. Codex oder Claude), damit aus dem bestehenden Projekt eine **konfigurierbare interne Workflow-Plattform** entsteht.

Ziel ist **nicht** nur weiteres Onboarding/Offboarding per Speziallogik zu ergänzen, sondern das System sauber von einem **datengetriebenen Lifecycle-Tool** zu einer **versionierten Workflow- und Automationsplattform** weiterzuentwickeln.

Diese Datei soll von der KI als **Arbeitsvertrag** verstanden werden:

* Was ist das Ziel?
* Was ist der aktuelle Architekturfehler?
* Was darf erhalten bleiben?
* Was muss umgebaut werden?
* In welcher Reihenfolge?
* Mit welchem Arbeitsmodus?

---

# 1. Zielbild

## Produktziel

Das bestehende Projekt soll zu einer Plattform weiterentwickelt werden, mit der ein Unternehmen interne Prozesse wie z. B. folgende Workflows umsetzen kann:

* Onboarding
* Offboarding
* Abteilungswechsel
* Rollenwechsel
* Namensänderung
* Änderungen an Mitarbeiterdaten
* spätere weitere interne Freigabe- und Lifecycle-Prozesse

## Fachliche Anforderungen

Die Plattform soll unterstützen:

* Starten von Workflows
* Aufgaben für Personen / Rollen / Abteilungen
* Kommentare auf Aufgaben
* Deadlines
* Statuswechsel
* Historie / Audit
* Mailversand / Benachrichtigungen
* Genehmigungen
* Bedingungen / Verzweigungen
* kontrollierte Hintergrund-Automationen

## Langfristiges Ziel

Ein Nicht-Entwickler soll **ohne Quellcodeänderung** neue Workflows aus sicheren, vordefinierten Bausteinen konfigurieren können.

Wichtig:
Das bedeutet **nicht**, dass Nicht-Entwickler beliebige Skripte oder beliebige technische Aktionen bauen dürfen.

Das Ziel ist:

* **fachliche Konfiguration no-code / low-code**
* **technische Ausführung streng kontrolliert und gekapselt**

---

# 2. Wichtiger Architekturkontext aus dem bestehenden Projekt

## Ist-Zustand des Projekts

Das bestehende Projekt ist bereits mehr als ein Prototyp. Es hat schon:

* Workflow-Instanzen
* Aufgaben
* Kommentare
* Deadlines
* Bedingungen
* Abhängigkeiten
* Task Templates
* mehrere Prozessarten
* Admin-Konfiguration
* Audit-/Notification-Richtung

## Hauptproblem

Das aktuelle System ist **kein echter generischer Workflow-Baukasten**, sondern ein **konfigurierbarer Spezialworkflow-Kern**.

Im Kern ist die Architektur aktuell eher:

* Prozessart anlegen
* Antworten/Felder definieren
* Task Templates definieren
* Bedingungen / Dependencies definieren
* Aufgaben generieren
* Statuslogik steuert den Ablauf

Das reicht für feste Lifecycle-Prozesse.
Es reicht **nicht** für eine generische Workflow-Plattform.

## Wichtigste Diagnose

Der aktuelle Fehler ist **nicht einfach „zu viel hardcoded“**.
Der eigentliche Fehler ist:

> Die Produktidee ist größer geworden als das aktuelle Kernmodell.

Die aktuelle Engine ist noch zu stark auf ein festes Ablaufmuster zugeschnitten.

---

# 3. Was erhalten bleiben soll

Die KI soll diese Teile **nicht unnötig zerstören oder neu erfinden**:

## Behalten

* Workflow / Task / Assignment / Comment / Audit als Grundkonzepte
* datengetriebene Answer Definitions / Formularlogik
* Task Templates als fachliche Quelle bestehender Prozesse
* Conditions / Dependencies als fachliches Wissen
* Rollen vs Verantwortlichkeiten
* Person-/Identity-Trennung als Zielrichtung
* Backend als Source of Truth
* bestehende Tests als Sicherheitsnetz
* vorhandene Admin-Konfigurationsidee

## Nicht weiter als Zielkern aufblähen

* feste Statusmaschine als Hauptmodell
* supervisor-first / department-first als impliziter Standardablauf
* Prozessarten primär per Seed-SQL weiterentwickeln
* Prozesslogik über immer mehr Spezialfälle im Code ergänzen
* Onboarding als versteckter Produktkern

---

# 4. Soll-Architektur

Die KI soll die Zielarchitektur in folgende Schichten aufteilen.

## A. Fachobjekte

Bleiben relativ stabil:

* Person
* technische Identity
* Department
* Responsibility
* Workflow Instance
* Task
* Kommentar
* Notification
* Audit

## B. Workflow Definition Layer

Neu und zentral:

* Workflow Definition
* Workflow Definition Version
* Workflow Nodes
* Workflow Edges
* Node Config

## C. Runtime / Orchestrierung

Neu und eigenständig:

* führt Definitionen aus
* aktiviert Nodes
* erzeugt Tasks
* wertet Entscheidungen aus
* startet Automationen
* behandelt Fehlerpfade

## D. Task-System

Bleibt eigenständig:

* Human Tasks
* Kommentare
* Fristen
* Status
* Eskalation
* Zuweisung

## E. Automation Layer

Neu und strikt kontrolliert:

* Action Definitions
* Worker / Jobs
* technische Integrationen
* Logging / Retry / Idempotenz

## F. Admin / Builder Layer

Später ausbaubar:

* Workflow-Vorlagen
* Definitionen anlegen
* Schritte konfigurieren
* Bedingungen setzen
* veröffentlichen

---

# 5. Zentrale Architekturprinzipien

Diese Regeln sind verbindlich.

## Prinzip 1: Keine freie technische Magie im Designer

Nicht-Entwickler dürfen:

* Schritte konfigurieren
* Zuständigkeiten festlegen
* Mails konfigurieren
* Bedingungen setzen
* Fristen definieren
* freigegebene Actions auswählen

Nicht-Entwickler dürfen **nicht**:

* PowerShell frei hinterlegen
* SQL frei definieren
* beliebige HTTP-Requests mit Secrets bauen
* technische Seiteneffekte ohne Guardrails erzeugen

## Prinzip 2: Workflow-Definition ist nicht gleich Task-Generierung

Tasks sind nur **eine Art von Laufzeitwirkung** eines Workflows.
Die Engine darf nicht weiter nur ein Task-Generator bleiben.

## Prinzip 3: Versionierung ist Pflicht

Laufende Workflow-Instanzen dürfen nicht durch spätere Admin-Änderungen zerstört werden.
Darum brauchen Workflow-Definitionen Versionen.

## Prinzip 4: Technische Actions sind Produktelemente

Actions wie `CreateAdUser`, `CreateMailbox`, `AssignGroups`, `CreateErpEmployee` sind **eigene kontrollierte Bausteine**, keine lose Scripting-Funktion.

## Prinzip 5: Migration statt Big Bang

Das bestehende System wird nicht weggeworfen.
Neue Architektur wird parallel eingeführt und alte Prozesse werden schrittweise migriert.

---

# 6. Ziel-Datenmodell

Die KI soll dieses Modell einführen.

## 6.1 Workflow Definition Layer

### `workflow_definitions`

* `id`
* `key`
* `name`
* `description`
* `category`
* `is_active`

### `workflow_definition_versions`

* `id`
* `workflow_definition_id`
* `version_number`
* `status` (`draft`, `published`, `retired`)
* `created_at`
* `published_at`

### `workflow_nodes`

* `id`
* `workflow_definition_version_id`
* `node_key`
* `node_type`
* `title`
* `description`
* `position_x`
* `position_y`
* `is_start`
* `is_end`

### `workflow_edges`

* `id`
* `workflow_definition_version_id`
* `from_node_id`
* `to_node_id`
* `condition_type`
* `condition_expression`
* `priority`

### `workflow_node_configs`

* `id`
* `node_id`
* `config_json`

Hinweis:
`config_json` ist erlaubt, aber der Inhalt muss strikt nach `node_type` validiert werden.

## 6.2 Runtime Layer

### `workflow_instances`

Bestehende Tabelle erweitern um:

* `workflow_definition_version_id`
* `current_runtime_status`
* `started_at`
* `completed_at`

### `workflow_node_instances`

* `id`
* `workflow_instance_id`
* `node_id`
* `status` (`pending`, `active`, `done`, `failed`, `cancelled`)
* `started_at`
* `completed_at`
* `result_json`

### `workflow_runtime_events`

* `id`
* `workflow_instance_id`
* `node_instance_id`
* `event_type`
* `payload_json`
* `created_at`

## 6.3 Task Layer

Bestehende Task-Tabellen weiterverwenden, aber künftig mit Bezug zu:

* `node_instance_id`

## 6.4 Automation Layer

### `action_definitions`

* `id`
* `key`
* `name`
* `description`
* `handler_type`
* `parameter_schema_json`
* `is_active`
* `requires_approval`
* `is_idempotent`

### `workflow_node_actions`

* `id`
* `node_id`
* `action_definition_id`
* `input_mapping_json`
* `on_error_behavior`

### `automation_jobs`

* `id`
* `workflow_instance_id`
* `node_instance_id`
* `action_definition_id`
* `status`
* `payload_json`
* `created_at`
* `started_at`
* `completed_at`

### `automation_job_attempts`

* `id`
* `automation_job_id`
* `attempt_number`
* `status`
* `error_message`
* `started_at`
* `completed_at`

### `automation_job_logs`

* `id`
* `automation_job_id`
* `level`
* `message`
* `details_json`
* `created_at`

---

# 7. Ziel-Node-Typen

Die KI soll zunächst nur einen kleinen, tragfähigen Satz umsetzen.

## Minimaler erster Satz

* `start`
* `form`
* `approval`
* `task`
* `decision`
* `end`

## Zweite Ausbaustufe

* `parallel_split`
* `parallel_join`
* `wait`
* `notification`
* `automation`
* `subworkflow`

## Node-Bedeutung

### `form`

Zeigt Eingabefelder an, speichert Antworten.

### `approval`

Genehmigung durch eine definierte Rolle / Responsibility / Manager.

### `task`

Normale Human Task.

### `decision`

Wertet Daten aus und entscheidet über den nächsten Pfad.

### `notification`

Versendet Mail / interne Benachrichtigung.

### `automation`

Startet eine kontrollierte technische Action.

---

# 8. Migrationsstrategie

Wichtig: Kein Big Bang.

## Ziel

* bestehende Prozesse bleiben möglichst lauffähig
* neues Architekturmodell wird parallel aufgebaut
* alte Prozesse werden schrittweise migriert

## Reihenfolge

1. Artefakt- und Secret-Hygiene
2. Produktkern ent-onboarden
3. Definition Layer einführen
4. Runtime parallel einführen
5. bestehende Prozesse mappen
6. Automation Layer bauen
7. Guided Builder einführen

---

# 9. Arbeitsmodus für die KI in der IDE

## Allgemeine Regeln

* Keine großflächigen spontanen Refactorings ohne Plan
* Erst Modell und Grenzen festziehen, dann implementieren
* Keine kosmetischen Umbauten priorisieren
* Alte und neue Architektur zunächst parallel halten
* Jede Änderung auf Zielarchitektur prüfen
* Keine neue Speziallogik für einzelne Prozessarten ergänzen, wenn sie das Kernmodell weiter verengt

## Wann Plan Mode AN sein soll

Plan Mode soll **AN** sein bei:

* neuen Datenmodellen
* Migrationsschritten
* Architekturentscheidungen
* Änderungen an Runtime / Orchestrierung
* Eingriffen in bestehende Kern-Workflows
* Änderungen mit Frontend + Backend + DB gleichzeitig

## Wann Plan Mode AUS sein kann

Plan Mode kann **AUS** sein bei:

* klar abgegrenzten Einzeldateiänderungen
* kleinen API-Erweiterungen
* UI-Anpassungen ohne Architekturwirkung
* Tests ergänzen
* kleine Validierungen / Mapping-Fixes

## Reasoning Effort Leitlinie

* **High**: Architektur, Datenmodell, Runtime, Migration
* **Medium**: modulare API-/Service-Implementierungen
* **Low**: UI-Feinschliff, Tests, kleine Refactors ohne Architekturwirkung

---

# 10. Konkreter Umsetzungsplan für die KI

## Phase 0 — Repository- und Sicherheitsbereinigung

### Ziel

Saubere Grundlage schaffen. Keine Architekturarbeit auf unsauberem Release-/Secret-Stand.

### Aufgaben

* `.git`, `node_modules`, `dist`, `bin`, `obj`, Logs und andere Build-Artefakte aus Übergaben/Exports entfernen
* produktionsnahe `.env`-Dateien prüfen und durch sichere Beispiel-/Template-Dateien ersetzen
* `.gitignore` / Exportprozess prüfen und härten
* Dokumentation ergänzen, wie lokale/dev/prod-Konfiguration sauber getrennt wird

### Deliverables

* bereinigtes Repo/ZIP
* dokumentierte Environment-Strategie
* aktualisierte Ignore-/Packaging-Regeln

### Reasoning effort

**Medium**

### Plan mode

**AN**, falls Packaging-/Repo-Struktur angepasst wird; sonst **AUS** bei rein lokalen Aufräumarbeiten

### Hinweise für die KI

Nicht in Kosmetik verlieren. Ziel ist operative Sicherheit und saubere Grundlage.

---

## Phase 1 — Produktkern ent-onboarden

### Ziel

Das Produkt darf intern nicht mehr architektonisch so tun, als sei Onboarding der einzige oder primäre Workflow.

### Aufgaben

* alle produktkernnahen `Onboarding*`-Namen identifizieren
* unterscheiden zwischen:

  * rein historischer Benennung
  * echter fachlicher Onboarding-Kopplung
* zentrale Extensions / Startup-Validierung / Routing / fachliche Services schrittweise neutralisieren
* feste Prozessschlüssel und onboadingspezifische Sonderpfade abbauen
* Zielpersonenlogik generischer machen
* Benachrichtigungspfad nicht mehr auf Onboarding-Fachbegriffe fest verdrahten

### Deliverables

* Liste aller Onboarding-Kopplungen
* priorisierte Umbenennungs-/Entkopplungsänderungen
* erster neutraler Produktkern

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Keine blinde globale Umbenennung. Erst analysieren, dann unterscheiden zwischen Domain-Begriff und Altlast.

---

## Phase 2 — Zielarchitektur dokumentieren und im Code verankern

### Ziel

Vor Implementierung muss das neue Kernmodell explizit dokumentiert und im Projekt referenzierbar sein.

### Aufgaben

* neues Architektur-Dokument im Repo anlegen, z. B. `docs/workflow-platform-target-architecture.md`
* darin festhalten:

  * Definition Layer
  * Versionierung
  * Runtime
  * Task-System
  * Automation Layer
  * Migrationsstrategie
* bestehende Dokumente nicht zerstören, sondern bei Bedarf ergänzen/verlinken
* technische Begriffe vereinheitlichen

### Deliverables

* verbindliches Zielarchitektur-Dokument
* Glossar der neuen Kernbegriffe

### Reasoning effort

**Medium**

### Plan mode

**AN**

### Hinweise für die KI

Die Dokumentation ist kein Beiwerk. Sie dient als Referenz für alle Folgeprompts.

---

## Phase 3 — Workflow Definition Layer einführen

### Ziel

Explizites Modell für versionierte Workflow-Definitionen schaffen.

### Aufgaben

* DB-Migrationen für folgende Tabellen einführen:

  * `workflow_definitions`
  * `workflow_definition_versions`
  * `workflow_nodes`
  * `workflow_edges`
  * `workflow_node_configs`
* Domänenmodelle / Entities / Repository-Zugriffe ergänzen
* Validierungslogik für Definitionen entwerfen
* zunächst nur Minimalmenge von Node-Typen unterstützen:

  * `start`
  * `form`
  * `approval`
  * `task`
  * `decision`
  * `end`

### Deliverables

* funktionierendes persistentes Definitionsmodell
* Validierung für Grundkonsistenz

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Nicht sofort grafischen Designer bauen. Erst tragfähiges Modell.

---

## Phase 4 — Runtime Layer parallel einführen

### Ziel

Neue Workflow-Definitionen sollen ausführbar werden, ohne die Altwelt sofort abzuschalten.

### Aufgaben

* Tabellen/Migrationen einführen:

  * `workflow_node_instances`
  * `workflow_runtime_events`
* bestehende `workflow_instances` um Bezug auf Definition/Version erweitern
* Runtime-Statusmodell definieren
* erste Orchestrierungslogik bauen:

  * Start-Node aktivieren
  * Form-/Approval-/Task-/Decision-/End-Nodes ausführen
* Fehler-/Abbruchpfade zunächst einfach halten, aber sauber modellieren
* alte und neue Ausführung zunächst parallel ermöglichen

### Deliverables

* minimal lauffähige Runtime für neue Definitionen
* Runtime-Status und Node-Status sauber getrennt

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Hier keine Abkürzungen über Task-Generator-Hacks einbauen. Das ist der eigentliche Kernumbau.

---

## Phase 5 — Bestehende Prozesse auf das neue Modell mappen

### Ziel

Vorhandenes fachliches Wissen aus Task Templates / Conditions / Dependencies in das neue Modell überführen.

### Aufgaben

* Bestandsanalyse der aktuellen Prozessarten
* Mapping-Dokument erstellen:

  * heutige Prozessart
  * heutige Templates
  * heutige Bedingungen
  * heutige Dependencies
  * Ziel-Nodes / Edges / Decisions
* zuerst folgende drei Workflows mappen:

  * Onboarding
  * Offboarding
  * Department Change
* Migrationsstrategie pro Workflow festlegen:

  * parallel
  * read-only Altversion
  * Umschalten per Feature Flag

### Deliverables

* Mapping-Dokument
* erste Definitionen für 3 Kernworkflows

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Nicht alles gleichzeitig migrieren. Erst drei repräsentative Workflows.

---

## Phase 6 — Task-System an Node-Runtime anbinden

### Ziel

Tasks sollen künftig aus `task`- und `approval`-Nodes entstehen, nicht direkt aus Prozessart-Generatorlogik.

### Aufgaben

* Task-Erzeugung vom neuen Runtime-Layer aus anbinden
* bestehende Task-Modelle weiterverwenden, wo sinnvoll
* Referenz `node_instance_id` ergänzen
* Kommentare, Deadlines, Status, Eskalationen sauber an neue Runtime anbinden
* API und UI so anpassen, dass neue und alte Instanzen vorübergehend koexistieren können

### Deliverables

* Node-basierte Task-Erzeugung
* funktionsfähige Kommentar-/Deadline-/Status-Anbindung

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Task-System nicht neu erfinden, sondern umhängen.

---

## Phase 7 — Startup-Validierung und Admin-Validierung generisch machen

### Ziel

Kein harter Fokus mehr auf einzelne Prozesskeys wie `onboarding`.

### Aufgaben

* Startup-Validierung auf alle aktiven/publizierten Definitionen erweitern
* Konsistenzprüfungen für Nodes, Edges, Start-/End-Knoten, Node-Configs ergänzen
* Validierung für Mail-/Action-Referenzen ergänzen
* Fehlermeldungen admin-tauglich formulieren

### Deliverables

* generische Validierungslogik
* robuste Fehlermeldungen bei inkonsistenten Definitionen

### Reasoning effort

**Medium**

### Plan mode

**AN**

### Hinweise für die KI

Nicht nur technische Exceptions. Auch valide Admin-Feedbacks vorsehen.

---

## Phase 8 — Automation Layer als eigenes Subsystem bauen

### Ziel

Kontrollierte Hintergrundautomationen einführen, ohne freie Skriptwildnis.

### Aufgaben

* Tabellen/Migrationen einführen:

  * `action_definitions`
  * `workflow_node_actions`
  * `automation_jobs`
  * `automation_job_attempts`
  * `automation_job_logs`
* Action-Konzept definieren:

  * Key
  * Beschreibung
  * Handler
  * Eingabeschema
  * Idempotenz
  * Fehlerverhalten
* Worker-Architektur definieren
* erste Actions umsetzen:

  * `CreateAdUser`
  * `CreateMailbox`
  * `AssignGroups`
  * `CreateErpEmployee`
  * `SendWelcomeMail`
* Fehlerbehandlung und Retry-Regeln einführen
* keine freie PowerShell-/SQL-/HTTP-Ausführung für Admins

### Deliverables

* funktionierender Action-Katalog
* erster Worker-/Job-Lauf
* Logging und Statusrückgabe

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Das ist ein sicherheitskritischer Bereich. Guardrails sind wichtiger als Geschwindigkeit.

---

## Phase 9 — Guided Admin Builder

### Ziel

Nicht-Entwickler sollen neue Workflow-Definitionen konfigurieren können, ohne direkt Code oder SQL zu schreiben.

### Aufgaben

* keine volle BPMN-/Drag-and-Drop-Lösung als ersten Schritt
* stattdessen geführten Builder bauen:

  * Definition anlegen
  * Version erstellen
  * Nodes hinzufügen
  * Edges definieren
  * Konfiguration setzen
  * validieren
  * veröffentlichen
* adminfreundliche Formulare für Node-Typen bauen
* Vorschau / Validierungsfehler sichtbar machen

### Deliverables

* erster Guided Builder
* Publish-/Draft-Modell

### Reasoning effort

**Medium**

### Plan mode

**AN**

### Hinweise für die KI

Erst guided builder. Kein voreiliger visueller Flow-Editor.

---

## Phase 10 — Altwelt gezielt zurückbauen

### Ziel

Sobald die ersten Kernworkflows stabil im neuen Modell laufen, die alte Speziallogik schrittweise entfernen.

### Aufgaben

* alte generatorbasierte Sonderpfade identifizieren
* Prozessarten mit neuer Definition als führend markieren
* nicht mehr benötigte Speziallogik isolieren und entfernen
* Altlasten nur löschen, wenn neue Tests grün sind und produktive Parität erreicht ist

### Deliverables

* reduzierte Altlogik
* klarerer Produktkern

### Reasoning effort

**High**

### Plan mode

**AN**

### Hinweise für die KI

Keine voreilige Löschung. Erst Parität, dann Rückbau.

---

# 11. Technische Arbeitsregeln für Codex / Claude

## Vor jedem größeren Schritt

Die KI soll zuerst liefern:

1. kurze Analyse des aktuellen Stands
2. geplante Änderungen
3. Risiken / Migrationsfolgen
4. betroffene Dateien / Module

## Bei Architekturänderungen

Die KI soll zuerst:

* Zielmodell benennen
* Abgrenzung zur Altwelt erklären
* dann erst implementieren

## Bei großen Schritten

Die KI soll kleine, überprüfbare Inkremente bevorzugen:

* DB-Migration
* Domainmodell
* Repository
* Service
* API
* UI
* Tests

Nicht alles in einem Riesenpatch.

## Bei Unsicherheit

Die KI soll **nicht** schnell irgendeine Speziallogik ergänzen, sondern lieber:

* bestehendes Muster analysieren
* Zielarchitektur dagegen prüfen
* minimalen sauberen Schnitt vorschlagen

---

# 12. Definition of Done pro Phase

Eine Phase ist erst dann abgeschlossen, wenn:

* Code kompiliert / läuft
* Tests ergänzt oder angepasst wurden
* bestehendes Verhalten nicht unbeabsichtigt gebrochen wurde
* Architekturänderung dokumentiert wurde
* die Änderung zur Zielarchitektur passt

---

# 13. Empfohlene Prompt-Nutzung in der IDE

## Für Codex / Claude bei großen Schritten

Verwende diese Datei als Primärkontext und ergänze dann einen klaren Arbeitsauftrag, z. B.:

> Lies `WORKFLOW_PLATFORM_IMPLEMENTATION_PLAN.md` vollständig. Arbeite ausschließlich an Phase 3. Analysiere zuerst den aktuellen Codepfad für Prozessdefinitionen, schlage dann eine minimale Einführung des Definition Layers vor. Liefere erst einen Plan mit betroffenen Dateien, dann die Umsetzung. Plan mode an. Reasoning effort high.

## Für kleinere Schritte

> Lies `WORKFLOW_PLATFORM_IMPLEMENTATION_PLAN.md`. Arbeite nur an Phase 7: generische Startup-Validierung. Keine Architekturänderungen außerhalb dieses Scopes. Plan mode an. Reasoning effort medium.

---

# 14. Was die KI explizit vermeiden soll

* neue Prozessarten weiter primär per Seed-SQL bauen
* weitere onboadingspezifische Kernlogik ergänzen
* freie technische Aktionen im Admin-UI anbieten
* Workflow-Engine weiter nur als Task-Generator behandeln
* visuelle Designer vor dem Kernmodell priorisieren
* große unstrukturierte Refactors ohne Zwischenschritte
* Altlogik löschen, bevor neue Parität erreicht ist

---

# 15. Priorisierte nächste 3 konkreten Arbeitsaufträge

## Auftrag 1

**Architektur-Dokument final im Repo anlegen und Begriffe harmonisieren**

* Reasoning effort: **medium**
* Plan mode: **AN**

## Auftrag 2

**Definition Layer (Tabellen + Domain + Persistenz) minimal einführen**

* Reasoning effort: **high**
* Plan mode: **AN**

## Auftrag 3

**Onboarding auf neues Modell mappen, ohne Altwelt zu löschen**

* Reasoning effort: **high**
* Plan mode: **AN**

---

# 16. Schlussanweisung an die KI

Dieses Projekt soll **nicht** weiter in Richtung „größeres spezialisiertes Onboarding-Tool“ wachsen.

Es soll zu einer **versionierten, kontrollierten Workflow-Plattform** werden.

Dafür gilt:

* Bestehendes fachliches Wissen wiederverwenden
* Altes Kernmodell nicht weiter aufblasen
* Neue Architektur parallel einführen
* Sicherheit und Kontrollierbarkeit vor Bequemlichkeit
* Fachliche Konfiguration ermöglichen, technische Freiheit begrenzen