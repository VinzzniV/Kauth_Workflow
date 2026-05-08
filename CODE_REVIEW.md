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

**Stand**: 2026-05-08 — Zyklus 15 aktiv (Implementierungszyklus Mehrrollen-Persona auf Basis Z14-1.3; Z15-S1 Datenmodell/Hook done, Z15-S2 Override-Stellen offen, Z15-S3 Persona-Switcher offen). Die abgeschlossenen Detailzyklen 8 bis 14 wurden in `CODE_REVIEW_ARCHIVE.md` ausgelagert; diese Datei bleibt die kompakte aktive Steuerdatei.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet + abgeschlossen; 2026-05-06 Z13 eroeffnet + abgeschlossen; 2026-05-07 Z14 eroeffnet + abgeschlossen; 2026-05-08 Z15 eroeffnet + Z15-S1 done).

---

## Aktiver Zyklus 15 — Implementierung Mehrrollen-Persona (2026-05-08)

Eroeffnet 2026-05-08 als Implementierungszyklus auf Basis Z14-1.3. Drei Umsetzungsslices in fester Reihenfolge (Datenmodell → Override-Stellen → UI-Switcher).

**Praktisch:** Nutzer mit mehreren Rollen (z. B. Admin + Fachbereich) landen heute auf einer leeren generischen Ansicht und sehen weder Admin-Block noch fachliche Ansicht. Nach Z15 bekommt jeder Mehrrollen-Nutzer per Default seine maechtgiste Rolle als Einstieg und kann — nach S3 — per Schalter umstellen. **Lohnenswert:** trifft genau die Power-User; Logik liegt zentral an zwei Override-Stellen. **Nutzen:** kein ungefuelltes Generic-Dashboard mehr fuer die haeufigsten Mehr-Rollen-Konstellationen; sauberer Vertrag als Andockpunkt fuer kuenftige Personas.

| ID | Aufgabe | Prio | Status |
|----|---------|------|--------|
| Z15-S1 | Datenmodell „aktive Ansicht": neuer Hook `useActiveView`, Fallback-Kaskade `localStorage → Default-Persona → generic`, Setter, Validierung gegen Capabilities; null Sicht-Konsumenten | HIGH | **done 2026-05-08** — `web/src/hooks/useActiveView.ts` + 24 Vitest-Tests gruen; `useRoleAwareNavigation.ts` + `roleModel.ts` NICHT angefasst |
| Z15-S2 | Zwei Override-Stellen aus Z14-1.1 gemeinsam auf `useActiveView` umstellen (`useRoleAwareNavigation.ts:253` + `roleModel.ts:210`); Mehrrollen-Nutzer sehen Default-Persona statt `generic` | HIGH | offen — naechster Schritt |
| Z15-S3 | Sichtbarer Persona-Switcher nur fuer `hasMultipleRoles === true`; schreibt ueber `useActiveView`; Single-Role-Nutzer sehen nichts | MEDIUM | offen — nach S2 |

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

## Archivstatus

- Die Detailzyklen **Z8 bis Z14** liegen jetzt in `CODE_REVIEW_ARCHIVE.md`.
- Die Detailhistorie von **Zyklus 7** liegt weiterhin in `CODE_REVIEW_ARCHIVE.md` und `KauthWorkflow/Architektur/Schritt7-Runtime-TaskSystem-Skizze.md`.
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
| 15 | 2026-05-08 | Implementierung Mehrrollen-Persona: S1 Datenmodell/Hook done; S2 Override-Stellen + S3 Switcher offen |
