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

**Stand**: 2026-05-08 — Zyklus 16 aktiv (Z16-S1 + Z16-S2 abgeschlossen). Aktiver Zyklus: Z16 (Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag).
**Letzte Reviews**: Claude (2026-04-23 Original; 2026-05-02..03 Zyklus 2–5; 2026-05-03..04 Zyklus 6; 2026-05-05 Zyklus 7; 2026-05-05 Zyklus 8 abgeschlossen; 2026-05-05 Zyklus 9 abgeschlossen; 2026-05-05 Zyklus 10 abgeschlossen; 2026-05-05 Zyklus 11 eroeffnet) + Codex-Fallback (2026-05-05 Z11-F1 Abschluss waehrend Claude-Rate-Limit) + Claude (2026-05-06 Z11-F2 Abschluss; 2026-05-06 Z11-F3 Abschluss = Z11 vollstaendig geschlossen; 2026-05-06 Z12 eroeffnet + abgeschlossen; 2026-05-06 Z13 eroeffnet + abgeschlossen; 2026-05-07 Z14 eroeffnet + abgeschlossen; 2026-05-08 Z15 eroeffnet + abgeschlossen; 2026-05-08 Z16 eroeffnet).

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

- Die Detailzyklen **Z8 bis Z15** liegen jetzt in `CODE_REVIEW_ARCHIVE.md`.
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

## Aktiver Zyklus 16 — Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag (2026-05-08)

Eroeffnet 2026-05-08. Thema: Die 360°-Mitarbeiterakte (`/people/:personId`) existiert bereits als vollwertige Seite, ist aber ohne eigenen Navigationseintrag und ueber einen konzeptionell falschen Permission-Vertrag gebunden. Personen-Suche (`/people/search`) ist an `CanCreateWorkflow` gehaengt — das ist semantisch falsch und schliesst Rollen aus, die Personen suchen muessen, ohne Workflows zu erstellen. Zusaetzlich fehlt fuer spaetere Automationen ein formaler Snapshot-Vertrag (welche Identity-Daten wurden wann gelesen).

**Praktisch:** HR kann heute nicht direkt zur Mitarbeiterakte navigieren — es gibt keinen Navigationseinstieg, sondern nur indirekte Links aus Workflows oder dem Rotationsplan. Das ist ein Workflowheld-Problem: Person ist der fachliche Primaeranker des Systems, hat aber keinen eigenen Bereich. **Lohnenswert:** Person-als-Einstieg staerkt den Architekturanker (Person ist nicht Anhang eines Workflows, sondern eigenstaendig), bereinigt den falschen Permission-Vertrag und schafft den Andockpunkt fuer Automationen, die sicher auf das verknuepfte Entra-Objekt zugreifen muessen. **Nutzen:** direkter HR/Admin-Zugang zur Akte; sauberer Permission-Vertrag; klarer Identity-Snapshot-Vertrag fuer Automationen.

**Leitplanken:**
- Guardrail halten: Person = fachlicher Anker, technische Identity (Entra) = getrennt; nicht vermischen.
- Keine neuen onboarding-spezifischen Einschraenkungen einfuehren.
- Backend ist Source of Truth: Permission-Vertrag zuerst im Backend, dann FE.
- Reihenfolge: S1 (Vertrag/Planung) → S2 (BE-Permission) → S3 (FE-Navigation) → S4 optional (Automation-Snapshot).

### Z16-S1 — Inventur + Vertragsentscheidung (done 2026-05-08)

**Ist-Zustand Inventur:**

**Route/Navigation/Einstiege:**
- `/people/:personId` → `web/src/pages/PersonWorkflowHistoryPage.tsx`; Route-Guard: `feature="workflowOverview"` in `web/src/App.tsx:147-155`
- Kein eigener Navigationseintrag in `web/src/navigation/useRoleAwareNavigation.ts` — keine direkte Navigation zur Akte
- Einstiege nur indirekt: `web/src/components/workflow-detail/WorkflowHeaderPanel.tsx`, `web/src/components/workflows/TargetPersonSelection.tsx`, `web/src/pages/RotationPlanDetailPage.tsx`
- Breadcrumb in `PersonWorkflowHistoryPage.tsx:784`: „Vorgaenge suchen" → Personenakte — erwartet Workflow-Such-Pfad als primaere Aufruferrolle
- `/people/search` ist keine eigene Seite, nur Backend-API + Typeahead-Picker

