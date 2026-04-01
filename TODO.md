# OnBoarding – AI-Ready TODO for Production Readiness

## Ziel
Dieses Dokument ist für KI-gestützte Umsetzung gedacht. Es zerlegt die wichtigsten Go-Live-Risiken in konkrete, ausreichend kleine Aufgabenpakete. Fokus: **Produktionsreife**, nicht kosmetische Optimierung.

## Status-Snapshot (Stand 2026-04-01)

Diese Einordnung basiert auf dem aktuellen Repo-Stand.

Bereits erledigt:
- Produktions-Deployment von Dev entkoppeln
- Seed-/Demo-Daten in Production technisch ausschließen
- P0.3 bis P0.7
- P1.1 bis P1.4

## Wichtig für die KIs
- Das Projekt ist **fachlich schon stark**, aber **noch nicht produktionsreif**.
- Größte Risiken liegen aktuell in:
  1. noch zu fragiler Frontend-State-Architektur
  2. clientseitig rekonstruierter Manager-/Supervisor-Logik
  3. zu großen Frontend-Dateien und Hooks
  4. fehlendem route-basiertem Code-Splitting
  5. weiterer Betriebs- und Observability-Härtung
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

## Reihenfolge
1. **P0 – Produktionsblocker beseitigen**
2. **P1 – Stabilität und Betriebsfähigkeit absichern**
3. **P2 – Architektur- und UX-Härtung**
4. **P3 – Performance und langfristige Wartbarkeit**

# P2 – Architektur- und Stabilitätsverbesserungen

## TASK P2.1 – Frontend-State-Management entschlacken
**Ziel:** Weniger fragile State-Synchronisation, weniger `setState` in Effects, besser vorhersagbares UI-Verhalten.

**Warum:** Wiederkehrende `set-state-in-effect`-Muster deuten auf ein zu fragiles Zustandsmodell hin.

**Aktueller Stand:**
- erledigt
- erledigt:
  - Draft-Konsolidierung in `useAdminNotificationEmailConfiguration`
  - Draft-Konsolidierung und Refresh-Resync in `useAdminUserManagement`
  - UID-basierter Requirement-Reset in `useRequirementEditor`
  - `WorkflowLinksPanel` nutzt jetzt React Query via `useRelatedWorkflows(uid)`
  - `useWorkflowCreation` nutzt React Query für `completedOnboardings` und `workflowConfig`
  - `useAdminTaskTemplateManagement` bündelt Loading-/Saving-/Deleting-Flags jetzt in einem gemeinsamen Operation-State
  - `useAdminAnswerDefinitionManagement` bündelt seine Operation-Flags jetzt intern
  - `useAdminRoleAnswerDefaults` bündelt Laden/Speichern jetzt in einem gemeinsamen Operation-State
- bewusst nicht umgebaut:
  - `useAdminOrganizationManagement`, weil die beiden Draft-Sync-Effects pro Draft-Record fachlich legitim und deutlich risikoärmer als ihr kosmetischer Nutzen sind
  - Dashboard-Hooks, weil die verbleibende Manager-/Supervisor-Problematik fachlich in P2.3 gelöst werden soll und kein reines Frontend-State-Thema mehr ist

**Konkrete Aufgaben:**
1. Analysiere die betroffenen Komponenten/Hooks systematisch.
2. Ordne pro Fall ein, ob State wirklich abgeleitet, memoized oder server-state-getrieben sein sollte.
3. Refactore kritische Komponenten so, dass unnötige Synchronisations-Effects verschwinden.
4. Achte darauf, UI-Verhalten nicht unbemerkt zu ändern.

**Definition of Done:**
- deutlich weniger problematische Synchronisations-Effects
- sauberere Datenflüsse in zentralen Workflow-/Dashboard-Komponenten

**Empfohlene KI:** Claude für Analyse, Codex für Umsetzung
**Reasoning:** very high

---

### Analyse & Design (Claude) – Implementierung durch Codex

#### Problemlandschaft

Es gibt drei wiederkehrende Anti-Patterns im Frontend:

