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

## Abgeschlossener Zyklus 12 — Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health (2026-05-06)

Z12 ist der naechste aktive Zyklus. Ziel: Admin-Dashboard zeigt fuer `admin` Runtime-/System-Health-Signale (API/DB/Directory/Mail + einfache Runtime-Metriken wie Prozess-Speicher, Uptime, Storage). Echte Host-/VM-Metrik bleibt bewusst ein optionaler Folgeschritt. Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 12".

**Praktisch:** Admins sehen ohne Server-Login direkt im Dashboard, ob die App und ihre Abhaengigkeiten gesund laufen. **Lohnenswert:** App-/Runtime-Health hat den groessten Hebel pro Aufwand und schafft den Anker, an dem ein spaeterer Host-Metrik-Ausbau sauber andocken kann. **Nutzen:** ein konsolidierter Betriebsblock statt verstreuter Indikatoren; klare Begriffstrennung App vs. Container vs. Host; ein expliziter Runtime-Health-Vertrag.

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z12-1.1 | Begriffsklaerung / Vertragsinventur Runtime Health (heutige Signale, fehlende Signale, App vs. Container vs. Host) | HIGH | high | opus | done (2026-05-06) — Inventur in `CODE_REVIEW.md` § Z12-1.1 (heutige Signale, fehlende App-/Runtime-Signale, App-/Container-/Host-Trennung, UI-Begriffsempfehlung) |
| Z12-1.2 | Vertrags-Skizze DTO + Schwellwerte + Begriffsabgrenzung App/Container/Host | HIGH | high | opus | done (2026-05-06) — Vertrags-Skizze in `CODE_REVIEW.md` § Z12-1.2 (`GET /admin/runtime-health` admin-only; `AdminRuntimeHealthDto` mit `application`/`dependencies`/`directory`/`storage[]`; Severity `ok/warning/critical/unknown`; Schwellwerte deklarativ; FE-Andock im bestehenden `admin-health-panel`; Z12-2.x-Abgrenzung gegen Host-/VM-Metrik / Prometheus / Trends / Alerts) |
| Z12-2.1 | Backend Runtime-Health Endpoint + Service (App-/Runtime-Signale, kein Host-/VM-Metrik-Code) | HIGH | medium..high | sonnet | done (2026-05-06) — `GET /admin/runtime-health` admin-only; `AdminRuntimeHealthService`; DTO-Familie + Severity-Logik + Storage via `RUNTIME_HEALTH_STORAGE_PATHS`; 42 neue Tests gruen |
| Z12-2.2 | Frontend Admin-Dashboard-Betriebsblock (andockend an `admin-health-panel`) | HIGH | medium..high | sonnet | done (2026-05-06) — `DashboardAdminRuntimeHealthBlock` als Zone 4 in `DashboardOverview`, nur fuer `dashboardPersona === "admin"`; Severity-Badge, API-Prozess-Kachel, Abhaengigkeiten-Kachel, Storage-Kacheln (optional); Polling 60s/120s |
| *(Folgeschritt)* | Optionaler Host-/VM-Metrik-Ausbau (CPU/RAM/Disk Server) — eigener Zyklus nach Z12-2.2, nur bei konkretem Bedarf | — | — | — | bewusst ausserhalb Z12 |

**Leitplanken Z12:**
- App-/Runtime-Health zuerst, **nicht** Host-/VM-Metrik. Wer im Slice Host-CPU/Disk/RAM mitnimmt, weicht den Zuschnitt auf.
- Bestehender Admin-Health-Begriff (`admin-health-panel` + `/health/*`) bleibt Anker; keine zweite parallele Betriebslogik.
- Begriffstrennung App vs. Container vs. Host ist Pflicht in Z12-1.1/Z12-1.2.
- Strenge Reihenfolge: Z12-1.1 → Z12-1.2 → Z12-2.1 → Z12-2.2.
- Z12-1.x sind reine Doku-Slices (kein Code, kein API-Vertrag, keine DB-Aenderung).
- Schwellwerte und Severity-Stufen werden in Z12-1.2 deklarativ skizziert, nicht in Z12-2.1 frei erfunden.
- Schreibregel anwenden: pro Slice kurze Bedeutung-/Nutzen-Erklaerung.
- Nach jedem Slice Commit + Doku (`CODE_REVIEW.md`, `TODO.md`, `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md`) im selben Pass.

