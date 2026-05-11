# Code Review — kauth_workflow

## Zweck

- aktive technische Review-Priorisierung
- Begruendung fuer den naechsten Arbeitszyklus
- kompakter Status fuer Mensch und KI

## Primaerquelle fuer

- aktuellen Review-Fokus
- Reihenfolge der Nacharbeit
- offene zyklusuebergreifende technische Befunde

## Nicht verwenden fuer

- kurzfristige Session-Notizen
- tiefes Slice-fuer-Slice-History-Studium abgeschlossener Zyklen

Dafuer sind `MEMORY.md`, `CODEX_SYNC.md` und `CODE_REVIEW_ARCHIVE.md` zustaendig.

## Wann aktualisieren

- wenn ein neuer Review-Zyklus eroeffnet wird
- wenn sich Priorisierung oder Folge-Slices aendern
- wenn ein aktiver Zyklus abgeschlossen wird

## Verwandte Dateien

- `TODO.md`
- `MEMORY.md`
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Stand/Code-Review-Status.md`
- `KauthWorkflow/Architektur/Migrationspfad.md`

---

## Schreibregel fuer Reviews und Findings (verbindlich)

Jedes Review-Finding und jeder Slice in dieser Datei muss neben dem technischen Befund in kurzen Saetzen erklaeren:

- **Was bedeutet das praktisch?** — was ein normal verstaendlicher Leser im Alltag merkt.
- **Warum lohnt es sich, das anzugehen?** — der konkrete Anlass oder das Risiko.
- **Was wird dadurch besser, sicherer, schneller oder wartbarer?** — der erwartete Nutzen.

Reine Technik-Beschreibung ohne Nutzen-/Bedeutung-Erklaerung ist nicht ausreichend. Die Regel gilt fuer alle neuen Zyklen, fuer einzelne Befunde und fuer den jeweils gefuehrten Slice-Plan. Bei zyklusuebergreifend offenen Befunden reicht ein kurzer Hinweis, warum sie aktuell nicht angegangen werden.

Diese Regel ist auch in `CLAUDE_CONTROL.md` als Arbeits-Pflicht fuer Claude unter Codex-Orchestrierung verankert.

---

**Stand**: 2026-05-11 — **Z20 vollstaendig abgeschlossen** (Admin/Directory/Runtime Read Contracts Phase 2, S1+B1+B2+B3+B4 erledigt). P3-Lookups, Directory-/Notification-/Rotation-/Auth-P1-Reads und Runtime-P2-Cursor-Subresources sind live. Z19 vollstaendig abgeschlossen (Backend Full Review / Holistic Audit, alle Slices S1..S9 erledigt). Z18, FE-8 und der Entra-Retrofit-Block (A1, A2, A3, B, C) bleiben am 2026-05-08 abgeschlossen.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..06 Zyklus 2–13; 2026-05-07 Z14; 2026-05-08 Z15–Z18 + A1/A2/A3/B/C; 2026-05-11 Z19 vollstaendig + Z20-S1/B1). Codex (2026-05-11 Z20-B2/B3/B4 Abschluss).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith reduziert; Lifecycle-Service nach Z7 echte Commit-Grenze fuer zentrale Runtime-Mutationen; groesste Resthebel liegen jetzt bei Skalierbarkeit und Lastpfaden |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor; Z18 hat 1 HIGH-, 5 MEDIUM- und 3 LOW-Findings sichtbar gemacht, vor allem bei Search-Performance, Accessibility und UI-Semantik |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Mehrere Listen-, Sweep- und Dispatch-Pfade sind noch Kandidaten fuer SQL-Pushdown, Pagination oder N+1-Abbau |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen weniger in Benennung als in Hotspot-Pfaden unter Last |

---

## Abgeschlossener Zyklus 20 — Admin/Directory/Runtime Read Contracts Phase 2 (2026-05-11)

**Praktisch:** Die zurueckgestellten Read-Vertraege aus Z10/Z11 sind jetzt geschlossen. Admin-Directory-Identities, Responsibility-Gaps, Pending-Imports, Notification-Templates, Rotation-Action-Templates, Runtime-Events, Runtime-Automation-Jobs, Auth-Identity-Listen und Unlinked-Directory-Identities liefern einheitliche P1-/P2-Huellen.

**Lohnenswert:** Die Arbeit wurde in einem Lauf gebuendelt, weil das Pattern nach B1 vollstaendig etabliert war. Dadurch entfallen mehrere Mikro-Zyklen mit identischem Doku-/Test-Overhead.

**Nutzen:** Das FE liest die neuen Huellen ueber Adapter kompatibel weiter; Runtime-Subresources sind cursorfaehig; Directory-Composite-Reads sind in P1-Listen gesplittet; die verbleibenden Z10/Z11-Mitnahmeschnitte sind erledigt.

**Verifikation:** `dotnet test api/API.Tests/API.Tests.csproj --artifacts-path .codex-artifacts` → 566 bestanden, 1 uebersprungen. `npm run build` im `web/` → erfolgreich.

Die detaillierte Slice-Map, die urspruengliche Reihenfolge-Begruendung und die ausfuehrliche Z20-Inventur liegen jetzt in `CODE_REVIEW_ARCHIVE.md`.

---

## Abgeschlossener Zyklus 19 — Backend Full Review / Holistic Audit (2026-05-11)

**Praktisch:** Der erste zusammenhaengende Backend-Gesamtreview seit Z8/Z9/Z11/Z12/Z13 ist komplett durch. Die groessten Betriebs- und Wartbarkeitshebel aus dem Audit sind abgearbeitet: Sweep-Timeout fuer den Directory-Sync, Schema-Paritaets-Check fuer `db/manual/`, `CancellationToken`-Propagation in den Lifecycle-/Runtime-Pfaden, Cursor-Pagination und Tests fuer `SystemEventLogService`, Failure-of-Failure-Schutz in der Automation sowie der letzte grosse AuthZ-Repo-Split.

**Lohnenswert:** Genau die Befunde, die im Alltag teuer werden, sind damit nicht mehr latent im System versteckt: haengende Background-Sweeps, stille Schema-Drift, offene DB-Transaktionen nach Request-Abbruch, klaimierte aber nie wieder aufgenommene Automation-Jobs und Merge-/Review-Kollisionen an 1000+ LOC AuthZ-Dateien.

**Nutzen:** Der aktive Review-Backlog ist wieder leer; der naechste Zyklus kann von einem deutlich sauberen Backend-Fundament starten statt alte Z19-Reste mitzuschleppen.

**Z19-Kurzresultat:**
- `S1..S9` sind vollständig erledigt.
- `DirectorySyncHostedService` hat jetzt einen harten Sweep-Timeout.
- `db/manual/manifest.json` + `SchemaParityTests.cs` sichern DB-Drift gegen `db/01_schema.sql` ab.
- Lifecycle-/Runtime-Abbruchpfade propagieren `CancellationToken` jetzt bis in die betroffenen Query-/Tx-Pfade.
- `SystemEventLogService` nutzt Cursor-Pagination und hat eigene Unit-Tests; `WorkflowAutomationService` faengt den Failure-of-Failure-Pfad ab.
- `PostgresUserAuthorizationRepository` ist nach Z9-Pattern in fokussierte Admin-Partial-Dateien zerlegt.

Die Detaildokumentation von Z19 liegt jetzt in `CODE_REVIEW_ARCHIVE.md` und im menschlichen Statusspiegel unter `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Archivstatus