**Backend-Zugriffsvertrag:**
- `GET /people/{personId}/workflows` → `CanAccessWorkflowOverview` (HR, Admin, Reader, Manager + `WorkflowsViewAll`/`ViewDepartment`-Permission) — `api/API/Endpoints/WorkflowEndpoints.cs:295-296`
- `GET /people/search` → `CanCreateWorkflow` (HR, Manager, Admin + workflow.create permission) — `api/API/Endpoints/WorkflowMasterDataEndpoints.cs:120-122` — **konzeptioneller Fehler**: Personensuche ist an „darf Workflows erstellen" gehaengt; ein Reader oder Worker-Abteilungsleiter mit `WorkflowsViewDepartment` koennte Personen nicht finden, auch wenn er die Akte oeffnen darf
- `POST /people` → `CanCreateWorkflow` — korrekt
- `GET /people/rotation-eligible` → `CanCreateWorkflow` — korrekt (Rotation-Erfassungskontext)

**Identity/Directory-Vertrag heute:**
- `PersonWorkflowHistoryDto` (`api/API/Contracts/WorkflowDtos.cs:807`) enthaelt: `PersonId`, `AppUserId?`, `DirectoryIdentityId?`, `DirectoryLinkStatus?`, `DirectoryDisplayName?`, `DirectoryUserPrincipalName?`, `DirectoryMail?`, `DirectoryEmployeeNumber?`
- `AutomationPropertyCatalog.cs:41-56` referenziert `appUserId` und `directoryIdentityId` als verfuegbare Automation-Properties — aber kein formaler Snapshot-Vertrag (welche Werte, zu welchem Zeitpunkt, fuer welche Automation aufgenommen)
- Was fehlt: kein `snapshotAt`-Zeitstempel, kein dediziertes `AutomationPersonIdentitySnapshotDto`, kein Audit-Eintrag „Identity zum Zeitpunkt X fuer Automation Y gelesen"

**Vertragsentscheidungen fuer Z16:**

| Entscheidung | Ergebnis | Begruendung |
|--------------|---------|-------------|
| Eigene Seite / Hauptnavigation | **JA** — `/people` als neue Listen-/Such-Seite mit Navigationseintrag fuer HR + Admin | Person ist fachlicher Primaeranker; ohne direkten Einstieg bleibt die 360°-Akte ein verstecktes Werkzeug statt ein zentrales Arbeitsmittel |
| Welche Rollen sehen `/people` Liste | **Admin + HR** — enger als `workflowOverview` (weil Manager/Reader keine Personenbestandsliste brauchen) | Abteilungsleitung und Reader brauchen die Akte fuer einzelne Personen (via Link), aber keine Suche ueber alle Personen |
| `/people/:personId` Detail | Bleibt wie heute: `workflowOverview` (Admin, HR, Manager, Reader) — kein Rueckbau | Bestehende Verlinkungen aus Workflow-Detail und Rotation bleiben valide |
| Eigener Feature-/Permission-Vertrag | **JA** — neues FE-Feature `peopleDirectory`; BE-Policy `CanAccessPeopleDirectory` (Admin + HR) | `workflowOverview` ist semantisch „Workflow-Liste sehen", nicht „Personenbestand browsen" — Trennung vermeidet spaetere Rollenausweitung durch Missbrauch des falschen Containers |
| `/people/search` Permission | Von `CanCreateWorkflow` auf **`CanAccessWorkflowOverview`** umstellen | Sofort sicherer, kein neues Policy-Konzept noetig in S2 — Personen suchen ist inhaltlich naeher an „Workflow-Uebersicht" als an „Workflow erstellen"; genauere Policy (`CanAccessPeopleDirectory`) koennte in S2 parallel definiert werden |
| Person vs. Identity fuer Automationen | Trennung **bleibt bestehen**: `personId` = fachlicher Anker, `directoryIdentityId` = Entra-Objekt-Anker | Wie in `KauthWorkflow/Domäne/Identity.md` und `Entscheidungen.md` verankert; Automation-Snapshot muss beide Felder plus `snapshotAt` enthalten |
| Mindestdaten Automation-Snapshot | `personId`, `appUserId`, `directoryIdentityId`, `directoryUserPrincipalName`, **`snapshotAt`** (neu) | Ohne Zeitstempel ist nicht nachvollziehbar, welcher Identity-Stand bei einer Automation gallt — ein spaerent gelinkter Entra-Account wuerde retroaktiv alle alten Automation-Logs betreffen |

