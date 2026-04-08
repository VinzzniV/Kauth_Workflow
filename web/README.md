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

Aktueller Stand:
- Seit T8 lebt der Workflow Builder auf der eigenen Route `/builder` statt im Admin-Settings-Workspace.
- Seit T1 ist der Builder canvas-first: Nodes und Edges werden im zentralen React-Flow-Canvas visualisiert, Node-Positionen werden ueber `position_x` / `position_y` persistiert und alte Versionen ohne Koordinaten bekommen im Frontend ein deterministisches Fallback-Layout.
- Seit T2 lassen sich Edges im Canvas direkt verbinden, auswaehlen und loeschen; der Persistenzpfad bleibt der bestehende Version-Replace-Flow ohne Fake-Edges.
- Seit T3 werden Nodes im Canvas als Builder-Cards statt als technische Datensaetze gerendert; Typ, Verhalten, Konfigurationsstatus und Flow-Kontext stehen jetzt vor `nodeKey`/`sortOrder`.
- Seit T4 oeffnet ein Klick auf einen Node rechts eine echte Builder-Sidebar fuer Basisdaten, typbezogene Konfiguration, ausgehende Weiterleitung und sichtbare Actions; Klick ins Leere schliesst diese Sidebar wieder.
- Seit T5 gibt es oberhalb des Canvas eine echte Builder-Toolbar fuer `Node hinzufuegen`, `Node loeschen`, `Validieren` und `Publish`; lokale Validierung kann jetzt explizit ohne Speichern ausgefuehrt werden.
- Seit T6 nutzt der Builder den bestehenden Backend-Action-Katalog als sichtbaren Action Layer: `automation`-Nodes zeigen verfuegbare kontrollierte Actions, erlauben nur katalogbasiertes Hinzufuegen und rendern aufgeloeste Action-Namen statt roher Keys.
- Seit T7 ist der Builder im sichtbaren UI weiter enttechnisiert: Definition/Version werden klarer als Ablaufstufen gezeigt, Button-Hierarchien sind strenger getrennt und technische Felder wurden weiter in sekundaere Bereiche verschoben.
- Seit T8 ist der Builder produktnäher geschnitten: eigene Hauptnavigation, canvas-first Seite mit mehr Arbeitsflaeche, kompakterem Kontext-Rail und Rollenmodus fuer `Builder` vs. `Admin Builder`.
- Seit T9 trennt der Builder technische Begriffe und sichtbare UI-Sprache sauberer: code-nahe Felder und Validierungsnachrichten bleiben ohne Umlaute und mit stabilen technischen Labels, waehrend Node-Typen und Builder-Hinweise weiter als Produkttexte erscheinen.
- Basis-Builder-Zugriff haengt jetzt an der bestehenden `workflowCreate`-Faehigkeit; Definition-/Versionsanlage, Action-Katalog und Publish bleiben bewusst auf den `Admin Builder` beschraenkt.
- Die tieferen Spezialeditoren fuer Bedingungen und Actions bleiben vorerst bewusst schlank und teilweise JSON-basiert; die weitere Produktisierung folgt in den naechsten Builder-Phasen.
- Seit T11 nutzt `/workflows/create` startbare Workflow-Definitionen aus dem Definition Layer statt `process_types`; die alten Admin-Sektionen fuer Process Types, Templates und Answer Defaults sind im sichtbaren Workspace ausgeblendet.

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

Admin-Workspaces und Builder-nahe Pflege:
`src/components/admin-config/`, `src/pages/AdminConfigPage.tsx` und `src/pages/WorkflowBuilderPage.tsx`

Builder-Zustand und Draft-Modell:
`src/hooks/useAdminWorkflowBuilder.ts` und `src/hooks/adminWorkflowBuilderModel.ts`

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
