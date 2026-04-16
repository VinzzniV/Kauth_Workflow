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
- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` und `TODO.md` bei allen Aufgaben zum Rotations-/Durchlauf-Feature
- `PROJECT_STRUCTURE.md` fuer Dateilayout, Module und Einstiegspunkte
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` fuer das stabile Sollbild
- `DECISIONS.md` fuer langfristige Architektur- und Produktentscheidungen
- `ENGINEERING_RULES.md` fuer Umsetzungs- und Handoff-Regeln
- `SETUP.md` fuer lokale Entwicklung, Deployment und Laufzeitkonfiguration
- `web/README.md` fuer Frontend-Orientierung

Pflicht nach dem Lesen:
- Vor der Implementierung kurz festhalten, welche gelesenen Dateien bei dieser Aufgabe mitgezogen werden muessen, falls sich Annahmen, Strukturen, Verhalten, Scope oder Betrieb aendern.
- Doku-Updates werden nicht spaeter gesammelt, sondern im selben Arbeitsgang eingeplant.
- Wenn eine Aufgabe aus `TODO.md` abgeschlossen wurde, muss der Aufgabenstatus im selben Arbeitsgang auf `done` gesetzt werden.

---

## Schreibziele

Verwende diese Dateien bewusst:

- stabile Projektwahrheit und Guardrails -> `PROJECT_CONTEXT.md`
- verbindliche Zielarchitektur der Plattform -> `PRODUCTIVE_TARGET_ARCHITECTURE.md`
- konkrete Umsetzungsreihenfolge und Phasen fuer das Rotations-/Durchlauf-Feature -> `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`
- langfristige Architektur- und Produktentscheidungen -> `DECISIONS.md`
- kurzfristiger Session-Kontext, aktive Risiken, naechste Schritte -> `MEMORY.md`
- Repo-/Modulstruktur und wichtige Einstiegspunkte -> `PROJECT_STRUCTURE.md`
- lokale Setup-, Deploy- und Laufzeitdoku -> `SETUP.md`
- priorisierte Arbeitspakete fuer das Rotations-/Durchlauf-Feature -> `TODO.md`
- Frontend-spezifische Orientierung -> `web/README.md`

---

## Mindest-Updates nach Aenderungen

Wenn sich diese Bereiche aendern, muessen die passenden Dokus im selben Arbeitsgang mitgezogen werden:

- Zielbild, Plattformbegriffe, Migrationsannahmen -> `PRODUCTIVE_TARGET_ARCHITECTURE.md` und ggf. `DECISIONS.md`
- Architekturphasen, Reihenfolge oder Deliverables des Rotations-/Durchlauf-Features -> `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` oder `TODO.md`
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
3. `PRODUCTIVE_TARGET_ARCHITECTURE.md`
4. `MEMORY.md`
5. `PROJECT_STRUCTURE.md`

Fuer Rotations-/Durchlauf-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`
4. `TODO.md`
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
3. `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` und `TODO.md`, falls Rotations-/Durchlauf-UI betroffen ist
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
- Nach dem Lesen der Pflichtdokumente immer aktiv pruefen, welche davon durch die aktuelle Aufgabe aenderungsrelevant werden.
- Wenn eine Datei ihren Zweck nicht mehr trifft, Inhalt verschieben oder Beschreibung anpassen.
