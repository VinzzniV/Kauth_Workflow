# IMPLEMENTATION_PLAN_ROTATION_ONBOARDING.md

## Kontext

Im Onboarding-Projekt soll ein neuer fachlicher Bereich entstehen, mit dem HR den Abteilungsdurchlauf von Azubis, Studenten oder Praktikanten planen kann. Statt Excel-Plänen soll der gesamte Prozess strukturiert in der Software abgebildet werden.

Ziel ist nicht nur die Anzeige eines Zeitplans, sondern ein fachlich sauberes Modell aus:
- Person
- Durchlaufplan
- Stationen / Abteilungsphasen
- abteilungsabhängigen Maßnahmenvorlagen
- automatisch erzeugten Aufgaben
- Benachrichtigungen vor Wechseln
- später optionalen IT-Automatisierungen

Die erste Version soll stabil, nachvollziehbar und manuell nutzbar sein. Vollautomatisierung ist ausdrücklich **nicht** Teil des MVP.

---

## Produktziel

Das System soll ermöglichen, dass HR für eine Person einen Durchlauf über mehrere Abteilungen plant. Auf Basis dieser Stationen werden automatisch Maßnahmen erzeugt und zuständige Personen rechtzeitig informiert, bevor ein Wechsel stattfindet.

Beispiel:
- Person ist vom 01.06 bis 20.06 in Abteilung A
- vom 21.06 bis 31.08 in Abteilung B
- ab 01.09 in Abteilung C

Vor dem Wechsel nach C sollen zuständige Personen und insbesondere die IT automatisch Benachrichtigungen erhalten. In der Anwendung sollen dann die zugehörigen Aufgaben sichtbar sein, z. B.:
- neue Rechte setzen
- alte Rechte entfernen
- Software installieren
- Zugang für ein Fachsystem beantragen
- manuelle Hinweise bestätigen

---

## Wichtige fachliche Regeln

1. Das System modelliert **Zeitintervalle**, nicht nur Monatsfelder.
2. HR plant nur den fachlichen Durchlauf, nicht die technische Umsetzung.
3. Technische Aufgaben werden aus Vorlagen generiert.
4. Jede Abteilung kann Eintritts- und Austrittsmaßnahmen definieren.
5. Benachrichtigungen müssen vor einem Wechsel automatisch ausgelöst werden.
6. Alle Aufgaben müssen Status, Verantwortliche, Fälligkeit und Historie haben.
7. MVP zuerst ohne echte AD-/Rechte-/Software-Automatisierung.
8. IT-Automatisierung wird später als Erweiterung ergänzt.
9. Alle Änderungen müssen nachvollziehbar sein.
10. Keine Excel-Logik im UI nachbauen. Intern mit Stationen arbeiten, nicht mit Monatsraster.

---

## Begriffe / Domänenmodell

### Person
Repräsentiert die bestehende Zielperson im System. Fuer das MVP wird **keine** neue freie Rotations-Person angelegt; der Durchlauf startet nur fuer Personen, deren Onboarding bereits abgeschlossen ist.

Beispiel-Felder:
- id
- appUserId
- departmentId
- createdAt
- updatedAt

### RotationPlan
Gesamtplan einer Person.

Beispiel-Felder:
- id
- personId
- sourceWorkflowId
- title
- status (draft, active, completed, archived)
- createdByUserId optional
- createdAt
- updatedAt

### RotationStation
Eine Station im Durchlaufplan.

Beispiel-Felder:
- id
- rotationPlanId
- departmentId
- location optional
- startDate
- endDate
- orderIndex
- notes optional
- status

### Department
Fachabteilung wie Einkauf, Produktion, IT, QS, AV usw.

Beispiel-Felder:
- id
- name
- code
- isActive

### DepartmentActionTemplate
Vorlage, welche Maßnahmen bei Eintritt oder Austritt in dieser Abteilung anfallen.

Beispiel-Felder:
- id
- departmentId
- triggerType (enter, exit)
- title
- description
- taskType (manual, technical, approval, information)
- ownerRole / ownerGroup / ownerUser
- dueOffsetDays
- reminderOffsetDays optional
- isAutomatable
- automationKey optional
- isActive

### GeneratedTask
Aus einer Station und einer Maßnahmenvorlage erzeugte Aufgabe.

