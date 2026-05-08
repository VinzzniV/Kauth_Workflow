# DOCS_CONTROL.md

## Zweck

Diese Datei steuert, wie die Repo-Dokumentation gelesen und gepflegt wird.
Sie ist die Ordnungsdatei fuer Dokumentationsfluss, nicht der Ort fuer Fachlogik oder Implementierungsdetails.

---

## Obsidian Vault

Stabiles Domänenwissen und Architekturkontext leben im Vault unter `KauthWorkflow/`.
Einstieg: `KauthWorkflow/00 Start.md`

Vault-Dateien nach Thema:
- Architektur, Zielarchitektur, Entscheidungen, Migrationspfad → `KauthWorkflow/Architektur/`
- Domänenkonzepte und Glossar (Workflow, Rotation, Identity, Automation, Begriffe) → `KauthWorkflow/Domäne/`
- Betrieb, Setup, Deployment → `KauthWorkflow/Betrieb/`
- Aktueller Stand (Code Review, Onboarding-Entkopplung) → `KauthWorkflow/Stand/`
- KI-Workflow, Engineering-Regeln → `KauthWorkflow/Arbeit/`
- Einstieg und Lerneinheiten → `KauthWorkflow/00 Start.md`, `KauthWorkflow/Lernen/`

---

## Zuerst lesen

