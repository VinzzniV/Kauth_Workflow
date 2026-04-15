# TODO.md

Diese Datei steuert nur die Reihenfolge der Umsetzung.
Die fachlichen Details, Architekturregeln, Scope-Grenzen, Datenmodelle, Akzeptanzkriterien und Deliverables stehen zentral in:

- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` lesen.

Mindestens immer lesen:
- `## Kontext`
- `## Produktziel`
- `## Wichtige fachliche Regeln`
- `## MVP-Scope`
- `## Technische Leitplanken`
- `## Gewünschtes Ergebnis für diese Implementierung`
- `## Was Codex zuerst tun soll`
- `## Wichtige Implementierungsregeln für Codex`

Zusätzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Zusätzlich muss pro Aufgabe immer der unten referenzierte Phasenabschnitt gelesen werden.

Wichtig:
- `TODO.md` enthält bewusst keine Detailanweisungen.
- Die KI darf die Aufgabe nicht allein aus `TODO.md` ableiten.
- Die KI muss für jede Aufgabe den Implementierungsplan als Primärquelle verwenden.

---

## Prioritäten

- `P0` = Domänenmodell, Persistenz, Validierung, Task-Generierung
- `P1` = Benachrichtigungen und Backend-Ablaufstabilität
- `P2` = HR-UI sowie IT-/Fachbereichs-UI
- `P3` = Audit, Tests, Robustheit, Doku, Vorbereitung für spätere Automatisierung

---

## Umsetzungssteuerung

### Aufgabe 1 — Impact-Analyse und Bestandsaufnahme
**Priorität:** P0  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `## Kontext`
- `## Produktziel`
- `## Wichtige fachliche Regeln`
- `## MVP-Scope`
- `## Technische Leitplanken`
- `## Was Codex zuerst tun soll`

**Ziel:**
- Bestehende Architektur analysieren
- Betroffene Module, Entitäten, Endpunkte, UI-Bereiche und Wiederverwendungspotenziale identifizieren
- Kurzen Impact-Report erstellen

**Abhängigkeit:** keine

---

### Aufgabe 2 — Phase 1: Domänenmodell und Persistenz
**Priorität:** P0  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 1 – Domänenmodell und Persistenz`
- zusätzlich `## Begriffe / Domänenmodell`

**Ziel:**
- Neue Kernentitäten, Tabellen, Fremdschlüssel, Indizes, Status-/Triggerwerte einführen

**Abhängigkeit:** Aufgabe 1

---

### Aufgabe 3 — Phase 2: Backend-Grundfunktionen für Planung
**Priorität:** P0  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 2 – Backend-Grundfunktionen für Planung`

**Ziel:**
- CRUD für Personen, Pläne und Stationen
- Validierungslogik für Zeiträume und Überschneidungen

**Abhängigkeit:** Aufgabe 2

---

### Aufgabe 4 — Phase 3: Abteilungen und Maßnahmenvorlagen
**Priorität:** P0  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 3 – Abteilungen und Maßnahmenvorlagen`
- zusätzlich `## Konkrete Beispiel-Seed-Daten`

**Ziel:**
- Departments und DepartmentActionTemplates pflegbar machen
- Erste Beispielvorlagen / Seed-Daten anlegen

**Abhängigkeit:** Aufgabe 2

---

### Aufgabe 5 — Phase 4: Task-Generierung aus Stationen
**Priorität:** P0  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 4 – Task-Generierung aus Stationen`

**Ziel:**
- Generator-Service einführen
- Aus Stationen und Vorlagen konkrete Tasks erzeugen
- Duplikate verhindern
- Regenerierungsstrategie für Planänderungen umsetzen

**Abhängigkeit:** Aufgabe 3, Aufgabe 4

---

### Aufgabe 6 — Phase 5: Benachrichtigungslogik
**Priorität:** P1  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 5 – Benachrichtigungslogik`

**Ziel:**
- Scheduler / Background-Job für kommende Wechsel
- Reminder- und Wechsel-Benachrichtigungen
- Deduplizierung von Versandereignissen

**Abhängigkeit:** Aufgabe 5

---

### Aufgabe 7 — Phase 6: Frontend HR – Personen und Durchlaufplan
**Priorität:** P2  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 6 – Frontend HR: Personen und Durchlaufplan`
- zusätzlich `## UX-Prinzipien`

**Ziel:**
- HR-Oberflächen für Personen, Pläne und Stationen
- funktionale Listen-/Detailansicht statt unnötig komplexer Kalenderlogik

**Abhängigkeit:** Aufgabe 3

---

### Aufgabe 8 — Phase 7: Frontend IT / Fachbereiche – Aufgaben und Wechsel
**Priorität:** P2  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 7 – Frontend IT / Fachbereiche: Aufgaben und Wechsel`
- zusätzlich `## UX-Prinzipien`

**Ziel:**
- Aufgabenübersichten, Wechselübersichten, Detailseiten und Statuspflege

**Abhängigkeit:** Aufgabe 5, Aufgabe 6

---

### Aufgabe 9 — Phase 8: Audit, Historie, Robustheit
**Priorität:** P3  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `# Phase 8 – Audit, Historie, Robustheit`

**Ziel:**
- Audit-Logging
- Tests für Kernlogik
- Randfälle absichern
- Robustheit der Fachabläufe erhöhen

**Abhängigkeit:** Aufgabe 5, Aufgabe 6, Aufgabe 7, Aufgabe 8

---

### Aufgabe 10 — Abschluss, Cleanup und Dokumentation
**Priorität:** P3  
**Vor Umsetzung lesen in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md`:**
- `## Erwarteter Output von Codex`
- `## Zusätzliche Hinweise für spätere Versionen`
- `## Was Codex zuerst tun soll`

**Ziel:**
- Offene technische Schulden bereinigen
- Dokumentation nachziehen
- Annahmen, Restpunkte und nächste Ausbaustufe dokumentieren

**Abhängigkeit:** Aufgabe 9

---

## Empfohlene Abarbeitungsreihenfolge

1. Aufgabe 1 — Impact-Analyse und Bestandsaufnahme  
2. Aufgabe 2 — Phase 1: Domänenmodell und Persistenz  
3. Aufgabe 3 — Phase 2: Backend-Grundfunktionen für Planung  
4. Aufgabe 4 — Phase 3: Abteilungen und Maßnahmenvorlagen  
5. Aufgabe 5 — Phase 4: Task-Generierung aus Stationen  
6. Aufgabe 6 — Phase 5: Benachrichtigungslogik  
7. Aufgabe 7 — Phase 6: Frontend HR – Personen und Durchlaufplan  
8. Aufgabe 8 — Phase 7: Frontend IT / Fachbereiche – Aufgaben und Wechsel  
9. Aufgabe 9 — Phase 8: Audit, Historie, Robustheit  
10. Aufgabe 10 — Abschluss, Cleanup und Dokumentation

---

## Abschlussregel für jede KI-Aufgabe

Nach jedem größeren Schritt muss berichtet werden:
1. Welche Dateien wurden geändert?
2. Auf welchen Abschnitt in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` wurde gearbeitet?
3. Wie passt die Änderung zur Zielarchitektur?
4. Welche Risiken oder Lücken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?