Beispiel-Felder:
- id
- personId
- rotationPlanId
- rotationStationId
- departmentId
- templateId optional
- title
- description
- taskType
- dueDate
- responsibleRef
- status (open, in_progress, completed, failed, cancelled)
- completionNote optional
- completedAt optional
- createdAt
- updatedAt

### Notification
Benachrichtigungen, die vor Wechseln oder bei Überfälligkeit versendet werden.

Beispiel-Felder:
- id
- taskId optional
- rotationStationId optional
- notificationType (upcoming_change, reminder, overdue, escalation)
- recipient
- subject
- payload
- sentAt optional
- status

### AuditLog
Nachvollziehbarkeit aller Änderungen.

Beispiel-Felder:
- id
- entityType
- entityId
- action
- performedBy
- payload
- createdAt

---

## MVP-Scope

### Im MVP enthalten
- Durchlaufplan fuer bestehende Person aus abgeschlossenem Onboarding anlegen
- Durchlaufplan anlegen
- Stationen anlegen, ändern, löschen
- Validierung von Stationen
  - keine Überschneidungen
  - sinnvolle Reihenfolge
  - optional Lückenerkennung
- Abteilungen verwalten
- Maßnahmenvorlagen pro Abteilung pflegen
- Generierung von Aufgaben aus Station + Vorlagen
- Übersicht kommender Wechsel
- Aufgabenlisten für IT / Fachbereiche
- Statuspflege von Aufgaben
- Benachrichtigungslogik vor Wechsel
- Audit / Änderungsverlauf im Kern
- Admin-/HR-/IT-Sicht sauber trennen

### Nicht im MVP
- automatische AD-Gruppenänderungen
- automatische Fileserver-Berechtigungen
- automatische Softwareverteilung
- automatische Erstellung externer Fachsystemzugänge
- komplexe Eskalationsworkflows mit Mehrstufigkeit
- Excel-Import als Pflichtfeature
- grafisch perfekte Kalenderansicht als erstes Ziel

---

## Technische Leitplanken

1. Bestehende Architektur respektieren.
2. Keine übermäßige Sonderlogik im Frontend verstecken.
3. Fachlogik zur Task-Generierung ins Backend.
4. Benachrichtigungen über klaren Scheduler / Background Worker.
5. Statusänderungen und relevante Änderungen auditierbar machen.
6. API und Datenmodell so vorbereiten, dass spätere Automatisierung anschließbar bleibt.
7. Keine harten IT-Sonderfälle direkt im MVP verbacken.
8. Vorlagenmodell generisch halten, damit später neue Abteilungen ohne Codeänderung pflegbar sind.

---

## Gewünschtes Ergebnis für diese Implementierung

Nach Abschluss soll ein Benutzer:
1. eine bestehende Person mit abgeschlossenem Onboarding auswaehlen koennen,
2. fuer diese Person einen Durchlaufplan mit mehreren Stationen erstellen koennen,
3. pro Abteilung vordefinierte Maßnahmenvorlagen pflegen koennen,
4. automatisch generierte Aufgaben sehen koennen,
5. kommende Wechsel und offene Aufgaben sehen koennen,
6. Benachrichtigungen vor Stationswechseln versenden lassen koennen,
7. Aufgaben manuell abarbeiten und dokumentieren koennen.

---

## Umsetzungsphasen

# Phase 1 – Domänenmodell und Persistenz

## Ziel
Grundstruktur der Datenbank und Kernentitäten einführen.

## Aufgaben
- Analysiere bestehende Datenbank-/Migrationsstruktur.
- Ergänze neue Tabellen/Modelle für:
  - rotation_plans
  - rotation_stations
  - department_action_templates
  - rotation_generated_tasks
  - rotation_notifications
  - rotation_audit_log
- Lege sinnvolle Fremdschlüssel und Indizes an.
- Verwende bestehende `people`, `departments`, `workflows` und `app_responsibilities` gezielt weiter.
- Referenziere im Durchlaufplan die Zielperson und den abgeschlossenen Onboarding-Quellworkflow.
- Achte auf konsistente Benennung im Projekt.
- Füge Basiskonstanten / Enums für Status- und Triggerwerte ein.

