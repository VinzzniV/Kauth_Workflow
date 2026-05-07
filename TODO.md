# TODO.md

## Zweck

- aktive Arbeitsplanung fuer Review-Nacharbeit
- nur offene oder unmittelbar relevante Arbeit

## Primaerquelle fuer

- naechsten Arbeitsschritt
- Reihenfolge der offenen Slices

## Nicht verwenden fuer

- lange Historie abgeschlossener Slices
- Architekturargumentation
- Session-Notizen

## Wann aktualisieren

- wenn ein neuer Zyklus startet
- wenn sich Priorisierung aendert
- wenn eine Aufgabe abgeschlossen oder deferred wird

## Verwandte Dateien

- `CODE_REVIEW.md`
- `MEMORY.md`
- `CODEX_SYNC.md`
- `FRONTEND_TODO.md`

---

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen.

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Schreibregel: jedes neue Review-Finding / jeder Slice muss neben dem technischen Befund kurz erklaeren, was er praktisch bedeutet, warum es sich lohnt, ihn anzugehen, und was dadurch besser, sicherer, schneller oder wartbarer wird. Detail in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

---

## Abgeschlossener Zyklus 14 — Mehrrollen-Persona-Kollisionen

Eroeffnet 2026-05-07 als reiner Review-/Planungszyklus, abgeschlossen 2026-05-07. **Keine Implementierung in Z14.** Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 14".

**Praktisch:** Nutzer mit mehreren Rollen (Admin + Fachbereich/Manager/HR) verlieren ihre fachlich erwartete Ansicht, weil die Persona-Ableitung sie auf `generic` zwingt. **Lohnenswert:** trifft genau die Power-User; Logik liegt zentral, Hebel pro Aufwand hoch. **Nutzen:** klarer Vertrag Rolle vs. Persona vs. aktive Ansicht, vorhersagbares Login-Verhalten, sauberer Andockpunkt fuer kuenftige Personas.

| ID | Aufgabe | Prio | Status |
|----|---------|------|--------|
| Z14-1.1 | Inventur Persona-/Mehrrollen-Kollisionen (alle Stellen, an denen `dashboardPersona` / `hasMultipleRoles` Sicht/Aktionen/Navigation/Insights kollabieren) | HIGH | done 2026-05-07 — Inventur in `CODE_REVIEW.md` § Z14-1.1 (Override `useRoleAwareNavigation.ts:253` + Routing-Override `roleModel.ts:210`; vier Sicht-Konsumenten kollabieren auf `generic`; Header/Aktionen/Routen-Guards bleiben capability-getrieben und sind nicht betroffen) |
| Z14-1.2 | Vertrags-/UX-Entscheidung: Begriffsklaerung + Optionen-Skizze fuer Mehrrollen-Behandlung (Switcher / Aggregat / Vorrang / Login-Auswahl) | HIGH | done 2026-05-07 — Vertrag in `CODE_REVIEW.md` § Z14-1.2 (Begriffsraster Rolle/Persona/aktive Ansicht; vier Optionen mit Pro/Contra; Vorzugsrichtung Persona-Switcher mit Vorrangs-Default + Persistenz, Admin-Vorrang als Default-Regel, Fallback `generic`; Vertragspflichten Default/Persistenz/Fallback/Schalter/Login-Routing getrennt; Capability-Schicht und Routen-Guards explizit unberuehrt; Andock an `loadDashboardInsights`/Query-Key/`DashboardOverview`-Schalter beschrieben) |
| Z14-1.3 | Slice-Plan Folgezyklus: 2–3 sichere Umsetzungsslices mit Reihenfolge-Begruendung | HIGH | done 2026-05-07 — Slice-Plan in `CODE_REVIEW.md` § Z14-1.3 (drei Slices: I „aktive Ansicht" als reines Datenmodell + Persistenz + Fallback ohne Sicht-Konsumenten; II zwei Override-Stellen aus Z14-1.1 zusammen auf die neue Quelle umstellen; III sichtbarer Persona-Switcher nur fuer `hasMultipleRoles === true`; Reihenfolge Datenmodell → Routing/Sicht → UI; pro Slice Modell `claude-sonnet-4-6` + `--effort medium`; Loader und drei `DashboardOverview`-Schalter bleiben unangetastet) |

**Zyklus 14 vollstaendig abgeschlossen (2026-05-07).** Kein aktiver Zyklus.

**Naechster Schritt:** Codex eroeffnet den Folgezyklus (Arbeitstitel Zyklus 15) auf Basis Z14-1.3 Slice I (Datenmodell „aktive Ansicht") und erzwingt `--model claude-sonnet-4-6` + `--effort medium` per CLI.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` Split + Coverage Z8-2.3-Batch-Helfer | Zyklus 3 / Z8 → Z9 | abgeschlossen als Zyklus 9 (2026-05-05) |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Abgeschlossene Zyklen

- Zyklen 7 bis 14 sind abgeschlossen.
- Kurzfassungen und Begruendungen stehen in `CODE_REVIEW.md`.
- Detailspiegel stehen in `CODEX_SYNC.md` und `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Arbeitsregel

Vor dem Start einer Aufgabe immer explizit nennen:
1. welche Aufgabe als naechstes ansteht
2. welches Reasoning sinnvoll ist
3. welches Modell empfohlen ist
4. wenn Claude per CLI laeuft: `--model` und `--effort` explizit setzen, nicht nur im Prompt empfehlen
