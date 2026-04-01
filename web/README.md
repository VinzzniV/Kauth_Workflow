# Web-Frontend

Das Frontend ist die React/Vite-Oberflaeche fuer den Employee-Lifecycle-Workflow. Es steuert Login, Navigation, Workflow-Erstellung, Detailansichten, Aufgabenarbeit und die Admin-Workspaces.

## Einstiegspunkte

`src/main.tsx`
Startet React, Router, React Query, Auth-/CurrentUser-Provider, Theme, Toasts und Dialoge.

`src/App.tsx`
Verdrahtet Login-Zustand, App-Layout und alle geschuetzten Routen.

## Wichtige Ordner

`src/pages/`
Fachseiten wie Dashboard, Workflow-Liste, Workflow-Detail, Suche, Supervisor-Schritt, Aufgaben, Personenhistorie, Simulation-Login, Entra-Login und Administration.

`src/components/`
Wiederverwendbare UI-Bausteine. Wichtig sind vor allem:
- `components/layout`
- `components/workflows`
- `components/workflow-detail`
- `components/dashboard`
- `components/admin-config`
- `components/feedback`

`src/auth/`
Auth-Provider, Session-Wiederherstellung, aktueller Benutzer, Rollenmodell, MSAL/Entra-Integration und Auth-Modus-Abstraktion.

`src/navigation/`
Route-Schutz und rollenabhaengige Navigation.

`src/services/`
HTTP-Clients, fachliche API-Module und React-Query-Layer. Das Frontend ist in mehrere Service- und Query-Module aufgeteilt.

`src/theme/`
Theme-Aufloesung und ThemeProvider.

`src/types/`
Gemeinsame Typen fuer Auth, Workflows und Admin-Daten.

`src/config/`
Runtime-Config fuer `app-config.js` und lokale Vite-Variablen.

## Service-Schnitt

Wichtige Service-Bereiche:
- `authApi.ts` fuer Login, Session und aktueller Benutzer
- `workflowApi.ts`, `taskApi.ts`, `peopleApi.ts`, `lookupApi.ts` fuer Fachdaten
- `adminApi.ts` und `adminConfigApi.ts` fuer Admin-, Permission-, Directory- und Runtime-Konfiguration
- `services/api/*` fuer Backend-DTOs, Mappings und den Basis-Client
- `services/queries/*` und `services/mutations/*` fuer React Query

## Wo aendere ich was?

Neue oder geaenderte Seiten:
`src/pages/`

Login, Session oder Auth-Modus:
`src/auth/AuthContext.tsx`, `src/auth/IdentityProvider.ts`, `src/auth/EntraIdentityProvider.ts`

Rollen, Freigaben, Default-Routen:
`src/auth/roleModel.ts`

Navigation oder Route-Schutz:
`src/navigation/`

HTTP-Client, DTO-Mapping oder Endpunktvertraege:
`src/services/` und `src/services/api/`

React-Query-Queries oder Mutations:
`src/services/queries/` und `src/services/mutations/`

Admin-Workspaces:
`src/components/admin-config/` und `src/pages/AdminConfigPage.tsx`

Workflow-Detail, Audit-Log und Verknuepfungen:
`src/components/workflow-detail/`

## Entwicklung

Die normale lokale Entwicklung laeuft ueber:
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
VITE_ENTRA_REDIRECT_URI=https://onboarding-test.example.local
```

Wichtig:
- `dev-sim` zeigt keine kuenstlichen Demo-Benutzer mehr.
- Die Simulations-Login-Seite listet nur lokal synchronisierte Verzeichnisidentitaeten.
- Dafuer braucht die lokal gestartete API gueltige `ENTRA_*`-Variablen und einen erfolgreichen Directory-Sync.
- Im deployten Web-Container sind nur `dev-sim` und `entra` als gueltige Auth-Modi erlaubt.
- Wenn `app-config.js` `authMode=entra` setzt, muessen `entraClientId`, `entraTenantId`, `entraAudience` und `entraRedirectUri` explizit gesetzt sein. Ein stiller Fallback auf alte Demo- oder Redirect-Defaults findet nicht mehr statt.

Fuer den kompletten Ablauf siehe [`../SETUP.md`](../SETUP.md).

## Befehle

```bash
npm install
npm run dev
npm run build
npm run lint
npm run test
```