## Akzeptanzkriterien
- Migrationen laufen sauber durch.
- Datenmodell ist konsistent.
- RotationStation kann sauber einem Plan und einer Abteilung zugeordnet werden.
- GeneratedTask kann auf Person, Plan, Station und optional Template referenzieren.
- Pro Person ist hoechstens ein aktiver Durchlaufplan gleichzeitig moeglich.
- Derselbe Quellworkflow ist nur erneut nutzbar, wenn ein bestehender referenzierender Plan bereits abgeschlossen oder archiviert ist.

---

# Phase 2 – Backend-Grundfunktionen für Planung

## Ziel
Backend-Grundfunktionen fuer Plananlage aus bestehender Person und abgeschlossenem Onboarding bereitstellen.

## Aufgaben
- Erstelle Backend-Endpunkte / Services für:
  - bestehende Person mit abgeschlossenem Onboarding suchen / auswaehlen
  - RotationPlan anlegen / lesen
  - RotationStation anlegen / ändern / löschen / listen
- Baue Validierungslogik:
  - ein Plan darf nur fuer abgeschlossene Onboardings gestartet werden
  - pro Person nur ein aktiver Plan
  - derselbe Quellworkflow darf nicht parallel mehrfach offen referenziert werden
  - Station darf nicht vor Planstart liegen
  - Enddatum muss >= Startdatum sein
  - Stationen eines Plans dürfen sich nicht überschneiden
  - Reihenfolge muss stabil sortierbar sein
- Ergänze sinnvolle DTOs / Mapper / Validators.
- Füge erste Tests für Validierungslogik hinzu.

## Akzeptanzkriterien
- Eine bestehende Person mit abgeschlossenem Onboarding kann mit einem Durchlaufplan gespeichert werden.
- Mehrere Stationen können angelegt werden.
- Ungültige Zeitüberschneidungen werden sauber abgefangen.
- API liefert verständliche Fehlermeldungen.

---

# Phase 3 – Abteilungen und Maßnahmenvorlagen

## Ziel
Pflegbares Vorlagenmodell einführen.

## Aufgaben
- Erstelle Backend-Funktionen für Departments und DepartmentActionTemplates.
- Ermögliche pro Abteilung mehrere Vorlagen:
  - triggerType = enter oder exit
  - verantwortliche Stelle
  - Fälligkeit relativ zum Stationsbeginn oder Stationsende
  - manuell oder technisch
- Baue Validierung:
  - nur bekannte Triggerwerte
  - dueOffsetDays plausibel
  - Template muss einer Abteilung zugeordnet sein
- Lege Seed-Daten oder Beispieldaten für 2–3 Abteilungen an, z. B.:
  - Einkauf
  - Produktion
  - IT

## Akzeptanzkriterien
- Admin kann Vorlagen pro Abteilung anlegen.
- Für eine Abteilung können Eintritts- und Austrittsmaßnahmen getrennt gepflegt werden.
- Templates sind später für Task-Generierung verwendbar.

---

# Phase 4 – Task-Generierung aus Stationen

## Ziel
Aus dem Plan entstehen konkrete Aufgaben.

## Aufgaben
- Implementiere einen Generator-Service:
  - liest Stationen
  - liest passende Vorlagen
  - erzeugt GeneratedTasks
- Regeln:
  - Eintritts-Templates beziehen sich auf Start einer Station
  - Austritts-Templates beziehen sich auf Ende einer Station
  - Tasks sollen eine Fälligkeit relativ zum Wechselzeitpunkt erhalten
- Verhindere doppelte Task-Erzeugung bei erneutem Lauf.
- Definiere Regenerierungsstrategie bei Planänderungen:
  - noch offene zukünftige Tasks anpassen oder neu erzeugen
  - bereits erledigte Tasks nicht blind überschreiben
- Ergänze Audit-Einträge bei Generierung / Anpassung.

## Akzeptanzkriterien
- Beim Anlegen oder Aktualisieren eines Plans entstehen passende Aufgaben.
- Für gleiche Daten entstehen keine Duplikate.
- Änderungen am Plan wirken sich nachvollziehbar auf zukünftige Tasks aus.

---

# Phase 5 – Benachrichtigungslogik

## Ziel
Vor Stationswechseln automatisch informieren.