- Die Detailzyklen **Z8 bis Z20** liegen in `CODE_REVIEW_ARCHIVE.md`.
- Die Detailhistorie von **Zyklus 7** liegt in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
- In dieser aktiven Datei bleiben nur Gesamtbewertung, offene zyklusuebergreifende Befunde und die grobe Historie.

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | deferred — admin-getriggert, kein Hot-Path; kein kleiner SQL-/Batch-Hebel ohne breiten Umbau an `SynchronizeRotationGeneratedTasks` | Zyklus 8 |

---

## Zyklus-Historie

| Zyklus | Datum | Hauptthema |
|--------|-------|------------|
| 1 | 2026-04-23 | Code-Review + Hardening (C1–C4, H1–H7, L1/L3/L5/L6) |
| 2 | 2026-05-02 | HQ1–HQ5 + LQ1–LQ7: Decision-Migration, SQL-Task-Filter, Audit-Trail, Testcontainers, Page-Refactor |
| 3 | 2026-05-02 | Test-Coverage + Wartbarkeits-Split: Hook-Tests, Repo-Splits, Hook-Zerlegung |
| 4 | 2026-05-02 | Naming + Haertungen: LegacyProcessTypeKey, effectiveResponsibilityIds, Error-Boundaries |
| 5 | 2026-05-02..03 | Legacy-Abbau (LA1–LA5): LegacyWorkflowStatus, setup-Node, definition_key, HasLegacyRolePermission, Specs am Node |
| 6 | 2026-05-03..04 | Runtime-Lifecycle (Schritt 7): Engine-Extraktion + Lifecycle-Service mit Conn+Tx-Scope |
| 7 | 2026-05-05 | Lifecycle-Service-Konsolidierung + Validation-Split |
| 8 | 2026-05-05 | Skalierbarkeits- & Last-Haertung — abgeschlossen |
| 9 | 2026-05-05 | `EntraDirectorySyncService`-Split / Testbarkeit — abgeschlossen |
| 10 | 2026-05-05 | Master-Data-/Admin-Listen-Wachstum, Pagination-/Such-Vertraege, Query-Kontrakt-Risiken — abgeschlossen |
| 11 | 2026-05-05..06 | Admin-/Master-Data-Listen-Vertraege in Umsetzung — abgeschlossen |
| 12 | 2026-05-06 | Admin-Dashboard-Betriebsblock fuer Runtime-/System-Health — abgeschlossen |
| 13 | 2026-05-06 | Echte Linux-Host-/VM-Metriken im Admin-Runtime-Health-Block — abgeschlossen |
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — abgeschlossen |
| 15 | 2026-05-08 | Implementierung Mehrrollen-Persona: S1 Datenmodell/Hook done; S2 Override-Stellen done; S3 Persona-Switcher done — **vollstaendig abgeschlossen** |
| 16 | 2026-05-08 | Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag — **vollstaendig abgeschlossen** (Z16-S4 deferred) |
| 17 | 2026-05-08 | Light/Dark-Mode-Theme-Leaks: `.card-primary` nutzte `--surface-hero-background` (dunkelblau) auch im Light Mode — Z17-S1 behoben |
| 18 | 2026-05-08 | Frontend Full Review — Z18-S1 done; Z18-S2 done (Batch A); Z18-S3 done (Batch B: F6/F7); Z18-S4 done (F4: /search→/workflows Redirect) — **vollstaendig abgeschlossen** |
| A1 | 2026-05-08 | People-Import Backend-Fundament — `POST /admin/people/import-from-directory` + `GET /admin/directory/unlinked-identities`; job_title-Durchleitung mitgeliefert — **abgeschlossen** |
| A2 | 2026-05-08 | Import-UI (Admin) — Neue Sektion „Aus Entra importieren" mit Abteilungs-Gruppierung, Checkbox-Auswahl, Vorschau (Name, Stelle, Konto-Status) und Import-Button; deaktivierte Konten standardmäßig ausgeblendet — **abgeschlossen** |
| A3 | 2026-05-08 | Mitarbeiterkarte fehlende Felder + retroaktiver Status — PATCH /admin/people/{personId} (entry_date, badge_number); Inline-Edit in PersonOverviewSection (Admin-only, useMutation + Invalidierung); Lücken-Warnung; Badge „Retroaktiv importiert"; erklärender Text im leeren Vorgangsbereich — **abgeschlossen** |
| B | 2026-05-08 | Entra-Stellenbezeichnungen in Abteilungs-Stellen importieren — GET /admin/master-data/departments/{id}/entra-job-titles + POST …/positions/import-from-entra; Checkbox-UI in AdminOrganizationDepartmentEditor mit bereits-vorhanden-Markierung — **abgeschlossen** |
| C | 2026-05-08 | Mitarbeiter-Verzeichnis zeigt jetzt auch aktive `directory_identities` ohne Mitarbeiterkarte — `GetPeopleDirectory` per `UNION ALL`, Status `directory_only`, nullable `personId`, Inline-Import-Button pro Verzeichnis-Eintrag in `PeopleDirectoryPage` — **abgeschlossen** |
| 19 | 2026-05-11 | Backend Full Review / Holistic Audit — **vollstaendig abgeschlossen** (alle Slices S1..S9 done) |
| 20 | 2026-05-11 | Admin/Directory/Runtime Read Contracts Phase 2 — **vollstaendig abgeschlossen** (S1+B1+B2+B3+B4 done) |
