# Admin-Gated Automation — Zielbild für Prod

#architektur #automation #zielbild

Wie Automation in der produktiven Nutzung ablaufen soll. Diese Datei beschreibt das Zielbild, nicht den Ist-Stand. Der heutige End-to-End-Pfad (Engine-driven, siehe [[Automation]]) ist die technische Grundlage, aber **nicht** die Form, in der Fachbereiche damit arbeiten werden.

---

## Der Kerngedanke

> **Eine Automation ist ein Plan, kein Befehl.**
>
> Der Plan wird vom System aus Workflow-Daten erzeugt, vom Admin in einer Vorschau reviewt, mit Passwort bestätigt und erst dann ausgeführt. Plan, Bestätigung und Ergebnis sind alle Audit-Artefakte und werden in der 360°-Mitarbeiterkarte sichtbar.

Damit ist klar:

- Workflows bleiben strukturell wie heute (`onboarding`, `offboarding`, `department_change`, `name_change`, …). HR und Abteilungsleitung füllen Anforderungen aus, daraus entstehen Aufgaben für die Fachbereiche.
- Aufgaben für Fachbereiche, die heute manuell durchgeführt werden (AD-Anlage, Gruppen-Zuweisung, Mailbox, Berechtigungen, Hardware-Vorbereitung), bekommen optional eine **Automation-Empfehlung**.
- Nicht jeder Task ist automatisierbar. Pro Task-Template wird konfiguriert, ob eine Automation hinterlegt ist.
- Automationen werden **nie** automatisch oder durch Nicht-Admins gestartet. Immer Admin-Review + Passwort-Bestätigung.

---

## Der Ablauf am Beispiel Onboarding

```
HR legt Onboarding an
        │
        ▼
Abteilungsleitung wählt Anforderungen
   (Position, Ordnerberechtigungen, Hardware,
    Programme, Referenzuser, …)
        │
        ▼
Workflow generiert Tasks für die Fachbereiche
        │
        ├─ Task "AD-Anlage"  (IT-Admin)
        │       ▲
        │       └─ trägt Automation-Empfehlung:
        │             CreateAdUserLdaps + AssignGroupsLdaps
        │
        ├─ Task "Mailbox einrichten"  (IT-Admin)
        │       └─ trägt CreateMailboxGraph
        │
        ├─ Task "Hardware vorbereiten"  (Fachbereich)
        │       └─ manuelle Aufgabe, keine Automation
        │
        └─ Task "Programme zuweisen"  (IT-Admin)
                └─ ggf. Automation
                ▼
IT-Admin öffnet einen Task
        │
        ▼
System sammelt Plan aus Workflow-Daten:
   • Vorname, Nachname, Position, Personalnummer
   • Referenzuser → Gruppen, ggf. Lizenz-SKU
   • Ordner-Berechtigungen, Beschreibung
        │
        ▼
Admin sieht VORSCHAU (WhatIf):
   "Folgendes wird passieren wenn du bestätigst:
   1. AD-User wird angelegt:
        DN: CN=max.mustermann,OU=Vertrieb,DC=...
        UPN: max.mustermann@example.com
        SAM: max.mustermann
        Attribute: Position=Vertriebsmitarbeiter,
                   Department=Vertrieb DE,
                   Manager=anna.beispiel
        Passwort: 16 Zeichen, Force-Change beim ersten Login
   2. Gruppen-Mitgliedschaften:
        + Vertrieb-DE           (von Referenzuser Anna)
        + CRM-User              (von Referenzuser Anna)
        + Office365-Standard    (von Position)
        + Drucker-Vertrieb-3OG  (von Standort)
   3. Mailbox-Lizenz:
        SKU: Office 365 E3 (...)
        Mailbox wird automatisch von Exchange provisioniert
        Adresse: max.mustermann@example.com
   4. Welcome-Mail an:
        max.mustermann@example.com
        mit Initial-Passwort"
        │
        ▼
Admin entfernt einzelne Gruppen, korrigiert Position, …
        │
        ▼
Admin bestätigt mit PASSWORT  (Re-Auth gegen Entra)
        │
        ▼
Ausführung läuft (mit Live-Log: was passiert gerade,
gibt es Fehler, Retry-Versuch?)
        │
        ▼
Post-Execution-SUMMARY für den Admin:
   "Ausgeführt am 2026-06-12 14:23, Dauer 47 s
    ✓ AD-User angelegt, DN: ...
    ✓ 4 Gruppen zugewiesen, 0 fehlgeschlagen
    ✓ Lizenz zugewiesen, Mailbox provisioniert nach 38 s
    ✓ Welcome-Mail versendet
    Vollständiger Identitäts-Snapshot: → 360°-Karte"
        │
        ▼
Task automatisch auf "abgeschlossen"
        │
        ▼
Workflow endet, wenn alle Fachbereich-Tasks durch sind
```