**Naechster konkreter Schritt:** Z12 vollstaendig abgeschlossen (2026-05-06). Kein aktiver Zyklus. Codex entscheidet, welcher Folgekandidat (optionaler Host-/VM-Metrik-Ausbau als eigener Zyklus, oder anderes offenes Thema) als naechster aktiver Zyklus eroeffnet wird.

---

## Abgeschlossener Zyklus 11 — Admin-/Master-Data-Listen-Vertraege in Umsetzung (2026-05-05..06)

Z11 ist abgeschlossen (2026-05-06). Reiner Umsetzungszyklus auf Basis von Z10-1.3. Alle drei Slices F1 → F2 → F3 done. Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 11".

**Praktisch:** F1 macht Erfassungseinstieg und Master-Data-Pflege schnell und konsistent; F2 macht Audit-Verlauf vollstaendig durchsuchbar (heute schneidet ein stiller `limit`-Cap die Historie ab); F3 macht den Builder schnell, auch wenn Definitionen wachsen. **Lohnenswert:** Vertrag jetzt bauen, statt unter Last halbgaarig nachzuruesten — drei kontrollierte Slices statt N parallele Mini-Vertraege. **Nutzen:** zwei zentrale Hull-Typen, zwei typed FE-Adapter, eine wiederverwendbare Refactor-Achse fuer alle spaeter folgenden Listen.

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z11-F1 | P1 (`AdminListPageDto<T>`) zentral einfuehren + B Master-Data/Lookups (`/departments`, `/roles`, `/admin/master-data/departments|positions|responsibilities`); Server-`search`/`sort`-Whitelist pro Endpunkt; FE: typed Wrapper + Aufrufer-Refactor | HIGH | high | opus | done (2026-05-05) — P1-Hull live, fuenf Endpunkte umgestellt, aktuelle FE-Consumer auf `items` adaptiert |
| Z11-F2 | P2 (`CursorPageDto<T>`) zentral einfuehren + Audit-Streams (`/admin/auth/audit`, `/admin/directory/audit`); opaque Base64-Cursor ueber `(occurredAt, id)`; FE: typed Wrapper + „Mehr laden"-Knopf in beiden Audit-Tabs | HIGH | medium..high | sonnet | done (2026-05-06) — `CursorPageDto<T>` + Keyset-Pagination fuer beide Audit-Endpunkte; typed FE-Wrapper + Akkumulations-Hook + „Mehr laden"-Knopf in beiden Tabs |
| Z11-F3 | P1 ausrollen + D Builder-Tabs (sieben scoped Endpunkte: `workflow-definitions`, `action-definitions`, `task-templates` + `…/conditions` + `…/dependencies`, `answer-definitions`, `role-answer-defaults`); Pflicht-Scope; Filterzustand im FE in URL-Query als separater UI-Slice ausgelagert | HIGH | medium..high | opus | done (2026-05-06) — sieben scoped Builder-Endpunkte auf P1, FE-Service-Wrapper + alle Builder-Hooks auf `page.items` adaptiert |

**Leitplanken Z11:**
- Strenge Reihenfolge F1 → F2 → F3. F3 setzt P1 aus F1 voraus.
- Pro Slice **eine** Hull-Familie. Kein Mischen P1/P2 in einem Slice.
- Pro Slice nur Read-/Listen-Vertraege; keine Schreibpfade, keine Composite-DTO-Umbauten, keine Versionierungs-/Publish-Pfade.
- Server-Clamp `limit` Default 50/Max 200; `search`/`sort` deklarativ; P2-Cursor opaque.
- FE-Folgen sind Konsequenz pro Slice — Eintrag in `FRONTEND_TODO.md` mit Trigger F1/F2/F3 erst beim Start des Slices, nicht praeventiv.
- Schreibregel anwenden: zu jedem Slice-Ergebnis kurze Bedeutung-/Nutzen-Erklaerung.
- Nach jedem Slice Commit + Doku (`CODE_REVIEW.md`, `TODO.md`, `MEMORY.md`, `CODEX_SYNC.md`, `KauthWorkflow/Stand/Code-Review-Status.md`) im selben Pass.

**Naechster konkreter Schritt:** Z11 ist mit F3 vollstaendig geschlossen — kein offener Slice in Z11 mehr. Codex entscheidet, welcher der nach Z11 vorgesehenen Folgekandidaten als naechster aktiver Zyklus eroeffnet wird.

