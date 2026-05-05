# Code Review Status

#stand #review

Aktueller Stand der Code Review. Was offen ist.

Primärquelle im Repo: `CODE_REVIEW.md`

> Diese Datei kann veralten. Für den aktuellen Stand immer `CODE_REVIEW.md` und `TODO.md` im Repo prüfen.

## Zweck

- menschlich lesbarer Spiegel des aktuellen Review-Status
- komprimierte Orientierung ohne die aktive Root-Datei zu ersetzen

## Nicht verwenden fuer

- den exakten naechsten Arbeitsschritt
- Slice-fuer-Slice-History abgeschlossener Zyklen

---

## Gesamtbewertung (Stand 2026-05-05 — nach Zyklus 7, Zyklus 8 aktiv)

| Bereich | Note | Hauptgrund |
|---------|------|-----------|
| Backend-Architektur | A- | Repo-Monolith reduziert; Lifecycle-Service nach Z7 zentrale Commit-Grenze fuer Runtime-/Task-Mutationen; Resthebel jetzt vor allem Skalierbarkeit |
| Datenbankdesign | A- | Solides Schema, gute Constraints |
| Auth & Berechtigungen | B+ | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | B+ | Engine als Domain-Service + HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | B+ | Builder + Listen-Workspaces refactored; Split-Views + Karten/Tabelle; Personenakte 360° |
| Testbarkeit | B | Testcontainers + Integration-Tests; Lifecycle-Service hat jetzt einen eigenen Service-Test fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | B- | Workflow-Task-Filter SQL-pre-narrowed |
| Sicherheit | B+ | `/client/log-events` rate-limited; dev-sim-Guard verifiziert |
| Lesbarkeit | B+ | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen eher bei Lastpfaden als bei Strukturhygiene |

---

## Zyklus-Historie

| Zyklus | Datum | Stichwort |
|--------|-------|-----------|
| 1 | 2026-04-23 | Code-Review + Hardening: C1–C4, H1–H7, L1/L3/L5/L6 |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, Task-Filter SQL-Push, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: HQ1-Z3 (Hook-Tests), HQ3-Z3 (Repo-Splits), HQ2-Z3 (Hook-Zerlegung) |
| 4 | 2026-05-02 | Naming + Härtungen: HQ1-Z4 LegacyProcessTypeKey, HQ2-Z4 effectiveResponsibilityIds, LQ1-Z4..LQ4-Z4 |
| 5 | 2026-05-02..03 | Legacy-Abbau: LA1 (LegacyWorkflowStatus), LA2 (setup-Node), LA3 (definition_key Filter), LA4 (HasLegacyRolePermission), LA5 (Specs am Node) |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope; 409 Tests gruen |
| 7 | 2026-05-05 | Lifecycle-Service-Konsolidierung (Z7-1 in 5 Sub-Slices) + Validation-Service-Split (Z7-3 in 4 Sub-Slices); 416/417 Tests gruen |

---

## Aktiver Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Naechster Schwerpunkt nach Abschluss von Zyklus 7. Skalierbarkeit ist mit B- die niedrigste Gesamtnote; Hygiene-Refactors (Lifecycle, Validation) sind erledigt. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 8".

| Befund | Prio | Status |
|--------|------|--------|
| Z8-1.1 — Inventur unbegrenztes Laden / In-Memory-Filter / N+1 | HIGH | done 2026-05-05 — Hotspot-Liste in `CODE_REVIEW.md` § Z8-1.1 |
| Z8-1.2 — Top-3-Hotspot-Auswahl + Slice-Plan | HIGH | done 2026-05-05 — Slice-Plan in `CODE_REVIEW.md` § Z8-1.2 |
| Z8-2.1 — Hotspot #1 `WorkflowCatalogService` N+1-Aufloesung | HIGH | done 2026-05-05 — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 — Hotspot #2+#3 `RotationNotificationService` Sweep+Apply (gemeinsamer Slice) | HIGH | done 2026-05-05 — Sweep batched (200), Apply Bulk-Metadata+Bulk-UPDATE |
| Z8-2.3 — Hotspot #4 `EntraDirectorySyncService` Group/Member Batch | HIGH | done 2026-05-05 — Bulk-Upsert via `unnest`+RETURNING und Bulk-Insert fuer Memberships ersetzen pro-Member Round-Trips |
| Z8-3.1 — #5 Verifikation + #7 Recipient-Bulk-Lookup | HIGH | done 2026-05-05 — #5 false positive (CPU/Policy), #7 Bulk-Lookup |
| Z8-3.2 — Hotspot #8 `RotationTaskGenerationService` | MEDIUM | deferred 2026-05-05 — kein kleiner SQL-/Batch-Hebel ohne breiten Umbau; admin-getriggert. Z8-3 geschlossen |
| Z8-4 — Test-Coverage fuer neu gepushte Pfade | MEDIUM | offen — Naechster Schritt |

Frontend-Folgen: aktuell **keine**. Z8 ist backend-fokussiert; FE-Items entstehen erst, falls API-Vertraege brechen.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` (2485 Z.) Split | deferred — kein Trigger |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein kleiner SQL-Hebel |
| FE-8 | `approval_task_template_key` → `approval_spec_key` Rename | defer ohne Trigger |

---

## Verwandte Notizen

- `CODE_REVIEW_ARCHIVE.md` — Detailarchiv abgeschlossener Review-Zyklen
- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Schritt7-Runtime-TaskSystem-Skizze]] — Architekturarbeit aus Zyklus 6
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