---

## Die Bausteine, die für dieses Zielbild noch fehlen

Was wir heute schon haben (siehe [[Automation]] und [[Hybrid-Worker-Sub-Architektur]]) ist die technische Infrastruktur unter dem Plan-Bestätigungs-Modell — Worker, Vault, Mapping-Sources, Failure-Klassifikation, Output-Verkettung. Was fehlt:

### 1. Pre-Execution-Plan (WhatIf-Modus)

Jeder Handler braucht eine zweite Ausführungs-Variante, die **ohne** AD-/Graph-Write berechnet, was er tun würde, und das als strukturierten Plan zurückgibt. Wie `-WhatIf` in PowerShell.

- **`CreateAdUserLdaps.PlanAsync(input)`** → liefert `AdUserPlan { TargetDn, Upn, Sam, Attributes, GeneratedPassword }`. Kein LDAP-Write. Read-only Pre-Search bleibt aber drin, damit der Plan ehrlich sagt: „User existiert schon, Plan würde `alreadyExisted=true` ergeben".
- **`AssignGroupsLdaps.PlanAsync(input)`** → liefert `GroupAssignmentPlan { Adds: [{group, source: 'reference-user' | 'position' | 'manual', alreadyMember?: bool}], Removes: [] }`. Wichtig: jede Gruppe trägt eine Begründung („kommt von Anna" / „Position Vertriebsmitarbeiter" / „manuell ergänzt").
- **`CreateMailboxGraph.PlanAsync(input)`** → liefert `MailboxPlan { SkuId, SkuDisplayName, ExpectedSmtp? }`. SKU-Name auflösen (heute nur GUID).
- **`SendWelcomeMailGraph.PlanAsync(input)`** → liefert `WelcomeMailPlan { Recipient, Subject, RenderedBodyPreview }` (mit Platzhalter-Werten, ohne das Klartext-Passwort).

Plan-Mode darf keine Side-Effects haben außer Read-Calls und Random-Generierung (Passwort).

### 2. Task-Automation-Binding

Ein `task`-Node trägt heute keine Automation-Information. Die Automation hängt an einem eigenen `automation`-Node.

Zielbild: Ein `task`-Node kann **optional eine oder mehrere Action-Definitions** als Bündel referenzieren. Wenn vorhanden, wird der Task in einer Admin-UI mit dem Plan-Bündel gerendert statt nur als Checkbox „erledigt".

Datenmodell-Skizze:
```
workflow_node_actions          (existiert) — heute an automation-Nodes
                                          → künftig auch an task-Nodes
task_template + task_node       (existieren) — bekommen ein optionales
                                              `automation_admin_role`-Feld
                                              (welcher Admin darf bestätigen)
```

Engine-driven `automation`-Nodes bleiben gültig für vollautomatische Use-Cases (z. B. interner System-Trigger ohne Fachbereich-Beteiligung).

### 3. Re-Auth-Gate

Vor Ausführung des Plans gibt der Admin sein Passwort ein. Backend prüft gegen Entra (oder den konfigurierten IdP) und bindet die Ausführung an diesen Re-Auth-Token. Token ist kurzlebig (z. B. 60 s), nicht wiederverwendbar.

Ein Re-Auth deckt entweder einen einzelnen Plan ab oder ein zusammenhängendes Plan-Bündel pro Task. Mehrere Tasks in einem Schritt zu bestätigen ist möglich, aber muss explizit so im UI angeboten werden („Alle 3 Pläne bestätigen") — keine implizite Auto-Eskalation.

Audit-Eintrag: wer (Admin-User) hat wann welchen Plan freigegeben, mit welchem Re-Auth-Token-Identifier.

### 4. Referenzuser-Mapping

Neue Mapping-Source `reference_user` mit Property-Whitelist:
- `reference_user.groups` → Liste der AD-Gruppen-Mitgliedschaften des Referenzusers
- `reference_user.licenseSkus` → zugewiesene Lizenz-SKUs

Beim Onboarding-Formular wählt HR/Abteilungsleitung optional einen Referenzuser. Der Plan zeigt dem Admin pro Gruppe die Herkunft („von Anna", „von Position-Profil", „manuell").

### 5. Action-Bündelung im Task

Ein IT-Admin-Task „AD-Anlage" sollte alle Actions zusammenfassen, die fachlich zur AD-Anlage gehören:
- `CreateAdUserLdaps` (User anlegen)
- `AssignGroupsLdaps` (Gruppen zuweisen)

Der Admin sieht ein **kombiniertes** Plan-UI, nicht zwei Einzel-Pläne. Backend führt sequenziell aus, Fehlerverhalten ist klar definiert: bei Fehler in Schritt N ist Schritt N-1 schon passiert (kein Rollback bei AD-Operationen möglich). Summary zeigt: „User angelegt, Gruppen-Assignment fehlgeschlagen — manueller Eingriff nötig". Der Task bleibt offen, der Admin sieht in der Summary klar den Teil-Zustand.

### 6. Post-Execution-Summary in der 360°-Mitarbeiterkarte

Heute liegt das Ergebnis verstreut in `automation_job_attempts.output_json` pro Workflow-Instance. Im Zielbild gibt es eine **person-zentrierte Aggregator-Sicht** in `/people/:personId`:

- **Identitäts-Snapshot**: aktueller DN, UPN, SAM, Attribute, Manager, Department, OU
- **Gruppen-Mitgliedschaften** mit Provenienz (welcher Workflow/Task hat welche Gruppe gesetzt, wann)
- **Mailbox-Stand**: SMTP, Lizenz, Provisionierungs-Zeit
- **Workflow-Spur**: chronologisch alle Workflows + Automation-Ausführungen, die diese Person betroffen haben
- **Initial-Passwort-Status**: gesetzt am, in Vault bis (TTL), abgelaufen, ggf. Resend-Aktion

Diese Sicht ist read-only (keine direkte Bearbeitung — Änderungen laufen immer über einen Workflow), aber Operations-fähig (Admin sieht den Stand und kann nachvollziehen).

### 7. Live-Log während Ausführung

Wenn der Admin den Plan bestätigt hat und die Automation läuft, sieht er live:
- aktueller Schritt
- erfolgreiche Operationen
- Fehler mit Klassifikation (`permanent` vs `transient`) und Retry-Anzeige

Nach Abschluss: strukturierte Summary (siehe Punkt 6).

### 8. Automation für Änderungs-Workflows

Bisher liegt der Fokus auf Onboarding (Create). Für Change-Workflows brauchen wir eigene Handler:

| Workflow | Neue Handler |
|---|---|
| Department-Change | `MoveAdUserOuLdaps`, `UpdateAdUserAttributesLdaps` (Manager, Description, Title, Department), `SyncGroupMembershipsLdaps` (Diff alt→neu mit Plan-Vorschau) |
| Name-Change | `RenameAdUserLdaps` (`samAccountName`, `userPrincipalName`, Anzeigename) — heikel, weil UPN-Wechsel Folgen in Entra/Exchange hat (alter UPN als Alias?) |
| Offboarding | `DisableAdUserLdaps`, `RemoveMailboxLicense`, `RemoveFromAllGroupsLdaps` (mit Plan-Vorschau der entfernten Gruppen) |

Jeder Handler braucht ebenfalls einen Plan-Mode (Punkt 1).

---

## Was sich am bestehenden Code ändert

Klein und additiv:

- `automation`-Node bleibt — für vollautomatische Use-Cases ohne Admin-Approval (z. B. interne Trigger)
- `task`-Node bekommt optionale Automation-Bindung
- Handler-Interface bekommt eine zweite Methode: `PlanAsync(input) → Plan` zusätzlich zu `ExecuteAsync(input) → Result`
- Mapping-Resolver wird auch beim Plan-Build aufgerufen — Plan zeigt damit die echten resolved Werte
- Vault bleibt unverändert: Initial-Passwort wird beim Plan-Build generiert und im Plan **maskiert** angezeigt („wird ein 16-stelliges Passwort erzeugt"), beim Execute landet es im Vault wie heute
- Re-Auth ist neue Querschnitts-Funktion (Endpoint + UI + Audit)
- 360°-Karte bekommt Automation-Spur — eigener Aggregator-Read aus `automation_job_attempts` plus Live-Anzeige des aktuellen Identitäts-Standes

Größer, aber zeitlich entkoppelt:

- Change-/Offboarding-Handler (je eigener Slice mit eigenem Plan-Mode-Design)
- Referenzuser-Mapping-Source als eigener Slice
- Bundle-Task-UI im Builder

---

## Reihenfolge fürs spätere Aufgreifen

Wenn das Zielbild umgesetzt wird, ist die natürliche Reihenfolge:

1. **Plan-Mode für die vier bestehenden Handler** — additiv, kein Bruch
2. **Re-Auth-Endpoint + UI-Baustein** — querschnitt, separat testbar
3. **Task-Automation-Binding** (Schema + Builder + Engine, Bundle-Plan-Rendering)
4. **360°-Karte-Aggregator** — kann unabhängig in Read-only laufen
5. **Referenzuser-Mapping-Source** — entkoppelt
6. **Change-/Offboarding-Handler** — pro Anwendungsfall eigener Slice

Die Punkte 1–4 sind die eigentliche Architekturlinie. 5–6 sind Inhalts-Slices, die auf 1–4 aufbauen.

Vor Beginn jedes Slices ein eigener Plan-Mode mit Stakeholder-Klärung — die Detail-Entscheidungen (z. B. Re-Auth-Lebensdauer, Bundle-Semantik bei Teilfehler, Referenzuser-Auswahl-UI) sind alle nicht trivial.

---

## Umsetzungsplan — Modell & Reasoning-Empfehlung pro Slice

Stand 2026-05-15. Basis: Etappe 9a Schritte 1–8 abgeschlossen. Die technische Infrastruktur (Worker, Vault, Handler, Engine) steht. Was fehlt ist die Admin-UX-Schicht: Plan → Review → Re-Auth → Ausführung → Summary.

### Hauptslices (Architekturlinie)

| Nr. | Slice | Modell | Reasoning Effort | Plan Mode | Begründung |
|---|---|---|---|---|---|
| 1 | **Pre-Execution-Plan (WhatIf)** | Sonnet | **mittel** | Ja — für Interface-Design; danach direkt pro Handler | `PlanAsync`-Vertrag muss einmal sauber entworfen werden (was darf Read-only, was nicht). Danach ist jeder der 4 Handler mechanische Erweiterung. |
| 2 | **Task-Automation-Binding** | **Opus** | **hoch** | **Zwingend** | Berührt Schema (Migration), Builder-UI und Engine gleichzeitig. Falsches Schema zieht nachträgliche Migration nach sich; bestehende Workflow-Instanzen dürfen nicht brechen. Höchstes Architekturrisiko aller Slices. |
| 3 | **Re-Auth-Gate** | **Opus** | **hoch** | **Zwingend** | Sicherheits-kritisch und Querschnitt (Endpoint, UI-Baustein, Audit-Eintrag). Entra-Re-Auth hat Fallstricke (Token-Lebensdauer, Replay, PKCE vs. OBO). Falsch umgesetzt ist es Security-Theater statt echter Schranke. |
| 4 | **360°-Karte-Aggregator** | Sonnet | **niedrig–mittel** | Kurz — für SQL-Design | Hauptarbeit ist Aggregator-Query über `automation_job_attempts` + neues Read-only-Frontend. SQL-Aggregation braucht Planungsrunde (Indexes, N+1), der Rest ist geradliniges CRUD + Page. |
| 5 | **Referenzuser-Mapping** | Sonnet | **mittel** | Ja — für Source-Design und Whitelist | Klar begrenzter Slice nach vorhandenem Muster. Plan Mode einmal für die Whitelist-Entscheidung (welche Properties erlaubt), dann direkte Impl. |
| 6 | **Action-Bündelung im Task (UI)** | Sonnet | **niedrig** | Nein (setzt 1 + 2 voraus) | UI-Rendering-Arbeit, sobald Schema + Plan-Vertrag aus 1/2 stehen. Kein Architekturrisiko, mechanische Erweiterung. |
| 7 | **Live-Log während Ausführung** | Sonnet | **mittel** | Ja — für Protokoll-Wahl (SSE vs. Polling) | Entscheidung SSE vs. Polling hat Folgen für Backend und UI. Einmal entschieden ist die Umsetzung geradlinig. |

### Change-/Offboarding-Handler (Inhalts-Slices, bauen auf 1–4 auf)

| Handler | Modell | Reasoning Effort | Plan Mode | Hinweis |
|---|---|---|---|---|
| `DisableAdUserLdaps` | Sonnet | **niedrig** | Nein | Klar begrenzter LDAPS-Write, Muster aus `CreateAdUserLdaps` übertragbar. |
| `RemoveFromAllGroupsLdaps` | Sonnet | **niedrig** | Nein | Spiegel zu `AssignGroupsLdaps`; Plan-Vorschau der entfernten Gruppen. |
| `RemoveMailboxLicense` | Sonnet | **niedrig** | Nein | Spiegel zu `CreateMailboxGraph`, klares Graph-API-Äquivalent. |
| `MoveAdUserOuLdaps` + `UpdateAdUserAttributesLdaps` | Sonnet | **mittel** | Kurz — für Whitelist + Drift-Schutz | Manager/Title/Department-Attribute: Whitelist und Drift-Schutz analog Schritt 5 designen. |
| `RenameAdUserLdaps` | **Opus** | **hoch** | **Zwingend** | UPN-Wechsel hat Folgewirkungen in Entra/Exchange (alter UPN als Alias?), ggf. SMTP-Adress-Drift. Heikelster Handler — Risikoanalyse vor Code. |

### Kleinere offene Punkte

| Punkt | Modell | Reasoning Effort | Plan Mode | Hinweis |
|---|---|---|---|---|
| Builder-UI für `automation_output`-Bedingungen | Sonnet | **niedrig** | Nein | Source-Dropdown + ConditionProperties-Filter nach bekanntem Muster; heute nur per JSON/Dev-Seed konfigurierbar. |
| Vault-Cleanup-Sweeper | Haiku | **niedrig** | Nein | TTL-getriebener Delete-Job, enge Anforderung, kein Architektureinfluss. |
| Key-Rotation | Sonnet | **mittel** | Kurz | `key_version`-Spalte + Multi-Decrypt-Fan-Out hat Fehlerquellen; kurze Planungsrunde nötig. |
| `CreateErpEmployee` (InforLN) | **Opus** | **hoch** | **Zwingend** | Wartet auf Stakeholder-Entscheidung. InforLN-Anbindung ist unbekanntes Terrain — Risikoabschätzung vor Umsetzung. |

### Faustregeln

| Reasoning Effort | Wann |
|---|---|
| **hoch** | Sicherheits-kritisch, Schema-Migration mit Folgewirkung, unbekanntes Integrations-Terrain, mehrere Schichten gleichzeitig berührt |
| **mittel** | Klarer Scope, aber Entwurfs-Entscheidung am Anfang nötig; vorhandenes Muster mit nicht-trivialem Whitelist/Vertrags-Design |
| **niedrig** | Muster existiert bereits, Arbeit ist mechanische Erweiterung oder isolierter Infrastruktur-Job |

- **Opus** — hoch + Plan Mode zwingend
- **Sonnet** — mittel oder niedrig, je nach Slice
- **Haiku** — niedrig, isoliert, kein Architektureinfluss

---

## Verwandte Notizen

- [[Automation]] — Ist-Stand der Automation-Infrastruktur (technische Grundlage)
- [[Hybrid-Worker-Sub-Architektur]] — Wo die Handler heute laufen
- [[Entscheidungen]] — Warum Automation kontrolliert und nicht frei ist
- [[Workflow]] — Workflow-Modell mit Nodes + Tasks
- [[Identity]] — Person als Anker, 360°-Karte als Ort für die Summary
