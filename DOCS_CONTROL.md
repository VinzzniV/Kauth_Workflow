# DOCS_CONTROL.md

## Zweck

Diese Datei steuert, wie die Repo-Dokumentation gelesen und gepflegt wird.
Sie ist die Ordnungsdatei fuer Dokumentationsfluss, nicht der Ort fuer Fachlogik oder Implementierungsdetails.

---

## Zuerst lesen

Vor allen nicht-trivialen Aenderungen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Zusatzlich je nach Aufgabe:
- `Workflow_Plattform_Implementation_Plan.md` bei Architektur-, Migrations-, Datenmodell-, Runtime- oder Plattformarbeit
- `PROJECT_STRUCTURE.md` fuer Dateilayout, Module und Einstiegspunkte
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` fuer das stabile Sollbild
- `DECISIONS.md` fuer langfristige Architektur- und Produktentscheidungen
- `ENGINEERING_RULES.md` fuer Umsetzungs- und Handoff-Regeln
- `SETUP.md` fuer lokale Entwicklung, Deployment und Laufzeitkonfiguration
- `TODO.md` fuer den priorisierten Umsetzungs-Backlog
- `web/README.md` fuer Frontend-Orientierung

---

## Schreibziele

Verwende diese Dateien bewusst:

- stabile Projektwahrheit und Guardrails -> `PROJECT_CONTEXT.md`
- verbindliche Zielarchitektur der Plattform -> `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- konkrete Umsetzungsreihenfolge und Phasen -> `Workflow_Plattform_Implementation_Plan.md`
- langfristige Architektur- und Produktentscheidungen -> `DECISIONS.md`
- kurzfristiger Session-Kontext, aktive Risiken, naechste Schritte -> `MEMORY.md`
- Repo-/Modulstruktur und wichtige Einstiegspunkte -> `PROJECT_STRUCTURE.md`
- lokale Setup-, Deploy- und Laufzeitdoku -> `SETUP.md`
- groesserer Backlog und priorisierte Arbeitspakete -> `TODO.md`
- Frontend-spezifische Orientierung -> `web/README.md`

---

## Mindest-Updates nach Aenderungen

Wenn sich diese Bereiche aendern, muessen die passenden Dokus im selben Arbeitsgang mitgezogen werden:

- Zielbild, Plattformbegriffe, Migrationsannahmen -> `PRODUCTIVE_TARGET_ARCHITECTURE.md` und ggf. `DECISIONS.md`
- Architekturphasen, Reihenfolge oder Deliverables -> `Workflow_Plattform_Implementation_Plan.md` oder `TODO.md`
- stabile Produktregeln oder Guardrails -> `PROJECT_CONTEXT.md`
- neue Ordner, neue Entry-Points, umbenannte Module -> `PROJECT_STRUCTURE.md`
- veraendertes Runtime-, Compose-, Deploy- oder Env-Verhalten -> `SETUP.md`
- neue offene Risiken oder bewusst unvollstaendige Nacharbeiten -> `MEMORY.md`
- veraenderte Frontend-Modulgrenzen oder Admin-/Builder-Flows -> `web/README.md`

---

## Lesereihenfolge nach Aufgabentyp

Fuer Architektur-, Datenmodell- oder Runtime-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `Workflow_Plattform_Implementation_Plan.md`
4. `PRODUCTIVE_TARGET_ARCHITECTURE.md`
5. `MEMORY.md`
6. `PROJECT_STRUCTURE.md`

Fuer Backend- oder Full-Stack-Feature-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `MEMORY.md`
4. `PROJECT_STRUCTURE.md`
5. aufgabenspezifische Dokus

Fuer Frontend-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `Workflow_Plattform_Implementation_Plan.md`, falls Builder/Workflow-UI betroffen ist
4. `web/README.md`
5. `MEMORY.md`

Fuer Infra, Auth oder Deployment:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `SETUP.md`
4. `PRODUCTIVE_TARGET_ARCHITECTURE.md`
5. `.env.prod.example` und Compose-Dateien

---

## Hygiene-Regeln

- Dieselbe Wahrheit nicht ohne Grund in mehreren Dateien pflegen.
- Stabile Informationen gehoeren nicht nur in `MEMORY.md`.
- Unsichere oder temporaere Notizen gehoeren nicht vorschnell in `PROJECT_CONTEXT.md`.
- Der Implementierungsplan ist die Arbeitsanweisung fuer die Migration, nicht der Ort fuer Session-Notizen.
- Wenn eine Datei ihren Zweck nicht mehr trifft, Inhalt verschieben oder Beschreibung anpassen.
