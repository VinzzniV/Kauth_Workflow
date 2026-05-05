# CODEX_SYNC.md

## Zweck

- aktiver Handoff zwischen Codex und Claude
- kurze Uebersicht ueber juengste, fuer die naechste Session relevante Aenderungen
- kein Ersatz fuer `git log`

## Primaerquelle fuer

- juengste relevante Repo-Aenderungen zwischen Claude und Codex
- Doku- oder Prozessaenderungen, die die naechste Session direkt beeinflussen

## Nicht verwenden fuer

- vollstaendige Projekt-Historie
- detaillierte abgeschlossene Review-Archivierung

Dafuer sind `git log`, `CODEX_SYNC_ARCHIVE.md` und `CODE_REVIEW_ARCHIVE.md` da.

## Wann aktualisieren

- nach jeder abgeschlossenen Aufgabe
- wenn sich durch eine Aufgabe der naechste Arbeitskontext fuer die andere KI aendert

## Verwandte Dateien

- `MEMORY.md`
- `TODO.md`
- `CLAUDE_CONTROL.md`
- `CODEX_SYNC_ARCHIVE.md`

---

## Regeln

- Eintrag pro abgeschlossener Aufgabe, nicht pro Commit.
- Kurz und arbeitsrelevant schreiben.
- Sobald Eintraege fuer die naechste Session nicht mehr wichtig sind, nach `CODEX_SYNC_ARCHIVE.md` verschieben.

---

## Aktiver Sync-Log

| Datum | Aufgabe | Status | Betroffene Dateien | Zusammenfassung | Offene Risiken |
| --- | --- | --- | --- | --- | --- |
| 2026-05-05 | GIT-HYGIENE-CLEANUP | done | `.gitignore`, `CODEX_SYNC.md` | Git-Hygiene gegen aktuellen Repo-Stand geprueft. `quality-gates.yml` passt weiter zu `api/API.Tests` und `web/package.json`, daher keine Workflow-Aenderung noetig. `.gitignore` um `.tmp-test-bin/` sowie lokale Obsidian-Workspace-Dateien (`KauthWorkflow/.obsidian/app.json`, `workspace.json`) ergaenzt. Offensichtlicher lokaler Muell entfernt: `.tmp-*`-Build/Test-Caches, `artifacts/`, `handoff/`-ZIP, Root-Logs, Root-`node_modules/` sowie versehentliches Root-Node-Setup (`package.json`, `package-lock.json`) aus dem Repo entfernt. | `OnboardingERD.pgerd` bleibt bewusst unberuehrt: aktuell unreferenziert, aber nicht eindeutig als Muell klassifizierbar. Falls gewuenscht spaeter gesondert entscheiden (behalten, verschieben oder entfernen). |
| 2026-05-05 | VAULT-DOCS-QUALITY-OVERHAUL | done | `KauthWorkflow/00 Start.md`, `KauthWorkflow/Domäne/Begriffe.md`, `CODE_REVIEW.md`, `CODE_REVIEW_ARCHIVE.md`, `CODEX_SYNC.md`, `CODEX_SYNC_ARCHIVE.md`, `MEMORY.md`, `TODO.md`, `PROJECT_CONTEXT.md`, `PROJECT_STRUCTURE.md`, `CLAUDE_CONTROL.md`, `CLAUDE.md`, `DOCS_CONTROL.md`, `FRONTEND_TODO.md`, `KauthWorkflow/Stand/Code-Review-Status.md`, `KauthWorkflow/Arbeit/KI-Workflow.md`, `KauthWorkflow/Lernen/01-Die-grosse-Landkarte.md` | Doku-Qualitaet fuer Mensch und KI strukturell angehoben: fehlenden Vault-Einstieg `00 Start.md` angelegt, Glossar `Begriffe.md` ergaenzt, aktive vs. historische Review-/Sync-Inhalte in `*_ARCHIVE.md` getrennt, zentrale Root-Dateien mit einheitlichem Zweck-/Primaerquelle-/Nicht-verwenden-Header versehen und `MEMORY.md`/`TODO.md` deutlich auf aktive Arbeit reduziert. | Die neue Struktur ist absichtlich kompakter. Falls einzelne Teams spaeter mehr fachliche Detailtiefe in `PROJECT_STRUCTURE.md` oder `Code-Review-Status.md` brauchen, dort gezielt ergaenzen statt die aktiven Steuerungsdateien wieder aufzublaehen. |
| 2026-05-05 | MD-CLEANUP-Z8 | done | `MEMORY.md`, `TODO.md`, `CODE_REVIEW.md`, `KauthWorkflow/Stand/Code-Review-Status.md`, `KauthWorkflow/Architektur/Migrationspfad.md`, `KauthWorkflow/Lernen/06-Mini-Projekt-Todo-Board.md`, `KauthWorkflow/Lernen/07-Einarbeitungsplan-im-echten-Repo.md`, `CODEX_SYNC.md` | Markdown-Cleanup auf aktuellen Stand gebracht: `MEMORY.md` auf aktiven Zyklus 8 reduziert, `TODO.md` und Review-Dateien von Altstaenden bereinigt, `Migrationspfad.md` Schritt 7 auf erledigt gesetzt. | Historische Handoff-Eintraege liegen jetzt in `CODEX_SYNC_ARCHIVE.md`, falls weitere Verdichtung noetig wird. |
| 2026-05-05 | LEARNING-PATH-ONBOARDING | done | `KauthWorkflow/Lernen/07-Einarbeitungsplan-im-echten-Repo.md`, `KauthWorkflow/Lernen/06-Mini-Projekt-Todo-Board.md`, `CODEX_SYNC.md` | Neue Lerneinheit fuer die Einarbeitung ins echte Repo angelegt; Mini-Projekt-Einheit um den Verweis auf den Einarbeitungspfad ergaenzt. | Bei formalerem Team-Onboarding den Lernpfad mit echten Einstiegsaufgaben abgleichen. |
| 2026-05-05 | CLAUDE-CONTROL | done | `CLAUDE_CONTROL.md`, `DOCS_CONTROL.md`, `CLAUDE.md`, `PROJECT_STRUCTURE.md`, `KauthWorkflow/Arbeit/KI-Workflow.md`, `CODEX_SYNC.md` | Operative Claude-Steuerung unter Codex-Orchestrierung eingefuehrt: Rollen, Slice-Workflow, Doku-Pflicht, Commit-Regel. | `CLAUDE_CONTROL.md` als Primaerquelle halten; keine konkurrierenden Regeltexte aufbauen. |
| 2026-05-05 | CLAUDE-REVIEW-ZYKLUS-8 | done | `CODE_REVIEW.md`, `TODO.md`, `KauthWorkflow/Stand/Code-Review-Status.md`, `CODEX_SYNC.md` | Zyklus 8 eroeffnet. Thema: Skalierbarkeits- & Last-Haertung. Erste Schritte: Z8-1.1 Inventur, Z8-1.2 Hotspot-Auswahl, Z8-2.x SQL-Pushdown/Pagination, Z8-3 Sweep-/Dispatch-Performance, Z8-4 Tests. | Z8-1.1 muss vor jeder Umsetzung passieren, sonst wird Hotspot-Auswahl zum Bauchgefuehl. |
