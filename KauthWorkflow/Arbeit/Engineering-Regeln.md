# Engineering-Regeln

#arbeit #regeln #stabil

Technische Arbeitsregeln für inkrementelle, migrationssichere Umsetzung.
Primärquelle im Repo war: `ENGINEERING_RULES.md` (in Vault migriert)

---

## Kernprinzipien

- Backend bleibt Source of Truth
- Keine Business-Logik-Drift ins Frontend
- Rollen und Responsibilities bleiben getrennt
- `assignment_type` bleibt strikt
- Workflow-Definition ist nicht gleich Task-Generierung
- Migration erfolgt inkrementell, nicht als Big Bang

---

## Änderungsregeln

- Nur ändern, was für den aktuellen Schritt erforderlich ist
- Keine unstrukturierten Nebenbei-Refactors
- Unsicherheit offen benennen statt raten
- Verhalten nicht stillschweigend ändern
- Alte und neue Architektur zunächst parallel halten

---

## Architekturregeln

- Keine neue onboarding-spezifische Kernlogik ergänzen
- Neue Prozessarten nicht weiter primär per Seed-SQL aufbauen
- Keine freien technischen Actions für Admins einführen
- Versionierung und Validierung vor Komfort-Features priorisieren
- Bei Kernumbauten erst Modell und Migrationsschnitt klären, dann implementieren

---

## Dokumentationsregeln

- Vor größeren Änderungen `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md` und `MEMORY.md` lesen
- Bei Rotations-/Durchlauf-Arbeit und Review-Nacharbeit immer auch `CODE_REVIEW.md` und `TODO.md` lesen
- Relevante Dokus im selben Arbeitsgang aktualisieren
- Nach dem Lesen aktiv festhalten, welche Dokus bei dieser Aufgabe potenziell mitgezogen werden müssen
- Stabile Wahrheit in `PROJECT_CONTEXT.md` oder Vault-Architektur-Dateien, nicht in ad-hoc Notizen
- Temporäre Findings in `MEMORY.md`, nicht in Langzeitdokus

---

## Arbeitsmodus

Für Datenmodell, Migration, Runtime und mehrschichtige Änderungen zuerst klein schneiden und in prüfbare Inkremente zerlegen.

Reihenfolge bevorzugen:
1. Datenmodell / Migration
2. Domainmodell / Repository
3. Service / Runtime
4. API
5. UI
6. Tests
7. Doku

---

## Dateigrößen

- Frontend-Komponenten > 400–500 Zeilen prüfen und bei Bedarf schneiden
- Backend-Klassen > 500–700 Zeilen prüfen und bei Bedarf schneiden
- Nie neue Logik in ohnehin überladene Dateien kippen, ohne Split zu bewerten

---

## API-Regeln

- Keine In-Memory-Filterung/Paginierung für produktive Datenpfade
- Ein Endpoint = eine klare Response-Form
- Fehler- und Validierungsverhalten konsistent halten

---

## Frontend-Regeln

- Keine doppelte Business-Logik
- Keine Builder- oder Admin-UI mit freier technischer Ausführung
- Legacy- und neue Runtime-Flows nur dort parallel zeigen, wo die Migration es erfordert

---

## Sicherheitsregeln

- Keine offenen Demo-Endpunkte ohne expliziten Dev-Guard
- Keine Secrets im Klartext in Repo, Artefakten oder DB als Standardmodell
- Alle Node-Configs und Action-Parameter validieren

---

## Handoff-Regeln

Folgendes darf nicht ins Handoff-ZIP oder ins Repo eingecheckt werden:
- `node_modules`
- `dist`
- `bin` / `obj`
- `.git`
- `.env.prod`
- `web/.env.local`

---

## Verwandte Notizen

- [[Entscheidungen]] — Warum diese Regeln gelten
- [[KI-Workflow]] — Wie KI diese Regeln anwendet
