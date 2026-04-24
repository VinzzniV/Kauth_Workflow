# Entscheidungen

#architektur #entscheidungen #stabil

Langfristige Architektur- und Produktentscheidungen. Diese ändern sich selten — wenn doch, ist das eine bewusste Richtungskorrektur.

Primärquelle im Repo: `PROJECT_CONTEXT.md` (DECISIONS.md in Vault migriert)

---

## Produktrichtung

### Die App wird eine Workflow-Plattform, kein größeres Onboarding-Tool

**Warum:** Onboarding ist fachlich wichtig, darf aber kein versteckter Produktkern sein. Neue Kernlogik darf nicht onboarding-spezifisch verengt werden. Langfristig sollen viele verschiedene interne Workflows auf derselben Basis laufen.

**Konsequenz:** Neue Features werden am generischen Plattformkern gemessen, nicht daran ob sie dem Onboarding-Flow helfen.

---

## Backend-Architektur

### Backend ist Source of Truth

**Warum:** Das Frontend ist Darstellung und Bedienoberfläche. Businessregeln zweimal zu pflegen (Frontend + Backend) führt zu Inkonsistenz und Bugs in Produktiv-Systemen.

**Konsequenz:** Kein Validierungslogik-Drift ins Frontend. UI-Regeln sind immer vom Backend-Verhalten ableitbar.

---

### Rollen ≠ Responsibilities

| Begriff | Bedeutung |
|---------|-----------|
| **Rolle** | Technischer Zugriff — wer darf was aufrufen |
| **Responsibility** | Fachliche Ownership — wer ist für was zuständig |

**Warum:** Die Trennung ermöglicht flexibles Zuständigkeitsmodell (z.B. "IT-Gruppe ist für Laptop-Aufgaben zuständig") unabhängig von Systemrollen.

**Konsequenz:** `assignment_type` bleibt strikt: `user` = persönlich, `responsibility` = geteilte Zuständigkeit. Keine impliziten Abkürzungen.

---

### Workflow-Definition ist eigener Kern

**Warum:** Task Templates und Prozessarten allein sind nicht das Endmodell. Die Plattform braucht einen expliziten Definition Layer mit Nodes, Edges und Versionierung — sonst kann kein Nicht-Entwickler Workflows ohne Codeänderung pflegen.

---

### Versionierung ist Pflicht

**Warum:** Laufende Instanzen dürfen durch spätere Admin-Änderungen nicht brechen. Ohne Versionierung würde jede Konfigurationsänderung laufende Prozesse gefährden.

**Konsequenz:** `workflow_instances` referenzieren immer eine feste Definition-Version. Admin-Änderungen erzeugen neue Versionen.

---

### Runtime ist mehr als Task-Generierung

**Warum:** Die ursprüngliche Engine war ein Task-Generator. Das ist zu eng. Eine echte Workflow-Engine aktiviert Nodes, wertet Entscheidungen aus, startet Automationen — Tasks sind nur eine Laufzeitwirkung von mehreren.

---

## Migrationsstrategie

### Migration statt Big Bang

**Warum:** Ein Big Bang birgt hohes Risiko für laufende Workflows in Produktion. Die bestehende Fachlogik in Templates, Conditions und Dependencies hat echten Wert, der nicht weggeworfen werden soll.

**Konsequenz:** Neue Architektur wird parallel zur Altwelt eingeführt. Altlogik erst nach Stabilität und Parität zurückbauen. Keine voreiligen Löschungen.

---

## Sicherheit & Kontrolle

### Keine freie technische Magie für Admins

**Warum:** Admins sind Fachanwender, keine Entwickler. Freie PowerShell, SQL oder HTTP-Requests mit Secrets wären ein unkontrollierbares Sicherheitsrisiko.

**Konsequenz:** Nur freigegebene, validierte Actions. Kein freies Scripting.

### Actions sind kontrollierte Produktelemente

**Warum:** Technische Actions wie `CreateAdUser` oder `SendWelcomeMail` sind definierte Bausteine mit bekanntem Verhalten, Retry-Logik und Logging — keine losen Skripte.

---

## Datenmodell & Konsistenz

### Completion-Regel bleibt streng

Ein Workflow gilt erst als abgeschlossen, wenn alle relevanten Laufzeitpfade sauber beendet sind. Keine UI-Abkürzungen.

### Statuskonsistenz ist Pflicht

- `cancelled` nicht auf `completed` mappen
- `skipped` nicht als Reparatur für falsch generierte Tasks missbrauchen
- Statusunterschiede nicht verstecken

### Parallelität bleibt erlaubt

Mehrere Teams oder Rollen können parallel arbeiten. Das Modell darf keine unnötige serielle Einbahnstrasse erzwingen.

---

## Identity & Directory

### Die App ist nicht das führende Benutzersystem

AD / Entra liefern die technische Identität. Person und technische Identity bleiben getrennte Konzepte.

### Gruppen sind Standard für Zugriff

Standardzugriff kommt über Gruppen-Mapping. Lokale Sonderfälle bleiben Ausnahme.

---

## Dokumentation & Prozess

### Dokumentation ist Teil der Architekturarbeit

Zielbild, Migration und Begriffe müssen im Repo nachvollziehbar sein. Architekturarbeit ohne Doku gilt nicht als fertig.

### Refactors brauchen einen klaren Grund

Keine großen unstrukturierten Refactors nebenbei. Bei Kernumbauten zuerst Zielmodell und Migrationsschnitt klären.

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was gebaut wird
- [[Migrationspfad]] — In welcher Reihenfolge
- [[Identity]] — Identity-Modell im Detail
- [[Automation]] — Automation Layer
