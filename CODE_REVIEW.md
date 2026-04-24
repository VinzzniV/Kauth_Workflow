# Code Review — kauth_workflow

**Reviewer**: Claude (Senior Software Architect Perspective)  
**Datum**: 2026-04-23  
**Scope**: Vollständige Backend- und Frontend-Analyse

---

## 1. Funktionsanalyse — Was kann das System wirklich?

### Vorhandene Features

| Feature | Zweck | Zielnutzer | Nutzen |
|---------|-------|------------|--------|
| Workflow-Erstellung | Standardisierte Onboarding/Offboarding-Prozesse | HR | Reproduzierbare Abläufe |
| Workflow-Antworten | Formulardaten pro Instanz | HR | Anforderungserfassung |
| Aufgabensystem | Task-Generierung, -Zuweisung, -Kommentierung | IT, Fachabteilung | Arbeitsverteilung |
| Supervisor-Schritt | Freigabeschritt mit Genehmigung | Manager | Kontrollpunkt |
| Abteilungsdurchlauf (Rotation) | Planung und Durchführung von Stellenwechseln | HR, IT, Abteilungen | Strukturierter Wechselprozess |
| Rotations-Aufgaben | Generierte Aufgaben pro Station/Template | IT, Fachabteilung | Automatische Aufgabenverteilung |
| Admin-Konfiguration | Rollen, Zuständigkeiten, Berechtigungen | Admin | Systemsteuerung |
| Workflow-Builder | Visuelle Definition von Workflows | Builder-Admins | Prozessgestaltung |
| Automatisierung | Job-Queue für automatische Aktionen | System | Reduzierung manueller Schritte |
| Verzeichnis-Sync | Entra/AD-Synchronisation | Admin | Identity-Integration |
| Mail-Vorlagen | Konfigurierbare E-Mail-Benachrichtigungen | Admin | Kommunikation |
| System-Log | Zentrales Event-Logging | Admin | Nachvollziehbarkeit |
| Personen-Verwaltung | Mitarbeiter-Datensätze, Lifecycle-Verlauf | HR | Personenstammdaten |

### Redundante / problematische Features

- **Doppelte Prozesslogik**: `process_types` (Legacy) und `workflow_definitions` (neu) existieren parallel — keine klare Abgrenzung, wann welches greift.
- **Doppelter Routing-Code**: `tasks/ref/{taskRef}` unterstützt `wf:*` und `rot:*` — gute Abstraktion, aber dahinter liegen zwei völlig getrennte Codepfade ohne gemeinsame Basis.
- **Simulation-Login**: `AUTH_MODE=dev-sim` nur für Entwicklung gedacht, aber kein Guard, der verhindert, dass es versehentlich in Prod aktiviert wird.
- **`completed-onboardings`-Endpunkt**: Legacy-Bezeichnung, misleading als Rotationsquelle; nicht klar aus dem Pfad ersichtlich.

### Fehlende Kernfunktionen

- **Keine Stations-Überschneidungsprüfung**: Zwei Stationen desselben Plans können sich zeitlich überschneiden — kein Code- oder DB-Guard dagegen.
- **Kein Datenbereinigungsmechanismus**: Draft-Pläne, abgebrochene Stationen, stornierte Aufgaben akkumulieren unbegrenzt.
- **Kein DAG-Validierungscheck vor Publish**: Workflow-Definitionen können mit offenen Pfaden veröffentlicht werden.
- **Keine Benutzerlösch-Logik beim Sync**: Entra-gelöschte User werden nicht deaktiviert.

---

## 2. Nutzersicht — Ist das sinnvoll nutzbar?

### Klare Workflows

- **HR-Workflow-Erstellung**: Klar — Workflow anlegen, Antworten ausfüllen, Aufgaben abarbeiten.
- **Rotation planen**: Klar — Person wählen, Stationen anlegen, Aufgaben generieren.
- **Admin-Konfiguration**: Funktionell vollständig, aber 46 Admin-Komponenten ohne klare Informationsarchitektur.

### Sackgassen und unklare Zustände

- **Rotation ohne Zuständigkeit**: ✓ Behoben (2026-04-24) — `DefaultResponsibilityId` ist jetzt Pflichtfeld. Bestehende Templates ohne Zuständigkeit zeigen weiter eine Warnung in der Admin-Ansicht.
- **Workflow-Builder ohne Publish-Validierung**: Man kann einen Workflow bauen und veröffentlichen, der logisch unvollständig ist (kein Exit-Knoten geprüft).
- **Automation-Keys ohne Binding**: Ein Template mit falschem `automation_key` schlägt erst beim Job-Execute fehl — kein Fehler beim Speichern.
- **Filter "Wechsel in X Tagen"**: War bis zum letzten Fix auf die gesamte Aufgabenliste angewendet — hat alle Aufgaben jenseits von 14 Tagen versteckt.
- **Auth-Simulation**: `SimulationLoginPage` zeigt statische Benutzer — kein Hinweis, was welcher User kann. Neue Entwickler müssen raten.