## Aufgaben
- Implementiere Scheduler / Background Job für tägliche Prüfung kommender Wechsel.
- Versende Benachrichtigungen basierend auf:
  - dueDate
  - reminderOffsetDays
  - Wechsel in X Tagen
- Benachrichtigung soll enthalten:
  - Person
  - aktueller Bereich
  - nächster Bereich
  - Wechseltermin
  - offene relevante Aufgaben
  - Link zur Detailansicht
- Speichere versendete Benachrichtigungen, um Doppelversand zu vermeiden.
- Baue zunächst einfache Strategie:
  - erste Info
  - Reminder
  - optional Überfälligkeits-Hinweis

## Akzeptanzkriterien
- Für anstehende Wechsel werden Benachrichtigungen erzeugt/versendet.
- Bereits versendete Hinweise werden nicht ständig neu erzeugt.
- Die Nachricht ist fachlich brauchbar und enthält Link zur Aufgabe.

---

# Phase 6 – Frontend HR: Personen und Durchlaufplan

## Ziel
HR kann die Planung ohne Excel durchführen.

## Aufgaben
- Erstelle UI für:
  - Personenliste
  - Personendetail
  - Durchlaufplan
  - Stationsliste / Timeline-nahe Darstellung
- Funktionen:
  - Station hinzufügen
  - Station bearbeiten
  - Station löschen
  - Reihenfolge und Zeitraum klar erkennbar
- Zeige Validierungsfehler direkt verständlich an.
- UI muss pragmatisch sein; keine überladene Kalenderlogik.
- Fokus auf funktionale Liste / Timeline statt grafischem Perfektionismus.

## Akzeptanzkriterien
- HR kann einen vollständigen Plan ohne technische Kenntnisse anlegen.
- Stationen sind klar lesbar.
- Konflikte werden verständlich angezeigt.

---

# Phase 7 – Frontend IT / Fachbereiche: Aufgaben und Wechsel

## Ziel
Zuständige Stellen sehen, was konkret zu tun ist.

## Aufgaben
- Erstelle Ansichten für:
  - offene Aufgaben
  - Aufgaben nach Abteilung
  - Aufgaben nach Person
  - kommende Wechsel
- Detailseite soll zeigen:
  - Person
  - aktuelle und nächste Station
  - generierte Maßnahmen
  - Fälligkeiten
  - Status
  - Notizen / Historie
- Erlaube Statuspflege:
  - offen
  - in Bearbeitung
  - erledigt
  - fehlgeschlagen
- Ergänze Filter:
  - Wechsel in den nächsten X Tagen
  - nur offene Aufgaben
  - nur IT-Aufgaben
  - nur eigene Abteilung

## Akzeptanzkriterien
- IT kann kommende Wechsel und offene Maßnahmen sauber sehen.
- Aufgaben sind pro Person nachvollziehbar.
- Statusänderungen funktionieren zuverlässig.

---

# Phase 8 – Audit, Historie, Robustheit

## Ziel
Nachvollziehbarkeit und Stabilität sicherstellen.

## Aufgaben
- Protokolliere:
  - Plan erstellt / geändert
  - Station erstellt / geändert / gelöscht
  - Tasks generiert / aktualisiert / abgeschlossen
  - Benachrichtigungen versendet
- Ergänze Tests für:
  - Überschneidungsvalidierung
  - Task-Generierung
  - Benachrichtigungs-Deduplizierung
- Prüfe leere Randfälle:
  - Plan ohne Stationen
  - Station ohne Vorlagen
  - Planänderung kurz vor Wechsel
  - gelöschte zukünftige Station

## Akzeptanzkriterien
- Relevante Änderungen sind nachvollziehbar.
- Kritische Kernlogik ist getestet.
- Fachlicher Ablauf ist robust.

---

## Zusätzliche Hinweise für spätere Versionen

Nicht jetzt umsetzen, aber Architektur vorbereiten für:
- AD-/Entra-Gruppenänderungen
- Fileserver-Berechtigungen
- Softwareverteilung
- Fachsystemzugänge
- manuelle Freigaben / Approvals
- Eskalationen mit mehreren Verantwortlichen
- Imports aus HR-Dateien
- Rollenmatrix je Standort und Abteilung

