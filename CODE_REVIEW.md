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

**Stand**: 2026-05-11 — **Z19 als aktiver Zyklus eroeffnet** (Backend Full Review / Holistic Audit). Z18, FE-8 und der Entra-Retrofit-Block (A1, A2, A3, B, C) bleiben am 2026-05-08 abgeschlossen. Z19-S1 ist ein reiner Doku-/Review-Slice: vollstaendiger Backend-Audit ueber Endpoints, Repositories, Services, Authorization, Auth-Pipeline, Background-Jobs, Schema-/Migrations-Hygiene und Test-Coverage-Luecken; liefert eine priorisierte Findings-Liste fuer Folgeslices.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..06 Zyklus 2–13; 2026-05-07 Z14; 2026-05-08 Z15–Z18 + A1/A2/A3/B/C; 2026-05-11 Z19 eroeffnet).

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

## Aktiver Zyklus 19 — Backend Full Review / Holistic Audit (2026-05-11)

Eroeffnet 2026-05-11 als reiner Review-/Planungszyklus, analog zu Z18 (Frontend Full Review). Keine Implementierung in S1.

**Praktisch:** Erstmals seit den punktuellen Backend-Zyklen Z8 (Skalierbarkeit), Z9 (Directory-Sync-Split), Z11 (Listen-Vertraege), Z12/Z13 (Runtime-Health) bekommt der gesamte Backend-Stack einen zusammenhaengenden Review-Pass — Endpoints, Repositories, Services, Authorization, Auth-Pipeline, `Services/Directory/`, Background-Jobs, Schema-/Migrations-Hygiene und Test-Coverage. Ergebnis ist eine priorisierte Findings-Liste (HIGH/MEDIUM/LOW) im Stil von Z18, an der Folgeslices gezielt aufraeumen.

**Lohnenswert:** Skalierbarkeit (B-) und Testbarkeit (B) sind die schwaechsten Noten in der Gesamtbewertung. Deferred Hotspots (Z8-3.2/#8 `RegenerateDepartmentPlansAsync`, Z16-S4 Automation-Snapshot-Vertrag) und die jueengsten DB-Drift-Vorfaelle (manuelle SQL-Helfer fuer `approval_spec_key` und `directory_identities.job_title`, dokumentiert in `db/manual/`) zeigen systemische Resthebel, die einzeln klein wirken, in Summe aber Wartbarkeit und Betriebsfestigkeit kosten. Eine breite Bestandsaufnahme jetzt ist deutlich billiger als wiederholte Hotfixes unter Last und schafft die Andockflaeche fuer den naechsten gerichteten Schritt Richtung Definition Layer / Runtime / Automation Layer.

**Nutzen:** vollstaendige Inventur der noch nicht von Z8/Z9/Z11/Z12/Z13 adressierten Hotspots; klare Begruendung fuer deferred / nicht-jetzt-Befunde; ein dokumentierter Slice-Plan, an dem nachfolgende Umsetzungszyklen sauber andocken; bessere Priorisierung gegen das Zielbild statt isolierter Mikro-Optimierungen; transparenter Vertrag, was nicht zum Audit gehoert (kein breiter Architektur-Umbau).

| Slice | Inhalt | Prio | Status |
|-------|--------|------|--------|
| Z19-S1 | Backend Full Review pass: Audit ueber `api/API/Endpoints`, `api/API/Repositories`, `api/API/Services`, `Authorization/`, `Auth/`, `Services/Directory/`, Background-/Sweep-Jobs, Schema-/Migrations-Hygiene (insb. DB-Drift-Pfad und `db/manual/`-Workflow), Test-Coverage-Luecken. Liefert priorisierte Findings (HIGH/MEDIUM/LOW) mit Begruendung, Bereich und vorgeschlagenem Slice-Schnitt. **Doku-only**, keine Code-Aenderung. | HIGH | offen |
| Z19-S2..N | Umsetzungsslices je nach Findings-Verteilung; Modell/Effort pro Slice gesetzt. | — | folgt nach S1 |

**Empfohlenes Modell/Effort fuer Folgeslices:**
- **Z19-S1** (Audit): `claude-opus-4-7` + `--effort high` — breites, dichtes Audit ueber mehrere Backend-Module, Begruendungs-/Schreibregel-Pflicht und Slice-Plan-Schnitt rechtfertigen Opus-Tiefe.
- **Z19-S2..N** (Umsetzung): typisch `claude-sonnet-4-6` + `--effort medium` fuer lokale Refactorings, Coverage-Auffuellungen und Vertrags-/Hygiene-Fixes; `claude-opus-4-7` + `--effort high` nur bei nicht-trivialen Engine-/Lifecycle- oder Authorization-Eingriffen.

**Bewusst NICHT in Z19:** breite Architektur-Umbauten am Workflow-Definition-/Runtime-/Automation-Layer (Migrationspfad-Arbeit bleibt eigenstaendig), neue Frontend-Findings (Z18 vollstaendig abgeschlossen), Berechtigungsmodell-Aenderungen ohne konkretes Risiko, Mobile-/Tablet-Layout (R10 bleibt eigener Backlog).

**Naechster Schritt:** Codex erzwingt Z19-S1 per CLI mit `--model claude-opus-4-7 --effort high`.

---

## Archivstatus

- Die Detailzyklen **Z8 bis Z16** liegen in `CODE_REVIEW_ARCHIVE.md`.
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
| 19 | 2026-05-11 | Backend Full Review / Holistic Audit — eroeffnet als Doku-Zyklus, S1 Audit-Pass offen |
