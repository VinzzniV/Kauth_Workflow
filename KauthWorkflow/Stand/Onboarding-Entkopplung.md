# Onboarding-Entkopplungs-Inventar

#stand #migration #onboarding

Inventar der gefundenen Onboarding-Kopplungen aus Phase 1 / T2.
Ziel: Trennung zwischen harmloser Altbenennung, echter Kernkopplung und bewusst stabil gehaltenen Legacy-Verträgen.

Primärquelle im Repo war: `ONBOARDING_COUPLING_INVENTORY.md` (in Vault migriert)

---

## Kategorien

| Kategorie | Bedeutung |
|-----------|-----------|
| **A — Historische Benennung** | Nur Name, Text oder Altpfad. Kein produktkernkritisches Verhalten. |
| **B — Interne Kernkopplung** | Macht `onboarding` technisch zu einem impliziten Sonderfall im Kern. |
| **C — Legacy-Vertrag** | Sichtbare oder externe Schnittstelle, die vorerst stabil bleibt. |

---

## Inventar

| Bereich | Fundstelle | Kategorie | Warum | Aktion jetzt | Aktion später |
|---------|-----------|-----------|-------|-------------|--------------|
| API Startup | `OnboardingStartupValidationExtensions.cs` | B | Validierung verlangt implizit `onboarding`-Prozesskey | Auf generische Validierung aller supervisor-pflichtigen Prozessarten umstellen | An Definition Layer anbinden |
| API Extensions | `OnboardingApplicationExtensions.cs` | B | Alt-Dateiname verankert Onboarding im Bootstrap | Neutral umbenennen | — |
| API Extensions | `OnboardingServiceCollectionExtensions.cs` | B | Alt-Dateiname verankert Onboarding im Bootstrap | Neutral umbenennen | — |
| API Master Data | `ResolveProcessTypeId(... default onboarding)` | C | `requirements` und `workflow-config` fallen implizit auf `onboarding` zurück | Bewusst stabil lassen | Auf explizite Definition-/Workflow-Auswahl umstellen |
| API Target Person | `/workflows/completed-onboardings` | abgeschlossen 2026-05-01 | Endpoint gelöscht — `/workflow-target-person-sources` ist jetzt der einzige Pfad | — | — |
| API Target Person | `/rotation/completed-onboardings` | abgeschlossen 2026-05-01 | Endpoint gelöscht — Frontend nutzt `/people/rotation-eligible` | — | — |
| DTO/Service | `CompletedOnboardingSearchResultDto` | abgeschlossen 2026-05-01 | DTO gelöscht — Endpoints geben direkt `WorkflowTargetPersonSourceDto` zurück | — | — |
| Frontend | `CompletedOnboardingSearchResult`, `completed-onboardings` Query Keys | abgeschlossen 2026-05-01 | Type-Aliases entfernt, `RotationEligiblePerson` als Alias auf `WorkflowTargetPerson`, Query-Keys auf `target-person-sources` und `rotation-eligible` umgestellt | — | — |
| BE+FE | `CompletedOnboarding*` Fachbegriff-Rest (Methode, DTO-Properties, UI-Labels) | abgeschlossen 2026-05-11 | `GetCompletedOnboardingSource` → `GetSourceWorkflow`; `LatestCompletedOnboardingWorkflowUid`/`At` → `LatestSourceWorkflowUid`/`CompletedAt`; SQL-CTEs, FE-Typen und UI-Labels vollständig entlegacyt | — | — |
| Authorization | `workflows.create.onboarding` Permission | abgeschlossen 2026-05-01 | Permission-Schema ist seit 6.3d-iv vollstaendig definitionsgetrieben (`workflows.create.<definition_key>`); Slice 7C entfernte ungenutztes hardcoded `WorkflowCreatePermissions`-Array | — | — |
| Responsibilities | `hr_onboarding` in Seeds | abgeschlossen 2026-05-01 | Slice 7B: Responsibility-Key umbenannt zu `hr_workflow_initiator`, Label "HR-Workflow-Initiierung", Backend-Fallback in `NotificationOperations` umgestellt | — | — |
| Mail-Texte | `NotificationEmailTemplateBuilder` Switch für `onboarding` | C | Sichtbare Mail-Labels mit Onboarding-Fachbegriffen | Bewusst stabil lassen | Mit UX-Entscheidung angleichen |
| Runtime/DB | `process_types`, task-getriebene Generierung | A | Lifecycle-Begriffe spiegeln Legacy-Architektur | Nur dokumentieren | Im Definition Layer ablösen |
| Branding/UI | `compose.yml`-Name, `web/index.html`, Theme-Key | A | Sichtbare Altbenennung, kein Kernverhalten | Nur dokumentieren | In Branding-Schnitt ändern |

---

## Ergebnis Phase 1

**B-Einträge erledigt:**
- Neutrale Dateinamen im Extension-/Startup-Bereich
- Generische Supervisor-Validierung statt festem `onboarding`-Pflichtprozess

**C-Einträge:** Bewusst stabil gehalten — keine API-, UI- oder Berechtigungs-Verträge aufgebrochen.

---

## Verwandte Notizen

- [[Migrationspfad]] — Übergeordneter Migrationspfad
- [[Entscheidungen]] — Onboarding ist nur ein Workflow
- [[Workflow]] — Generisches Workflow-Modell