**A. setState-in-useEffect zur Prop-/Server-Synchronisation** – Serverstate oder Props werden per useEffect in viele einzelne useState-Felder kopiert. Erzeugt unnötige Render-Zyklen und macht den Datenfluss schwer nachvollziehbar.

**B. Extreme useState-Fragmentierung** – Hooks mit 10–25 einzelnen useState-Aufrufen für Daten, Drafts und Operationsstatus. Führt zu inkonsistenten Batch-Updates und riesigen Return-Objekten.

**C. Manueller Fetch statt React Query** – Einige Hooks fetchen mit useEffect + cancelled-Flag, obwohl der Hook-Kontext bereits React Query verwendet. Erzeugt redundante Loading/Error-States.

#### Priorisierte Änderungsliste

##### Paket 1 – HIGH: Draft-Konsolidierung (größter Impact, geringstes Risiko)

**1.1 `web/src/hooks/useAdminNotificationEmailConfiguration.ts`**

Problem: 8 einzelne Draft-useState (Zeile 20–27) + useEffect (Zeile 32–53) mit 8 Settern zur Synchronisation aus `notificationEmailConfiguration`.

Fix:
- Einen `NotificationDraft`-Typ anlegen mit allen 8 Feldern als Properties.
- Funktion `buildNotificationDraft(source: AdminNotificationEmailConfiguration | null): NotificationDraft` extrahieren.
- Die 8 `useState` durch einen einzigen `useState<NotificationDraft>` ersetzen.
- Den useEffect (Zeile 32–53) auf eine Zeile reduzieren: `setDraft(buildNotificationDraft(notificationEmailConfiguration))`.
- `hasNotificationEmailDraftChanges` (Zeile 55–80): statt 8 einzelner Dependencies nur `draft` und `notificationEmailConfiguration` vergleichen.
- `saveNotificationEmailConfiguration` (Zeile 82–117): liest Felder aus dem `draft`-Objekt statt aus 8 Variablen.
- Das Return-Objekt ändert sich: statt 8 einzelner Setter exportiert der Hook `draft`, `setDraft` oder `updateDraft(key, value)`.
- **Achtung**: Die konsumierenden Komponenten (AdminConfig-Seite) müssen auf `draft.notificationEnabledDraft` statt `notificationEnabledDraft` umgestellt werden. Suche alle Imports/Verwendungen von `useAdminNotificationEmailConfiguration`.

**1.2 `web/src/hooks/useAdminUserManagement.ts`**

Problem (a): 25 useState-Aufrufe (Zeile 53–77). Davon 6 edit-Draft-Felder (Zeile 60–65), 6 new-Draft-Felder (Zeile 66–71), 5 Operation-Flags (Zeile 72–77).

Problem (b): useEffect (Zeile 105–126) synchronisiert 8 Felder aus `selectedUser` – redundant zu `selectUser`-Callback (Zeile 82–96), der fast identische Zuweisungen macht.

Fix:
- `UserEditDraft`-Typ mit den 6 Edit-Feldern (`externalKey`, `displayName`, `email`, `notificationEmail`, `departmentId`, `isActive`).
- `UserNewDraft`-Typ mit den 6 New-Feldern (gleiche Struktur).
- Funktion `buildEditDraft(user: AdminUser | null): UserEditDraft` extrahieren.
- Die 12 Draft-useState durch `useState<UserEditDraft>` + `useState<UserNewDraft>` ersetzen.
- `selectUser` (Zeile 82–96): Setzt `selectedUserId`, `editDraft`, `selectedUserRoleIds`, `selectedUserGroupIds`.
- useEffect (Zeile 105–126): Nur noch Fallback für externe `users`-Array-Änderungen (z. B. nach Server-Refresh). Reduziert sich auf: `setEditDraft(buildEditDraft(selectedUser)); setSelectedUserRoleIds(...)`.
- Die 5 `isSaving*`-Flags können zu einem `savingOperation: string | null`-State zusammengefasst werden (Werte: `"masterData" | "roles" | "groups" | "groupRoles" | "creating" | null`).
- **Achtung**: Großer Hook mit vielen Consumern. Regressionstests nach dem Refactoring laufen lassen.

