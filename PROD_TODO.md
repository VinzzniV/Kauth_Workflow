# PROD_TODO.md

## Zweck

- Produkt-/UX-/Produktionsreife-Slices aus dem aktuellen Z21-Review (Stand 2026-05-12)
- ein eigener Slice-Plan fuer den Schritt von „funktioniert im Dev-Modus" zu „produktiv verantwortbar"
- nicht doppelt zu `TODO.md` oder `FRONTEND_TODO.md` pflegen — diese Datei zeigt nur die Slice-Reihenfolge und den jeweils erwarteten Aufwand/Modell-Profil

## Primaerquelle fuer

- naechsten produkt-/produktivitaetsrelevanten Slice
- Reihenfolge der Z21-Nacharbeit
- Bewertung Reasoning Effort + Modell + Plan-Mode pro Slice

## Nicht verwenden fuer

- abgeschlossene Detailhistorie → `CODE_REVIEW_ARCHIVE.md`
- aktive Review-Befunde → `CODE_REVIEW.md` (Z21)
- frontend-spezifische Backlog-Items → `FRONTEND_TODO.md`
- backend-Review-Nacharbeit → `TODO.md`

## Verwandte Dateien

- `CODE_REVIEW.md` — aktiver Z21-Review mit allen Findings und Belegen
- `TODO.md` — backend- und review-bezogene Nacharbeit, zyklusuebergreifend offene Befunde
- `FRONTEND_TODO.md` — frontend-spezifische Backlog-Items, UI-/UX-Slices
- `MEMORY.md` — kurzfristiger Fokus + aktive Watchouts
- `DOCS_CONTROL.md` — Doku-Lesereihenfolge und Pflege-Regeln
- `KauthWorkflow/Architektur/Entscheidungen.md` — fuer Architektur-Slices (z. B. S2)

## Wann aktualisieren

- wenn ein Slice abgeschlossen ist (Eintrag entfernen oder als done markieren und in `CODE_REVIEW.md` Z21-Block aktualisieren)
- wenn sich Priorisierung oder Slice-Schnitt aendert
- wenn ein Slice wegen einer Architektur-Entscheidung blockiert ist

## Pflicht vor jeder Aufgabe

