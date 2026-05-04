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
- `components/layout` — PageHeader, Shell-nahe Bausteine und gemeinsame Arbeitsmodus-Schalter wie `ViewModeToggle`
- `components/workflows`
- `components/workflow-detail`
- `components/dashboard`
- `components/admin-config`
- `components/feedback`
- `components/rotation` — Stations-Timeline (`RotationStationTimeline`), Kalenderansicht, Stationsformular, Audit-Log und Benachrichtigungshistorie für Rotation-Detailseiten

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
- Seit T8 ist der Builder produktnäher geschnitten: eigene Hauptnavigation, canvas-first Seite mit mehr Arbeitsflaeche, kompakterem Kontext-Rail und Rollenmodus fuer `Bearbeitungsmodus` vs. `Admin-Modus`.
- Seit T9 trennt der Builder technische Begriffe und sichtbare UI-Sprache sauberer: sichtbare Builder-Texte sprechen jetzt staerker von `Ablauf`, `Ablaufvorlage`, `Entwurf`, `Bausteinen` und `Eigenschaften`, waehrend code-nahe Felder ohne Umlaute und mit stabilen technischen Labels im erweiterten Bereich bleiben.
- Die Canvas-Karten lesen sich jetzt staerker wie Prozessbausteine statt wie Datensaetze: sichtbar sind vor allem Titel, Typ, manuell/automatisch, Zustaendigkeit, optionale Benachrichtigte/Frist, Wirkung und der naechste Schritt. Dafuer nutzt der Builder zusaetzliche Frontend-Lookups auf Process Types, Task Templates und Responsibility-Owner, ohne den Backend-Vertrag fuer Workflow-Definitionen zu aendern.
- Der Builder nutzt jetzt ein strukturiertes Top-to-Bottom-DAG-Layout statt eines links-nach-rechts Bandes. Verzweigungen und Zusammenfuehrungen werden dabei mit sichtbaren Junctions und balancierten Branch-Lanes dargestellt, damit Parallelitaet und Reconvergence wie in einem Flussdiagramm lesbar bleiben.
- Fuer echte Parallelitaet kennt der Definition Layer jetzt zusaetzlich die Gateway-Typen `parallel_split` und `parallel_join`. Der Builder rendert diese als explizite Strukturpunkte; die Runtime aktiviert bei `parallel_split` mehrere Folgepfade und wartet bei `parallel_join` auf alle eingehenden Teilpfade.
- Lifecycle-Workflows koennen jetzt zusaetzlich einen fachlichen `setup`-Baustein nutzen. In dieser Darstellung bleibt der Hauptfluss auf Phasen wie `Start`, `Anforderungen erfassen`, optionale `Freigabe`, `Setup` und `Abschluss` begrenzt; interne Boolean-Entscheidungen werden nicht mehr als eigene Hauptknoten gezeigt.
- `setup`-Karten gruppieren Fachbereichsaufgaben als kompakte Unterelemente, zum Beispiel `IT: AD, Mail, Hardware` oder `QS: ...`, und ziehen diese rein aus Process-Type-/Template-/Responsibility-Lookups. Die eigentliche Runtime-Logik bleibt dabei unveraendert im Backend.
- Die sichtbare Builder-Sidebar blendet technische Bedingungsfelder fuer Hauptpfade jetzt aus; `answerKey`, Operatoren und Rohbedingungen sind keine sichtbare Standard-Bearbeitungsflaeche mehr.
- Basis-Builder-Zugriff haengt jetzt an der bestehenden `workflowCreate`-Faehigkeit; Anlage von Ablaufvorlagen und Staenden, Aktionskatalog und Freigabe bleiben bewusst auf den `Admin-Modus` beschraenkt.
- Die tieferen Spezialeditoren fuer Bedingungen und Actions bleiben vorerst bewusst schlank und teilweise JSON-basiert; die weitere Produktisierung folgt in den naechsten Builder-Phasen.
- Seit T11 nutzt `/workflows/create` startbare Workflow-Definitionen aus dem Definition Layer statt `process_types`; die alten Admin-Sektionen fuer Process Types, Templates und Answer Defaults sind im sichtbaren Workspace ausgeblendet.
- Seit dem mitarbeiterzentrierten Lifecycle-Schnitt legt `/workflows/create` bei neuen Onboardings zuerst einen kanonischen Mitarbeiter per `POST /people` an und startet danach den Workflow mit `targetPersonId`; bestehende Lifecycle-Prozesse suchen ihre Zielperson ueber `GET /people/search` statt ueber abgeschlossene Onboardings.
- Die Workflow-Detailansicht zeigt parallel laufende Bereiche jetzt klarer ueber Pflichtfortschritt, sichtbare `blocked`-Status und konkrete aktuelle Fachbereiche statt generischer Parallel-Hinweise.
- Operative Listen koennen seit FE-28 zwischen Karten- und Tabellenmodus wechseln. `WorkflowList`, `MyTasks`, `RotationOperations` und `PersonWorkflowHistory` nutzen dafuer `components/layout/ViewModeToggle`; die Tabellen sortieren clientseitig die aktuelle Sicht und fallen mobil auf Karten-/Listenansichten zurueck.
- Seit FE-29 nutzen `MyTasks` und `WorkflowList` Split-Workspaces: die Liste bleibt links erhalten, waehrend Aufgabenbearbeitung bzw. Vorgangs-Vorschau rechts im Kontext angezeigt werden.
- Seit FE-30 (Slice 1) ist der Workflow Builder canvas-first: Sektion 2 stellt das Topologie-Diagramm gross neben einem rechten Eigenschaften-Panel dar, das beim Anklicken eines Schritts oder Übergangs den passenden Editor zeigt (Schritt-Karte bzw. Edge-Editor mit Quelle/Ziel/Reihenfolge/Bedingung). Die lange Listenansicht aller Schritte und die Tabelle aller Übergänge bleiben weiterhin verfügbar, sind jetzt aber als zugeklappte `details`-Bereiche sekundär.
- Seit FE-30 (Slice 2) werden lokale und Backend-Validation-Issues direkt am betroffenen Objekt sichtbar: Schritt-Knoten im Topologie-Diagramm bekommen einen Issue-Marker mit Anzahl, betroffene Übergänge werden gestrichelt rot dargestellt, in der sekundären Schritt-Liste und Übergangs-Tabelle erscheinen Severity-Badges, und das rechte Eigenschaften-Panel zeigt die zum aktuell selektierten Objekt gehörenden Issues kompakt oben. Lokale Validierung wird jetzt live aus dem Draft abgeleitet (kein expliziter `Lokal prüfen`-Klick mehr nötig); die globale Validierungssektion bleibt als Gesamtüberblick und vermerkt, wie viele Issues bereits am Objekt markiert sind.
- Seit FE-30 (Slice 3) lassen sich Übergänge direkt im Topologie-Diagramm erzeugen: am rechten Rand jedes Schritts mit Schritt-Key erscheint im Hover ein „+"-Anker, der den Connect-Mode startet. Im Connect-Mode legt ein Klick auf einen Zielschritt über den bestehenden `addEdge`-Pfad eine neue Verbindung an; das frisch angelegte Edge wird im Eigenschaften-Panel selektiert. Esc, ein Klick auf den Canvas-Hintergrund oder ein erneuter Klick auf den Quell-Anker brechen ab. Backend-Vertrag bleibt unverändert (Replace-Flow inkl. Priorisierung beim Save).