**1.3 `web/src/hooks/useRequirementEditor.ts`**

Problem: useEffect (Zeile 31–38) setzt `requirementSelections` aus `workflow.requirements`. Danach wird `requirementSelections` durch User-Interaktion mutiert – es ist also echte Draft-State, kein reiner derived State.

Fix:
- Die sauberste Lösung: Der Aufrufer setzt `key={workflow?.uid ?? ""}` auf die Komponente, die `useRequirementEditor` nutzt. Damit wird der Hook bei Workflow-Wechsel neu gemountet und der Initialwert aus `useState(() => buildRequirementSelections(workflow?.requirements ?? []))` greift sauber.
- Alternativ (wenn `key`-Ansatz zu invasiv): useEffect beibehalten, aber den Abhängigkeitsarray auf `[workflow?.uid]` statt `[workflow]` einschränken, damit nicht jede Reference-Änderung des Workflow-Objekts die Selections resettet.

##### Paket 2 – HIGH: Große Hooks konsolidieren

**2.1 `web/src/hooks/useAdminTaskTemplateManagement.ts`**

Problem: 23 useState-Aufrufe (Zeile 118–140). 6 Loading-Flags, 2 Deleting-IDs, 3 Saving-Flags, Daten + Drafts.

Fix:
- Loading-/Saving-/Deleting-States in ein `OperationState`-Objekt:
  ```typescript
  type OperationState = {
    loadingProcessTypes: boolean;
    loadingTemplates: boolean;
    loadingDependencyGraph: boolean;
    loadingConditions: boolean;
    loadingDependencies: boolean;
    saving: boolean;
    deleting: boolean;
    savingCondition: boolean;
    deletingConditionId: number | null;
    savingDependency: boolean;
    deletingDependencyId: number | null;
  };
  ```
- Helper-Funktionen: `startOp(key)`, `endOp(key)` um die Set-Aufrufe zu reduzieren.
- Die Daten-States (`processTypes`, `templates`, `dependencyGraph`, `conditions`, `dependencies`, `answerDefinitions`) können bleiben – sie haben unterschiedliche Lifecycles.
- Die Draft-States (`draft`, `conditionDraft`, `dependencyDraft`) können bleiben, sind schon als Objekte modelliert (gut).

**2.2 `web/src/hooks/useAdminOrganizationManagement.ts`**

Problem: Zwei useEffect-Blöcke (Zeile 51–63, 65–77) synchronisieren Draft-Records aus Props.

Bewertung: Eigentlich vertretbar – jeder Effect setzt genau 1 State-Variable. Das ist das Standard-Pattern für "editable draft from server data". Der Effect bleibt nötig, weil die Drafts vom User editierbar sind.

Fix (optional, geringer Impact):
- `useMemo` ist hier NICHT korrekt, weil die Drafts mutierbar sein müssen.
- Wenn gewünscht: `departmentDrafts` und `responsibilityDrafts` in ein gemeinsames `drafts`-Objekt zusammenfassen und den Initializer zusammenführen. Aber das ist kosmetisch.
- **Empfehlung: Niedrige Priorität, nur machen wenn Paket 1 abgeschlossen.**

##### Paket 3 – MEDIUM: Manual Fetch durch React Query ersetzen

**3.1 `web/src/hooks/useWorkflowCreation.ts`**

Problem: Der Hook nutzt React Query für `processTypes` und `roles`/`departments` (gut), aber manuellen Fetch mit useEffect + cancelled-Flag für `completedOnboardings` (Zeile 203–249) und `workflowConfig` (Zeile 255–289). Das erzeugt 6 manuelle useState-Felder (`completedOnboardings`, `completedOnboardingsLoading`, `completedOnboardingsError`, `workflowConfig`, `workflowConfigLoading`, `workflowConfigError`).

Fix:
- Zwei neue React-Query-Hooks anlegen: `useCompletedOnboardingsSearch(search, enabled)` und `useWorkflowConfig(roleId, processTypeKey, enabled)`.
- Die 6 manuellen useState + 2 useEffects entfallen komplett.
- Der Debounce-Effect (Zeile 162–168) ist legitim und bleibt.

