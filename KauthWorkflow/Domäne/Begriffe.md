# Begriffe

#domäne #glossar

Kompaktes Glossar fuer zentrale Projektbegriffe.

Die Datei soll Mehrdeutigkeiten zwischen Fachdomäne, Architektur und Altlogik reduzieren.

---

## Zweck

- gemeinsame Sprache fuer Menschen und KIs
- Missverstaendnisse zwischen Legacy- und Zielbild verringern
- schnelle Nachschlagequelle fuer Einarbeitung und Architekturarbeit

## Primaerquelle fuer

- Kurzdefinitionen zentraler Fach- und Technikbegriffe
- Abgrenzung aehnlicher Begriffe

## Nicht verwenden fuer

- aktuelle Priorisierung
- Umsetzungsstatus
- tiefe Architekturargumente

---

## Glossar

### Person

Kanonischer fachlicher Mitarbeiteranker.
Nicht gleich technische Identity.

### Technische Identity

AD-/Entra-Konto oder technischer Benutzerkontext.
Nicht gleich Person.

### Role

Zugriffs- und Berechtigungsebene.
Antwortet auf: **Was darf ich sehen oder tun?**

### Responsibility

Fachliche Ownership oder Zuständigkeit.
Antwortet auf: **Wer ist inhaltlich verantwortlich?**

### Workflow Definition

Beschreibt den fachlichen Ablauf als versionierbares Modell.
Besteht aus Nodes, Edges und Konfiguration.

### Workflow Definition Version

Eine feste, veröffentlichte oder bearbeitbare Version einer Definition.
Laufende Instanzen muessen auf ihrer Version stabil bleiben.

### Workflow Runtime

Die Ausführungsschicht, die Definitionen aktiviert, Node-Fortschritt auswertet und Laufzeiteffekte erzeugt.

### Workflow Instance

Eine konkrete laufende Instanz einer Workflow Definition.

### Task-System

Der Teil der Plattform fuer Human Tasks, Zuweisung, Status, Kommentare, Fristen und Eskalation.

### Task

Ein konkretes Arbeitspaket fuer einen Benutzer oder eine Responsibility.

### Workflow-Task

Task, der zu einer Workflow-Instanz gehoert und Runtime-Fortschritt ausloesen kann.

### Rotation-Task

Task aus dem Rotations-/Durchlauf-Bereich.
Wird ueber `rot:*` adressiert und laeuft nicht durch den Workflow-Node-Lifecycle.

### Rotation

Eigener fachlicher Slice fuer Abteilungsdurchlaeufe.
Nutzt Teile des allgemeinen Task- und Notification-Systems, ist aber nicht identisch mit dem Workflow-Definition-Layer.

### Node

Ein Schritt in einer Workflow Definition, z. B. `form`, `approval`, `task`, `automation`, `end`.

### Measure Node

Fachlicher Maßnahmen-Block wie `measure_provision`, `measure_deprovision`, `measure_change`, `measure_rename`.
Im Zielbild der sichtbare Fachcontainer fuer Aufgaben.

### Legacy

Historisch gewachsene Altlogik oder Altverträge, die noch parallel zur Zielarchitektur bestehen.

### Zielbild

Das langfristige, stabile Sollmodell der Plattform.
Beschrieben in [[Zielarchitektur]].

### Commit-Grenze

Die Schicht, die Connection, Transaction und den logischen Schreibschnitt kontrolliert.
Im aktuellen Runtime-Pfad soll das der `WorkflowLifecycleService` sein.

### SQL-Pushdown

Verlagerung von Filter-, Sortier- oder Paginierungslogik aus In-Memory-Pfaden in die Datenbankabfrage.

### N+1

Leistungsmuster, bei dem zunaechst eine Liste geladen wird und danach fuer jedes Element weitere Einzelabfragen folgen.

### Slice

Kleiner, klar begrenzter Arbeits- oder Refactor-Schritt mit ueberschaubarem Risiko.

---

## Verwandte Dateien

- [[Zielarchitektur]]
- [[Workflow]]
- [[Rotation]]
- [[Identity]]
- `PROJECT_CONTEXT.md`
