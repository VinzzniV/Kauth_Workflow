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

- **Schritt 7 (Runtime ↔ Task-System) ist aktiv.** Architektur-Skizze + Slice 0 Inventur done (2026-05-03). Option B (Engine als pure Domain-Service, analog H6) angenommen. Q1–Q5 entschieden; Q6 (rekursiver Loop-Pfad) offen vor Slice 1. Skizze: `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- Slice 1 (Engine-Extraktion, ~3–5 d, opus) ist gated auf Q6-Entscheidung + lokale Postgres-DB.
- `DOCS_CONTROL.md` bleibt zentraler Einstieg; pro Aufgabe mitdenken, welche Doku im selben Arbeitsgang aktualisiert wird.

## Active Risks / Watchouts

- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:26432` — relevant fuer Slice 1 (Verhaltens-Paritaets-Beweis).
- Permission-Schema ist seit Slice 6.3d-iv vollstaendig definitionsgetrieben (`workflows.create.<definition_key>`).
- LA5 done: Task-Specs liegen am `workflow_node_id`. `workflow_definitions.approval_task_template_key` heisst nominell noch `_template_key` — Naming-Cleanup als FE-8 backlog.
- FE-9 done: Spec-Carry-Over via `WorkflowDefinitionNodeDto.Specs`. AdminTaskTemplate-Editor schreibt weiter auf published Version — Cross-Version-Leak bleibt Watch-Item (FE-11/13).

## Temporary Notes

- `KauthWorkflow/Architektur/Zielarchitektur.md` beschreibt das stabile Plattform-Zielbild.
- `PROJECT_STRUCTURE.md` und `web/README.md` muessen bei sichtbaren Admin-/UI-Verschiebungen mitgezogen werden.
- Diese Datei enthaelt nur noch Hinweise fuer die naechsten Sessions, keine laengere Historie.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