**3.2 `web/src/components/workflow-detail/WorkflowLinksPanel.tsx`**

Problem: Manueller Fetch (Zeile 18–36) mit cancelled-Flag.

Fix: Durch einen `useRelatedWorkflows(uid)`-React-Query-Hook ersetzen. Drei useState + 1 useEffect entfallen.

**Empfehlung: Paket 3 nur machen, wenn die React-Query-Infrastruktur (`services/queries/`) das Muster schon klar vorgibt.** Die manuellen Fetches funktionieren korrekt – das Refactoring ist Konsistenz, nicht Bugfix.

##### Paket 4 – MEDIUM: Admin-Config-Hooks Konsolidierung

**4.1 `web/src/hooks/useAdminAnswerDefinitionManagement.ts`**
**4.2 `web/src/hooks/useAdminRoleAnswerDefaults.ts`**

Beide haben 9–10 useState-Aufrufe mit demselben Muster: `processTypes`, `selectedProcessTypeId`, Daten, Draft, Loading-Flags. Gleiche Konsolidierungsstrategie wie bei TaskTemplate (Operation-State-Objekt).

Diese Hooks sind kleiner und weniger kritisch. **Nur machen, wenn Paket 1+2 abgeschlossen.**

#### Nicht ändern (legitimie Patterns)

- **`useTaskInteraction.ts`**: Record-basierter State, kein Sync-Effect. Sauber.
- **`dashboardInsights.ts`**: Reine async Utility-Funktionen, kein React-State.
- **`DashboardOverview.tsx` + `useDashboardInsights.ts`**: Nutzt React Query korrekt.
- **`useWorkflowCreation.ts` Zeile 162–168**: Debounce-Effect – legitimer Seiteneffekt.
- **Page-Level Effects** (WorkflowListPage, WorkflowSearchPage): URL-Synchronisation und Debounce – beides legitime Seiteneffekte.
- **Admin-Hooks: useEffect für Init-Load** (z. B. `getAdminProcessTypes()` on mount): Akzeptabel. Diese in React Query umzubauen wäre nice-to-have, aber kein State-Management-Problem.

#### Reihenfolge für Codex

1. **Paket 1.1** – `useAdminNotificationEmailConfiguration` (kleinster Hook, idealer Pilot)
2. **Paket 1.3** – `useRequirementEditor` (einfachste Änderung)
3. **Paket 2.1** – `useAdminTaskTemplateManagement` (OperationState-Konsolidierung)
4. **Paket 1.2** – `useAdminUserManagement` (größter Hook, höchstes Risiko)
5. **Paket 3** und **Paket 4** – nur wenn Tests nach 1–4 stabil grün

#### Testrichtlinien

- Nach jeder Hook-Änderung: `npm run lint` + `npm test` im Frontend
- UI-Verhalten manuell prüfen: Admin-Config-Seite durchklicken, Draft-Änderungen vornehmen, speichern, Entity-Wechsel testen
- **Kritisch**: Beim User-Management-Hook sicherstellen, dass Entity-Wechsel (User wählen → anderer User) die Drafts korrekt resettet
- Die Return-Signatur der Hooks ändert sich (einzelne Felder → Objekte). Alle Importe in konsumierenden Komponenten anpassen.

---

## TASK P2.2 – Frontend-Teststrategie an echte Modulgrenzen anpassen
**Ziel:** Tests sollen nicht von zufälligen Barrel-Strukturen abhängen.

**Warum:** Aktuelle Tests brechen teilweise wegen inkonsistenter Import-/Mock-Grenzen.

**Konkrete Aufgaben:**
1. Lege fest, welche Module offiziell öffentliche Verträge darstellen.
2. Passe Tests und ggf. Service-Schicht an diese Grenzen an.
3. Verhindere, dass Tests auf zufällige interne Re-Exports angewiesen sind.

**Definition of Done:**
- stabilere Testarchitektur
- weniger fragile Mocks bei Refactorings

**Empfohlene KI:** Claude für Review, Codex für Refactor
**Reasoning:** high

---

### Claude-Analyse und Codex-Anweisungen für P2.2

#### Analyse-Ergebnis

