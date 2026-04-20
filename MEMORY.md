# MEMORY.md

## Zweck

Diese Datei ist nur kurzfristiges Arbeitsgedaechtnis.
Sie ist kein Backlog, kein Changelog und keine zweite Architektur-Doku.
Sollte die Datei Inhatle enthalten, die nicht mehr aktuell sind, dann bereinige diese

Verwende sie nur fuer:
- aktuellen Fokus
- aktive Stolperfallen
- wenige temporaere Hinweise fuer die naechsten Sessions

---

## Current Focus

- `DOCS_CONTROL.md` ist wieder der zentrale Einstieg fuer KI-Arbeit im Repo.
- Fuer das Rotations-/Durchlauf-Feature sind `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` und `TODO.md` die aktive Arbeitsgrundlage.
- Nach dem Lesen der Pflichtdokumente muss pro Aufgabe mitgedacht werden, welche Doku im selben Arbeitsgang aktualisiert werden muss.
- Abgeschlossene Aufgaben muessen in `TODO.md` im selben Arbeitsgang auf `done` gesetzt werden.
- Der sichtbare Builder-Pfad ist `/builder`; alte Admin-Einstiege sind nur noch Legacy-/Redirect-Pfade.
- Phase 1 fuer Rotation/Durchlauf hat jetzt einen ersten Persistenzschnitt in `db/54_rotation_phase1_persistence.sql` und gespiegelt in `db/01_schema.sql`.
- Phase 2 laeuft jetzt ueber eigene Backend-Pfade unter `/rotation/*` mit `RotationPlanningService` und `PostgresWorkflowRepository.RotationOperations.cs`.
- Phase 3 hat jetzt Admin-Pflege fuer `department_action_templates` unter `/admin/rotation/action-templates` sowie Dev-Beispieldaten in `db/55_rotation_dev_template_examples.sql`.
- Phase 4 ist jetzt im Backend aktiv: `db/56_rotation_task_generation_sync.sql`, `RotationTaskGenerationService`, `PostgresWorkflowRepository.RotationTaskGenerationOperations.cs` und die neuen `/tasks/ref/{taskRef}`-Pfade ziehen Rotation-Tasks in den operativen Task-Slice.
- Phase 5 ist jetzt im Backend aktiv: `RotationNotificationService`, `RotationNotificationHostedService`, `PostgresWorkflowRepository.RotationNotificationOperations.cs` sowie die erweiterten Mail-Templates versenden taegliche `upcoming_change`, `reminder` und `overdue` fuer Rotation.
- Phase 6 hat jetzt den ersten HR-Frontend-Slice: `/rotation` ist die Uebersicht fuer bestehende Durchlaufplaene, `/rotation?mode=create` startet Suche und Plananlage aus `Neuer Vorgang`, `/rotation/plans/:planId` dient fuer Stationspflege und Generated-Task-Vorschau.
- Phase 7 ist jetzt im Frontend aktiv: `/rotation/operations` und `/rotation/tasks/:taskRef` liefern fuer IT/Fachbereiche Wechseluebersichten, Aufgaben nach Abteilung/Person, Statuspflege und Detailansichten auf Basis des familienfaehigen `/tasks`-Envelopes mit `taskRef`.
- Phase 8 ist abgeschlossen: `GET /rotation/plans/{planId}/audit` und `GET /rotation/plans/{planId}/notifications` liefern Plan-/Versions-/Versandhistorie; `RotationAuditLog` und `RotationNotificationsPanel` zeigen diese Historie in den bestehenden Detailseiten (`RotationPlanDetailPage`, `RotationTaskDetailPage`); alle 319 Backend- und 122 Frontend-Tests grueen.
- Aufgabe 10 ist abgeschlossen: Abschluss-Dokumentation in `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` eingetragen (neue/geaenderte Dateien, Migrationen, Annahmen, naechste Ausbaustufe); alle 10 Aufgaben des Rotations-/Durchlauf-Implementierungsplans sind erledigt.
- Follow-up nach Phase 10: Die Suche fuer `completed-onboardings` startet im Repository jetzt von abgeschlossenen Onboarding-Workflows aus und matched erst dann gegen `people`; damit werden valide abgeschlossene Onboardings fuer die Durchlauf-Anlage wieder sichtbar.
- Der Lifecycle-/Mitarbeiter-Schnitt ist jetzt personenzentriert: neue Onboardings legen zuerst einen kanonischen `people`-Datensatz an, starten danach den Workflow mit `targetPersonId` und halten Workflow-Felder nur noch als Snapshot.
- Neue API-Lesepfade fuer diesen Schnitt sind `POST /people`, `GET /people/search`, `GET /people/{personId}/workflow-history` und `GET /people/rotation-eligible`; Rotation startet fachlich von der Person und nutzt das letzte abgeschlossene Onboarding nur noch als Provenienz.
- Directory-Linking fuer wiederverwendbare Mitarbeiter laeuft jetzt ueber `employee_number` / Entra-`employeeId`; `PersonLifecycleProjectionService` schreibt kanonische Personenfelder aus abgeschlossenen Lifecycle-Workflows fort.

## Active Risks / Watchouts

- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- Mehrere sichtbare Legacy-Vertraege bleiben bewusst bestehen, vor allem `completed-onboardings`, `CompletedOnboardingSearchResultDto`, `workflows.create.onboarding` und `hr_onboarding`.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432`.

## Temporary Notes

- `PRODUCTIVE_TARGET_ARCHITECTURE.md` beschreibt das stabile Plattform-Zielbild.
- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` und `TODO.md` sind abgeschlossen; alle 10 Aufgaben erledigt.
- `ONBOARDING_COUPLING_INVENTORY.md` ist die Referenz fuer Phase 1 / T2 und trennt `A` Benennung, `B` Kernkopplung und `C` Legacy-Vertrag.
- `LEGACY_WORKFLOW_MAPPING.md` dokumentiert die T6-Abbildung der ersten drei Legacy-Prozesse in den Definition Layer.
- Der Canvas-Builder laeuft ueber die eigene Route `/builder`; `/admin/config?section=builder|templates|answers|defaults` redirectet dorthin.
- Diese Datei enthaelt nur noch Hinweise fuer die naechsten Sessions, keine laengere Historie.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