**Nicht in Z11 (Begruendung in `CODE_REVIEW.md` § Z10-1.3):** A Identity-Listen, C `/admin/directory/identities`, C Gaps/Pending Split, E Notification-Templates, F Rotation Action-Templates, G Runtime-Sub-Resources, H `/workflow-definitions/startable`. Diese werden nach Z11 als billige Mitnahmeschnitte mit dem jetzt etablierten P1-/P2-Adapter geplant — der Composite-Split fuer C Gaps/Pending bleibt eigener vorbereiteter Slice. Sichtbare Builder-Inspector-Paging-/Filter-UI mit URL-Query bleibt eigenstaendiger UI-Slice ausserhalb Z11.

---

## Abgeschlossener Zyklus 10 — Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken (2026-05-05)

Z10 ist abgeschlossen (2026-05-05) — alle drei Planungs-Slices done, formaler Zyklusabschluss vollzogen. Naechster Schritt: Eroeffnung eines Umsetzungszyklus (vorgeschlagen Z11) auf Basis von `CODE_REVIEW.md` § Z10-1.3 (F1 → F2 → F3). Thema: Admin-/Master-Data-/Directory-Listen werden zu grossen Teilen ohne Pagination, Server-Suche und stabilen Sort-Vertrag bedient — vor weiterem Wachstum werden Vertraege gezogen, statt am Schmerzpunkt nachzuschieben. Hotspot #6 aus Z8-1.2 (`GetDepartmentsAsync`/`GetRolesAsync`) ist nur die sichtbarste Stelle.

**Praktisch:** Listen werden bei wachsendem Bestand spuerbar langsamer, Suche/Filter fuehlen sich unvollstaendig an, weil viele Stellen heute im Browser filtern. **Lohnenswert:** Vertrag jetzt klaeren ist deutlich billiger als Hotfix unter Last; vermeidet halbgaarige Workarounds und API-Brueche fuer das FE. **Nutzen:** stabile Antwortzeiten, vollstaendige Server-Suche, einheitlicher Listen-/Such-/Sort-Vertrag, der wiederverwendbar ist. Detail in `CODE_REVIEW.md` § „Abgeschlossener Zyklus 10".

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z10-1.1 | Inventur: alle Admin-/Master-Data-/Directory-Read-Endpunkte ohne Pagination/Suche/Sort-Vertrag dokumentieren (Datei/Symbol, Rueckgabeform, FE-Aufrufer, Kardinalitaet, Spuerbarkeit fuer Nutzer) | HIGH | high | opus | done (2026-05-05) — Inventur in `CODE_REVIEW.md` § Z10-1.1 (Bloecke A–H + bereits saubere Listen + bewusst aussen vor) |
| Z10-1.2 | Vertrags-Skizze: pro Endpunkt entscheiden — `limit`/`offset` vs. Cursor, Server-`search` ja/nein, stabiler `sort`-Vertrag, Antwort-Hull (`items` + `total`/`nextCursor`); jeweils kurz erklaeren, was sich fuer Nutzer aendert und welche FE-Adaption noetig waere | HIGH | high | opus | done (2026-05-05) — Vertrags-Skizze in `CODE_REVIEW.md` § Z10-1.2 (Muster P1/P2/P3 + Bloecke A–H + konsolidierte FE-Konsequenz) |
| Z10-1.3 | Slice-Plan fuer Folgezyklus: 2–3 sichere Umsetzungsslices (API-Vertrag + minimale FE-Adaption) priorisieren; benennen, welche Endpunkte bewusst noch nicht angefasst werden und warum | HIGH | medium..high | opus | done (2026-05-05) — Slice-Plan in `CODE_REVIEW.md` § Z10-1.3 (F1 P1+B Master-Data → F2 P2+Audit-Streams → F3 P1-Ausrollen+D Builder; bewusst spaeter: A Identity-Listen, C Identities, C Gaps/Pending Split, E Notification-Templates, F Rotation, G Runtime, H Startable — jeweils mit Begruendung) |

**Leitplanken Z10:**
- Reiner Review-/Planungszyklus. Keine Code-Umsetzung in Z10. Kein Slice darf in Z10 als done markiert werden, der einen API-Vertrag oder eine DB-Aenderung enthaelt.
- FE-Folgen erst eintragen, wenn die Inventur sie sichtbar macht. Kein praeventives Frontend-TODO.
- Reihenfolge streng sequenziell: Z10-1.1 → Z10-1.2 → Z10-1.3.
- Schreibregel anwenden: zu jedem Finding klare Bedeutung-/Nutzen-Erklaerung.

---

## Abgeschlossener Zyklus 9 — `EntraDirectorySyncService`-Split / Testbarkeit (2026-05-05)

