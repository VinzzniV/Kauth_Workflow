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

## Schreibregel (verbindlich)

Jedes Review-Finding und jeder Slice in dieser Datei wird neben dem technischen Befund kurz aus Nutzersicht erklaert: was es praktisch bedeutet, warum es sich lohnt, das anzugehen, und was dadurch besser, sicherer, schneller oder wartbarer wird. Detail in `CODE_REVIEW.md` § „Schreibregel fuer Reviews und Findings" und `CLAUDE_CONTROL.md`.

---

## Gesamtbewertung (Stand 2026-05-06 — Zyklus 11 abgeschlossen, kein aktiver Zyklus; Zyklus 8/9/10 abgeschlossen)

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
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung: #1 Bulk-Lookup, #2/#3 Sweep+Apply Batching, #4 Entra Group/Member Bulk, #7 Recipient-Bulk; #5 false positive; #8 deferred; Z8-4 Coverage |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit (LQ2-Z3) — abgeschlossen (Split + Coverage) |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — abgeschlossen (Review-/Planungszyklus, alle Slices done) |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege in Umsetzung — abgeschlossen (F1 P1+B Master-Data, F2 P2+Audit, F3 P1+D Builder, alle Slices done) |

---

## Abgeschlossener Zyklus 11 — Admin-/Master-Data-Listen-Vertraege in Umsetzung (2026-05-05..06)

Eroeffnet 2026-05-05, abgeschlossen 2026-05-06 als reiner Umsetzungszyklus auf Basis von Z10-1.3. Reihenfolge F1 → F2 → F3 streng sequenziell durchgespielt. **Alle drei Slices done.** Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 11".

**Praktisch:** F1 macht Erfassungseinstieg und Master-Data-Pflege spuerbar schnell und konsistent; F2 macht Audit-Verlauf vollstaendig durchsuchbar (heute schneidet ein stiller `limit`-Cap die Historie ab); F3 macht den Builder schnell, auch wenn Definitionen wachsen. **Lohnenswert:** Vertrag jetzt sauber bauen, statt unter Last halbgaarig nachzuruesten — drei kontrollierte Slices statt N parallele Mini-Vertraege. **Nutzen:** zwei zentrale Hull-Typen (`AdminListPageDto<T>`, `CursorPageDto<T>`), zwei typed FE-Adapter, eine wiederverwendbare Refactor-Achse fuer alle spaeter folgenden Listen.

| Befund | Prio | Status |
|--------|------|--------|
| Z11-F1 — P1-Hull einfuehren + B Master-Data/Lookups (`/departments`, `/roles`, `/admin/master-data/{departments,positions,responsibilities}`); Server-`search`/`sort`-Whitelist; FE: typed Wrapper + Aufrufer-Refactor | HIGH | done 2026-05-05 — P1-Hull live fuer 5 Endpunkte, FE-Adapter aktiv, aktuelle Consumer auf `items` adaptiert |
| Z11-F2 — P2-Hull einfuehren + Audit-Streams (`/admin/auth/audit`, `/admin/directory/audit`); opaque Base64-Cursor; FE: typed Wrapper + „Mehr laden"-Knopf | HIGH | done 2026-05-06 — `CursorPageDto<T>` + Keyset-Pagination, typed FE-Wrapper, Akkumulations-Hook, „Mehr laden"-Knopf in beiden Audit-Tabs |
| Z11-F3 — P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte: `workflow-definitions`, `action-definitions`, `task-templates` + `…/conditions` + `…/dependencies`, `answer-definitions`, `role-answer-defaults`); Pflicht-Scope; Filterzustand im FE in URL-Query als separater UI-Slice ausgelagert | HIGH | done 2026-05-06 — sieben scoped Builder-Endpunkte auf P1, FE-Service-Wrapper + alle Builder-Hooks auf `page.items` adaptiert |

**Nicht in Z11 (Begruendung in `CODE_REVIEW.md` § Z10-1.3):** A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation Action-Templates, G Runtime-Sub-Resources, H `/workflow-definitions/startable`. Diese werden nach Z11 als billige Mitnahmeschnitte mit dem dann etablierten P1-/P2-Adapter geplant; der Composite-Split fuer C Gaps/Pending bleibt eigener vorbereiteter Slice.

Frontend-Folgen Z11-Fx: Konsequenz pro Slice, kein praeventiver Sammeleintrag — Z11-F1, Z11-F2 und Z11-F3 sind in `FRONTEND_TODO.md` als abgeschlossene Trigger vermerkt.

---

## Abgeschlossener Zyklus 10 — Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken (2026-05-05)