Dazu spätere Struktur vorbereiten:
- automationKey
- isAutomatable
- optionale technische Executor-Schicht
- klare Trennung zwischen Task-Definition und Task-Ausführung

---

## UX-Prinzipien

1. HR arbeitet auf Personen-/Stations-Ebene, nicht auf Technik-Ebene.
2. IT arbeitet auf Aufgaben-/Maßnahmen-Ebene.
3. Fachbereiche sehen nur für sie relevante Aufgaben.
4. Die Software ersetzt Excel nicht nur optisch, sondern prozessual.
5. Jede Aktion muss klar machen:
   - Wer?
   - Wann?
   - Wechsel wohin?
   - Was ist zu tun?
   - Bis wann?
   - Wer ist verantwortlich?

---

## Konkrete Beispiel-Seed-Daten

Bitte im Entwicklungssystem Beispielkonfiguration anlegen:

### Departments
- BS
- VT
- Einkauf
- Produktion
- IT

### Beispiel-Templates für Einkauf
- enter:
  - "Ordnerrechte Einkauf setzen"
  - "Habel-Zugang beantragen"
  - "Ansprechpartner informieren"
- exit:
  - "Ordnerrechte Einkauf entfernen"
  - "Habel-Zugang entziehen"

### Beispiel-Templates für Produktion
- enter:
  - "Produktionsfreigaben prüfen"
  - "Benötigte Software bereitstellen"
- exit:
  - "Produktionszugänge entfernen"

### Beispiel-Templates für IT
- enter:
  - "IT-Startcheck durchführen"
- exit:
  - "IT-bezogene Sonderrechte prüfen"

---

## Beispiel-Fachablauf zum Testen

Lege Testperson mit abgeschlossenem Onboarding an bzw. verwende eine bestehende Person:
- Vorname: Anika
- Nachname: Sattler
- Typ: Studentin

Lege Plan an:
- 01.06.2026 bis 20.06.2026 -> BS
- 21.06.2026 bis 31.08.2026 -> VT
- 01.09.2026 bis 31.12.2026 -> Einkauf

Erwartung:
- Für Eintritt in Einkauf werden Tasks aus Einkauf-enter erzeugt.
- Für Austritt aus VT werden VT-exit-Tasks erzeugt, sofern definiert.
- Vor 01.09.2026 werden Benachrichtigungen ausgelöst.
- Aufgaben erscheinen in IT-/Fachbereichsübersichten.

---

## Was Codex zuerst tun soll

1. Relevante bestehende Domänen, Tabellen, Endpunkte und UI-Strukturen analysieren.
2. Dann einen kurzen Impact-Report erstellen:
   - welche Dateien/Module betroffen sind
   - welche neuen Entitäten nötig sind
   - wo bestehende Workflow-/Task-Logik wiederverwendbar ist
3. Danach Phase 1 bis 3 implementieren.
4. Dann Zwischenstand mit kurzer Zusammenfassung:
   - was wurde umgesetzt
   - welche offenen Architekturentscheidungen bestehen
5. Danach Phase 4 und 5 implementieren.
6. Danach Phase 6 und 7.
7. Zum Schluss Tests, Audit, Cleanup, Doku.

---

## Wichtige Implementierungsregeln für Codex

- Keine unnötigen Großumbauten außerhalb dieses Features.
- Bestehende Patterns des Projekts bevorzugen.
- Keine neue Parallelarchitektur einführen, wenn bestehende Strukturen passen.
- Fachlogik nicht im Frontend verstecken.
- Lieber zuerst solide Listen-/Detailansichten statt überkomplexe Kalenderoberflächen.
- Saubere Benennung und klare Trennung zwischen:
  - Planungsdaten
  - Vorlagen
  - generierten Aufgaben
  - Benachrichtigungen
  - späterer Automatisierung
- Bei Unsicherheit lieber generische Erweiterbarkeit statt vorschneller IT-Speziallogik.

---

## Erwarteter Output von Codex

Codex soll nicht nur Code schreiben, sondern am Ende auch kurz dokumentieren:
- welche Dateien neu sind
- welche Dateien geändert wurden
- welche Migrationen erstellt wurden
- welche Annahmen getroffen wurden
- welche TODOs für die nächste Ausbaustufe bleiben
