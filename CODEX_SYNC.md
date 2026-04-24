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
- Claude prueft bei jedem Sync-Eintrag, ob die Aenderung die eigene Arbeit (CLA-*) beeinflusst.
- Veraltete Eintraege (aelter als 4 Wochen, Task `done` in TODO.md) koennen entfernt werden.

---

## Sync-Log

| Datum | Aufgabe | Status | Betroffene Dateien | Zusammenfassung | Offene Risiken |
|-------|---------|--------|--------------------|-----------------|----------------|
| 2026-04-24 | OPS-VM-NODE-GUARD | done | `scripts/start-vm.sh`, `web/package.json`, `KauthWorkflow/Betrieb/Setup.md` | Node-Version-Guard fuer den Linux-Dev-Start eingebaut und Frontend auf `node >= 20.19.0` dokumentiert, damit Vite-Starts auf alten VM-Setups frueh und klar scheitern. | Auf bestehenden VMs mit Node 18 muss Node vor dem naechsten Dev-Start manuell aktualisiert werden. |
| 2026-04-24 | OPS-VM-START | done | `.gitignore`, `scripts/start-vm.sh`, `PROJECT_STRUCTURE.md`, `KauthWorkflow/Betrieb/Setup.md`, `KauthWorkflow/Betrieb/Deployment-Checkliste.md` | Linux-VM-Startscript fuer `dev` und `prod` inkl. `status/logs/stop/restart` hinzugefuegt und Betriebsdoku auf den neuen Startpfad umgestellt. | `dev` auf der VM setzt neben Docker auch `dotnet` und `npm` voraus; bei externer Nutzung sollte `DEV_PUBLIC_BASE_URL` bewusst gesetzt werden. |
| — | — | — | — | Kein aktiver Zyklus — Review 2026-04-23 vollstaendig abgeschlossen (COD-1..6, CLA-1..4) | — |

---

## Hinweis fuer Codex

Nach jeder Aufgabe:
1. Eintrag in die Tabelle oben einfuegen.
2. Status in `TODO.md` auf `[done]` setzen.
3. Wenn sich durch die Aenderung Annahmen aus `CODE_REVIEW.md` aendern, dort den betroffenen Abschnitt aktualisieren.

## Hinweis fuer Claude

Am Sitzungsanfang:
1. `CODEX_SYNC.md` lesen.
2. Pruefen ob neue `PARTIAL`- oder `done`-Eintraege vorliegen, die CLA-*-Aufgaben beeinflussen.
3. Wenn ja: betroffene Dateien lesen bevor eigene Arbeit beginnt.
