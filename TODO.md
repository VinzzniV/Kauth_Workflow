# TODO.md

Diese Datei steuert die Reihenfolge der Umsetzung aktiver Review-Zyklen.
Die aktuelle Priorisierung und Review-Begruendung stehen zentral in `CODE_REVIEW.md`.

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen (Priorisierungs- und Analyseabschnitte).

Zusaetzlich immer mitlesen: `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `MEMORY.md`.

Pflicht nach dem Lesen:
- Vor der Umsetzung kurz festhalten, welche Dokus mitgezogen werden muessen, falls sich Struktur, Scope, Verhalten, Setup oder Risiken aendern.
- Doku-Aenderungen gehoeren in denselben Arbeitsgang wie die Code-Aenderung.
- Bei Abschluss Status hier auf `done` setzen.

## Pflicht zwischen Aufgaben

Bevor die KI mit einer neuen Aufgabe anfaengt, **muss** sie ansagen:

1. **Welche Aufgabe als naechstes ansteht** (mit ID/Block-Bezeichner aus TODO.md)
2. **Reasoning Effort** (`low` / `medium` / `high`)
3. **Empfohlenes Modell** (`sonnet` / `opus`)

Format-Beispiel: *„Naechster Schritt: Z7-1.1 Lifecycle-Inventur. Reasoning: high. Modell: opus."*

## Aufgabenteilung Codex/Claude

> Fuer jede Aufgabe gilt: Die KI, die sie abschliesst, traegt Datum + kurze Aenderungszusammenfassung in `CODEX_SYNC.md` ein.
> Claude liest `CODEX_SYNC.md` am Sitzungsanfang, um Codex-Aenderungen nachzuvollziehen.

---

## Aktiver Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Detail und Begruendung in `CODE_REVIEW.md` § "Aktiver Zyklus 8" und in `KauthWorkflow/Stand/Code-Review-Status.md`. Frontend ist nicht betroffen, solange keine API-Vertraege brechen.

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z8-1.1 | Inventur: unbegrenztes Laden, In-Memory-Filter/-Sort, N+1 | HIGH | high | opus | offen |
| Z8-1.2 | Top-3-Hotspot-Auswahl + Slice-Plan | HIGH | high | opus | wartet auf Z8-1.1 |
| Z8-2.x | SQL-Pushdown / Pagination der Top-Hotspots | HIGH | medium..high | sonnet/opus | wartet auf Z8-1.2 |
| Z8-3 | Sweep- und Dispatch-Performance | MEDIUM | medium | sonnet | offen |
| Z8-4 | Test-Coverage fuer neu gepushte Pfade | MEDIUM | medium | sonnet | wartet auf Z8-2 |

**Naechster Schritt:** Z8-1.1 (Inventur). Reasoning: high. Modell: opus. Output: nummerierte Hotspot-Liste mit Aufrufer-Pfad und Datenkardinalitaet, ohne Code-Change.

---

## Abgeschlossener Zyklus 7 — Lifecycle-Service-Konsolidierung (2026-05-05)

Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 7" und in `KauthWorkflow/Stand/Code-Review-Status.md`.

Ergebnis: Lifecycle-Service ist Commit-Grenze fuer Create/Form/Approval/Task; Validation-Service ist in Draft-/Snapshot-/Helper-/Catalog-Slices aufgeteilt.

---

## Watch-Items / Defer

| ID | Aufgabe | Status |
|----|---------|--------|
| LQ2-Z3 | `EntraDirectorySyncService` (2485 Z.) Split | defer ohne Trigger (Risiko niedrig — Timer-Pfad, kein User-Pfad) |

---

## Offene Restposten (zyklusuebergreifend)

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |

---

## Abgeschlossene Zyklen

Zyklen 1–7 (2026-04-23 bis 2026-05-05) sind abgeschlossen. Detail-Historie via `git log`; Highlights pro Zyklus in `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Abschlussregel fuer jede KI-Aufgabe

Nach jedem groesseren Schritt muss berichtet werden:
1. Welche Dateien wurden geaendert?
2. Auf welchen Abschnitt in `CODE_REVIEW.md` wurde gearbeitet?
3. Wie passt die Aenderung zur Zielarchitektur?
4. Welche Risiken oder Luecken bleiben offen?
5. Welche Tests wurden angepasst oder fehlen noch?
6. Welche Doku musste mitgezogen werden?
7. Wurde die erledigte Aufgabe in `TODO.md` auf `done` gesetzt?
