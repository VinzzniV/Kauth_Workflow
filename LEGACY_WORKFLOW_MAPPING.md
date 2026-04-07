# LEGACY_WORKFLOW_MAPPING.md

## Zweck

Dieses Artefakt dokumentiert den T6-Mapping-Schnitt zwischen der bisherigen lifecycle-/task-getriebenen Logik und dem neuen Workflow-Definition-Layer.

Der Fokus liegt bewusst nur auf:
- `onboarding`
- `offboarding`
- `department_change`

Alle drei werden in T6:
- parallel zur Legacy-Welt eingefuehrt
- als publizierte Definitionen bereitgestellt
- nur ueber die neue Admin-Runtime nutzbar
- noch nicht in den produktiven Legacy-Create-Pfad umgehaengt

Es gibt in T6 bewusst:
- kein Feature-Flag-System
- keinen Produkt-Umschaltpunkt
- keine Parallel-Splits/-Joins
- keine Automation-/Notification-Nodes
- keine echte Task-System-Anbindung aus der Node-Runtime

## Gemeinsame T6-Regeln

- `definition_key` entspricht dem jeweiligen Legacy-`process_type.key`.
- Der erste `form`-Node nutzt immer `legacyProcessTypeKey`, damit die bestehende Answer-Logik weiterverwendet wird.
- Jeder `task`-Node nutzt `legacyTemplateKey`, damit die heutige fachliche Bedeutung der Task-Templates lesbar bleibt.
- Bedingungen aus `task_template_conditions` werden in `decision`-Nodes ueberfuehrt.
- Dependencies aus `task_template_dependencies` werden in eine linearisierte Node-Reihenfolge umgebaut.
- Abweichungen zur heutigen Legacy-Parallellogik werden explizit benannt und akzeptiert.

Migrationsstrategie fuer alle drei:
- `parallel`
- Legacy bleibt produktive Altversion
- neue Definition ist admin-only runtime path
- kein Feature-Flag in T6

## Onboarding

### Legacy-Ausgangslage

- `process_type`: `onboarding`
- Einstieg heute ueber supervisor-gesteuerten Anforderungsschritt
- Requirements in `workflow_answer_definitions` plus Visibility-/Reset-/Validation-Regeln
- grosse Menge bedingter Task-Templates fuer AD, Rechte, Mail, Fachsysteme, Hardware und Fachanwendungen
- Dependencies modellieren heute Teilketten und implizite Parallelitaet

### T6-Zielabbildung

- `start -> form(onboarding)`
- `supervisor_fills_document` wird fachlich in den `form`-Node gezogen
- danach lineare Decision-/Task-Kette fuer:
  - AD-Account
  - Vergleichsuser-Rechte
  - Mailbox
  - Habel
  - LN
  - Internet
  - Laufwerksrechte
  - Office
  - Hardware
  - Telefon
  - Catia
  - Datev
  - Tisoware
  - Babtec
  - Gewatec
  - Provis
  - Consense

### Bekannte T6-Abweichungen

- heutige Parallelitaet zwischen mehreren Fachbereichs-/IT-Aufgaben wird linearisiert
- `supervisor_fills_document` ist kein eigener `approval`- oder `task`-Node mehr
- Hardware-/Systemzweige laufen nacheinander statt parallel

## Offboarding

### Legacy-Ausgangslage

- `process_type`: `offboarding`
- Requirements sind `ob_*`-Antworten
- explizite Gate-Aufgabe `ob_last_day_confirmed`
- bedingte Tasks fuer Account-Deaktivierung, Hardware-Rueckgabe und Fachsysteme
- `ob_mailbox_disable` haengt von `ob_ad_account_disable` ab

### T6-Zielabbildung

- `start -> form(offboarding)`
- `task(ob_last_day_confirmed)` als frueher Gate-Node
- danach linearisierte Sequenz fuer:
  - optionales Austrittsgespraech
  - optionalen Wissenstransfer
  - Badge-/Schluesselrueckgabe
  - AD
  - Mail
  - Habel
  - LN
  - Hardware
  - Telefon
  - Babtec
  - Gewatec
  - Provis
  - Consense

### Bekannte T6-Abweichungen

- heutige gleichzeitige Deaktivierungs-/Rueckgabeaufgaben werden nacheinander abgearbeitet
- `ob_badge_key_return` bleibt in der Kette, statt parallel zu anderen Ruecknahmeaufgaben zu laufen

## Department Change

### Legacy-Ausgangslage

- `process_type`: `department_change`
- Requirements sind `dc_*`-Antworten
- HR-Aufgaben:
  - `dc_hr_system_update`
  - `dc_change_date_confirmed`
- danach bedingte Aufgaben fuer AD, Laufwerke, Mail, Hardware und Fachsysteme

### T6-Zielabbildung

- `start -> form(department_change)`
- `task(dc_hr_system_update)`
- `task(dc_change_date_confirmed)`
- danach linearisierte Decision-/Task-Kette fuer:
  - AD-Gruppen
  - Laufwerke
  - Mail
  - Hardware
  - Habel
  - LN
  - Babtec
  - Gewatec
  - Provis
  - Consense

### Bekannte T6-Abweichungen

- gleichzeitige Zugangs- und Systemanpassungen werden sequenziell modelliert
- es gibt noch keinen separaten Mechanismus fuer fachlich parallele Teilpfade

## Ausblick nach T6

T6 ist absichtlich nur der erste Mapping-Schnitt.

Die heute bewusst noch offenen Punkte gehen in spaetere Phasen:
- echte Task-System-Anbindung aus der Node-Runtime in T7
- weitere Entkopplung vom Onboarding-Kern in T8
- Automation-/Notification-Nodes in T9
- Parallel-Splits/-Joins und Builder-Ausbau spaeter
