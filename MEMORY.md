# MEMORY.md

## Zweck

Diese Datei ist nur kurzfristiges Arbeitsgedaechtnis.
Sie ist kein Backlog, kein Changelog und keine zweite Architektur-Doku.

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

## Active Risks / Watchouts

- Der Code spiegelt die neue Zielarchitektur noch nicht vollstaendig; die Dokumentation ist absichtlich schon weiter als der Ist-Stand.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- Mehrere sichtbare Legacy-Vertraege bleiben bewusst bestehen, vor allem `completed-onboardings`, `CompletedOnboardingSearchResultDto`, `workflows.create.onboarding` und `hr_onboarding`.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:25432`.
- Offene Folgearbeit liegt jetzt vor allem bei Benachrichtigungslogik, tieferer Audit-/Robustheitsabsicherung und den spaeteren Frontend-Slices fuer Rotation.

## Temporary Notes

- `PRODUCTIVE_TARGET_ARCHITECTURE.md` beschreibt das stabile Plattform-Zielbild.
- `IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md` und `TODO.md` bleiben die aktive Feature-Steuerung fuer Rotation/Durchlauf.
- `ONBOARDING_COUPLING_INVENTORY.md` ist die Referenz fuer Phase 1 / T2 und trennt `A` Benennung, `B` Kernkopplung und `C` Legacy-Vertrag.
- `LEGACY_WORKFLOW_MAPPING.md` dokumentiert die T6-Abbildung der ersten drei Legacy-Prozesse in den Definition Layer.
- Der Canvas-Builder laeuft jetzt ueber die eigene Route `/builder`; `/admin/config?section=builder|templates|answers|defaults` redirectet auf diese Produktseite.
- Diese Datei enthaelt nur noch Hinweise fuer die naechsten Sessions, keine laengere Historie.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