Z9 ist abgeschlossen (2026-05-05). Alle Slices done: Z9-1.1/1.2 Inventur+Plan, Z9-2.1/2.2/2.3 File-Splits, Z9-3 Coverage gegen die neuen Interfaces. Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 9".

| Block | Aufgabe | Prio | Reasoning | Modell | Status |
|-------|---------|------|-----------|--------|--------|
| Z9-1.1 | Boundary-/Split-Inventur (oeffentliche API, Aufrufer, interne Achsen, Test-Isolations-Hindernisse) | HIGH | high | opus | done (2026-05-05) — `CODE_REVIEW.md` § Z9-1.1 |
| Z9-1.2 | Extract-Plan: File-/Klassen-Schnitt + Reihenfolge + Test-Strategie | HIGH | high | opus | done (2026-05-05) — `CODE_REVIEW.md` § Z9-1.2 |
| Z9-2.1 | Pre-Cleanup (DepartmentLead-Resolver + Single-Row-Helfer loeschen) + `SyncAllAsync`-Phasen-Strukturierung in der Hauptdatei | HIGH | medium..high | sonnet | done (2026-05-05) — Dead-Code geloescht (`SyncDepartmentLeadAssignmentsFromDirectory` + 4 Sub-Helfer + `LogDirectoryAuditEventAsync` + `CreateDepartmentLeadAuditSnapshot` + Records; `UpsertDirectoryIdentity`/`InsertGroupMembership`); Reflection-Test entfernt; `SyncAllAsync` in `RunGroupSyncAsync`/`RunDirectoryProjectionAsync`/`RunActivationAsync` zerlegt |
| Z9-2.2 | Graph-Adapter (`IEntraGraphClient` + `EntraGraphClient`) unter `Services/Directory/` extrahieren | HIGH | medium..high | sonnet | done (2026-05-05) — Adapter unter `api/API/Services/Directory/`, Microsoft.Graph aus Hauptdatei raus, DI ergaenzt |
| Z9-2.3 | DB-Sync-Operations-Modul (`IEntraDirectorySyncOperations` + Impl) unter `Services/Directory/` extrahieren | HIGH | medium | sonnet | done (2026-05-05) — `IEntraDirectorySyncOperations` + `EntraDirectorySyncOperations` unter `api/API/Services/Directory/`, Service konsumiert per Konstruktor; Reflection-Test auf direkten Aufruf der neuen Operations umgestellt |
| Z9-3 | Coverage: Integration-Tests fuer `UpsertDirectoryIdentitiesBatch`/`InsertGroupMembershipsBatch` + Unit-Tests Orchestrator gegen Graph-/Ops-Stubs | MEDIUM | medium | sonnet | done (2026-05-05) — `EntraDirectorySyncServiceTests` (3/3 gruen, Stub-Pfade `MissingConnectionString`/`MissingCredentials`/`GraphFailed`) + `EntraDirectorySyncOperationsIntegrationTests` (3 Tests fuer `UpsertDirectoryIdentitiesBatchAsync` + `InsertGroupMembershipsBatchAsync`, Fixture-Gating wie Z8-4.1) |

Z9 abgeschlossen; Folge-Zyklus oder zyklusuebergreifende Themen siehe unten.

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
| LQ2-Z3 | `EntraDirectorySyncService` Split + Coverage Z8-2.3-Batch-Helfer | Zyklus 3 / Z8 → Z9 | abgeschlossen als Zyklus 9 (2026-05-05) |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Abgeschlossene Zyklen

- Zyklus 7 ist abgeschlossen. Kurzfassung in `CODE_REVIEW.md`, Detail in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Stand/Code-Review-Status.md`.
- Zyklus 8 ist abgeschlossen (2026-05-05). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 8".
- Zyklus 9 ist abgeschlossen (2026-05-05). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 9".
- Zyklus 10 ist abgeschlossen (2026-05-05). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 10".
- Zyklus 11 ist abgeschlossen (2026-05-06). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 11".
- Zyklus 12 ist abgeschlossen (2026-05-06). Detail in `CODE_REVIEW.md` § "Abgeschlossener Zyklus 12".

---

## Arbeitsregel

Vor dem Start einer Aufgabe immer explizit nennen:
1. welche Aufgabe als naechstes ansteht
2. welches Reasoning sinnvoll ist
3. welches Modell empfohlen ist
4. wenn Claude per CLI laeuft: `--model` und `--effort` explizit setzen, nicht nur im Prompt empfehlen
