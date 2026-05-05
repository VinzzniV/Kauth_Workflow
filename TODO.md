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

---

## Aktiver Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Detail und Begruendung in `CODE_REVIEW.md` § "Aktiver Zyklus 8" und in `KauthWorkflow/Stand/Code-Review-Status.md`.

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z8-1.1 | Inventur: unbegrenztes Laden, In-Memory-Filter/-Sort, N+1 | HIGH | high | opus | done (2026-05-05) — Ergebnis in `CODE_REVIEW.md` § Z8-1.1 |
| Z8-1.2 | Top-3-Hotspot-Auswahl + Slice-Plan | HIGH | high | opus | done (2026-05-05) — Slice-Plan in `CODE_REVIEW.md` § Z8-1.2 |
| Z8-2.1 | Hotspot #1 — `WorkflowCatalogService` N+1 fuer `IsManagerCreatableDefinition` aufloesen | HIGH | medium..high | opus | done (2026-05-05) — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 | Hotspot #2+#3 (gemeinsamer Slice) — `RotationNotificationService` Daily-Sweep `LIMIT`/Batch + Batch-Update Apply | HIGH | medium..high | sonnet | done (2026-05-05) — Sweep batched (BatchSize 200), Apply nutzt Bulk-Metadata + Bulk-UPDATE via `unnest` |
| Z8-2.3 | Hotspot #4 — `EntraDirectorySyncService.SyncAllAsync` Group-Member-Schleifen auf Batch-Upsert/-Insert | HIGH | medium | sonnet | done (2026-05-05) — Bulk-Upsert via `unnest`+RETURNING und Bulk-Insert fuer Memberships statt pro-Member Round-Trips |
| Z8-3.1 | Hotspot #5 Verifikation + Hotspot #7 Recipient-Bulk-Lookup | HIGH | medium | sonnet | done (2026-05-05) — #5 false positive (CPU-/Policy-Pfad), #7 nutzt jetzt `LoadActiveUserNotificationRecipientsBulk` einmalig (auch im Create-Pfad mitgezogen) |
| Z8-3.2 | Hotspot #8 `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` | MEDIUM | medium | sonnet | deferred (2026-05-05) — kein kleiner SQL-/Batch-Hebel ohne breiten Umbau; admin-getriggert. Z8-3 damit geschlossen. Detail: `CODE_REVIEW.md` § Z8-3.2 |
| Z8-4 | Test-Coverage fuer neu gepushte Pfade | MEDIUM | medium | sonnet | in Arbeit — Z8-4.1 done (2026-05-05): Integration-Tests fuer `LoadActiveUserNotificationRecipientsBulk`. EntraDirectorySync-Batch-Helfer als nicht-sauber-isoliert-testbar offen benannt (gehoert zu LQ2-Z3-Split). |

**Naechster Schritt:** Z8 abschliessen — entscheiden, ob weitere Coverage (z. B. RotationNotification-Sweep-Batching, EntraDirectorySync ueber LQ2-Z3-Split) noch in Z8 oder erst nach Split-Slice angegangen wird. Reasoning: medium. Modell: sonnet.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` (2485 Z.) Split | Zyklus 3 | deferred — kein Trigger |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Abgeschlossene Zyklen

- Zyklus 7 ist abgeschlossen. Kurzfassung in `CODE_REVIEW.md`, Detail in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Arbeitsregel

Vor dem Start einer Aufgabe immer explizit nennen:
1. welche Aufgabe als naechstes ansteht
2. welches Reasoning sinnvoll ist
3. welches Modell empfohlen ist
