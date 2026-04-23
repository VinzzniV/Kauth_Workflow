# ENGINEERING_RULES.md

## Kernprinzipien

- Backend bleibt Source of Truth
- Keine Business-Logik-Drift ins Frontend
- Rollen und Responsibilities bleiben getrennt
- `assignment_type` bleibt strikt
- Workflow-Definition ist nicht gleich Task-Generierung
- Migration erfolgt inkrementell, nicht als Big Bang

---

## Aenderungsregeln

- Nur aendern, was fuer den aktuellen Schritt erforderlich ist
- Keine unstrukturierten Nebenbei-Refactors
- Unsicherheit offen benennen statt raten
- Verhalten nicht stillschweigend aendern
- Alte und neue Architektur zunaechst parallel halten

---

## Architekturregeln

- Keine neue onboarding-spezifische Kernlogik ergaenzen
- Neue Prozessarten nicht weiter primaer per Seed-SQL aufbauen
- Keine freien technischen Actions fuer Admins einfuehren
- Versionierung und Validierung vor Komfort-Features priorisieren
- Bei Kernumbauten erst Modell und Migrationsschnitt klaeren, dann implementieren

---

## Dokumentationsregeln

- Vor groesseren Aenderungen `DOCS_CONTROL.md`, `PROJECT_CONTEXT.md` und `MEMORY.md` lesen
- Bei Rotations-/Durchlauf-Arbeit und Review-Nacharbeit immer auch `CODE_REVIEW.md` und `TODO.md` lesen
- Relevante Dokus im selben Arbeitsgang aktualisieren
- Nach dem Lesen aktiv festhalten, welche Dokus bei dieser Aufgabe potenziell mitgezogen werden muessen
- Stabile Wahrheit in `PROJECT_CONTEXT.md` oder `DECISIONS.md`, nicht in ad-hoc Notizen
- Temporare Findings in `MEMORY.md`, nicht in Langzeitdokus

---

## Arbeitsmodus

- Fuer Datenmodell, Migration, Runtime und mehrschichtige Aenderungen zuerst klein schneiden und in pruefbare Inkremente zerlegen
- Reihenfolge bevorzugen:
  1. Datenmodell / Migration
  2. Domainmodell / Repository
  3. Service / Runtime
  4. API
  5. UI
  6. Tests
  7. Doku

---

## Dateigroessen

Grosse Dateien vermeiden:
- Frontend-Komponenten > 400-500 Zeilen pruefen und bei Bedarf schneiden
- Backend-Klassen > 500-700 Zeilen pruefen und bei Bedarf schneiden

Nie:
- neue Logik in ohnehin ueberladene Dateien kippen, ohne Split zu bewerten

---

## API-Regeln

- Keine In-Memory-Filterung/Paginierung fuer produktive Datenpfade
- Ein Endpoint = eine klare Response-Form
- Fehler- und Validierungsverhalten konsistent halten

---

## Frontend-Regeln

- Keine doppelte Business-Logik
- Keine Builder- oder Admin-UI mit freier technischer Ausfuehrung
- Legacy- und neue Runtime-Flows nur dort parallel zeigen, wo die Migration es erfordert

---

## Sicherheitsregeln

- Keine offenen Demo-Endpunkte ohne expliziten Dev-Guard
- Keine Secrets im Klartext in Repo, Artefakten oder DB als Standardmodell
- Alle Node-Configs und Action-Parameter validieren

---

## Handoff-Regeln

- Kein `node_modules`
- Kein `dist`
- Kein `bin` / `obj`
- Kein `.git`
- Artefakte sind nie Source of Truth
