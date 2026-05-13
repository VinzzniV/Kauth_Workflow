# TODO - Produktreife

## Zweck

Aktiver Produkt- und Umsetzungsplan. Nur Punkte, die direkt auf Endbenutzer-Nutzen, fachliche Funktion, Automatisierung oder Produktionsreife einzahlen.

Historie und erledigte Slices liegen in `CODE_REVIEW_ARCHIVE.md` (Abschnitte „Zyklus 21 (Done-Findings)" + „Zyklus 21 (weitere Done-Findings, 2026-05-12) — Erweiterung"). Etappe 9a Schritt 6 (Temporary-Credentials-Vault, 2026-05-13), Schritt 7 (CreateMailboxGraph, 2026-05-13) und Schritt 8 (AlreadyExists-Branch im Workflow-Engine, 2026-05-13) sind abgeschlossen; Belege siehe Commits `ee77538`/`1d5328a`/`a422c81`/`0600934` (Schritt 6), `4c2ea47`/`161db55`/`c23ff86`/`c9382d4` (Schritt 7) und `13371cc` (Schritt 8) + Doku-Commit dieses Slices.

## Leseregeln

1. Vor Arbeiten an Backend, Frontend oder Dokumentation zuerst `DOCS_CONTROL.md` lesen.
2. Danach `PROJECT_CONTEXT.md`, `CODE_REVIEW.md`, `MEMORY.md` und diese Datei lesen.
3. Fuer UI-Arbeiten zusaetzlich `FRONTEND_TODO.md` lesen.
4. Nach Umsetzung eines Punktes Status, Erkenntnisse und Folgepunkte hier aktualisieren.

## Stand 2026-05-13

Z21-S1..S3 + S5..S10 + S6b sind 2026-05-12 abgeschlossen. Migrationspfad-Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 alle durch. Verifizierter Ist-Stand (per `scripts/verify-prod-ready.sh`):
- API Release-Build: ✅ gruen
- API-Test-Build: ✅ 7 Errors (Baseline aus frueheren Refactorings; keine neuen seit 2026-05-12)
- FE-Build: ✅ gruen
- FE-Tests: ✅ 323 passed
- `start-vm.sh`-Syntax: ✅ gruen
- Worker.Core + Worker.Tests Build: ✅ gruen (deterministisch, Linux)
- Worker Core tests: ✅ 57 passed (48 vor Schritt 6 + 9 VaultKeyLoaderTests)

Nicht code-pruefbar (Nutzer-Aufgabe): Browser-Smoke Builder-Form-Editor (R8), Mobile-Layout (R10), Graph-/Mail-Live-Verifikation mit echten Credentials, Worker-Host-Build (net8.0-windows) auf einer Windows-Maschine, E2E `CreateAdUserLdaps` gegen Test-DC + Vault-Roundtrip, E2E `SendWelcomeMailGraph` mit Vault-Mapping und echtem Passwort im Mail-Body.

## Aktive TODOs

| ID | Aufgabe | Prio | Status | Nutzen |
| --- | --- | --- | --- | --- |
| Z21-S4 | Realen Automation-Pfad aus der Hybrid-AD-Entscheidung ableiten. | HIGH | Etappe 9a Schritt 1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 ✓ 2026-05-13. Schritt 8 hat den **AlreadyExists-Branch im Workflow-Engine** geschlossen: Decision-Nodes koennen jetzt zusaetzlich zu Formular-Antworten ueber `automation_output`-Bedingungen verzweigen. Neuer JSON-Discriminator `referenceKind: 'answer' | 'automation_output'` in `workflow_edges.condition_expression` (Bestandsform bleibt als `answer`-Default gueltig — kein Migration-Skript). `WorkflowRuntimeSnapshot.AutomationOutputsByNodeKey` wird im EngineAdapter ueber den bestehenden Schritt-7-Helper befuellt; `Plan(...)` reicht es an allen vier Stellen durch. Drei Validation-Schranken (Property-Union-Schnellcheck im Parser; Direct-Predecessor- und Action-Key-Schranke im Plan-Lauf gegen `IncomingEdgesByTargetNodeId` + `NodeActionsByNodeId` mit Single-Action-Pflicht) erzwingen den "nur direkter Predecessor"-Scope technisch. Initial-Whitelist `CreateAdUserLdaps.alreadyExisted` (Boolean, Operator-Set `is_true`/`is_false`); Catalog-DTO um `ConditionProperties` erweitert mit Drift-Schutz-Test gegen die Engine-Map. Praktisch: Workflow `CreateAdUserLdaps → DecisionNode → (true) End-Skip | (false/Fallback) AssignGroups → SendWelcomeMail` laeuft jetzt sauber durch — AlreadyExists ist kein Permanent-Failure mehr. **Naechster Code-Slice (eigener Plan-Mode-Slice):** `CreateErpEmployee` als eigener Backend-Architektur-Slice (InforLN; zurueckgestellt bis Stakeholder-Wunsch). Vor Live-Inbetriebnahme: alle Schritt-6/7-Voraussetzungen (DPAPI-Vault, Graph-App-Permissions). | Macht aus vorbereiteter Automation einen implementierbaren Produktionspfad — End-to-End Onboarding mit AD-Anlage + automatischer Mailbox + echtem Passwort in der Welcome-Mail. AlreadyExists-Fall ist jetzt als Workflow-Pfad modellierbar (Skip-Edge nach CreateAdUserLdaps), nicht mehr nur als Permanent-Failure. |

## Nachgelagert

| ID | Aufgabe | Prio | Status | Grund fuer Nachrang |
| --- | --- | --- | --- | --- |
| Z21-N1 | Bundle-Splitting fuer grosse Frontend-Chunks pruefen. | LOW | offen | Build funktioniert; Performance relevant, aber nicht produktionsblockierend. |
| Z21-N2 | Mobile Feinschliffe fuer Admin-/Builder-Masken pruefen. | LOW | offen | Primaerer Nutzungsfall ist Desktop. |
| Z21-N3 | Alte Demo-/Testdaten und unklare Beispielinhalte bereinigen. | LOW | offen | Sinnvoll vor Produktivnahme, aber nicht blockierend. |

## Bewusst entfernt aus dem aktiven Backlog

- Erledigte Z21-Slices wandern direkt nach Abschluss in `CODE_REVIEW_ARCHIVE.md`.
- Reine Stil-, Refactoring- und Kosmetikthemen ohne direkten Einfluss auf Endbenutzer, Betrieb oder Produktionsreife sind nicht aufgenommen.
