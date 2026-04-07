# Web-Frontend

Das Frontend ist die React/Vite-Oberflaeche der internen Workflow-Plattform.
Aktuell bedient es noch stark den bestehenden lifecycle-/task-getriebenen Kern und wird schrittweise auf Definition Layer, neue Runtime und spaeter den Guided Builder ausgerichtet.

## Einstiegspunkte

`src/main.tsx`
Startet React, Router, React Query, Auth-/CurrentUser-Provider, Theme, Toasts und Dialoge.

`src/App.tsx`
Verdrahtet Login-Zustand, Layout und geschuetzte Routen.

## Wichtige Ordner

`src/pages/`
Fachseiten fuer Dashboard, Workflow-Liste, Suche, Erstellung, Details, Aufgaben, Login und Administration.

`src/components/`
Wiederverwendbare UI-Bausteine, besonders:
- `components/layout`
- `components/workflows`
- `components/workflow-detail`
- `components/dashboard`
- `components/admin-config`
- `components/feedback`

`src/auth/`
Auth-Provider, Session-Wiederherstellung, aktueller Benutzer, Rollenmodell, MSAL/Entra-Integration.

`src/navigation/`
Route-Schutz und rollenabhaengige Navigation.

`src/services/`
HTTP-Client, fachliche API-Module und React-Query-Layer.

`src/config/`
Runtime-Config aus `app-config.js` oder lokalen Vite-Variablen.

## Frontend-Richtung

Das Frontend soll sich in diese Richtung entwickeln:
- Admin-UI fuer versionierte Workflow-Definitionen statt nur Prozessarten-/Template-Pflege
- Runtime-Ansichten, die alte und neue Workflow-Instanzen voruebergehend parallel darstellen koennen
- spaeter Guided Builder fuer Nodes, Edges, Node-Konfiguration und Veroeffentlichung

Wichtig:
- keine freie technische Automationskonfiguration im UI
- keine Business-Regeln duplizieren
- keine onboarding-spezifischen Kernannahmen weiter zementieren

## Service-Schnitt

Wichtige Service-Bereiche:
- `authApi.ts` fuer Login, Session und aktuellen Benutzer
- `workflowApi.ts`, `taskApi.ts`, `peopleApi.ts`, `lookupApi.ts` fuer Fachdaten
- `adminApi.ts` und `adminConfigApi.ts` fuer Administration und Konfiguration
- `services/api/*` fuer DTOs, Mapping und Basis-Client
- `services/queries/*` und `services/mutations/*` fuer React Query

## Wo aendere ich was?

Neue oder geaenderte Seiten:
`src/pages/`

Login, Session oder Auth-Modus:
`src/auth/`

Navigation oder Route-Schutz:
`src/navigation/`

HTTP-Client, DTO-Mapping oder Endpunktvertraege:
`src/services/` und `src/services/api/`

Admin-Workspaces und spaeter Builder-nahe Pflege:
`src/components/admin-config/` und `src/pages/AdminConfigPage.tsx`

Workflow-Detail, Audit-Log und Aufgabenansichten:
`src/components/workflow-detail/`

## Entwicklung

Normale lokale Entwicklung:
- lokale DB per Docker
- API lokal per `dotnet run`
- Frontend lokal per Vite

`web/.env.local`:

```env
VITE_API_PROXY_TARGET=http://127.0.0.1:5001
VITE_AUTH_MODE=dev-sim
```

Optionale lokale Entra-Tests:

```env
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=
VITE_ENTRA_TENANT_ID=
VITE_ENTRA_AUDIENCE=api://00000000-0000-0000-0000-000000000000
VITE_ENTRA_REDIRECT_URI=https://workflow-test.example.local
```

Wichtig:
- `dev-sim` zeigt keine kuenstlichen Demo-Benutzer.
- Die Simulations-Login-Seite basiert auf synchronisierten Verzeichnisidentitaeten.
- Bei `authMode=entra` muessen alle Entra-Runtime-Werte explizit gesetzt sein.

## Befehle

```bash
npm install
npm run dev
npm run build
npm run lint
npm run test
```
