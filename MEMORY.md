# MEMORY.md

## Zweck

Diese Datei ist nur kurzfristiges Arbeitsgedaechtnis.
Sie ist kein Backlog, kein Changelog und keine zweite Architektur-Doku.
Sollte die Datei Inhalte enthalten, die nicht mehr aktuell sind, dann bereinige diese.

Verwende sie nur fuer:
- aktuellen Fokus
- aktive Stolperfallen
- wenige temporaere Hinweise fuer die naechsten Sessions

---

## Current Focus

- `DOCS_CONTROL.md` ist wieder der zentrale Einstieg fuer KI-Arbeit im Repo.
- Nach dem Lesen der Pflichtdokumente muss pro Aufgabe mitgedacht werden, welche Doku im selben Arbeitsgang aktualisiert werden muss.
- Der Lifecycle-/Mitarbeiter-Schnitt ist jetzt personenzentriert: neue Onboardings legen zuerst einen kanonischen `people`-Datensatz an, starten danach den Workflow mit `targetPersonId` und halten Workflow-Felder nur noch als Snapshot.
- Mail-Vorlagen und Mail-Preview sind jetzt im Admin-Bereich pflegbar; fachliche Namen duerfen in Benachrichtigungen nicht aus technischen Versionsnamen abgeleitet werden.
- Die Anforderungsmaske nutzt im Edit-Modus Kartenlogik fuer Boolean-Felder; `hardware_takeover_details` erfasst uebernommene Hardware bei `hardware_available = true`.

## Active Risks / Watchouts

- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- Permission-Schema ist seit Slice 6.3d-iv vollstaendig definitionsgetrieben (`workflows.create.<definition_key>`). Responsibility `hr_onboarding` wurde 2026-05-01 zu `hr_workflow_initiator` umbenannt (Slice 7B).
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432`.
- LA5 done (2026-05-03): Task-Specs liegen jetzt am `workflow_node_id` der published Version. AdminTaskTemplate-DTO behaelt Form (Frontend-Rename auf `Spec*` ist Watch-Item, ~22 Files). `workflow_definitions.approval_task_template_key` heisst nominell noch `_template_key` — Naming-Cleanup defer. Wenn echte Versions-Wechsel ueber den Builder passieren: Spec-Carry-Over zwischen Versionen ist noch nicht implementiert (siehe Legacy-Abbau-Plan LA5 Watch-Items).

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` beschreibt das stabile Plattform-Zielbild.
- `PROJECT_STRUCTURE.md` und `web/README.md` muessen bei sichtbaren Admin-/UI-Verschiebungen mitgezogen werden.
- Diese Datei enthaelt nur noch Hinweise fuer die naechsten Sessions, keine laengere Historie.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
