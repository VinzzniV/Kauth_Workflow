# MEMORY.md

## Zweck

Diese Datei ist nur kurzfristiges Arbeitsgedaechtnis.
Sie ist kein Backlog, kein Changelog und keine zweite Architektur-Doku.

Verwende sie nur fuer:
- aktuellen Fokus
- aktive Stolperfallen
- wenige temporaere Hinweise fuer die naechsten Sessions

---

## Current Focus

- Die Root-Dokumentation wurde auf den neuen Implementierungsplan fuer die Workflow-Plattform harmonisiert.
- Der fruehere Legacy-Mapping-Schritt fuer `onboarding`, `offboarding` und `department_change` ist umgesetzt; die Builder-Todo-Nummerierung startet davon getrennt.
- T6 ist umgesetzt: der Builder zeigt den Action Layer jetzt als eigenen Builder-Bestandteil mit sichtbarem Action-Katalog, kontrolliertem Hinzufuegen auf `automation`-Nodes und aufgeloesten Action-Namen im Canvas.
- T7 ist umgesetzt: der Builder-Ablauf ist im UI klarer gestaffelt, die Toolbar-Hierarchie ist strenger und sichtbare technische Felder wurden weiter reduziert, ohne neue Builder-Logik einzufuehren.
- T8 ist umgesetzt: der Workflow Builder lebt jetzt auf `/builder` als eigene Produktseite mit mehr Canvas-Flaeche, eigenem Navigationseintrag und zwei Builder-Modi (`Builder` vs. `Admin Builder`).
- T9 ist umgesetzt: Builder-relevante technische Labels und code-nahe Validierungs-/Formulartexte sind jetzt konsistent ohne Umlaute, waehrend sichtbare Produktlabels davon klar getrennt bleiben.
- T11 ist umgesetzt: der oeffentliche Create-Flow, Start-Katalog, Notification-Labeling und die ersten Lookup-Pfade sind definition-first; `processTypeKey`, `/process-types` und `/workflows/completed-onboardings` bleiben nur noch als Uebergangs-Aliasse.

## Active Risks / Watchouts

- Der Code spiegelt die neue Zielarchitektur noch nicht vollstaendig; die Dokumentation ist absichtlich schon weiter als der Ist-Stand.
- Laufende `dotnet run`- oder `dotnet watch`-Prozesse koennen lokale Builds und Tests blockieren.
- Mehrere sichtbare Legacy-Vertraege bleiben bewusst bestehen, vor allem `completed-onboardings`, `CompletedOnboardingSearchResultDto`, `workflows.create.onboarding` und `hr_onboarding`.
- Die drei T6-Mappings sind bewusst linearisiert; Parallel-Splits/-Joins und echte Task-Anbindung folgen erst in spaeteren Phasen.
- T6 liefert bewusst den sichtbaren Action Layer auf Basis des bestehenden Katalogs; tiefere Spezialeditoren fuer Mapping und Parameter bleiben weiter offen.
- T8 oeffnet Read/Load/Save des Builders fuer Basis-Builder auf bestehender `workflowCreate`-Faehigkeit; Action-Katalog, Definition-/Versionsanlage und Publish bleiben bewusst Admin-Builder-only.
- Nicht-Admin-Builder duerfen aktuell bestehende Drafts pflegen, aber keine Automation-Struktur, keine Definitionen/Versionen und keinen Publish veraendern.
- Die node-spezifischen `config`- und `inputMapping`-Felder bleiben bewusst noch teilweise formular- und JSON-basiert innerhalb der Sidebar; komfortablere Spezialeditoren sind weiterhin offen.
- Die erste T11-Uebergangsrelease behaelt Legacy-Aliasse und Legacy-Permissions fuer `onboarding`, `offboarding` und `department_change`; weitere Rueckbau-Schritte muessen diese Restpfade spaeter wirklich entfernen.
- DB-getriebene Integrations- und End-to-End-Tests haengen lokal weiter an einer verfuegbaren PostgreSQL-Instanz auf `127.0.0.1:25432`.
- Lokale `dotnet test`-Laeufe koennen weiterhin an einer laufenden `API.exe` haengen; Build-Artefakte waren in dieser Session zusaetzlich durch bestehende Assembly-Attribut-Kollisionen im API-Projekt blockiert.

## Temporary Notes

- `Workflow_Plattform_Implementation_Plan.md` ist jetzt die primaere Migrationsanweisung fuer Architekturarbeit.
- `PRODUCTIVE_TARGET_ARCHITECTURE.md` beschreibt das stabile Sollbild, `TODO.md` die priorisierte Reihenfolge.
- `ONBOARDING_COUPLING_INVENTORY.md` ist die Referenz fuer Phase 1 / T2 und trennt `A` Benennung, `B` Kernkopplung und `C` Legacy-Vertrag.
- `LEGACY_WORKFLOW_MAPPING.md` dokumentiert die T6-Abbildung der ersten drei Legacy-Prozesse in den Definition Layer.
- T1 bis T6 erweitern den Builder schrittweise ohne neue Endpunkte: Positionen, Edges, Node-Card-Rendering, rechte Sidebar, Toolbar und Action Layer laufen weiter ueber denselben Definition-Layer-Replace-Pfad und vorhandenen Draft-State.
- T7 reduziert sichtbare Technikreste im Builder, ohne den Definition-Layer-Replace-Pfad, Canvas-Logik oder Sidebar-Architektur anzufassen.
- Der Canvas-Builder laeuft jetzt ueber die eigene Route `/builder`; `/admin/config?section=builder|templates|answers|defaults` redirectet auf diese Produktseite.
- T9 fuehrt fuer den Builder eine klare Trennung zwischen technischen Labels (`Node Key`, `Node Type`, `Condition Expression`, `Input Mapping (JSON)`) und sichtbarer Produktsprache ein; keine Umlaute mehr in code-nahen Feldern und Validierungsnachrichten.
- T11 blendet die alte Process-Type-/Template-/Answer-Definition-Konfiguration im Admin-Workspace aus; Builder + Definition Layer sind jetzt der sichtbare Pflegepfad.

## Cleanup Rule

- Eintraege entfernen, sobald sie fuer die naechsten Sessions nicht mehr helfen.
- Keine historischen Zusammenfassungen hier sammeln.
