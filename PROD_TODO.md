# PROD_TODO.md

## Stand 2026-05-12 — Slice-Plan abgeschlossen

Der Z21-Slice-Plan (PROD_TODO Z21-S1..S6 + S5b) ist vollständig durch. Detail-Belege liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung"). Commit-Spuren via `git log --grep "Z21-S"`.

Aktiver, offener Folge-Slice (HIGH) ist **TODO.md Z21-S4 „Realer Automation-Pfad"** — blockiert bis Migrationspfad-Etappe 9a Schritt 1 (Worker-Deployment, Transport, AD-Schreibmechanik, Auth, Audit-Rückkanal) entschieden ist.

## Verhältnis zu `TODO.md`

`PROD_TODO.md` und `TODO.md` führen eigene Z21-Slice-Nummerierungen. Für Querverweise:

| PROD_TODO | TODO.md | Inhalt |
|---|---|---|
| Z21-S1 | Z21-S1 | Simulation klar markieren + Mail-Dispatch-Health |
| Z21-S2 | Z21-S3 | Hybrid-AD-Architekturentscheidung |
| Z21-S3 | Z21-S2 | Workflow-Storno fuer laufende Vorgaenge |
| Z21-S4 | — | FE-UX-Buendel (Persona-Switcher, Detail-Tabs, directory_only, Nav-Reihenfolge) |
| Z21-S5 (UX) | Z21-S6 | Builder fachsprachlicher (Mapping-Labels + Wording) |
| Z21-S5b | Z21-S6b | AND/OR-Mehrbedingungen Decision-Conditions |
| Z21-S6 | — | `start-vm.sh dev`-Vorab-Check |
| — | Z21-S4 | Realer Automation-Pfad (offen, blockiert) |
| — | Z21-S5 | Runtime-/Admin-Sicht fehlgeschlagene Automation/Mailversand (done) |

## Verwandte Dateien

- `CODE_REVIEW.md` — aktive offene Findings (P0-1 Automation real, P0-2 Hybrid-AD-Implementierung)
- `TODO.md` — aktiver Plan, einzig offener Punkt Z21-S4
- `CODE_REVIEW_ARCHIVE.md` — vollstaendige Z21-Done-Historie
- `MEMORY.md` — aktueller Fokus
- `DOCS_CONTROL.md` — Lesereihenfolge

## Schreibregel

Jeder neue Slice muss in `CODE_REVIEW.md` neben der Technik kurz erklaeren, was er praktisch bedeutet, warum er sich lohnt und was dadurch besser/sicherer/schneller/wartbarer wird.