Vor allen nicht-trivialen Aenderungen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`
- `CODEX_SYNC.md` — immer lesen, wenn Codex zuletzt am Repo gearbeitet hat (neue Eintraege pruefen)
- `CLAUDE_CONTROL.md` — wenn Claude im Repo als Worker unter Codex-Orchestrierung arbeitet

Zusaetzlich je nach Aufgabe:
- `CODE_REVIEW.md` und `TODO.md` bei allen Aufgaben zum Rotations-/Durchlauf-Feature oder bei Review-Nacharbeit
- `PROJECT_STRUCTURE.md` fuer Dateilayout, Module und Einstiegspunkte
- `KauthWorkflow/00 Start.md` fuer Vault-Navigation und Lesepfade
- `KauthWorkflow/Architektur/Zielarchitektur.md` fuer das stabile Sollbild
- `KauthWorkflow/Architektur/Entscheidungen.md` fuer langfristige Architektur- und Produktentscheidungen
- `KauthWorkflow/Domäne/Begriffe.md` fuer Kernbegriffe bei Einarbeitung oder Mehrdeutigkeiten
- `KauthWorkflow/Arbeit/Engineering-Regeln.md` fuer Umsetzungs- und Handoff-Regeln
- `CLAUDE_CONTROL.md` fuer Rollen, Commit-Regel und Slice-Workflow zwischen Codex und Claude
- `KauthWorkflow/Betrieb/Setup.md` fuer lokale Entwicklung, Deployment und Laufzeitkonfiguration
- `web/README.md` fuer Frontend-Orientierung

Pflicht nach dem Lesen:
- Vor der Implementierung kurz festhalten, welche gelesenen Dateien bei dieser Aufgabe mitgezogen werden muessen, falls sich Annahmen, Strukturen, Verhalten, Scope oder Betrieb aendern.
- Doku-Updates werden nicht spaeter gesammelt, sondern im selben Arbeitsgang eingeplant.
- Wenn eine Aufgabe aus `TODO.md` abgeschlossen wurde, muss der Aufgabenstatus im selben Arbeitsgang auf `done` gesetzt werden.
- `MEMORY.md` muss immer aufgeraeumt werden, sobald Inhalte nicht mehr gebraucht werden.
- Dokumentationsdateien muessen auf dem aktuellen Ist-Stand gehalten werden; veraltete Hinweise sind im selben Arbeitsgang zu bereinigen.
- Wenn Codex Claude ueber die CLI orchestriert, muessen das vorgesehene Modell und der vorgesehene Reasoning-/Effort-Level nicht nur in `TODO.md`/`CODE_REVIEW.md` stehen, sondern bei jedem Lauf explizit per CLI-Parametern gesetzt werden.
- Wenn Codex Claude als Orchestrator steuert und ein Slice oder Zyklus abgeschlossen ist, muessen Claude und Codex die aktiven Steuerdateien (`TODO.md`, `CODE_REVIEW.md`, `MEMORY.md`, ggf. `CODEX_SYNC.md`) im selben Arbeitsgang entschlacken: abgeschlossene Detailbloecke entfernen, nur in Archiv-/Statusdateien stehen lassen und keine erledigten Altstaende in aktiven Dateien liegen lassen.

---

## Schreibziele

Verwende diese Dateien bewusst:

- stabile Projektwahrheit und Guardrails → `PROJECT_CONTEXT.md`
- verbindliche Zielarchitektur der Plattform → `KauthWorkflow/Architektur/Zielarchitektur.md`
- aktuelle CodeReview, Risiken und empfohlene Nacharbeit → `CODE_REVIEW.md`
- abgeschlossene Review-Details → `CODE_REVIEW_ARCHIVE.md`
- langfristige Architektur- und Produktentscheidungen → `KauthWorkflow/Architektur/Entscheidungen.md`
- kurzfristiger Session-Kontext, aktive Risiken, naechste Schritte → `MEMORY.md`
- Repo-/Modulstruktur und wichtige Einstiegspunkte → `PROJECT_STRUCTURE.md`
- aeltere Claude/Codex-Handoffs → `CODEX_SYNC_ARCHIVE.md`
- lokale Setup-, Deploy- und Laufzeitdoku → `KauthWorkflow/Betrieb/Setup.md`
- priorisierte Arbeitspakete fuer das Rotations-/Durchlauf-Feature → `TODO.md`
- Claude-Steuerung unter Codex-Orchestrierung → `CLAUDE_CONTROL.md`
- Frontend-spezifische Orientierung → `web/README.md`

---

## Mindest-Updates nach Aenderungen

Wenn sich diese Bereiche aendern, muessen die passenden Dokus im selben Arbeitsgang mitgezogen werden:

- Zielbild, Plattformbegriffe, Migrationsannahmen → `KauthWorkflow/Architektur/Zielarchitektur.md` und ggf. `KauthWorkflow/Architektur/Entscheidungen.md`
- Review-Priorisierung, Reihenfolge oder Deliverables der Nacharbeit → `CODE_REVIEW.md` oder `TODO.md`
- stabile Produktregeln oder Guardrails → `PROJECT_CONTEXT.md`
- neue Ordner, neue Entry-Points, umbenannte Module → `PROJECT_STRUCTURE.md`
- veraendertes Runtime-, Compose-, Deploy- oder Env-Verhalten → `KauthWorkflow/Betrieb/Setup.md`
- neue offene Risiken oder bewusst unvollstaendige Nacharbeiten → `MEMORY.md`
- veraenderte Frontend-Modulgrenzen oder Admin-/Builder-Flows → `web/README.md`

---

## Lesereihenfolge nach Aufgabentyp

Fuer Architektur-, Datenmodell- oder Runtime-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `KauthWorkflow/Architektur/Zielarchitektur.md`
4. `MEMORY.md`
5. `PROJECT_STRUCTURE.md`

Fuer Rotations-/Durchlauf-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `CODE_REVIEW.md`
4. `TODO.md`
5. `MEMORY.md`
6. `PROJECT_STRUCTURE.md`

Fuer Backend- oder Full-Stack-Feature-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `MEMORY.md`
4. `PROJECT_STRUCTURE.md`
5. `CLAUDE_CONTROL.md`, falls Claude als Worker unter Codex-Orchestrierung arbeitet
6. aufgabenspezifische Dokus

Fuer Frontend-Arbeit:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `CODE_REVIEW.md` und `TODO.md`, falls Rotations-/Durchlauf-UI oder Review-Nacharbeit betroffen ist
4. `web/README.md`
5. `MEMORY.md`

Fuer Infra, Auth oder Deployment:
1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `KauthWorkflow/Betrieb/Setup.md`
4. `KauthWorkflow/Architektur/Zielarchitektur.md`
5. `.env.prod.example` und Compose-Dateien

---

## Hygiene-Regeln

- Dieselbe Wahrheit nicht ohne Grund in mehreren Dateien pflegen.
- Stabile Informationen gehoeren in den Vault (`KauthWorkflow/`), nicht nur in `MEMORY.md`.
- Unsichere oder temporaere Notizen gehoeren nicht vorschnell in `PROJECT_CONTEXT.md`.
- `CODE_REVIEW.md` ist die aktuelle Review-Quelle fuer Nacharbeit, nicht der Ort fuer Session-Notizen.
- Nach dem Lesen der Pflichtdokumente immer aktiv pruefen, welche davon durch die aktuelle Aufgabe aenderungsrelevant werden.
- Wenn eine Datei ihren Zweck nicht mehr trifft, Inhalt verschieben oder Beschreibung anpassen.
- `MEMORY.md` darf nur aktive, kurzfristig relevante Hinweise enthalten und muss bei veralteten Eintraegen sofort bereinigt werden.
- Markdown-Dokumente sind keine Ablage fuer veraltete Zwischenstaende; wenn sich der Ist-Stand aendert, muessen die betroffenen Dateien direkt mitgezogen werden.
- Aktive Steuerdateien bleiben absichtlich kurz; abgeschlossene Zyklen, Slices und Detailtabellen gehoeren nach Abschluss in `CODE_REVIEW_ARCHIVE.md`, `CODEX_SYNC_ARCHIVE.md` oder einen passenden Statusspiegel, nicht dauerhaft in `TODO.md` oder `CODE_REVIEW.md`.