Eroeffnet und abgeschlossen 2026-05-05 als reiner Review-/Planungszyklus. Alle drei Slices done. Formaler Zyklusabschluss vollzogen. Naechster Schritt: Eroeffnung Z11 auf Basis F1/F2/F3 aus `CODE_REVIEW.md` § Z10-1.3. Folge-Hebel aus Z8-1.2 (#6 `GetDepartmentsAsync`/`GetRolesAsync` ohne Pagination), aber breiter gefasst: das Vertrags-Thema betrifft mehrere Admin-/Master-Data-/Directory-Read-Pfade und nicht nur Departments/Rollen.

**Praktisch:** Admin-Listen werden bei wachsendem Bestand spuerbar langsamer; Suche und Filter laufen heute ueberwiegend im Browser, deshalb fuehlen sich Ergebnisse irgendwann unvollstaendig oder „zufaellig sortiert" an. **Lohnenswert:** ein einheitlicher Listen-/Such-/Sort-Vertrag jetzt zu definieren ist deutlich billiger als spaeterer Hotfix unter Last und vermeidet API-Brueche fuer das FE. **Nutzen:** stabile Antwortzeiten, vollstaendige Server-Suche, klarer Vertrag, der an mehreren Endpunkten gleich aussieht und so neue Listen direkt mitnimmt.

| Befund | Prio | Status |
|--------|------|--------|
| Z10-1.1 — Inventur aller Admin-/Master-Data-/Directory-Read-Endpunkte ohne Pagination/Suche/Sort-Vertrag (Datei/Symbol, FE-Aufrufer, Kardinalitaet, Spuerbarkeit fuer Nutzer) | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.1 (Bloecke A Identity, B Master-Data + `/departments`/`/roles`, C Directory, D Builder, E Notification-Templates, F Rotation, G Runtime-Subresources, H Startable; saubere Listen und Single-Doc-Endpunkte explizit ausgenommen) |
| Z10-1.2 — Vertrags-Skizze pro Endpunkt (`limit`/`offset` vs. Cursor, Server-`search`, stabiler `sort`, Antwort-Hull) inkl. FE-Adaption-Folgen | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.2 (Muster P1 Standard Admin Page / P2 Cursor Stream / P3 Typeahead Lookup; Bloecke A–H; Identity-/Directory-/Audit getrennt nach P1/P2; Builder als scoped P1; Runtime-Sub-Resources P2) |
| Z10-1.3 — Slice-Plan fuer Folgezyklus: erste 2–3 sichere Umsetzungsslices mit Begruendung der Reihenfolge | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z10-1.3 (F1 P1+B → F2 P2+Audit → F3 P1+D Builder; bewusst spaeter: A Identity-Listen, C Identities, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime, H Startable mit Einzelbegruendung) |

Frontend-Folgen Z10-1.x: aktuell **keine**. Z10 produziert Inventur und Vertrags-Skizze, kein Code-Change. FE-Eintraege entstehen erst, wenn aus Z10-1.2 konkrete API-Vertragsaenderungen folgen — dann mit Trigger-Kennzeichnung in `FRONTEND_TODO.md`, nicht praeventiv.

Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 10".

---

## Abgeschlossener Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

Abgeschlossen 2026-05-05. Folge-Hebel aus Z8: die in Z8-2.3 eingefuehrten Bulk-Helfer lagen `private` hinter dem 2.4k-Z. `SyncAllAsync`-Service. Z9 hat den Service in Graph-Adapter (`IEntraGraphClient`) und DB-Sync-Operations (`IEntraDirectorySyncOperations`) zerlegt und die Coverage gegen die neuen Interfaces gesetzt.

| Befund | Prio | Status |
|--------|------|--------|
| Z9-1.1 — Boundary-/Split-Inventur | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z9-1.1 |
| Z9-1.2 — Extract-Plan (File-/Klassen-Schnitt) | HIGH | done 2026-05-05 — `CODE_REVIEW.md` § Z9-1.2 |
| Z9-2.1 — Pre-Cleanup (Dead-Code raus) + `SyncAllAsync` in Phasen-Methoden | HIGH | done 2026-05-05 — Dead-Code raus (DepartmentLead-Resolver + 5 Sub-Helfer + Single-Row-Helfer + Reflection-Test); `SyncAllAsync` in `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync` zerlegt; 2591 → 2104 Z. |
| Z9-2.2 — Graph-Adapter `IEntraGraphClient` unter `Services/Directory/` extrahieren | HIGH | done 2026-05-05 — `IEntraGraphClient`/`EntraGraphClient` neu unter `api/API/Services/Directory/`; `Microsoft.Graph` aus Hauptdatei raus; DI scoped |
| Z9-2.3 — DB-Sync-Operations-Modul `IEntraDirectorySyncOperations` unter `Services/Directory/` extrahieren | HIGH | done 2026-05-05 — `IEntraDirectorySyncOperations`/`EntraDirectorySyncOperations` neu unter `api/API/Services/Directory/`; Service konsumiert per Konstruktor; Reflection-Test umgestellt; LOC Hauptdatei ~2030 → 1563 |
| Z9-3 — Coverage Batch-Helfer (aus Z8-4 verschoben) + Orchestrator-Stub-Tests | MEDIUM | done 2026-05-05 — `EntraDirectorySyncServiceTests` (3/3 gruen, Stub-Pfade ConnectionString/MissingCredentials/GraphFailed) + `EntraDirectorySyncOperationsIntegrationTests` (Upsert+Dedup, Membership-Insert+Idempotenz; Fixture-Gating wie Z8-4.1) |

Frontend-Folgen: keine. Detail in `CODE_REVIEW.md` § "Aktiver Zyklus 9".

---

## Abgeschlossener Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

Geschlossen 2026-05-05. Skalierbarkeit war mit B- die niedrigste Gesamtnote nach Zyklus 7. Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 8".

| Befund | Prio | Status |
|--------|------|--------|
| Z8-1.1 — Inventur unbegrenztes Laden / In-Memory-Filter / N+1 | HIGH | done 2026-05-05 — Hotspot-Liste in `CODE_REVIEW.md` § Z8-1.1 |
| Z8-1.2 — Top-3-Hotspot-Auswahl + Slice-Plan | HIGH | done 2026-05-05 — Slice-Plan in `CODE_REVIEW.md` § Z8-1.2 |
| Z8-2.1 — Hotspot #1 `WorkflowCatalogService` N+1-Aufloesung | HIGH | done 2026-05-05 — Bulk-Lookup `GetManagerCreatableDefinitionKeys()` |
| Z8-2.2 — Hotspot #2+#3 `RotationNotificationService` Sweep+Apply (gemeinsamer Slice) | HIGH | done 2026-05-05 — Sweep batched (200), Apply Bulk-Metadata+Bulk-UPDATE |
| Z8-2.3 — Hotspot #4 `EntraDirectorySyncService` Group/Member Batch | HIGH | done 2026-05-05 — Bulk-Upsert via `unnest`+RETURNING und Bulk-Insert fuer Memberships ersetzen pro-Member Round-Trips |
| Z8-3.1 — #5 Verifikation + #7 Recipient-Bulk-Lookup | HIGH | done 2026-05-05 — #5 false positive (CPU/Policy), #7 Bulk-Lookup |
| Z8-3.2 — Hotspot #8 `RotationTaskGenerationService` | MEDIUM | deferred 2026-05-05 — kein kleiner SQL-/Batch-Hebel ohne breiten Umbau; admin-getriggert. Z8-3 geschlossen |
| Z8-4 — Test-Coverage fuer neu gepushte Pfade | MEDIUM | done 2026-05-05 — Z8-4.1 Bulk-Recipient-Helper; Z8-2.1/Z8-2.2-Coverage in den Umsetzungs-Slices; EntraDirectorySync-Batch-Helfer-Coverage nach LQ2-Z3 verschoben |

Frontend-Folgen: aktuell **keine**. Z8 ist backend-fokussiert; FE-Items entstehen erst, falls API-Vertraege brechen.

---

## Zyklusuebergreifend offen

| ID | Aufgabe | Status |
|----|---------|--------|
| R8 | Browser-Verifikation Form-Editor | offen — Nutzer-Aufgabe |
| R10 | Mobile-Layout Form-Editor | backlog |
| L2 | Datenbereinigung Drafts | deferred — Produkt-Entscheidung |
| LQ2-Z3 | `EntraDirectorySyncService` Split + Coverage Z8-2.3-Batch-Helfer | abgeschlossen als Zyklus 9 (2026-05-05) |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein kleiner SQL-Hebel |
| FE-8 | `approval_task_template_key` → `approval_spec_key` Rename | defer ohne Trigger |

---

## Verwandte Notizen

- `CODE_REVIEW_ARCHIVE.md` — Detailarchiv abgeschlossener Review-Zyklen
- [[Migrationspfad]] — Gesamtbild offener Punkte
- [[Schritt7-Runtime-TaskSystem-Skizze]] — Architekturarbeit aus Zyklus 6
- [[Rotation]] — Rotation-spezifische Issues
- [[Zielarchitektur]] — Worauf hingearbeitet wird
