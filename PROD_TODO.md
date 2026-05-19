# PROD_TODO.md

## Stand 2026-05-19 — Z21-Plan archiviert, Folge-Slices laufen als AGA-N*

Der urspruengliche Z21-Slice-Plan (PROD_TODO `Z21-S1..S6` + `S5b`) ist vollstaendig durch; auch der damalige Folgeblock `TODO.md Z21-S4` wurde inzwischen ueber Migrationspfad-Etappe 9a Schritt 1..8, Admin-Gated-Automation Slices 1..7 und AGA-N2 (echtes Entra-Re-Auth, 2026-05-19) abgearbeitet. Detail-Belege liegen in `CODE_REVIEW_ARCHIVE.md`, Commit-Spuren via `git log --grep "Z21-S"` bzw. `git log --grep "Etappe 9a"` / `git log --grep "Admin-Gated"` / `git log --grep "AGA-N2"`.

Aktuell ist in `TODO.md` kein produktionsblockierender Slice mehr offen. Verbleibende Punkte (`AGA-N1` InforLN-ERP stakeholder-blockiert, `AGA-N6` Builder-UI fuer `automation_output`-Bedingungen, `AGA-N7` operative Resthebel) liegen unter „Nachgelagert".

## Verhältnis zu `TODO.md`

`PROD_TODO.md` dokumentiert die historische Z21-Slice-Map und die Modell-/Effort-Empfehlungen. Aktive Folge-Slices nach Abschluss von Z21/9a laufen jetzt in `TODO.md` als `AGA-N*`. Für die historische Querverweis-Matrix gilt weiter:

| PROD_TODO | TODO.md | Inhalt |
|---|---|---|
| Z21-S1 | Z21-S1 | Simulation klar markieren + Mail-Dispatch-Health |
| Z21-S2 | Z21-S3 | Hybrid-AD-Architekturentscheidung |
| Z21-S3 | Z21-S2 | Workflow-Storno fuer laufende Vorgaenge |
| Z21-S4 | — | FE-UX-Buendel (Persona-Switcher, Detail-Tabs, directory_only, Nav-Reihenfolge) |
| Z21-S5 (UX) | Z21-S6 | Builder fachsprachlicher (Mapping-Labels + Wording) |
| Z21-S5b | Z21-S6b | AND/OR-Mehrbedingungen Decision-Conditions |
| Z21-S6 | — | `start-vm.sh dev`-Vorab-Check |
| — | Z21-S4 | Realer Automation-Pfad (historisch; inzwischen ueber 9a + Admin-Gated-Slices erledigt) |
| — | Z21-S5 | Runtime-/Admin-Sicht fehlgeschlagene Automation/Mailversand (done) |

## Verwandte Dateien

- `CODE_REVIEW.md` — aktueller offener Produkt-/Security-Fokus (verbleibende Resthebel nach Abschluss von AGA-N2)
- `TODO.md` — aktiver Plan (nachgelagert: `AGA-N1`, `AGA-N6`, `AGA-N7`)
- `CODE_REVIEW_ARCHIVE.md` — vollstaendige Z21-Done-Historie
- `MEMORY.md` — aktueller Fokus
- `DOCS_CONTROL.md` — Lesereihenfolge

## Schreibregel

Jeder neue Slice muss in `CODE_REVIEW.md` neben der Technik kurz erklaeren, was er praktisch bedeutet, warum er sich lohnt und was dadurch besser/sicherer/schneller/wartbarer wird.
