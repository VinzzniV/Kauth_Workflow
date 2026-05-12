# PROD_TODO.md

## Zweck

- Produkt-/UX-/Produktionsreife-Slices aus dem Z21-Review
- eigener Slice-Plan fuer den Schritt von „funktioniert im Dev-Modus" zu „produktiv verantwortbar"
- nicht doppelt zu `TODO.md` oder `FRONTEND_TODO.md` pflegen

## Nicht verwenden fuer

- abgeschlossene Detailhistorie → `CODE_REVIEW_ARCHIVE.md`
- aktive Review-Befunde → `CODE_REVIEW.md`
- frontend-spezifische Backlog-Items → `FRONTEND_TODO.md`
- backend-Review-Nacharbeit → `TODO.md`

## Stand 2026-05-12

Der urspruengliche Z21-Slice-Plan (S1..S6) ist abgeschlossen. Details + Belege liegen in `CODE_REVIEW.md` (aktive Findings) bzw. `CODE_REVIEW_ARCHIVE.md` (Detailhistorie). Commit-Spuren via `git log --grep "Z21-S"`.

## Offene Folge-Slices

| ID | Inhalt | Aufwand | Reasoning | Modell | Plan-Mode |
|---|---|---|---|---|---|
| **Z21-S5b** (in `TODO.md` Z21-S6b) | Decision-Conditions: AND/OR-Mehrbedingungen am Edge — Runtime + Schema + FE-Editor, Rueckwaertskompat fuer Single-Condition | mittel (~1 Tag) | high | opus | **an** |

Weitere offene Z21-Slices ausserhalb des PROD_TODO-Plans (Folge-Aufbauten, in `TODO.md` gefuehrt):
- Z21-S4 (TODO): Realer Automation-Pfad aus Hybrid-AD-Entscheidung — blockiert bis Migrationspfad-Etappe 9a Schritt 1 entschieden ist.
- Z21-S5 (TODO): Runtime-/Admin-Sicht fuer fehlgeschlagene Automation und Mailversand.
- Z21-S7..S10 (TODO): Durchlaufplanung-Absicherung, Workflow-Detail-Ergonomie, Mitarbeiter-Trennung, Produktions-Verifikationslauf.

## Verhaeltnis zu `TODO.md` (Slice-Nummerierung)

`PROD_TODO.md` und `TODO.md` fuehren eigene Z21-Slice-Nummerierungen. Mapping fuer Querverweise:

| PROD_TODO | TODO.md | Inhalt |
|---|---|---|
| Z21-S1 | Z21-S1 | Simulation klar markieren + Mail-Dispatch-Health |
| Z21-S2 | Z21-S3 | Hybrid-AD-Architekturentscheidung |
| Z21-S3 | Z21-S2 | Workflow-Storno fuer laufende Vorgaenge |
| Z21-S4 | — | FE-UX-Buendel (Persona-Switcher, Detail-Tabs, directory_only, Nav-Reihenfolge) |
| Z21-S5 (UX-Teil) | Z21-S6 (teil-done) | Builder fachsprachlicher (Mapping-Labels + Wording) |
| Z21-S5b | Z21-S6b | AND/OR-Mehrbedingungen Decision-Conditions (Resthebel) |
| Z21-S6 | — | `start-vm.sh dev`-Vorab-Check |

## Bewusst nicht im Z21-Slice-Plan

| ID | Grund |
|----|-------|
| P2-4 Frontend-Bundle | aktuell vertretbar, kein Endnutzer-Schmerz |
| P2-5 Tailwind-Tokens | unter `FRONTEND_TODO.md` „Bewusst NICHT angefasst" |
| P3-1 Inline-Styles `PeopleDirectoryPage` | Backlog |
| P3-3 R8/R10 | offene Nutzer-Aufgaben (Browser-Verifikation, Mobile-Layout), nicht code-pruefbar |

## Verwandte Dateien

- `CODE_REVIEW.md` — aktiver Review mit allen offenen Findings + Belegen
- `TODO.md` — backend-/review-bezogene Nacharbeit, zyklusuebergreifend
- `FRONTEND_TODO.md` — frontend-spezifische Backlog-Items
- `MEMORY.md` — kurzfristiger Fokus + aktive Watchouts
- `DOCS_CONTROL.md` — Doku-Lesereihenfolge und Pflege-Regeln
- `KauthWorkflow/Architektur/Entscheidungen.md` — Architektur-Entscheidungen

## Schreibregel

Jeder neue Slice muss in `CODE_REVIEW.md` neben der Technik kurz erklaeren, was er praktisch bedeutet, warum er sich lohnt und was dadurch besser/sicherer/schneller/wartbarer wird.
