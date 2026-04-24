# Rotation (Abteilungsdurchlauf)

#domäne #rotation

Der Abteilungsdurchlauf ist das Feature für strukturierte Stellenwechsel. Eine Person durchläuft mehrere Abteilungen in definierten Zeiträumen — jede Station erzeugt Aufgaben.

---

## Was ist ein Abteilungsdurchlauf?

Ein Durchlauf (Rotation) ist ein geplanter Wechselvorgang, bei dem eine Person nacheinander mehrere Abteilungen durchläuft. Typischer Einsatz: Azubi-Ausbildung, interne Rotation, Übergänge.

---

## Datenmodell

```
rotation_plans
  └── rotation_stations           (eine Station = eine Abteilung, ein Zeitraum)
        └── rotation_generated_tasks  (automatisch erzeugte Aufgaben pro Station)
              └── rotation_task_assignments  (Zuweisung: is_primary=TRUE ist maßgeblich)
              └── rotation_task_comments

department_action_templates       (Vorlagen für automatische Aufgaben pro Abteilung)
rotation_notifications            (Benachrichtigungen: upcoming_change, reminder, overdue)
rotation_audit_log                (Unveränderlicher Log für den Durchlauf)
```

---

## Ablauf

### Schritt 1 — Plan anlegen (HR)
- Person auswählen (muss ein abgeschlossenes Onboarding haben)
- Person ist fachlicher Anker über `people`-Tabelle
- `source_workflow_id` hält das letzte abgeschlossene Onboarding als Provenienz-Nachweis

### Schritt 2 — Stationen anlegen (HR)
- Eine Station = eine Abteilung + Zeitraum (von/bis)
- Stationen dürfen sich zeitlich **nicht** überschneiden (wird beim Erstellen/Aktualisieren geprüft, API antwortet mit `409 Conflict`)

### Schritt 3 — Aufgaben generieren (automatisch)
- Basierend auf `department_action_templates` der jeweiligen Abteilung
- Templates definieren: Aufgabentyp, Trigger (`enter`/`exit`), Anchor-Date, Zuständigkeit
- Aufgaben werden mit `rotation_task_assignments` verknüpft

### Schritt 4 — Aufgaben bearbeiten (IT, Fachbereiche)
- Über `/rotation/operations` (Übersicht) und `/rotation/tasks/:taskRef` (Detail)
- Aufgaben sind über einheitliche `taskRef`-Adressen (`rot:*`) erreichbar

### Schritt 5 — Benachrichtigungen (automatisch, täglich)
- `upcoming_change`, `reminder`, `overdue`
- Dedupliziert, per Mail via Graph/Notification-System

---

## Task-Sichtbarkeit — Kritische Regel

Eine Rotationsaufgabe ist **nur sichtbar**, wenn:
- Sie eine `rotation_task_assignments`-Zeile mit `is_primary=TRUE` hat
- Die Responsibility in dieser Zeile zur aktuellen Person passt

→ Template ohne `default_responsibility_id` erzeugt Aufgaben, die für **niemanden** (außer Admins) sichtbar sind. Kein automatischer Block — nur Admin-Warnung in der Konfiguration.

**Implementierung (nach CLA-1):** Rotationsaufgaben werden per SQL-WHERE auf zugewiesene Responsibilities gefiltert — nicht mehr in-memory.

---

## department_action_templates

| Feld | Bedeutung |
|------|-----------|
| `automation_key` | Handler-Schlüssel für automatische Aktionen (muss registriert sein) |
| `trigger_type` | `enter` (Einzug) oder `exit` (Auszug) |
| `default_responsibility_id` | Zuständigkeit — Pflicht für Sichtbarkeit |
| `anchor_date` | Wann die Aufgabe fällig ist |

`automation_key` wird beim Speichern gegen `WorkflowAutomationHandlerRegistry` validiert — unbekannte Keys werden abgelehnt.

---

## Bekannte Einschränkungen / Offene Punkte

| Problem | Beschreibung | Status |
|---------|-------------|--------|
| Template ohne Zuständigkeit | Aufgaben unsichtbar, kein harter Block | offen (C2) |
| Repository-Schnitt | `RotationTaskGenerationOperations` (1.444 Zeilen) im Monolith | offen (CLA-3) |
| Keine Datenbereinigung | Draft-Pläne, abgebrochene Stationen akkumulieren | offen (L2) |

---

## Wichtige Backend-Dateien

| Datei | Zweck |
|-------|-------|
| `api/API/Endpoints/RotationPlanningEndpoints.cs` | HR-seitige Endpunkte |
| `api/API/Endpoints/AdminRotationConfigEndpoints.cs` | Admin-Templates |
| `api/API/Services/RotationPlanningService.cs` | Planung, Stations-Validierung |
| `api/API/Services/RotationTemplateAdminService.cs` | Template-Pflege |
| `api/API/Services/RotationTaskGenerationService.cs` | Task-Generierung |
| `api/API/Services/RotationNotificationService.cs` | Benachrichtigungen |
| `api/API/RotationDomainConstants.cs` | Zentrale Status-/Typ-Konstanten |
| `db/54_rotation_phase1_persistence.sql` | Basisschema |
| `db/56_rotation_task_generation_sync.sql` | Task-Sync-Erweiterungen |

---

## Frontend-Seiten

| Route | Seite | Nutzer |
|-------|-------|--------|
| `/rotation` | Übersicht + Anlage neuer Pläne | HR |
| `/rotation/plans/:planId` | Plan-Detail, Stationen, Audit, Notifications | HR |
| `/rotation/operations` | Aufgaben-Übersicht | IT, Fachbereiche |
| `/rotation/tasks/:taskRef` | Aufgaben-Detail | IT, Fachbereiche |

---

## Verwandte Notizen

- [[Workflow]] — Workflow-Konzept allgemein
- [[Identity]] — Personen, Responsibilities
- [[Automation]] — Automation Layer für Actions
- [[Code-Review-Status]] — Offene Punkte
