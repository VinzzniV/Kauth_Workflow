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

**Stand**: 2026-05-05 — nach Abschluss von Zyklus 7. Zyklus 8 aktiv.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 eroeffnet).

---

## Aktuelle Gesamtbewertung

| Bereich | Note | Hauptbegruendung |
|---------|------|------------------|
| Backend-Architektur | **A-** | Repository-Monolith reduziert; Lifecycle-Service nach Z7 echte Commit-Grenze fuer zentrale Runtime-Mutationen; groesste Resthebel liegen jetzt bei Skalierbarkeit und Lastpfaden |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints |
| Auth & Berechtigungen | **B+** | Permission-Audit hat Reason-Feld; Person-Matching-Audit live |
| Rotation-Feature | **B+** | RotationTaskRegenerationEngine als pure Domain-Engine; HQ5-Hooks getestet; Sweep-Timeout |
| Frontend-Architektur | **B+** | Builder + Listen-Workspaces refactored; Split-Views; Karten-/Tabellenmodus; AdminConfig-Bundle-Refactor |
| Testbarkeit | **B** | Testcontainers + Integration-Tests; Lifecycle-Service hat eine eigene Service-Testdatei fuer Routing, Rollback und Automation-Scope |
| Skalierbarkeit | **B-** | Mehrere Listen-, Sweep- und Dispatch-Pfade sind noch Kandidaten fuer SQL-Pushdown, Pagination oder N+1-Abbau |
| Sicherheit | **B+** | `/client/log-events` rate-limited; dev-sim-Guard hard-throw |
| Lesbarkeit | **B+** | Konventionen durchgaengig; grobe Monolithen reduziert, Resthebel liegen weniger in Benennung als in Hotspot-Pfaden unter Last |

---

## Aktiver Zyklus 8 — Skalierbarkeits- & Last-Haertung (2026-05-05)

**Thema:** Nach Abschluss der Lifecycle- und Validation-Hygiene aus Zyklus 7 ist Skalierbarkeit (Note **B-**) die niedrigste Gesamtbewertung und damit der naechste sinnvolle Hebel. Z2 hat den Workflow-Task-Filter SQL-pre-narrowed, aber an mehreren Stellen laufen Listen, Filter und Sweeps weiter ungebremst durch In-Memory-Pfade. Das ist keine Theorie-Schwaeche, sondern wird bei realer Last sichtbar (Workflow-Liste, MyTasks, RotationOperations, Notification-Dispatch, RotationTask-Sweep).

**Begruendung gegen alternative Zyklen:**
- *EntraDirectorySyncService Split (LQ2-Z3, 2485 Z.)* bleibt deferred ohne Trigger — Timer-Pfad, kein User-Pfad, keine offene Beschwerde. Reine Bewegung.
- *Auth-Haertung* — Note B+ stabil, keine konkrete neue Luecke seit Zyklus 4.
- *Frontend-Polish* — nach FE-25..FE-31 stabil; B+ ohne offenen Schmerzpunkt.
- *Repository-Splits* ohne Anlass — reine Hygiene ohne klaren Last- oder Produkthebel.

**Fokus:**
1. Inventur saemtlicher Pfade mit unbeschraenktem Laden, In-Memory-Filter/-Sortierung und N+1-Risiko.
2. Top-Hotspots als SQL-Pushdown / Pagination loesen.
3. Background-Sweeps (RotationTask-Sweep, Notification-Dispatch) auf Last gegenpruefen.
4. Test-Coverage fuer die neu gepushten Pfade nachziehen.

**Priorisierung:**

| ID | Befund | Prio |
|----|--------|------|
| Z8-1.1 | Inventur: Endpunkte + Repos mit unbeschraenktem Laden, In-Memory-Filter/-Sort, N+1 | **HIGH** — offen |
| Z8-1.2 | Top-3-Hotspot-Auswahl + Slice-Plan auf Basis der Inventur | **HIGH** — offen |
| Z8-2.x | SQL-Pushdown / Pagination der Top-Hotspots (pro Hotspot ein Slice) | **HIGH** — wartet auf Z8-1.2 |
| Z8-3 | Sweep- und Dispatch-Performance (`RotationTaskRegenerationEngine`-Sweep, Notification-Dispatch) | MEDIUM — offen |
| Z8-4 | Test-Coverage fuer die neu gepushten Pfade (Integration + Unit) | MEDIUM — wartet auf Z8-2 |

**Empfohlener Einstieg:** Z8-1.1 als reine Inventur — opus/high. Output: konkret nummerierte Hotspot-Liste mit Aufrufer-Pfad und Datenkardinalitaet, kein Code-Change. Erst auf dieser Basis entscheidet Z8-1.2, ob Pagination, Sortier-Pushdown oder N+1-Aufloesung den groessten Hebel hat.

**Frontend-Folgen:** aktuell **keine**. Z8 ist backend-fokussiert. Wenn Z8-2 API-Vertraege aendert (z. B. Pagination-Tokens, Sortier-Parameter), entstehen erst dann FE-Items in `FRONTEND_TODO.md`. Bis dahin wird keine FE-Arbeit kuenstlich erzeugt.

---

## Abgeschlossener Zyklus 7 — Kurzfassung

Zyklus 7 hat zwei zentrale Hygiene-Bloecke abgeschlossen:
- Lifecycle-Service-Konsolidierung: `WorkflowLifecycleService` ist jetzt die Commit-Grenze fuer Create/Form/Approval/Task.
- Validation-Split: `WorkflowDefinitionValidationService` wurde in Draft-/Snapshot-/Helper-/Catalog-Slices zerlegt.

Die Detailhistorie von Zyklus 7 liegt in:
- `CODE_REVIEW_ARCHIVE.md`
- `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`

---

## Offene Befunde aus frueheren Zyklen

| ID | Aufgabe | Status | Quelle |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | offen — Nutzer-Aufgabe, KI kann nicht pruefen | L7 |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | backlog — kein konkreter Bedarf | L7 |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | deferred — wartet auf Produkt-Entscheidung | Zyklus 1 |
| LQ2-Z3 | `EntraDirectorySyncService.cs` (2485 Z.) Split | deferred ohne Trigger — Risiko niedrig (Timer-Pfad). Refactor erst bei Anlass | Zyklus 3 |

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
| 8 | 2026-05-05 (aktiv) | Skalierbarkeits- & Last-Haertung |
