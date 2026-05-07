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

**Stand**: 2026-05-07 — Aktiver Zyklus 14 (Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung). Die abgeschlossenen Detailzyklen 8 bis 13 wurden in `CODE_REVIEW_ARCHIVE.md` ausgelagert; diese Datei bleibt die kompakte aktive Steuerdatei.
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet + abgeschlossen; 2026-05-06 Z13 eroeffnet + abgeschlossen; 2026-05-07 Z14 eroeffnet).

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

- Die Detailzyklen **Z8 bis Z13** liegen jetzt in `CODE_REVIEW_ARCHIVE.md`.
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
| 14 | 2026-05-07 | Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung — aktiv |

---

## Aktiver Zyklus 14 — Mehrrollen-Persona-Kollisionen in Uebersicht / Navigation / rollenabhaengiger Darstellung

Eroeffnet 2026-05-07 als reiner Review-/Planungszyklus. Ziel: Problem sauber eroeffnen und schneiden, **keine Umsetzung**.

**Praktisch:** Ein Benutzer mit mehreren Rollen — typisch Admin + Fachbereich, Admin + Manager, Admin + HR — landet im Dashboard und in der Navigation auf einer generischen Sammelansicht und verliert dabei die fachlich erwartete Sicht. Wer sich als „Admin" einloggen will, sieht den Admin-Betriebsblock nicht, weil die Mehrfachrolle die Persona auf `generic` zwingt; wer als Manager arbeiten will, verliert die Manager-Aktionen aus demselben Grund. Die Wahl der Ansicht ist heute implizit, nicht steuerbar, und greift bereits bei zwei Rollen.

**Warum lohnt es sich, das anzugehen:** Genau die Personen mit den meisten Rollen sind die Power-User des Systems (Admins, die zugleich Fachbereich/HR/Manager-Aufgaben haben). Der Effekt trifft also nicht Randfaelle, sondern den Alltag der Schluesselnutzer. Die Logik liegt zentral an wenigen Stellen (`web/src/auth/roleModel.ts`, `web/src/navigation/useRoleAwareNavigation.ts`, Dashboard- und Insights-Schichten), d. h. der Hebel pro Aufwand ist hoch und ein sauberer Vertrag laesst sich definieren, bevor weitere Rollen-/Persona-Verzweigungen entstehen.

**Was wird dadurch besser:** klare Begriffstrennung Rolle vs. Persona vs. aktive Ansicht; vorhersagbares Verhalten beim Login mit mehreren Rollen; ein dokumentierter Vertrag, an dem neue Personas (z. B. spaetere Spezialrollen) andocken koennen, statt jedes Mal die `generic`-Falle zu erweitern.

**Konkreter Anlass / technischer Verdacht (nicht abschliessend, Ergebnis von Z14-1.1):** in `web/src/navigation/useRoleAwareNavigation.ts` wird die effektive `dashboardPersona` bei `capabilities.hasMultipleRoles === true` deterministisch auf `"generic"` gesetzt; in `web/src/auth/roleModel.ts` wird `dashboardPersona` aus der hoechstrangigen Rolle abgeleitet (admin > hr > manager > worker > reader), aber das Ergebnis wird durch die Mehrfachrollen-Regel im Navigation-Hook ueberschrieben. Folge: Admin-Aktionen, HR-/Manager-Aktionen und persona-spezifische Insight-Bloecke werden ausgeblendet, sobald irgendeine zweite Rolle mitlaeuft. Diese Inventur ist Aufgabe von Z14-1.1; der Verdacht hier ist Anker, nicht Befund.

**Leitplanken Z14:**
- Keine Implementierung in Z14, nur Review-/Planungsdoku.
- Keine neuen Produktentscheidungen als abgeschlossen darstellen — Z14 hebt das Problem, schneidet Optionen, schlaegt Folge-Slices vor.
- Schreibregel verbindlich: jeder Slice nennt praktische Bedeutung, Anlass und erwarteten Nutzen.
- Kein verstecktes UI-Refactor unter „Persona-Vertrag".
- Begriffstrennung Pflicht: **Rolle** (Berechtigung), **Persona** (abgeleitete Standard-Sicht), **aktive Ansicht** (was der Nutzer aktuell sehen will). Diese Drei werden in Z14-1.2 sauber definiert, nicht vorausgesetzt.

| Befund | Prio | Status |
|--------|------|--------|
| Z14-1.1 — Inventur Persona-/Mehrrollen-Kollisionen: alle Stellen, an denen `dashboardPersona` / `hasMultipleRoles` die Sicht/Aktionen/Navigation/Insights veraendern oder kollabieren; betroffene Bereiche (Dashboard-Overview, Navigation, Aktionen, Insights, Admin-Betriebsblock, ggf. Sub-Pages); Auflistung der heutigen `generic`-Faelle und ihrer Konsequenzen fuer den Nutzer | HIGH | offen |
| Z14-1.2 — Vertrags-/UX-Entscheidung: Begriffsklaerung Rolle vs. Persona vs. aktive Ansicht; Optionen fuer Mehrrollen-Behandlung skizzieren (z. B. Persona-Switcher mit Default + Persistenz; Aggregations-Persona statt `generic`; Admin-Vorrang fuer Admin+X; explizite Login-Auswahl); Pro/Contra je Option, ohne Festlegung; Vorgabe, was der Vertrag liefern muss (sichtbarer Schalter, Persistenz, Default-Regel, Fallback) | HIGH | offen |
| Z14-1.3 — Slice-Plan Folgezyklus: 2–3 sichere Umsetzungsslices mit Reihenfolge-Begruendung (typisch: Vertrag/Datenmodell zuerst, dann FE-Switcher, dann Aufraeumen der `generic`-Faelle in den abhaengigen Bloecken); ausdruecklich kein Code | HIGH | offen |

**Nicht in Z14:**
- Backend-Aenderungen am Berechtigungsmodell — Rollen/Permissions bleiben unveraendert; Z14 betrifft die Ableitung der **Sicht**, nicht der **Rechte**.
- Neue Personas oder Spezialrollen einfuehren.
- Mobile-/Tablet-Layout fuer das Dashboard (R10 bleibt eigener Backlog-Eintrag).
- UI-Polish ausserhalb der Persona-/Sicht-Logik.

**Naechster Schritt:** Z14-1.1 — Inventur. Codex entscheidet ueber Modell/Effort und Reihenfolge.
