# KauthWorkflow — Wissensbasis

Dieser Vault ist die menschlich navigierbare Wissensbasis zum Projekt.
Er ergänzt die Repo-Docs (CLAUDE.md, TODO.md etc.) um stabiles Domänenwissen und Architekturkontext.

---

## Was ist das Projekt?

Eine interne Workflow-Plattform, die gerade von einem konfigurierbaren Employee-Lifecycle-Tool zu einem versionierten, plattformartigen System migriert wird.

Ausgangszustand: task-getriebener Onboarding/Offboarding-Kern, stark prozessart-spezifisch.
Zielzustand: generische, versionierte Workflow-Engine mit kontrollierten Automationen und Builder-Oberfläche.

---

## Navigation

### Architektur
- [[Zielarchitektur]] — Schichten, Prinzipien, Stabiles Sollbild
- [[Migrationspfad]] — Wo wir stehen, wohin wir gehen, in welcher Reihenfolge
- [[Entscheidungen]] — Langfristige Architektur- und Produktentscheidungen mit Begründung

### Domäne
- [[Workflow]] — Was ein Workflow ist, Instanzen, Tasks, Status
- [[Rotation]] — Abteilungsdurchlauf: Konzept, Datenmodell, Ablauf
- [[Identity]] — Entra, Personen, Rollen vs. Responsibilities
- [[Automation]] — Automation Layer: Actions, Jobs, Kontrolle

### Stand & Arbeit
- [[Code-Review-Status]] — Offene Punkte, was erledigt ist, was noch aussteht
- [[KI-Workflow]] — Wie Claude und Codex mit Vault und Repo arbeiten

---

## Wichtige Guardrails auf einen Blick

- Backend ist Source of Truth — keine Businesslogik im Frontend
- Rollen ≠ Responsibilities — niemals vermischen
- Workflow-Definition ≠ Task-Generierung — die Engine ist mehr als ein Task-Generator
- Versionierung ist Pflicht — laufende Instanzen dürfen nicht durch Admin-Änderungen brechen
- Onboarding ist nur ein Workflow — kein versteckter Produktkern
- Migration statt Big Bang — Altes und Neues parallel halten bis zur Parität

---

## Repo-Docs (Primärquellen für KI-Arbeit)

| Datei | Zweck |
|-------|-------|
| `DOCS_CONTROL.md` | Lesereihenfolge, Schreibziele, Pflegeregeln |
| `PROJECT_CONTEXT.md` | Stabile Projektwahrheit, fachliche Guardrails |
| `MEMORY.md` | Kurzfristiger Session-Kontext |
| `CODE_REVIEW.md` | Aktuelle Review + priorisierte Nacharbeit |
| `TODO.md` | Umsetzungssteuerung |
| `CODEX_SYNC.md` | Handoff-Protokoll zwischen Claude und Codex |