Wichtig:
- keine freie technische Automationskonfiguration im UI
- keine Business-Regeln duplizieren
- keine onboarding-spezifischen Kernannahmen weiter zementieren

## Service-Schnitt

Wichtige Service-Bereiche:
- `authApi.ts` fuer Login, Session und aktuellen Benutzer
- `workflowApi.ts`, `taskApi.ts`, `peopleApi.ts`, `lookupApi.ts` fuer Fachdaten
- `rotationApi.ts` und `services/queries/rotationQueries.ts` fuer HR-Planung und Uebersicht des Rotations-/Durchlauf-Slices; die Personenauswahl fuer neue Durchlaeufe kommt aus `/people/rotation-eligible`
- `adminApi.ts` und `adminConfigApi.ts` fuer Administration und Konfiguration
- `adminApi.ts` enthaelt seit T13 zusaetzlich die Admin-Endpunkte fuer Mail-Vorlagen, Preview-Zielsuche und read-only Preview-Rendering
- `systemLogReporter.ts` fuer dedupliziertes Client-Error-Reporting an `/client/log-events`
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

`AdminConfigPage` komponiert 7 Domain-Hooks zu 7 Domain-Bundles (`meta`, `user`, `organization`, `access`, `directory`, `notification`, `system`) und reicht diese an `AdminConfigWorkspaceContent` durch. Jede `renderXWorkspace`-Funktion in `AdminConfigWorkspaceSections.tsx` extrahiert nur das eigene Bundle — kein flacher 100-Prop-Spread. Bundle-Typen liegen in `src/components/admin-config/adminConfigWorkspaceContentTypes.ts`.

Wichtiger aktueller Admin-Slice:
- `Administration > System` ist die zentrale Betriebs- und Log-Konsole mit `src/components/admin-config/AdminSystemLogSection.tsx`
- `Administration > System > Mail-Vorlagen` nutzt `src/components/admin-config/AdminNotificationTemplateSection.tsx` und `src/hooks/useAdminNotificationTemplates.ts` fuer Betreff/Text, Trigger-Hinweise, echte Preview-Zielsuche und read-only Mail-Preview
- `requestJson` meldet fehlgeschlagene API- und Transportfehler automatisch an den Backend-Log-Ingest
- lokale sichtbare Fehler aus Admin-Seiten, Builder und Toasts werden zusaetzlich ueber `src/services/systemLogReporter.ts` erfasst
- der fruehere Admin-Bereich `Massenaktionen` wurde entfernt

Builder-Zustand und Draft-Modell:
`src/hooks/useAdminWorkflowBuilder.ts` und `src/hooks/adminWorkflowBuilderModel.ts`

Workflow-Detail, Audit-Log und Aufgabenansichten:
`src/components/workflow-detail/`

Rotation-/Durchlaufplanung fuer HR:
`src/pages/RotationPlanningPage.tsx`, `src/pages/RotationPlanDetailPage.tsx`, `src/services/rotationApi.ts`, `src/services/queries/rotationQueries.ts` und `src/types/rotation.ts`
`/rotation` ist die Uebersicht ueber bestehende Durchlaufplaene und deren Stände; die eigentliche Anlage eines neuen Abteilungsdurchlaufs startet fuer HR ueber `Neuer Vorgang` und den Einstieg nach `/rotation?mode=create`.

Rotation-/Durchlaufoperationen fuer IT und Fachbereiche:
`src/pages/RotationOperationsPage.tsx`, `src/pages/RotationTaskDetailPage.tsx`, `src/services/taskApi.ts`, `src/services/mutations/workflowMutations.ts`, `src/utils/taskStatus.ts` und die taskRef-faehigen Task-Envelope-Mappings in `src/services/api/`

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