Alle 18 Testdateien in `web/tests/` wurden geprüft. Die Mehrheit der Tests importiert bereits korrekt aus den Domain-Modulen (`adminApi`, `workflowApi`, `lookupApi`, `taskApi`, `types/auth`, `types/workflow`). Zwei strukturelle Probleme wurden gefunden:

**Problem A – Compat-Barrel-Import in `lifecycleApi.test.ts`** (Severity: HIGH)

`web/tests/lifecycleApi.test.ts` importiert `getProcessTypes` über den Compat-Barrel `lifecycleApi.ts`, der nichts anderes macht als alle Domain-Module re-exportieren:
```
// lifecycleApi.ts – nur ein Barrel:
export * from "./adminApi";
export * from "./lookupApi";
...
```
`getProcessTypes` liegt tatsächlich in `web/src/services/lookupApi.ts`. Der Testname und Import-Pfad spiegeln also nicht mehr die reale Modulstruktur wider. Wenn der Barrel umgebaut oder entfernt wird, bricht der Test.

Der im Test verwendete Low-Level-Mock (`vi.mock("../src/services/api/client", ...)`) ist bewusst gewählt, weil der Test prüft, dass `getProcessTypes` den korrekten URL-Pfad `/process-types` aufruft und kein Caching betreibt. Dieser Ansatz ist korrekt – der Mock selbst muss nicht geändert werden.

**Problem B – Veralteter und duplizierter `createUser`-Helper** (Severity: HIGH)

`createUser` existiert in zwei Testdateien:
- `web/tests/AdminDepartmentsSection.test.tsx` – **veraltet**, fehlen 9 Pflichtfelder von `AdminUser`:
  `directorySynced`, `departmentSource`, `departmentOverrideActive`, `directoryIdentityId`, `userPrincipalName`, `directoryDisplayName`, `effectiveRoles`, `permissionOverrides`, `effectivePermissions`
- `web/tests/AdminConfigPage.test.tsx` – vollständig, entspricht aktuellem `AdminUser`-Typ

`createDepartment` ist ebenfalls dupliziert:
- `web/tests/AdminDepartmentsSection.test.tsx`
- `web/tests/AdminConfigPage.test.tsx`

Beide Funktionen gehören in `web/tests/testUtils.tsx`, wo bereits `createWorkflowSummary`, `createTaskWithWorkflow` und `createRequirementSnapshot` zentralisiert sind.

**Kein Problem (Referenz):**

Diese Muster sind korrekt und sollen nicht geändert werden:
- `workflowDetailModel.test.ts` → importiert direkt aus `components/workflow-detail/workflowDetailModel.ts` (co-located Model-File, kein Barrel)
- `dashboardInsights.test.ts` → importiert direkt aus `components/dashboard/dashboardInsights.ts`, mockt `workflowApi` auf Modul-Ebene – korrekt
- `AdminConfigPage.test.tsx`, `MyTasksPage.test.tsx`, `CreateWorkflowPage.test.tsx`, etc. → mocken und importieren alle aus den richtigen Domain-Modulen

---

#### Codex-Anweisung: Paket A – `lifecycleApi.test.ts` korrigieren

**Ziel:** Testdatei auf das echte Quellmodul zeigen lassen.

1. Datei `web/tests/lifecycleApi.test.ts` umbenennen zu `web/tests/lookupApi.test.ts`

2. In der umbenannten Datei den Import-Pfad ändern:
   ```ts
   // VORHER (Zeile 9):
   import { getProcessTypes } from "../src/services/lifecycleApi";
   
   // NACHHER:
   import { getProcessTypes } from "../src/services/lookupApi";
   ```

3. Den Mock (`vi.mock("../src/services/api/client", ...)`) und alle Test-Assertions (`mockedRequestJson`) **nicht ändern** – der Low-Level-Mock ist bewusst gewählt, um das URL-Routing zu prüfen.

4. Den `describe`-Block-Namen anpassen:
   ```ts
   // VORHER:
   describe("lifecycleApi.getProcessTypes", () => {
   
   // NACHHER:
   describe("lookupApi.getProcessTypes", () => {
   ```

