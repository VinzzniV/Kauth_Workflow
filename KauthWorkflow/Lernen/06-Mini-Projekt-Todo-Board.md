# Einheit 6 — Mini-Projekt ausserhalb von `kauth_workflow`

Das eigentliche Uebungsprojekt liegt bewusst **ausserhalb** des Hauptprojekts:

`C:\Users\vinzent.niederwieser\kauth_workflow_mini_practice\todo-practice`

## Warum dieses Projekt?

Du willst nicht erst abstrakt lernen, sondern dieselbe Architektur noch einmal in klein nachbauen:

```text
Frontend -> Endpoint -> Service -> Repository -> Datenbank
```

Genau das macht dieses Mini-Projekt.

## Was ist drin?

- kleines React-Frontend
- ein API-Endpoint-File mit `GET` und `POST`
- ein Service
- ein Repository
- eine PostgreSQL-Tabelle `todo_items`

## Wichtige Lernidee

Nicht nur "zum Laufen bringen", sondern jedes Teil bewusst verstehen:

1. Was nimmt der Endpoint entgegen?
2. Welche Regel prueft der Service?
3. Welches SQL fuehrt das Repository aus?
4. Wie kommt das JSON im Frontend an?

## Empfohlene Reihenfolge

1. `README.md` im Mini-Projekt lesen
2. API starten
3. Frontend starten
4. Ein Todo anlegen
5. Danach `TodoEndpoints.cs`, `TodoService.cs`, `PostgresTodoRepository.cs` lesen
6. Erst dann das Frontend lesen

## Erste aktive Uebung

Baue selbst einen neuen Flow:

`Todo als erledigt markieren`

Dafuer brauchst du:

- neuen Endpoint
- neue Service-Methode
- neues SQL im Repository
- neuen Button im Frontend

Das ist klein genug fuer einen Lernschritt und nah genug an der echten Projektarchitektur.
