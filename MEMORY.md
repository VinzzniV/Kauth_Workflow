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

- Die Root-Dokumentation wurde auf den neuen Implementierungsplan fuer die Workflow-Plattform harmonisiert.
- T6 ist umgesetzt: `onboarding`, `offboarding` und `department_change` sind als publizierte, admin-only nutzbare Workflow-Definitionen gemappt.

## Active Risks / Watchouts

- Der Code spiegelt die neue Zielarchitektur noch nicht vollstaendig; die Dokumentation ist absichtlich schon weiter als der Ist-Stand.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- Mehrere sichtbare Legacy-Vertraege bleiben bewusst bestehen, vor allem `completed-onboardings`, `CompletedOnboardingSearchResultDto`, `workflows.create.onboarding` und `hr_onboarding`.
- Die drei T6-Mappings sind bewusst linearisiert; Parallel-Splits/-Joins und echte Task-Anbindung folgen erst in spaeteren Phasen.

## Temporary Notes

- `Workflow_Plattform_Implementation_Plan.md` ist jetzt die primaere Migrationsanweisung fuer Architekturarbeit.
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` beschreibt das stabile Sollbild, `TODO.md` die priorisierte Reihenfolge.
- `ONBOARDING_COUPLING_INVENTORY.md` ist die Referenz fuer Phase 1 / T2 und trennt `A` Benennung, `B` Kernkopplung und `C` Legacy-Vertrag.
- `LEGACY_WORKFLOW_MAPPING.md` dokumentiert die T6-Abbildung der ersten drei Legacy-Prozesse in den Definition Layer.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