---

#### Codex-Anweisung: Paket B – Test-Helper zentralisieren

**Ziel:** `createUser` und `createDepartment` aus den lokalen Testdateien in `testUtils.tsx` auslagern.

**Schritt 1: `web/tests/testUtils.tsx` ergänzen**

Am Ende der Datei (nach `createRequirementSnapshot`) folgende zwei Exporte hinzufügen:

```ts
import type { AdminUser, AdminDepartmentAssignment } from "../src/types/auth";

export function createAdminUser(overrides: Partial<AdminUser> = {}): AdminUser {
  return {
    userId: 1,
    externalKey: "lea.lead",
    displayName: "Lea Lead",
    email: "lea.lead@demo.local",
    notificationEmail: null,
    isActive: true,
    hasManagerAccess: true,
    departmentId: 1,
    departmentName: "IT",
    directorySynced: false,
    departmentSource: "manual",
    departmentOverrideActive: false,
    directoryIdentityId: null,
    userPrincipalName: null,
    directoryDisplayName: null,
    roles: [],
    groups: [],
    effectiveRoles: [],
    permissionOverrides: [],
    effectivePermissions: [],
    ...overrides,
  };
}

export function createAdminDepartmentAssignment(
  overrides: Partial<AdminDepartmentAssignment> = {}
): AdminDepartmentAssignment {
  return {
    departmentId: 1,
    departmentName: "IT",
    departmentLeadUserId: 10,
    departmentLeadDisplayName: "Lea Lead",
    requirementOwnerUserId: 11,
    requirementOwnerDisplayName: "Mia Manager",
    updatedAt: "2026-03-24T08:00:00.000Z",
    ...overrides,
  };
}
```

**Achtung:** Der Import von `AdminUser` und `AdminDepartmentAssignment` ist oben ggf. bereits im Import-Block vorhanden oder muss ergänzt werden. In `testUtils.tsx` ist Zeile 9 bereits `import type { ... } from "../src/types/workflow"` — der neue Import muss als eigene Zeile für `../src/types/auth` hinzugefügt werden.

**Schritt 2: `web/tests/AdminDepartmentsSection.test.tsx` anpassen**

a) Die lokalen Funktionen `createDepartment` (Zeilen 6–17) und `createUser` (Zeilen 19–33) löschen.

b) Import aus `testUtils` ergänzen:
   ```ts
   import { createAdminUser, createAdminDepartmentAssignment } from "./testUtils";
   ```

c) Alle Aufrufe `createDepartment(...)` → `createAdminDepartmentAssignment(...)` umbenennen.

d) Alle Aufrufe `createUser(...)` → `createAdminUser(...)` umbenennen.

e) `import type { AdminDepartmentAssignment, AdminUser } from "../src/types/auth";` entfernen, wenn nach der Änderung nicht mehr direkt benötigt.

**Schritt 3: `web/tests/AdminConfigPage.test.tsx` anpassen**

a) Die lokalen Funktionen `createUser` (ca. Zeilen 52–76) und `createDepartment` (ca. Zeilen 78–90+) löschen.

b) Import aus `testUtils` ergänzen:
   ```ts
   import { createAdminUser, createAdminDepartmentAssignment, renderWithApp } from "./testUtils";
   ```

c) Alle Aufrufe `createDepartment(...)` → `createAdminDepartmentAssignment(...)` umbenennen.

d) Alle Aufrufe `createUser(...)` → `createAdminUser(...)` umbenennen.

e) Die `import type { AdminUser, AdminDepartmentAssignment, ... }` Zeile (Zeile 6–14) nur bereinigen, wenn die verbleibenden Typen noch direkt in der Datei gebraucht werden; andere Typen wie `AdminGraphApplicationConfiguration`, `AdminGroup`, etc. werden weiterhin für lokale Hilfsfunktionen in der Datei gebraucht.

---

#### Hinweis für Codex: Abhängigkeit zu P2.1

