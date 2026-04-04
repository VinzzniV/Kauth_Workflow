# OnBoarding – AI-Ready TODO for Production Readiness

## Ziel
Dieses Dokument ist für KI-gestützte Umsetzung gedacht. Es zerlegt die wichtigsten Go-Live-Risiken in konkrete, ausreichend kleine Aufgabenpakete. Fokus: **Produktionsreife**, nicht kosmetische Optimierung.

## Status-Snapshot (Stand 2026-04-02)

Diese Einordnung basiert auf dem aktuellen Repo-Stand.

Bereits erledigt:
- Produktions-Deployment von Dev entkoppeln
- Seed-/Demo-Daten in Production technisch ausschließen
- P0.3 bis P0.7
- P1.1 bis P1.4
- P2.1 Frontend-State-Management entschlackt
- P2.2 Frontend-Teststrategie an echte Modulgrenzen angepasst
- P2.3 Manager-/Supervisor-Dashboard an serverseitige Supervisor-Zuordnung angepasst
- P2.4 Verbleibende große Frontend-Orchestrierung entlang fachlicher Verantwortlichkeiten zerlegt
- P3.1 Route-basiertes Code-Splitting im Frontend eingeführt
- P3.2 Backend-Service-/Repository-Grenzen geschärft
- P3.3 Observability-/Logging-Härtung umgesetzt

## Wichtig für die KIs
- Das Projekt ist **fachlich schon stark**, aber **noch nicht produktionsreif**.
- Größte Risiken liegen aktuell nicht mehr in den zuvor identifizierten P0-P3-Arbeitspaketen, sondern eher in künftigen neuen Produktanforderungen oder zusätzlicher Optimierung ausserhalb dieses Backlogs.
- Änderungen sollen **nicht blind umsetzen**, sondern immer auf Auswirkungen auf Dev, Demo und Production prüfen.
- Bevor größere Umbauten passieren, soll die KI vorhandene Dateien, Doku und Compose-/Env-Struktur vollständig lesen.
- Keine Pseudo-Fixes. Wenn Architekturproblem erkannt wird, lieber sauber umstellen statt nur Symptome patchen.

---

## Empfohlene KI-Nutzung

### Codex – bevorzugt für
- konkrete Implementierung
- kleine bis mittlere Refactorings
- Testfixes
- Lint-Fixes
- Compose-/Config-Anpassungen
- CI-Dateien und Skripte

### Claude – bevorzugt für
- Architekturentscheidungen
- Review von Security-/Deployment-Konzepten
- Zerlegung großer Baustellen
- Risikoanalyse vor Refactors
- Review von API-/Frontend-Vertragsgrenzen

### Reasoning-Empfehlung
- **low**: klar lokalisierte, mechanische Änderungen
- **medium**: mehrere Dateien, aber klares Zielbild
- **high**: Refactoring mit Seiteneffekten oder fachlichen Verträgen
- **very high**: Architektur, Security, Deployment, systemweite Änderungen

---

## Backlog-Status

Der aktuell dokumentierte Backlog aus diesem Dokument ist abgearbeitet. Neue Punkte sollten nur ergänzt werden, wenn ein neuer Produktivitäts-, Betriebs- oder Architekturbedarf konkret beschrieben ist.
