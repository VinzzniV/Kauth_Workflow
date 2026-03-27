# TODO.md

## AI Rules (STRICT)

Before any task:
- read PROJECT_CONTEXT.md, DECISIONS.md, ENGINEERING_RULES.md
- summarize key rules
- list affected files BEFORE changing anything
- DO NOT refactor unrelated code
- backend is source of truth
- `skipped` is NOT allowed

For every task:
- show what you checked
- show verification (commands or reasoning)
- do not assume success → prove it

---

# 🔴 MUST (KRITISCH / SOFORT)

---

## [F7] Vorgangsauswahl: Onboarding / Offboarding / Änderungen trennen

### Problem
Aktuell sehen Abteilungsleiter bei der Anforderungsauswahl alle Anforderungen aus allen Prozesstypen (Onboarding, Offboarding, Abteilungswechsel, Positionswechsel, Namensänderung, Rollenwechsel) auf einmal — über 60 Einträge ohne Kontext. Es gibt keinen Startpunkt „Was für ein Vorgang soll erzeugt werden?".

### Ziel
HR (und eingeschränkt Abteilungsleitung) wählt zuerst den Vorgangstyp. Je nach Typ gelten andere Abläufe, Rollen und Anforderungen.

---

### Vorgangstypen & Berechtigungen

| Vorgangstyp       | HR | Abteilungsleitung |
|-------------------|----|-------------------|
| Onboarding        | ✅ | ❌                |
| Offboarding       | ✅ | ❌                |
| Namensänderung    | ✅ | ✅                |
| Abteilungswechsel | ✅ | ✅                |
| Positionswechsel  | ✅ | ✅                |
| Rollenwechsel     | ✅ | ✅                |

---

### Ablauf: Onboarding (unverändert)
1. HR wählt Vorgangstyp „Onboarding"
2. HR füllt Name, Personalnummer, Deadline, Abteilung, Stelle aus
3. Workflow wird erstellt
4. Abteilungsleitung wählt Anforderungen (gefiltert auf Onboarding-Definitionen)
5. Fachabteilungen erhalten Aufgaben basierend auf gewählten Anforderungen

---

### Ablauf: Offboarding & Änderungen (NEU)
Voraussetzung: Mitarbeiter muss bereits geonboarded sein (abgeschlossener Onboarding-Workflow im Archiv).

1. HR (oder AL bei Änderungen) wählt Vorgangstyp
2. Mitarbeitersuche: Suche über abgeschlossene Onboarding-Workflows (Name, Personalnummer)
3. Auswahl des Mitarbeiters → Verknüpfung mit dem Quell-Onboarding-Workflow
4. **Anforderungen ableiten:** Welche Systeme/Zugänge beim Onboarding eingerichtet wurden (aktive Anforderungsantworten), wird automatisch vorbelegt als Startpunkt für das Offboarding/die Änderung
5. Abteilungsleitung (oder HR) prüft/justiert die abgeleiteten Anforderungen
6. Je nach tatsächlich gesetzten Anforderungen werden nur die relevanten Fachabteilungen, HR-Schritte und Rollen benachrichtigt und erhalten Aufgaben

---

### Technische Umsetzung

#### Backend
- Neuer Endpunkt: `GET /workflows/completed-onboardings?search=...` → gibt abgeschlossene Onboarding-Workflows zurück (Name, Personalnummer, Abschluss-Datum) für die Mitarbeitersuche
- Workflow-Erstellung (`POST /workflows`) erweitern: optionales Feld `sourceWorkflowId` für Verknüpfung
- Beim Erstellen mit `sourceWorkflowId`: Antworten des Quell-Workflows lesen und per `workflow_answer_derivation_rules` in den neuen Workflow vorbefüllen (bereits in DB-Schema vorhanden, Tabellen `workflow_links` + `workflow_answer_derivation_rules`)
- `AuthorizationPolicyService`: Vorgangstyp-Berechtigungen (Onboarding/Offboarding nur HR, Änderungen auch AL)

#### Frontend
- Neuer Startschirm für „Neuen Vorgang anlegen": Auswahl des Prozesstyps mit Beschreibung und Berechtigungsfilter
- Bei Offboarding/Änderung: Mitarbeitersuchmaske (sucht in abgeschlossenen Onboardings) vor dem Formular
- Anforderungsauswahl (Supervisor-Step): Zeigt nur Anforderungen des gewählten Prozesstyps, nicht alle

#### Datenbank
- Tabellen `workflow_links` und `workflow_answer_derivation_rules` sind bereits vorhanden (`db/23_workflow_links.sql`)
- Derivation Rules (Onboarding → Offboarding, Onboarding → Abteilungswechsel) sind bereits als Seed vorhanden

---

### Schritte

1. Backend: `GET /workflows/completed-onboardings` Endpunkt
2. Backend: `POST /workflows` mit `sourceWorkflowId` + automatische Antwortableitung
3. Backend: Berechtigungsprüfung pro Prozesstyp
4. Frontend: Vorgangsauswahl-Startschirm
5. Frontend: Mitarbeitersuchmaske für Offboarding/Änderungen
6. Frontend: Anforderungsauswahl gefiltert auf den jeweiligen Prozesstyp

### Effort
Very High
Tool: Claude + Codex

---

## [F5] Introduce proper auth (ONLY LATE)

Effort: Very High
Tool: Claude

---

## [F6] Expand to employee lifecycle platform

Effort: High
Tool: Claude

---

# ✅ DONE CHECKLIST (MANDATORY)

Before marking any task as done:

- [ ] Only relevant files changed
- [ ] No unintended behavior changes
- [ ] No ENGINEERING_RULES violated
- [ ] No duplicate logic introduced
- [ ] Verification shown (not assumed)
