# 00 Start

#start #index

Einstieg in den Obsidian-Vault.

Diese Datei ist die erste Orientierung fuer Menschen und KIs, die verstehen wollen:
- welche Bereiche der Vault hat
- welche Datei fuer welches Thema die Primaerquelle ist
- welcher Lesepfad fuer welchen Zweck sinnvoll ist

---

## Zweck

- schneller Einstieg in den Vault
- klare Trennung zwischen Vault-Wissen und Repo-Steuerungsdateien
- Lesepfade fuer Architektur, Betrieb, Einarbeitung und KI-Zusammenarbeit

## Primaerquelle fuer

- Navigation im Vault
- Zuordnung: Welche Datei beantwortet welche Frage?
- Lesepfade fuer neue Entwickler, Architekturarbeit und KI-Arbeit

## Nicht verwenden fuer

- aktuellen Umsetzungsstatus einzelner Tasks
- kurzfristige Session-Notizen
- Handoff zwischen Claude und Codex

Dafuer sind im Repo die Dateien `CODE_REVIEW.md`, `TODO.md`, `MEMORY.md` und `CODEX_SYNC.md` zuständig.

---

## Der Unterschied: Vault vs. Repo

| Bereich | Zweck |
|--------|-------|
| Repo-Root-MDs | operative Wahrheit fuer aktuelle Arbeit |
| `KauthWorkflow/` | stabiles Wissen, Architektur, Domäne, Betrieb, Lernen |

Merksatz:
- **Aktueller Arbeitsstand** liegt im Repo.
- **Stabiles Erklaerwissen** liegt im Vault.

---

## Vault-Bereiche

| Bereich | Inhalt | Typische Fragen |
|--------|--------|-----------------|
| `Architektur/` | Zielbild, Entscheidungen, Migrationspfad, groessere Skizzen | Wohin entwickelt sich das System? |
| `Domäne/` | Workflow, Rotation, Identity, Begriffe | Was bedeuten die Kernkonzepte? |
| `Code-Landkarte/` | fachlicher Bereich → Code-Files (Brücke Fach ↔ Code) | Wo liegt der Code für „Abteilungswechsel" / „Automation" / …? |
| `Betrieb/` | Setup, Laufzeit, Deployment | Wie starte und betreibe ich das System? |
| `Stand/` | Detail-Doku zu größeren Architektur-Bausteinen | Wie ist Komponente X im Detail aufgebaut? |
| `Arbeit/` | KI-Workflow, Engineering-Regeln | Wie arbeiten Mensch, Claude und Codex zusammen? |
| `Lernen/` | gefuehrte Lerneinheiten | Wie arbeite ich mich in das Projekt ein? |

---

## Wichtige Primaerquellen

### Operative Repo-Dateien

| Datei | Wofuer sie die Primaerquelle ist |
|------|----------------------------------|
| `DOCS_CONTROL.md` | Lesereihenfolge, Doku-Hygiene, Update-Regeln |
| `PROJECT_CONTEXT.md` | stabile Guardrails und Produktziel |
| `PROJECT_STRUCTURE.md` | Repo-Struktur und Einstiegspunkte |
| `CODE_REVIEW.md` | aktueller technischer Review-Fokus |
| `TODO.md` | aktive Arbeitsplanung |
| `MEMORY.md` | kurzfristiger Fokus fuer die naechste Session |
| `CODEX_SYNC.md` | Handoff Codex ↔ Claude |
| `CLAUDE_CONTROL.md` | operative Claude-Steuerung unter Codex-Orchestrierung |

### Vault-Dateien

| Datei | Wofuer sie die Primaerquelle ist |
|------|----------------------------------|
| [[Zielarchitektur]] | stabiles Sollbild |
| [[Entscheidungen]] | langfristige Architekturentscheidungen |
| [[Migrationspfad]] | grobe Transformationsreihenfolge |
| [[Begriffe]] | Glossar zentraler Fach- und Technikbegriffe |
| [[Hybrid-Worker-Sub-Architektur]] | Detail-Doku zum Windows-Worker für AD-Schreibaktionen |
| [[Admin-Gated-Automation]] | Zielbild für Prod: Plan-Vorschau, Admin-Approval mit Re-Auth, Post-Execution-Summary |
| [[Konfiguration]] | Variablen-Übersicht: alle Konfig-Werte + kanonische Speicherorte (Backend + Frontend + Worker) |
| [[00 Uebersicht\|Code-Landkarte]] | Brücke Fach → Code: wo liegen die Files für „Abteilungswechsel" etc. |
| [[KI-Workflow]] | Zusammenarbeit von Mensch, Claude und Codex |

---

## Lesepfade

### Neuer Entwickler

1. `PROJECT_CONTEXT.md`
2. `PROJECT_STRUCTURE.md`
3. [[Zielarchitektur]]
4. [[Begriffe]]
5. `KauthWorkflow/Lernen/01-Die-grosse-Landkarte.md`
6. `KauthWorkflow/Lernen/07-Einarbeitungsplan-im-echten-Repo.md`

### Architekturarbeit

1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. [[Zielarchitektur]]
4. [[Entscheidungen]]
5. [[Migrationspfad]]
6. `CODE_REVIEW.md`

### Aktuelle Review-Nacharbeit

1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `CODE_REVIEW.md`
4. `TODO.md`
5. `MEMORY.md`
6. `CODEX_SYNC.md`

### Claude unter Codex-Orchestrierung

1. `DOCS_CONTROL.md`
2. `PROJECT_CONTEXT.md`
3. `MEMORY.md`
4. `CODEX_SYNC.md`
5. `CLAUDE_CONTROL.md`
6. je nach Aufgabe `CODE_REVIEW.md`, `TODO.md`, `PROJECT_STRUCTURE.md`

### Einarbeitung

1. `KauthWorkflow/Lernen/01-Die-grosse-Landkarte.md`
2. `KauthWorkflow/Lernen/02-Anfrage-durch-den-Code.md`
3. `KauthWorkflow/Lernen/03-Csharp-Syntax.md`
4. `KauthWorkflow/Lernen/06-Mini-Projekt-Todo-Board.md`
5. `KauthWorkflow/Lernen/07-Einarbeitungsplan-im-echten-Repo.md`

---

## Gute Arbeitsregel

Wenn unklar ist, ob eine Information stabil oder kurzfristig ist:
- **stabil** → in den Vault
- **operativ / aktuell / tasknah** → in die Repo-MDs

---

## Verwandte Dateien

- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `PROJECT_STRUCTURE.md`
- [[Zielarchitektur]]
- [[Begriffe]]
- [[KI-Workflow]]