P2.1 konsolidiert useState-Fragmentation in Admin-Hooks. Wenn dabei die Props-Schnittstellen von Komponenten wie `AdminDepartmentsSection` geändert werden (z.B. einzelne State-Variablen zu Objekt-Props zusammengefasst), dann muss das Rendering in `AdminDepartmentsSection.test.tsx` angepasst werden. Das ist eine spätere Aufgabe und nicht Teil von P2.2.

P2.2-Änderungen sind von P2.1 unabhängig und können vorher oder parallel angewendet werden.

---

## TASK P2.3 – Manager-/Supervisor-Dashboard serverseitig sauber modellieren
**Ziel:** Sichtbarkeits- und Queue-Logik soll aus dem Backend kommen, nicht im Frontend rekonstruiert werden.

**Warum:** Das Risiko für Berechtigungsdrift ist hoch, wenn Manager-Insights generisch aus Workflows abgeleitet werden.

**Aktueller Stand:**
- teilweise umgesetzt: Der Backend-Endpoint `/workflows/supervisor-step` existiert bereits
- offen: `dashboardInsights.ts` baut Manager-Sicht aktuell weiterhin über generische Workflow-Abfragen zusammen

**Konkrete Aufgaben:**
1. Prüfe aktuelle Dashboard-Datenpfade.
2. Vergleiche generische Workflow-Abfragen mit dedizierten Supervisor-/Queue-Endpoints.
3. Vereinheitliche das Modell so, dass der Client keine Rollenlogik erraten muss.
4. Passe Tests entsprechend an.

**Definition of Done:**
- Dashboard nutzt fachlich korrekte Backend-Quellen
- weniger clientseitige Rekonstruktion von Berechtigungslogik

**Empfohlene KI:** Claude für fachlich-technische Analyse, Codex für Umsetzung
**Reasoning:** very high

---

## TASK P2.4 – Große Frontend-Dateien und Hooks zerlegen
**Ziel:** Reduzierung von Komplexität und Refactoring-Risiko.

**Warum:** Mehrere Dateien sind zu groß und tragen zu Test-/State-Problemen bei.

**Konkrete Aufgaben:**
1. Identifiziere die größten und risikoreichsten Komponenten/Hooks.
2. Teile sie entlang fachlicher Verantwortlichkeiten auf.
3. Vermeide reine kosmetische Extraktion; Ziel sind klarere Grenzen.
4. Passe Tests parallel an.

**Definition of Done:**
- zentrale Problemdateien sind kleiner und klarer getrennt
- Lesbarkeit und Testbarkeit steigen messbar

**Empfohlene KI:** Codex
**Reasoning:** high

---

# P3 – Performance und langfristige Wartbarkeit

## TASK P3.1 – Route-basiertes Code-Splitting im Frontend einführen
**Ziel:** Initialbundle verkleinern und selten genutzte Bereiche lazy laden.

**Warum:** Das Hauptbundle ist für den aktuellen Reifegrad unnötig groß.

**Konkrete Aufgaben:**
1. Analysiere Bundle-Zusammensetzung.
2. Führe `React.lazy` / route-basiertes Splitting für seltene Bereiche ein.
3. Beginne mit Admin-, Graph-, Settings- oder seltenen Detailseiten.
4. Prüfe Ladezustände und Fehlergrenzen.

**Definition of Done:**
- Hauptbundle spürbar reduziert
- große Nebenbereiche werden lazy geladen

**Empfohlene KI:** Codex
**Reasoning:** medium

---

## TASK P3.2 – Repository-/Service-Grenzen im Backend nachschärfen
**Ziel:** Businesslogik nicht zu stark in SQL-nahe Repositories drücken.

**Warum:** Das Backend ist ordentlich, aber einige Repositories sind zu groß und fachlich schwer.

**Aktueller Stand:**
- teilweise umgesetzt: `PostgresWorkflowRepository` ist bereits in mehrere Operations-Dateien aufgeteilt
- offen: Die fachliche Grenze zwischen Repository- und Service-Logik ist damit noch nicht automatisch sauber

**Konkrete Aufgaben:**
1. Identifiziere größte Repository-Klassen.
2. Prüfe, welche Logik in Domain-/Application-Services gehört.
3. Refactore nur dort, wo echte Lesbarkeit und Testbarkeit steigen.
4. Vermeide unnötigen Umbau kurz vor Betrieb, wenn Risiko größer als Nutzen ist.

