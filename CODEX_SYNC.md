# CODEX_SYNC.md

Dieses Dokument ist das Handoff-Protokoll zwischen Codex und Claude.

## Zweck

- Codex traegt hier nach jeder abgeschlossenen Aufgabe einen Eintrag ein.
- Claude liest diese Datei am Anfang jeder Session, um sich ueber Codex-Aenderungen zu informieren.
- So entsteht kein blinder Fleck, wenn beide KIs am selben Repo arbeiten.

## Regeln

- Eintrag pro abgeschlossener Aufgabe (nicht pro Commit).
- Format: Datum | Aufgaben-ID | betroffene Dateien | kurze Zusammenfassung | offene Risiken.
- Wenn eine Aufgabe nur teilweise umgesetzt wurde, Eintrag mit Status `PARTIAL` und Erklaerung.
- Claude prueft bei jedem Sync-Eintrag, ob die Aenderung die eigene Arbeit beeinflusst.
- Veraltete Eintraege (Task `done` in TODO.md) koennen entfernt werden — Detail-Historie bleibt im git log.

---

## Sync-Log

| Datum | Aufgabe | Status | Betroffene Dateien | Zusammenfassung | Offene Risiken |
| --- | --- | --- | --- | --- | --- |

_Keine offenen Sync-Eintraege. Done-Eintraege bis 2026-05-03 entfernt — siehe `git log` fuer Detail-Historie._
