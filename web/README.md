# Web-Frontend

Das Frontend bildet die Oberflaeche fuer Mitarbeiterprozesse. Hier liegen Seiten, Rollenlogik, Navigation und der zentrale Zugriff auf die Backend-Endpunkte.

## Wichtige Ordner

`src/pages/`  
Komplette Fachseiten wie Dashboard, Workflow-Uebersicht, Workflow-Detail, Aufgabenliste und Admin-Konfiguration.

`src/components/`  
Wiederverwendbare UI-Bausteine. Besonders wichtig sind `components/dashboard`, `components/layout` und `components/workflows`.

`src/auth/`  
Auth- und Rollenlogik des Frontends. Hier wird gesteuert, wer eingeloggt ist und welche Bereiche sichtbar sind.

`src/navigation/`  
Routing-Helfer und Guards. Relevant, wenn sich Sichtbarkeit oder Menuefuehrung aendert.

`src/services/`  
Zentraler API-Zugriff. Aenderungen an Backend-Endpunkten oder Request-Handling passieren in `lifecycleApi.ts`.

`src/types/`  
Gemeinsame Typen fuer Workflow-, Aufgaben- und Auth-Daten.

## Wo aendere ich was?

Neue oder geaenderte Seiten:
`src/pages/`

Rollen, Freigaben, Standardrouten:
`src/auth/roleModel.ts`

Login- oder Session-Verhalten:
`src/auth/AuthContext.tsx`

Seitennavigation oder Route-Schutz:
`src/navigation/`

Backend-Endpunkte, DTO-Mapping, Fetch-Logik:
`src/services/lifecycleApi.ts`

Workflow-Eingaben, Aufgabenanzeige, Dashboard-UI:
`src/components/workflows/` und `src/components/dashboard/`

## Einstiegspunkte

`src/main.tsx`  
Startet React, Router und die globalen Provider.

`src/App.tsx`  
Verdrahtet Auth-Status, App-Layout und die geschuetzten Routen.

## Entwicklung

API-Basis-URL:
`VITE_API_BASE` in `web/.env.local`

Beispiel:

```env
VITE_API_BASE=http://localhost:5001
```

Es gibt dafuer eine Vorlage in [`web/.env.example`](./.env.example).

Auth-Hinweis:
- Das Frontend verwendet aktuell bewusst Demo-Login gegen die API.
- Diese Demo-Auth ist fuer Demo/Dev gedacht und kein Produktionsmodell.
- Die Trennung der Umgebungen ist in [`../ENVIRONMENTS.md`](../ENVIRONMENTS.md) beschrieben.
- Fuer lokale Entwicklung wird `VITE_API_BASE` ueber `web/.env.local` gesetzt; produktive Werte gehoeren nicht in eingecheckte Dateien.

Wichtige Befehle:

```bash
npm install
npm run dev
npm run build
```
