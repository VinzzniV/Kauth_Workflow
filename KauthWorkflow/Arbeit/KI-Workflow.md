# KI-Workflow — Wie Claude und Codex mit dem Vault arbeiten

#arbeit #ki #prozess

---

## Vault vs. Repo-Docs — Wofür was

| Repo-Docs | Obsidian Vault |
|-----------|---------------|
| Maschinenlesbar, für Claude/Codex | Menschlich navigierbar, für dich |
| Kurzlebig / aufgabennah | Langlebige Wissensbasis |
| Pflicht-Lesereihenfolge für KI-Arbeit | Nachschlagewerk für Konzeptfragen |
| Aktueller Session-Kontext (MEMORY.md) | Stabiles Domänenwissen |

Der Vault **ersetzt nicht** die Repo-Docs. Er ergänzt sie mit menschlicher Navigierbarkeit und stabilem Hintergrundwissen.

Einstieg in den Vault: [[00 Start]]

---

## Wann Claude den Vault lesen soll

| Situation | Warum |
|-----------|-------|
| Architektur-/Planungsaufgabe (neue Features, Schnittentscheidungen) | Zielarchitektur und Entscheidungen als Kontext |
| Unbekannte Domänenkonzepte (z.B. "Was ist eine Responsibility?") | Identity, Workflow, Rotation als Nachschlagewerk |
| "Passt das zur Architektur?" | Entscheidungen + Zielarchitektur prüfen |

Wie dem Claude mitteilen: "Lies Architektur/Zielarchitektur.md bevor du planst" oder einfach auf eine Vault-Datei verweisen.

---

## Wann Codex den Vault lesen soll

Codex arbeitet primär aus `CODE_REVIEW.md § 7` (konkrete Fix-Snippets). Der Vault ist für Codex nützlich bei:

| Situation | Welche Datei |
|-----------|-------------|
| "Was ist der Unterschied Rolle vs. Responsibility?" | [[Identity]] |
| "Was macht ein department_action_template?" | [[Rotation]] |
| "Welche Node-Typen gibt es?" | [[Zielarchitektur]] |
| "Ist mein Fix architekturkonform?" | [[Entscheidungen]] |

---

## Wann in den Vault geschrieben werden soll

### Vault-Updates durch Claude

| Auslöser | Was updaten |
|----------|------------|
| Architekturentscheidung gefallen (langfristig, neue Konsequenz) | [[Entscheidungen]] ergänzen |
| Migrationsetappe abgeschlossen (komplett, nicht einzelner Slice) | [[Migrationspfad]] grobes Big-Picture updaten |
| Neues Domänenkonzept eingeführt | Passende Domänen-Datei ergänzen |

**Wichtig — was NICHT in den Vault gehört:** Slice-für-Slice-Status, Sub-A/B/C-Detailprotokolle, „Schritt N ✓ 2026-...-NN"-Listen, Plan-Mode-Reste, abgeschlossene Skizzen. Diese gehören in `CODE_REVIEW.md`, `TODO.md` und ins git log. Der Vault ist für Menschen, die jetzt verstehen wollen, wie das System aufgebaut ist — nicht für die Historie, wie es dort hingekommen ist.

### Nicht in den Vault schreiben

- Kurzfristiger Session-Kontext → gehört in `MEMORY.md`
- Konkrete To-dos und Aufgaben → gehört in `TODO.md`
- Code-Änderungen und Diffs → gehören ins Repo
- Build/Setup-Anleitung → gehört in [[Setup]]

---

## Codex-Handoff auf einen Blick

Codex braucht für jede Aufgabe aus `TODO.md`:

1. Primärquelle: `CODE_REVIEW.md § 7` (konkrete Snippets)
2. Pflichtlektüre: `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md`, `MEMORY.md`
3. Nach Abschluss: Eintrag in `CODEX_SYNC.md`, Status in `TODO.md` auf `done`

Wichtige Regeln für Codex:
- Nur ändern, was für den aktuellen Schritt nötig ist
- Keine Nebenbei-Refactors
- Unsicherheit benennen statt raten
- Keine neuen großen Dateien ohne Split-Bewertung

---

## Split-Workflow: Claude vs. Codex

| Aufgabentyp | Wer |
|-------------|-----|
| Architektur, Planung, Migrationsschnitte | Claude |
| Sicherheitsrelevante SQL-Logik, Auth-Schnitte | Claude |
| Routine-Implementierung mit vorgelegtem Snippet | Codex |
| Mechanische Umbauarbeit (Suche-Ersetze) | Codex |
| Große Refactors mit vielen Abhängigkeiten | Claude Opus |

---

## Codex als Orchestrator fuer Claude

Wenn Codex Claude steuert, gilt zusaetzlich:

- Codex waehlt den naechsten Slice und begrenzt den Auftrag.
- Claude arbeitet nur den beauftragten Slice ab.
- Claude aktualisiert die noetigen Dokus im selben Arbeitsgang.
- Claude committet nach jedem abgeschlossenen Slice.
- Wenn ein Block zu gross ist, schlaegt Claude einen kleineren Slice-Plan vor, statt halb umzubauen.

Die operative Regeldatei dafuer ist `CLAUDE_CONTROL.md` im Repo-Root.

---

## Vault-Pflege

- Vault-Dateien werden nicht bei jeder Session aktualisiert
- Updates nur wenn sich stabiles Wissen wirklich ändert
- Vault ist Nachschlagewerk, kein Echtzeit-Spiegel
- Beim Slice-Abschluss: knapp halten. Details (Sub-A/B/C, exakte Commit-Liste, Migration-Skripte) bleiben in `CODE_REVIEW.md` und im git log

---

## Verwandte Notizen

- [[00 Start]] — Einstieg in den Vault
- [[Begriffe]] — Glossar zentraler Projektbegriffe
- [[Zielarchitektur]] — Stabiles Sollbild
- [[Migrationspfad]] — Aktueller Stand