### Inkonsistente Bedienung

- **Zwei Task-Typen, eine UI**: Workflow-Aufgaben und Rotations-Aufgaben sehen identisch aus (`/tasks/ref/`), haben aber unterschiedliche Felder und Bearbeitungsrechte.
- **Audit-Log**: Für Workflows vorhanden, für Rotation separat — keine gemeinsame Ansicht.
- **Mail-Vorlagen**: 6 vordefinierte Typen, kein Hinweis welcher Typ wann ausgelöst wird.

---

## 3. Funktionsprüfung — Funktioniert das logisch?

### Aufgabensichtbarkeit (Rotation)

**Problem**: `GetTasksAsync` lädt alle Tasks aus der DB, filtert dann in-memory via `CanUpdateTaskStatus`. Für Rotationsaufgaben prüft `MatchesTaskAssignment()` die primäre Zuweisung (`rotation_task_assignments WHERE is_primary=TRUE`). Wenn kein Eintrag vorhanden, ist die Aufgabe für alle Nicht-Admins unsichtbar. Diese Logik ist korrekt, aber fehleranfällig durch Template-Konfigurationsfehler (NULL responsibility).

**Risiko**: Skalierungsproblem. Alle Aufgaben werden aus der DB geladen, dann gefiltert. Bei 10.000+ Aufgaben blockiert das.

### Personen-Matching beim Onboarding

**Methode**: `GetCompletedOnboardingSource()` nutzt `LATERAL JOIN` mit Employee-Number-Fallback.  
**Risiko**: Wenn Personendaten unvollständig (kein employee_number), kann das falsche Person matchen. Kein Log-Eintrag, kein Audit-Trail für die Matching-Entscheidung.

### Transaktionssicherheit

- `RotationPlan`-Erstellung: Plan anlegen + Stationen anlegen — kein expliziter Transaktions-Scope. Wenn Station-Insert fehlschlägt, existiert verwaister Plan.
- `SynchronizeRotationGeneratedTasks`: Erstellt/updated/cancelt Tasks — mehrere SQL-Statements ohne explizite Transaktion im Repository-Code sichtbar.

### Automatisierungsjobs

- 3 Versuche, fest codiert: 1min, 5min, 5min.
- Kein Dead-Letter-Queue, kein Alert bei dauerhaftem Fehler.
- Payload-Schema nicht beim Speichern validiert.

### Notification-Templates

- Platzhalter (`{{PERSON_NAME}}` etc.) werden zur Laufzeit ersetzt.
- Kein Check beim Template-Speichern, ob Platzhalter zur Datenstruktur passen.
- Render-Fehler erst beim Versenden sichtbar.

---

## 4. Architektur & Design

### Trennung UI / Logik / Datenhaltung

**Backend: Grundsätzlich sauber**
- Services kapseln Fachlogik
- Repositories trennen SQL-Zugriff
- Endpoints sind thin (keine Logik, nur Routing + Auth)

**Hauptproblem: Monolithisches Repository**

`PostgresWorkflowRepository.cs` ist über 35 Partial-Files mit **23.758 Zeilen** verteilt. Eine Klasse. Enthält:
- Workflow-CRUD
- Runtime-Events
- Rotations-Generierung (1.444 Zeilen)
- Automatisierungs-Jobs
- Notifications
- Audit-Logs
- Personen-Matching

Das ist kein Repository-Pattern — das ist eine Daten-God-Klasse. Unit-Tests sind strukturell unmöglich, ohne alles zu mocken.

**Frontend: Zustand-Management-Lücke**

- TanStack Query für Server-State: korrekt eingesetzt.
- Lokaler UI-Zustand in großen Pages (564–617 Zeilen): zu viel.
- `RotationOperationsPage.tsx` und `RotationPlanDetailPage.tsx` handhaben State, Filter, Berechnungen und Rendering in einer Komponente.
- Keine Error-Boundaries sichtbar — ein Render-Fehler bricht die gesamte Seite.

**Versteckte Abhängigkeiten**

- `AuthorizationPolicyService.cs` kennt `EffectiveResponsibilities` direkt. Wenn das Modell sich ändert, bricht die Policy.
- `RotationTaskGenerationOperations.cs` kodiert `"enter"`/`"exit"` als Strings, nicht als enum — Tippfehler erzeugen Silent Failures.

---

## 5. Realitätscheck (Kritisch)

### Overengineered

