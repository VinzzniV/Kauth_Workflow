# ONBOARDING_COUPLING_INVENTORY.md

Dieses Artefakt inventarisiert die aktuell gefundenen Onboarding-Kopplungen gemaess Phase 1 / T2.
Ziel ist die Trennung zwischen harmloser Altbenennung, echter Kernkopplung und bewusst vorerst stabil gehaltenen Legacy-Vertraegen.

## Kategorien

- `A Historische Benennung`
  Nur Name, Text oder Altpfad. Kein produktkernkritisches Verhalten.
- `B Interne Kernkopplung`
  Macht `onboarding` technisch zu einem impliziten Sonderfall im Kern.
- `C Legacy-Vertrag`
  Sichtbare oder externe Schnittstelle, die vorerst stabil bleibt.

## Inventar

| Bereich | Fundstelle | Kategorie | Warum gekoppelt | Aktion jetzt | Aktion spaeter | Zielbegriff |
|---|---|---|---|---|---|---|
| API Startup | `api/API/Extensions/OnboardingStartupValidationExtensions.cs` | B | Validierung verlangt heute implizit genau den Prozesskey `onboarding` | Auf generische Validierung aller aktiven supervisor-pflichtigen Prozessarten umstellen | spaeter an Definition Layer / Runtime-Validierung anbinden | generische Workflow-/Lifecycle-Validierung |
| API Extensions | `api/API/Extensions/OnboardingApplicationExtensions.cs` | B | Alt-Dateiname verankert Onboarding im Bootstrap, obwohl Symbol bereits neutral ist | Datei neutral umbenennen | keine | Lifecycle-/Workflow-Extensions |
| API Extensions | `api/API/Extensions/OnboardingServiceCollectionExtensions.cs` | B | Alt-Dateiname verankert Onboarding im Bootstrap, obwohl Symbol bereits neutral ist | Datei neutral umbenennen | keine | Lifecycle-/Workflow-Extensions |
| API Master Data | `ResolveProcessTypeId(... default onboarding)` in `PostgresWorkflowRepository.MasterDataOperations.cs` | C | `requirements` und `workflow-config` fallen implizit auf `onboarding` zurueck | bewusst unveraendert lassen | spaeter auf explizite Definition-/Workflow-Auswahl umstellen | expliziter Workflow-Kontext |
| API Target Person Flow | `/workflows/completed-onboardings` in `WorkflowMasterDataEndpoints.cs` | C | fachlich sichtbarer Endpunkt fuer Zielpersonen-Auswahl basiert auf Onboarding als Quellworkflow | bewusst unveraendert lassen | spaeter generischen Zielpersonen-/Quellworkflow-Pfad einfuehren | `workflow-target-sources` oder generischer Person-/History-Pfad |
| API DTO/Service | `CompletedOnboardingSearchResultDto`, `SearchCompletedOnboardingsAsync` | C | oeffentliche und interne Vertragsnamen sind onboarding-spezifisch | bewusst unveraendert lassen | spaeter generische DTO-/Service-Aliase und Migration | generischer Workflow-Source-Search |
| Workflow-Erstellung Frontend | `CompletedOnboardingSearchResult`, `selectedCompletedOnboarding`, `completed-onboardings` Query Keys | C | kompletter Zielpersonenpfad spricht von abgeschlossenem Onboarding | bewusst unveraendert lassen | spaeter UI und API gemeinsam auf generische Zielpersonenquelle umstellen | Zielperson-/Quellworkflow-Auswahl |
| Authorization | `workflows.create.onboarding` in `AuthorizationPermissions.cs` und `db/38_permission_model.sql` | C | sichtbarer Berechtigungsschluessel fuer Legacy-Prozessarten | bewusst unveraendert lassen | spaeter Permission-Modell an Definitionen/Workflow-Keys ausrichten | definitions- oder workflow-key-basierte Rechte |
| Responsibilities | `hr_onboarding` in Seeds und Repository-Queries | C | Responsibility-Key ist fachlich und seed-seitig verankert | bewusst unveraendert lassen | spaeter Responsibility-Begriffe fachlich pruefen und ggf. neutralisieren | `hr_lifecycle` oder fachlich engerer Begriff |
| Mail-Texte | `NotificationEmailTemplateBuilder` Switch fuer `onboarding` | C | sichtbare Mail-Labels enthalten bewusst Onboarding-Fachbegriffe | bewusst unveraendert lassen | spaeter mit Produkt-/UX-Entscheidung angleichen | workflow- oder prozessspezifische Labels |
| Runtime/DB Modell | `process_types`, task-getriebene Generierung, Legacy-Manager-Creatable-Keys | A | Lifecycle- und Prozessart-Begriffe spiegeln die aktuelle Legacy-Architektur, nicht nur Onboarding | nur dokumentieren | spaeter im Definition Layer und Runtime-Umbau abloesen | Workflow Definition / Version / Nodes |
| Branding/UI | `compose.yml` Name, `.env.prod.example`, `web/index.html`, Theme-Key | A | sichtbare Altbenennung, aber kein Kernverhalten | nur dokumentieren | spaeter in einem gezielten Branding-/Vertrags-Schnitt aendern | Workflow Platform / neutrales Branding |

## Ergebnis fuer Phase 1

In diesem ersten konservativen Schnitt werden nur die `B`-Eintraege umgesetzt:

- neutrale Dateinamen im Extension-/Startup-Bereich
- generische Supervisor-Validierung statt festem `onboarding`-Pflichtprozess

Alle `C`-Eintraege bleiben bewusst stabil, um keine API-/UI-/Berechtigungs-Vertraege aufzubrechen.
