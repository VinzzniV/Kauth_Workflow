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

## Aktiver Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

Z9 ist eroeffnet. Thema: LQ2-Z3 als aktiver Hebel — Test-Isolation der in Z8-2.3 eingefuehrten Batch-Helfer durch File-Split. Begruendung und Slice-Plan in `CODE_REVIEW.md` § "Aktiver Zyklus 9".

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z9-1.1 | Boundary-/Split-Inventur (oeffentliche API, Aufrufer, interne Achsen, Test-Isolations-Hindernisse) | HIGH | high | opus | done (2026-05-05) — `CODE_REVIEW.md` § Z9-1.1 |
| Z9-1.2 | Extract-Plan: File-/Klassen-Schnitt + Reihenfolge + Test-Strategie | HIGH | high | opus | done (2026-05-05) — `CODE_REVIEW.md` § Z9-1.2 |
| Z9-2.1 | Pre-Cleanup (DepartmentLead-Resolver + Single-Row-Helfer loeschen) + `SyncAllAsync`-Phasen-Strukturierung in der Hauptdatei | HIGH | medium..high | sonnet | done (2026-05-05) — Dead-Code geloescht (`SyncDepartmentLeadAssignmentsFromDirectory` + 4 Sub-Helfer + `LogDirectoryAuditEventAsync` + `CreateDepartmentLeadAuditSnapshot` + Records; `UpsertDirectoryIdentity`/`InsertGroupMembership`); Reflection-Test entfernt; `SyncAllAsync` in `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync` zerlegt |
| Z9-2.2 | Graph-Adapter (`IEntraGraphClient` + `EntraGraphClient`) unter `Services/Directory/` extrahieren | HIGH | medium..high | sonnet | offen — naechster Schritt |
| Z9-2.3 | DB-Sync-Operations-Modul (`IEntraDirectorySyncOperations` + Impl) unter `Services/Directory/` extrahieren | HIGH | medium | sonnet | offen — wartet auf Z9-2.2 |
| Z9-3 | Coverage: Integration-Tests fuer `UpsertDirectoryIdentitiesBatch`/`InsertGroupMembershipsBatch` + Unit-Tests Orchestrator gegen Graph-/Ops-Stubs | MEDIUM | medium | sonnet | offen — nach Z9-2.x |

**Naechster Schritt:** Z9-2.2 — Graph-Adapter (`IEntraGraphClient` + `EntraGraphClient`) unter `api/API/Services/Directory/` extrahieren gemaess `CODE_REVIEW.md` § Z9-1.2.

---

## Abgeschlossener Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Z8 ist geschlossen. Alle priorisierten Hotspots sind entweder gepushed (#1/#2/#3/#4/#7), als false positive verifiziert (#5) oder bewusst deferred (#8). Z8-4 Coverage abgeschlossen; Coverage fuer EntraDirectorySync-Batch-Helfer in LQ2-Z3 verschoben.



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
| Z8-4 | Test-Coverage fuer neu gepushte Pfade | MEDIUM | medium | sonnet | done (2026-05-05) — Z8-4.1 Bulk-Recipient-Helper; Z8-2.1/Z8-2.2-Coverage liegt in den Umsetzungs-Slices; EntraDirectorySync-Batch-Helfer-Coverage in LQ2-Z3 verschoben |

Z8 abgeschlossen; Folge-Zyklus Z9 eroeffnet (siehe oben).

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` (2591 Z.) Split — inkl. Coverage fuer `UpsertDirectoryIdentitiesBatch`/`InsertGroupMembershipsBatch` | Zyklus 3 / Z8 → Z9 | aktiv als Zyklus 9 (2026-05-05) |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Abgeschlossene Zyklen

- Zyklus 7 ist abgeschlossen. Kurzfassung in `CODE_REVIEW.md`, Detail in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Stand/Code-Review-Status.md`.
- Zyklus 8 ist abgeschlossen (2026-05-05). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 8".

---

## Arbeitsregel

Vor dem Start einer Aufgabe immer explizit nennen:
1. welche Aufgabe als naechstes ansteht
2. welches Reasoning sinnvoll ist
3. welches Modell empfohlen ist