**Breaking-Risk-Klaerung Z16-S2 (explizit geprueft):** Der Switch `/people/search` von `CanCreateWorkflow` auf `CanAccessWorkflowOverview` betrifft in der Theorie User, die NUR eine `workflows.create.*`-Permission haben (keine Rolle). Dieser User-Typ ist ueber das Admin-Permission-Modell erzeugbar, kommt im normalen Anlege-Flow aber nicht vor. Der Switch erweitert gleichzeitig den Zugriff fuer Reader und `WorkflowsViewDepartment`-User — semantisch korrekt, da Personensuche ein Lesevorgang ist. Entscheidung: Switch erfolgt wie in Z16-S1 beschlossen.

**Slice-Plan Z16:**

| Slice | Inhalt | Reihenfolge-Begruendung | Modell/Effort |
|-------|--------|------------------------|---------------|
| Z16-S1 | Inventur + Vertragsentscheidung (dieser Slice) | Vertrag zuerst, kein Code-Risiko | sonnet / medium |
| Z16-S2 | BE-Permission-Vertrag: `CanAccessPeopleDirectory` Policy; `/people/search` von `CanCreateWorkflow` auf `CanAccessWorkflowOverview` umstellen; neuer `GET /admin/people` Admin-Listenendpunkt mit P1-Hull | BE ist Source of Truth; FE darf nicht vor stabilem BE starten | sonnet / medium |
| Z16-S3 | FE-Navigation: `peopleDirectory`-Feature, `PersonSearchPage` unter `/people`, Navigationseintrag HR + Admin, Breadcrumb in `PersonWorkflowHistoryPage.tsx` aktualisieren | Baut auf Z16-S2 auf; klarer Vertrag noetig bevor FE-Routing und Nav-Eintrag angelegt werden | sonnet / medium |
| Z16-S4 (optional) | Automation-Snapshot-Vertrag formal: `AutomationPersonIdentitySnapshotDto` im Backend, `snapshotAt` Feld, Audit-Eintrag bei Automation-Start | Inkrementell; kann nach S3 unabhaengig freigegeben werden; benoetigt Produkt-Entscheidung ob Snapshot in `workflow_automation_jobs` gespeichert wird | opus / high |

| Befund | Prio | Status |
|--------|------|--------|
| Z16-S1 — Inventur + Vertragsentscheidung Mitarbeiterakte | HIGH | done 2026-05-08 |
| Z16-S2 — BE-Permission-Vertrag + Admin-People-Endpunkt | HIGH | done 2026-05-08 |
| Z16-S3 — FE-Navigation: peopleDirectory-Feature + PersonSearchPage + Nav-Eintrag | HIGH | offen |
| Z16-S4 — Automation-Snapshot-Vertrag formal (optional) | MEDIUM | deferred — wartet auf Produkt-Entscheidung Snapshot-Persistenz |

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
| 16 | 2026-05-08 | Mitarbeiterakte als eigener Navigationsbereich + sauberer Identity-/Permission-Vertrag — **Z16-S1 done** |
