# Automation Layer

#domäne #automation

Wie automatische technische Aktionen im System kontrolliert ablaufen.

---

## Grundprinzip: Kontrolliert, nicht frei

Admins konfigurieren fachlich — sie dürfen **keine** freien technischen Aktionen hinterlegen.

Verboten:
- Freies PowerShell
- Freies SQL
- Beliebige HTTP-Requests mit Secrets aus dem Admin-UI

Erlaubt:
- Freigegebene, validierte Actions aus dem Action-Katalog

---

## Datenmodell

```
action_definitions          — Katalog freigegebener Aktionen (z.B. CreateAdUser, SendWelcomeMail)
workflow_node_actions       — Verknüpfung von Workflow-Nodes mit Actions

automation_jobs             — Job-Instanz für eine konkrete Ausführung
automation_job_attempts     — Versuche (max. 3: 1min, 5min, 5min Retry-Delays)
automation_job_logs         — Logs pro Versuch
```

---

## Ausführung

1. Workflow-Runtime aktiviert einen `automation`-Node
2. Ein `automation_job` wird erzeugt
3. `WorkflowAutomationHostedService` pollt und verarbeitet Jobs
4. Handler aus `WorkflowAutomationHandlerRegistry` wird aufgerufen
5. Erfolg oder Fehler wird in `automation_job_attempts` + `automation_job_logs` geschrieben
6. Bei Fehlschlag: bis zu 3 Versuche (Retry nach 1min, 5min, 5min)

---

## Handler-Registry

`WorkflowAutomationHandlerRegistry` ist die zentrale Registrierung aller verfügbaren Handler.

- Beim Speichern eines Templates mit `automation_key` wird dieser gegen die Registry validiert
- Unbekannte Keys werden **abgelehnt** — kein Silent Failure beim Konfigurieren

Aktuell verfügbare (simulierte) Handler:
- `CreateAdUser` — AD-Account anlegen
- `SendWelcomeMail` — Willkommensmail senden

→ Noch keine echten produktiven Handler. Simulation-Stand.

---

## Bekannte Lücken

| Problem | Status |
|---------|--------|
| Retry-Delays hardcodiert (1m/5m/5m) | offen (L1) |
| Kein Dead-Letter-Queue bei dauerhaftem Fehler | offen |
| Kein Alert/Benachrichtigung bei dauerhaftem Job-Fehler | offen |
| Keine echten produktiven Handler | laufende Migration |

---

## Notification-Templates (Mail)

Konfigurierbare Mail-Vorlagen für 6 Typen:

| Typ | Auslöser |
|-----|---------|
| `workflow_created` | Workflow angelegt |
| `task_ready` | Task bereit |
| `workflow_completed` | Workflow abgeschlossen |
| `upcoming_change` | Bevorstehender Wechsel (Rotation) |
| `reminder` | Erinnerung |
| `overdue` | Überfällig |

Platzhalter (`{{PERSON_NAME}}` etc.) werden gegen einen erlaubten Katalog geprüft — unbekannte Platzhalter werden beim Speichern abgelehnt.

---

## Wichtige Backend-Dateien

| Datei | Zweck |
|-------|-------|
| `api/API/Services/WorkflowAutomationService.cs` | Job-Claiming, Ausführung, Retry |
| `api/API/Services/WorkflowAutomationHostedService.cs` | Background Worker |
| `api/API/Services/WorkflowAutomationHandlerRegistry.cs` | Handler-Registrierung |
| `api/API/Services/SimulatedWorkflowAutomationHandlers.cs` | Simulierte Handler |
| `api/API/Repositories/.../AutomationOperations.cs` | DB-Zugriff |
| `api/API/Services/NotificationTemplateService.cs` | Template-Pflege + Validierung |
| `db/01_schema.sql` | Konsolidiertes Schema inkl. Automation-Layer |

---

## Verwandte Notizen

- [[Zielarchitektur]] — Automation Layer als Schicht
- [[Entscheidungen]] — Warum keine freie Automation
- [[Workflow]] — Automation-Nodes im Workflow-Flow