Vor jedem Slice zuerst lesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`
- `CODE_REVIEW.md` (aktiver Z21-Block)
- diesen File
- bei Frontend-Anteilen zusaetzlich `FRONTEND_TODO.md` und `web/README.md`

Schreibregel: jeder Slice muss in `CODE_REVIEW.md` neben der Technik kurz erklaeren, was er praktisch bedeutet, warum er sich lohnt, und was dadurch besser/sicherer/schneller/wartbarer wird (siehe `CODE_REVIEW.md` § „Schreibregel").

---

## Slice-Plan Z21

Reihenfolge nach Risiko/Endnutzer-Nutzen. Die Slice-IDs `Z21-S1`..`Z21-S6` referenzieren die Findings im aktiven Z21-Block in `CODE_REVIEW.md`.

| # | Slice | Findings | Aufwand | Reasoning | Modell | Plan-Mode | Status |
|---|---|---|---|---|---|---|---|
| 1 | **Z21-S1** Simulation deutlich markieren + Mail-Dispatch-Health | P0-1 + P1-3 | klein-mittel (~½–1 Tag) | medium | sonnet | aus | **done 2026-05-12** |
| 2 | **Z21-S2** Hybrid-AD-Architekturentscheidung | P0-2 | klein in Zeilen, gross in Tragweite | high | opus | **an** | offen |
| 3 | **Z21-S3** Workflow-Storno fuer laufende Vorgaenge | P0-3 | mittel-gross (~1–2 Tage) | high | opus | **an** | offen |
| 4 | **Z21-S4** FE-UX-Buendel (Persona-Switcher, Workflow-Detail-Tabs, Listen-Trennung, directory_only) | P1-1 + P1-4 + P2-1 + P2-2 | mittel (~1 Tag) | medium | sonnet | aus | offen |
| 5 | **Z21-S5** Builder fachsprachlicher (Mapping-Labels + Condition-Wording) | P1-2 | mittel (~1 Tag) | high | opus | **an** | offen |
| 6 | **Z21-S6** `start-vm.sh dev`-Vorab-Check | P1-5 | klein (~½ h) | low | sonnet | aus | offen |

### Z21-S1 · Simulation deutlich markieren + Mail-Dispatch-Health

**Buendelung:** P0-1 (Automation ist nur Simulation, im UI nicht erkennbar) und P1-3 (Mail-Dispatch-Health im Admin-Dashboard sichtbarer) berueren denselben Aggregator (Admin-Warnings + Status-Tiles).

**Praktisch:** Admin sieht im Builder und im Admin-Dashboard sofort, dass keine echten externen Wirkungen entstehen und ob aktuelle Mail-Dispatches fehlschlagen.

**Lohnenswert:** Verhindert produktive Fehlbedienung (Workflow „succeeded", aber AD-Konto existiert nicht). Aufwand klein gegenueber Risiko.

**Nutzen:** „Wahrheit ueber den tatsaechlichen Runtime-Stand" wird sichtbar ohne Logs zu kennen.

**Scope:**
- Backend: `action_definitions.is_simulated` (oder Ableitung aus `backing_kind LIKE 'simulated_%'`) im List-Endpoint + Action-Definition-DTO sichtbar machen.
- Optional kleiner Endpoint fuer aktuelle Dispatch-Failures (letzte N Stunden) als Aggregat fuer das Admin-Dashboard.
- FE: Badge „Simuliert" in `WorkflowBuilderActionEditor` neben jeder Action; Warning-Item im `AdminOverviewWorkspaceSection`; rotes Stat-Tile bei kuerzlich blockierten Dispatches.
- Tests: ein Backend-Test fuer das DTO, ein FE-Snapshot/Render-Test.

**Risiko/Reversibilitaet:** klein. Reines Anzeigen-/Aggregator-Slice, kein Lifecycle-Mutationspfad.

---

### Z21-S2 · Hybrid-AD-Architekturentscheidung

**Reine Doku-/Entscheidungsslice.** Kein Code.

**Praktisch:** Vor jeder echten Automation-Handler-Arbeit muss klar sein, welcher Schreibweg gilt: Entra-fuehrt (Linux-Stack reicht) vs. AD-on-prem-fuehrt (Windows-Worker noetig) vs. Spiegelung.

**Lohnenswert:** Jede Implementierung ohne Entscheidung wird teuer verworfen. Auch Z21-S1 trifft hier indirekt zu (was an echten Handlern danach kommt).

**Nutzen:** Klare Richtung; danach kann S5 und ein moegliches „echte Handler"-Folgeslice methodisch gestartet werden.

**Scope:**
- `KauthWorkflow/Architektur/Entscheidungen.md` Abschnitt „AD/Entra-Schreibrichtung" mit gewaehlter Option + Begruendung.
- `KauthWorkflow/Architektur/Migrationspfad.md` neue Etappe „Schreibender Automation-Layer".
- Ggf. `PROJECT_CONTEXT.md` Guardrail-Block.

**Plan-Mode AN:** vor Schreibarbeit eine der drei Optionen waehlen (Frage an Produkt/Stakeholder).

**Blockiert:** echte Automation-Handler-Implementation und damit den Schritt aus dem F-Status in der Z21-Produkttabelle.

---

### Z21-S3 · Workflow-Storno fuer laufende Vorgaenge

**Praktisch:** HR/Admin kann einen laufenden Vorgang mit Begruendung sauber abbrechen statt mit Datenmuell zu leben.

**Lohnenswert:** Stornieren ist in jedem realen HR-Workflow taeglicher Edge-Case. Ohne Storno werden Audit-Pfade durch Workarounds zerstoert.

**Nutzen:** Vollstaendiger Audit-Trail, keine manuellen Reparatur-Workarounds, statistisch saubere Zahlen.

**Scope:**
- Backend: `POST /workflows/{uid}/cancel` mit Grund-Pflichtfeld. `WorkflowLifecycleService.CancelWorkflowAsync` als neuer Lifecycle-Pfad (Commit-Grenze wahren). Offene Tasks → `cancelled`. Notifications stoppen. Audit-Eintrag inkl. Reason.
- AuthZ: HR + Admin. Manager nur bei eigener Abteilung.
- DB: ggf. Migration fuer `cancellation_reason`/`cancelled_by_person_id`/`cancelled_at` auf `workflow_instances`.
- FE: Button + Confirmation-Dialog in `WorkflowManagementPanel`, sichtbar bei Status `running`; Pflichtfeld Reason.
- Tests: Backend-Integrationstest (Statusuebergang, Tasks, Audit), AuthZ-Test, FE-Test fuer den Button-Pfad.

**Risiko/Reversibilitaet:** Lifecycle-Mutationspfad, sorgfaltspflichtig. Migration nicht rueckgaengig machbar, daher in `db/manual/` mit Manifest-Eintrag verankern.

**Plan-Mode AN:** Designfragen vor Implementierung klaeren —
- Statusuebergaenge: erlaubte Quellen fuer `cancelled` (nur `running` oder auch `awaiting_approval`?).
- Reason-Schema: freier Text vs. Liste vordefinierter Gruende + Freitext.
- Rotation-Plan-Verknuepfungen bei Cancel: was passiert mit abhaengigen Rotation-Tasks?
- Re-Open: bewusst nicht (Konsistenz) vs. Admin-only.

---

### Z21-S4 · FE-UX-Buendel

**Buendelung:** Alles reine Frontend-UX/Wording/Layout-Arbeit, keine Backend-Vertraege.

**Praktisch:** Mehrere kleine taegliche Reibungspunkte gleichzeitig entschaerfen.

**Lohnenswert:** Jeder einzelne Punkt zu klein fuer eigenen Zyklus, gebuendelt ein spuerbar besseres Tagesgefuehl.

**Nutzen:** Klarere Navigation, klarere Workflow-Detail, klarere Mitarbeiter-Liste, klarerer Persona-Switcher.

**Scope (4 Sub-Slices, 1 PR oder 4 Mini-PRs):**
- **P1-4 Persona-Switcher**: Hinweistext „Ansicht wechseln (nur Anzeige, keine Rechteaenderung)" + Active-Role-Badge.
- **P2-2 Workflow-Detail-Tabs**: `WorkflowDetailPage` mit Tabs („Status & Aufgaben" / „Anforderungen" / „Audit & Links") analog `PersonWorkflowHistoryPage`.
- **P1-1 directory_only-Trennung**: eigene Sektion „Aus Entra noch nicht uebernommen" in `PeopleDirectoryPage`; primaerer Import-Einstiegspunkt klaeren. Optional `P3-1` mitnehmen (Inline-Styles raus).
- **P2-1 Listen-Trennung**: rollenspezifische Default-Einstiege, klarere Page-Subheads, ggf. ergaenzende Beschreibungstexte in `useRoleAwareNavigation.ts`.

**Risiko/Reversibilitaet:** klein-mittel, reversibel, kein Datenpfad.

---

### Z21-S5 · Builder fachsprachlicher (Mapping-Labels + Condition-Wording)

**Praktisch:** Mapping-Editor zeigt menschlich lesbare Labels statt Backend-Property-Pfade; Condition-Editor wird sprachlich naeher an Fachanwendung.

**Lohnenswert:** Erfuellt das Zielarchitektur-Versprechen „Nicht-Entwickler konfiguriert" deutlicher. Heutiger Stand ist Power-User-tauglich, nicht Fachanwender-tauglich.

**Nutzen:** Breiterer Admin-Personenkreis kann den Builder ohne Schulung bedienen.

**Scope:**
- Mapping-Editor (`WorkflowBuilderActionMappingEditor.tsx`): Labels aus dem Catalog-DTO (Backend liefert `Label` zusaetzlich zu `Properties`) oder FE-Lookup-Tabelle. JSON-Power-Modus bleibt Opt-In.
- Conditions (`WorkflowBuilderConditionEditor.tsx`): Standard-Modus weiter ausbauen, Operatoren mit fachlichen Texten („ist gleich" statt `eq`).
- Optional `P3-2` mitnehmen (Wording Schritt/Knoten/Baustein vereinheitlichen).
- Tests: Catalog-Endpunkt + Mapping-Editor-Snapshot.

**Plan-Mode AN:** Vorab klaeren —
- Labels im Backend-DTO vs. FE-Lookup? (Backend ist source-of-truth-konsistenter, FE ist schneller iterierbar.)
- Wie mit unbekannten Properties umgehen, fuer die kein Label existiert?

---

### Z21-S6 · `start-vm.sh dev`-Vorab-Check

**Praktisch:** Skript meldet sofort, wenn `dotnet`/`npm` fehlen, statt halb durchzulaufen.

**Lohnenswert:** Spart Setup-Zeit bei Demo-/Test-VMs.

**Nutzen:** Schneller produktiver Dev-Stand auf einer neuen Linux-VM.

**Scope:**
- `scripts/start-vm.sh`: `command -v dotnet` / `command -v npm` mit klarer Fehlermeldung und Verweis auf `KauthWorkflow/Betrieb/Setup.md`.
- Setup-Doku-Hinweis prominenter.

**Risiko/Reversibilitaet:** trivial.

---

## Bewusst nicht im Z21-Slice-Plan

| ID | Grund |
|----|-------|
| P2-4 Frontend-Bundle | aktuell vertretbar, kein Endnutzer-Schmerz |
| P2-5 Tailwind-Tokens | unter `FRONTEND_TODO.md` „Bewusst NICHT angefasst" |
| P3-1 Inline-Styles `PeopleDirectoryPage` | wenn S4 P1-1 ohnehin in der Datei arbeitet, mitnehmen; sonst Backlog |
| P3-2 Wording „Schritt/Knoten/Baustein" | mitnehmen mit S5; sonst Backlog |
| P3-3 R8/R10 | offene Nutzer-Aufgaben (Browser-Verifikation, Mobile-Layout), nicht code-pruefbar |

---

## Reihenfolge-Empfehlung

1. **Z21-S1 zuerst.** Hoechster Risiko-Reduktionsnutzen pro Aufwand. Sofort starten.
2. **Z21-S2 parallel** als Entscheidungs-Trigger (Produkt-/Stakeholder-Frage stellen, damit S5 und ein moegliches „echte Handler"-Folgeslice nicht spaeter blockiert sind).
3. **Z21-S3** als zweiter Show-Stopper aus dem Weg raeumen.
4. **Z21-S4** als spuerbarer UX-Sprung im Alltag.
5. **Z21-S5** loest das Builder-Zielarchitektur-Versprechen ein.
6. **Z21-S6** als Aufraeumarbeit.

Abschluss eines Slices loescht den jeweiligen Eintrag aus der Tabelle oben und wandert als Abschnitt in den Z21-Block in `CODE_REVIEW.md` (mit „done"-Vermerk + Datum), nicht hierher.