**Definition of Done:**
- klarere Verantwortlichkeiten in kritischen Backend-Pfaden
- bessere Testbarkeit komplexer Businesslogik

**Empfohlene KI:** Claude für Planung, Codex für gezielte Umsetzung
**Reasoning:** high

---

## TASK P3.3 – Observability verbessern
**Ziel:** Produktionsfehler schneller erkennen und eingrenzen.

**Warum:** Kurz vor Go-Live reicht Basislogging selten aus.

**Konkrete Aufgaben:**
1. Prüfe aktuelles Logging und Error-Handling.
2. Ergänze strukturierte Logs an kritischen Workflow-, Auth- und Notification-Pfaden.
3. Definiere minimale Betriebsmetriken und sinnvolle Fehlerkontexte.
4. Vermeide Secret-/PII-Leaks in Logs.

**Definition of Done:**
- kritische Produktionspfade sind nachvollziehbarer
- Fehleranalyse wird deutlich leichter

**Empfohlene KI:** Claude für Logging-Strategie, Codex für Implementierung
**Reasoning:** high

---

# Empfohlene Arbeitsweise für die KIs

## Für Claude-Prompts
Verwende Claude primär dann, wenn zuerst **Analyse, Priorisierung oder Architekturentscheidung** nötig ist.

### Gute Claude-Aufgaben
- „Analysiere die aktuelle Compose-/Env-Struktur und entwirf ein sauberes Produktionsmodell.“
- „Bewerte, wie das Secret-Handling ohne Klartext in der DB umgebaut werden sollte.“
- „Prüfe, ob das Dashboard Rollenlogik korrekt aus dem Backend bezieht oder clientseitig rekonstruiert.“
- „Zerlege die betroffenen Frontend-State-Probleme in konkrete Refactor-Pakete.“

## Für Codex-Prompts
Verwende Codex primär für **Umsetzung, Refactoring, Dateianpassungen, Tests und Skripte**.

### Gute Codex-Aufgaben
- „Bringe alle Frontend-Lint-Fehler sauber auf grün.“
- „Repariere die fehlschlagenden Frontend-Tests an den echten Modulgrenzen.“
- „Füge eine minimale CI-Pipeline für Build/Test/Lint hinzu.“

---

# Konkrete Reihenfolge für die nächste KI-Arbeit

## Sprint 1
1. P2.1 State-Management entschlacken
2. P2.3 Manager-/Supervisor-Dashboard serverseitig härten
3. P2.4 große Dateien/Hooks zerlegen

**Empfohlene KI-Mischung:** Claude für Zerlegung/Review, Codex für Umsetzung

## Sprint 2
1. P1.4 Produktions-Checkliste schreiben
2. P2.2 Teststrategie stabilisieren
3. P3.1 Code-Splitting

**Empfohlene KI-Mischung:** Claude zuerst, dann Codex

## Sprint 3
1. P2.1 State-Management entschlacken
2. P2.3 Manager-/Supervisor-Dashboard serverseitig härten
3. P2.4 große Dateien/Hooks zerlegen

**Empfohlene KI-Mischung:** Claude für Zerlegung/Review, Codex für Umsetzung

## Sprint 4
1. P3.1 Code-Splitting
2. P3.2 Backend-Service-/Repository-Grenzen
3. P3.3 Observability

---

# Klare Gesamtpriorität

## Sofort
- State-Architektur
- Produktions-Checkliste
- Dashboard-/Rollenlogik aus dem Backend

## Danach
- Bundle / Code-Splitting
- größere Frontend-Zerlegung

## Später
- Repository-/Service-Härtung
- Observability

---

# Endzustand, den die KIs anstreben sollen
Nach Abschluss der verbleibenden Aufgaben soll das Projekt nicht „schöner“, sondern **real robuster und wartbarer** sein:
- weniger fragile Frontend-State- und Testarchitektur
- serverseitig modellierte Rollen-/Queue-Logik
- kleinere, klarere Module
- besseres Betriebs- und Fehlerverhalten
