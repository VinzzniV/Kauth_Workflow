# TODO.md

Diese Datei steuert nur noch die Reihenfolge der Umsetzung.
Die Details, Regeln, Architekturgrenzen und Deliverables stehen zentral in `Workflow_Plattform_Implementation_Plan.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `Workflow_Plattform_Implementation_Plan.md` lesen.

Mindestens immer lesen:
- `# 1. Zielbild`
- `# 3. Was erhalten bleiben soll`
- `# 4. Soll-Architektur`
- `# 5. Zentrale Architekturprinzipien`
- `# 8. Migrationsstrategie`
- `# 9. Arbeitsmodus fuer die KI in der IDE`
- `# 11. Technische Arbeitsregeln fuer Codex / Claude`
- `# 12. Definition of Done pro Phase`
- `# 14. Was die KI explizit vermeiden soll`
- `# 16. Schlussanweisung an die KI`

Zusaetzlich muss pro Aufgabe immer der unten referenzierte Phasenabschnitt gelesen werden.

Wichtig:
- `TODO.md` enthaelt bewusst keine Detailanweisungen mehr.
- Die KI soll die Aufgabe nicht aus `TODO.md` allein ableiten.
- Die KI soll fuer jede Aufgabe den Implementierungsplan als Primaerquelle verwenden.

---

## Prioritaeten

- `P0` = Grundlage und Sicherheits-/Migrationsblocker
- `P1` = Definition Layer und Runtime-Kern
- `P2` = Anbindung, Migration, Validierung
- `P3` = Ausbau von Automation und Builder

---

## Umsetzungssteuerung

### [ ] T0. Artefakt- und Secret-Hygiene
**Prioritaet:** P0
**Reasoning Effort:** Medium
**Plan Mode:** AN, falls Packaging-/Repo-Struktur angepasst wird; sonst AUS bei rein lokalen Aufraeumarbeiten

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 0 — Repository- und Sicherheitsbereinigung`

### [x] T1. Begriffe und Zielarchitektur-Doku harmonisieren
**Prioritaet:** P0
**Reasoning Effort:** Medium
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 2 — Zielarchitektur dokumentieren und im Code verankern`

### [x] T2. Onboarding-Kopplungen inventarisieren
**Prioritaet:** P0
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 1 — Produktkern ent-onboarden`

### [x] T3. Workflow Definition Layer einfuehren
**Prioritaet:** P1
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 3 — Workflow Definition Layer einführen`

### [x] T4. Runtime Layer parallel einfuehren
**Prioritaet:** P1
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 4 — Runtime Layer parallel einführen`

### [x] T5. Generische Startup- und Admin-Validierung
**Prioritaet:** P1
**Reasoning Effort:** Medium
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 7 — Startup-Validierung und Admin-Validierung generisch machen`

### [x] T6. Bestehende Workflows mappen
**Prioritaet:** P2
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 5 — Bestehende Prozesse auf das neue Modell mappen`

### [x] T7. Task-System an Node-Runtime anbinden
**Prioritaet:** P2
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 6 — Task-System an Node-Runtime anbinden`

### [x] T8. Onboarding-Kernlogik weiter neutralisieren
**Prioritaet:** P2
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 1 — Produktkern ent-onboarden`

### [ ] T9. Automation Layer bauen
**Prioritaet:** P3
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 8 — Automation Layer als eigenes Subsystem bauen`

### [ ] T10. Guided Admin Builder
**Prioritaet:** P3
**Reasoning Effort:** Medium
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 9 — Guided Admin Builder`

### [ ] T11. Altwelt gezielt zurueckbauen
**Prioritaet:** P3
**Reasoning Effort:** High
**Plan Mode:** AN

Lese zuerst:
- `Workflow_Plattform_Implementation_Plan.md`
- Abschnitt `## Phase 10 — Altwelt gezielt zurückbauen`

---

## Empfohlene Reihenfolge

1. T0 Artefakt- und Secret-Hygiene
2. T1 Begriffe und Zielarchitektur-Doku harmonisieren
3. T2 Onboarding-Kopplungen inventarisieren
4. T3 Workflow Definition Layer
5. T4 Runtime Layer
6. T5 Generische Validierung
7. T6 Workflow-Mapping
8. T7 Task-System anbinden
9. T9 Automation Layer
10. T10 Guided Builder
11. T11 Altwelt-Rueckbau

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `Workflow_Plattform_Implementation_Plan.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Migrationsrisiken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