- **Workflow-Builder mit Canvas-UI**: Visueller DAG-Builder für Workflows, die im Moment noch hauptsächlich manuell von Admins verwaltet werden. Aufwand vs. Nutzen unklar, da die Nutzerbasis vermutlich keine drag-and-drop Workflow-Konfiguratoren erwartet.
- **Dual-Mode Auth (dev-sim + entra)**: Sinnvoll für Entwicklung, aber die Sim-Schicht ist zu komplex — statische User, fest kodierte Rollen. Würde eine einfache .env-getriebene Fallback-Logik reichen?
- **Automatisierungs-Layer mit Handler-Registry**: Für zwei simulierte Handler (CreateAdUser, SendWelcomeMail) ist eine Registry mit Retry-Queue und Versuch-Tracking massiv übergebaut. Noch kein echter Handler existiert.

### Unnötig komplex

- **RotationTaskGenerationOperations.cs** (1.444 Zeilen): Die Regenerierungslogik könnte in einer domänenorientierten Service-Klasse mit klaren Methoden liegen. Stattdessen ist sie SQL + C#-Mapping ohne klare Phasengrenzen.
- **46 Admin-Komponenten**: Für eine interne Plattform mit kleiner Nutzerbasis viel UI-Komplexität. Viele Formulare haben ähnliche Struktur (CRUD), werden aber separat implementiert.
- **RotationPlan-Konfliktvalidierung**: Mehrschichtige Validierung (DB-Trigger + Service + API), jede Ebene prüft andere Aspekte — schwer zu verstehen welche Ebene was verhindert.

### Was kein Nutzer je anfassen wird

- **Workflow-Definition-Versioning**: Publish-Flow mit Versionsnummern und DAG-Replacement. In der Praxis ändern Admins vermutlich 1–2x pro Jahr eine Definition. Das Feature ist für Nutzungsfrequenz über-engineered.
- **`/client/log-events` Endpoint** ohne Auth: Frontend-Fehler landen im System-Log. Kein Missbrauchsschutz, kein Rate-Limiting sichtbar.

---

## 6. Offene Punkte (nach Review-Zyklus 2026-04-23)

Alle priorisierten Aufgaben (COD-1..6, CLA-1..4) sind abgeschlossen. Folgende Punkte wurden bewusst ausgeklammert oder entstanden als Folgearbeit:

### Nicht im Zyklus adressiert

- **C2** (HIGH): ✓ erledigt (2026-04-24) — `DefaultResponsibilityId` ist jetzt Pflichtfeld in Service + Frontend. SQL-Sichtbarkeitsfilter korrigiert (`rta.responsibility_id` → `rta.assignee_responsibility_id`, `rta.user_id` → `rta.assignee_user_id`).
- **H6** (HIGH): `RotationTaskRegenerationEngine` aus `PostgresRotationRepository.TaskGenerationOperations.cs` als reinen Domain-Service extrahieren — Regenerierungslogik ist weiter im Repository eingebettet, Unit-Tests ohne DB nicht möglich.
- **L1**: Hardcodierte Retry-Delays (1m, 5min, 5min) nicht konfigurierbar.
- **L2**: Datenbereinigung für Drafts/abgebrochene Pläne fehlt.
- **L3**: `/client/log-events` ohne Rate-Limiting.
- **L5**: Permission-Audit ohne Begründungsfeld.
- **L6**: `AUTH_MODE=dev-sim` kein Guard gegen Prod-Aktivierung.
- **L7**: `WorkflowBuilderPage` + Canvas-Feature für vermutlich sehr seltene Nutzung übergebaut.

---

## 7. Gesamtbewertung

| Bereich | Note | Hauptbegründung |
|---------|------|-----------------|
| Backend-Architektur | **B-** | Gute Service-Trennung, aber monolithisches Repository und fehlende Transaktionen |
| Datenbankdesign | **A-** | Solides Schema, gute Constraints, klare Tabellen — leichte Redundanz in Sync-Triggern |
| Auth & Berechtigungen | **B** | Durchdacht, aber Kanten-Cases (gelöschte User, leere Zuständigkeiten) unbehandelt |
| Rotation-Feature | **B-** | Funktioniert, aber Kernlogik im falschen Layer (Repository statt Service) |
| Frontend-Architektur | **C+** | Seiten zu groß, fehlende Error-Boundaries, Filter-Bug (gefixt) belegt strukturelles Problem |
| Testbarkeit | **D** | Monolithisches Repository verhindert Unit-Tests; nur Integration-Tests sichtbar |
| Skalierbarkeit | **C** | In-Memory-Filter für Tasks ist ein strukturelles Scaling-Problem |
| Sicherheit | **B** | Auth konsistent umgesetzt; `/client/log-events` ohne Rate-Limiting ist ein kleines Leck |

**Stand nach Review-Zyklus 2026-04-23:** CRITICAL- und HIGH-Punkte des Zyklus abgeschlossen. Backend-Architektur und Sicherheit deutlich verbessert. Offene Punkte sind in Abschnitt 6 dokumentiert.
