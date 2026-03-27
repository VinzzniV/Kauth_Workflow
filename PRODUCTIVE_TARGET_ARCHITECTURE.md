# Produktive Zielarchitektur

Dieses Dokument beschreibt das Soll-Bild fuer den produktiven Ausbau des Projekts.
Es ersetzt kein Umsetzungsdetail, aber es definiert die fachlichen und technischen Leitplanken, damit weitere Entwicklung nicht in die falsche Richtung laeuft.

---

## 1. Zielbild

Die Anwendung soll produktiv als internes Employee-Lifecycle-System betrieben werden.

Rahmenbedingungen:
- Fuehrendes Identitaetssystem ist das on-prem Active Directory.
- AD wird in die Cloud synchronisiert.
- Die Anwendung ist in Azure registriert, unter anderem fuer Mailversand.
- Die Anwendung soll on-prem auf einem eigenen Server in Docker betrieben werden.

Kernprinzip:
- Identitaet kommt aus AD/Entra.
- Die App mappt und erweitert, sie verwaltet Identitaeten nicht als primaeres System.
- Fachliche Prozesslogik, Verantwortlichkeiten und Ausnahmen liegen in der App.

---

## 2. Authentifizierung

Produktiv darf die Anwendung nicht auf Demo-Login, Header-Identity oder lokale Demo-Sessions bauen.

Soll-Zustand:
- Anmeldung ueber Microsoft Entra ID mit OpenID Connect / OAuth2.
- Frontend authentifiziert gegen Entra.
- API validiert Access Tokens serverseitig.
- Benutzerkontext wird aus stabilen externen Identitaetsmerkmalen aufgebaut.

Pflichtfelder im Benutzerkontext:
- `entra_object_id`
- `user_principal_name`
- `display_name`
- `mail`
- optionale Gruppeninformationen oder serverseitig aufgeloeste Gruppen

Wichtige Regel:
- Kein produktiver Login ueber Demo-Endpunkte.
- Kein Mail-Link, der eine Demo-Session erzeugt.
- Keine Header-basierte Pseudo-Authentifizierung in Produktion.

---

## 3. Rollen- und Rechtekonzept

Die App braucht ein hybrides Berechtigungsmodell.

### 3.1 Basiszugang

Steuert, wer die Anwendung ueberhaupt nutzen darf.

Beispiel:
- `Onboarding-App-Users`

### 3.2 Standardrollen

Standardrollen sollen primaer aus AD-/Entra-Gruppen abgeleitet werden.

Beispiele:
- HR
- IT
- Manager
- Viewer
- Admin

Diese Rollen sind Zugriffsrollen, nicht fachliche Ownership.

### 3.3 Lokale Fachzuordnungen

Nicht alles ist sinnvoll ueber AD-Gruppen abbildbar. Daher darf die App lokal verwalten:
- fachliche Verantwortlichkeiten
- Freigabeverantwortungen
- Vertretungen
- temporaere Ausnahmen
- bereichsspezifische Owner

Regel:
- Standardrechte aus Gruppen
- Ausnahmen lokal
- keine manuelle Benutzeranlage als Normalfall

---

## 4. Mapping-Modell

Es gibt zwei moegliche Modelle:
- direkte 1:1-Zuordnung von Gruppen auf App-Rollen
- Mapping externer Gruppen auf interne App-Rollen

Empfohlenes Zielbild:
- direkte Gruppenableitung dort, wo die AD-/Entra-Struktur sauber passt
- zusaetzliche Mapping-Tabelle fuer Faelle, in denen externe Gruppen nicht 1:1 auf App-Rollen passen

Die App soll daher eine Konfiguration fuer Gruppen-Mappings haben, aber nicht Benutzer manuell anlegen oder als lokale Stammdatenquelle missbrauchen.

Beispiel Mapping:

| Externe Gruppe | App-Rolle | Scope | Bemerkung |
|---|---|---|---|
| `HR-Team-Berlin` | `HR_EDITOR` | `global` | direkt gemappt |
| `AL-Produktion` | `MANAGER` | `department` | ggf. mit Bereichsbezug |

---

## 5. Datenmodell-Soll

Das bestehende Modell koppelt fachliche Personendaten zu eng an lokale App-Benutzer.
Produktiv muss das getrennt werden.

### 5.1 Identitaet

Technische Identitaet aus AD/Entra.

Vorgeschlagene Entitaet:
- `directory_identities`

Beispielfelder:
- `entra_object_id`
- `onprem_object_guid` oder anderes stabiles on-prem Merkmal
- `user_principal_name`
- `mail`
- `display_name`
- `account_enabled`
- `source_system`
- `last_synced_at`
- `is_managed_externally`

### 5.2 Mitarbeiter

Fachlicher Mitarbeiterdatensatz.

Vorgeschlagene Entitaet:
- `employees`

Beispielfelder:
- `employee_number`
- `first_name`
- `last_name`
- `department_id`
- `employment_status`
- `entry_date`
- `exit_date`
- optionale Verknuepfung auf `directory_identities`

Wichtige Regel:
- Ein Mitarbeiter ist kein Synonym fuer einen Login.
- Ein Mitarbeiterdatensatz darf existieren, ohne dass die App ihn als lokalen Benutzer "angelegt" hat.

### 5.3 Gruppen

Externe Gruppen sollen als Projektion gefuehrt werden.

Vorgeschlagene Entitaet:
- `directory_groups`

Beispielfelder:
- `external_group_id`
- `display_name`
- `description`
- `source_system`
- `last_synced_at`

### 5.4 App-spezifische Zuordnungen

Lokal verbleiben:
- `app_role_mappings`
- `responsibility_assignments`
- `delegation_assignments`
- `approval_assignments`

### 5.5 Workflow-Daten

Workflow-, Aufgaben- und Anforderungsdaten bleiben Fachobjekte der Anwendung und duerfen nicht ueber Identitaetsabkuerzungen modelliert werden.

---

## 6. Admin-UI-Soll

Die Admin-Oberflaeche darf langfristig kein User-CRUD-Werkzeug sein.

Stattdessen braucht sie diese Bereiche:

### 6.1 Verzeichnis-Sync
- letzter erfolgreicher Sync
- fehlgeschlagene Syncs
- neue Benutzer/Gruppen
- deaktivierte Benutzer
- Konflikte

### 6.2 Gruppen-Mapping
- welche externen Gruppen welche App-Rollen liefern
- optionaler Scope
- aktiv/inaktiv

### 6.3 Verantwortlichkeiten
- wer ist fachlich zustaendig fuer welchen Bereich oder Prozess
- Vertretungen
- Eskalationen

### 6.4 Ausnahmen
- lokale Sonderrechte
- zeitlich begrenzte Freigaben
- dokumentierte Abweichungen

### 6.5 Audit
- wer hat wann welches Mapping oder welche Verantwortlichkeit geaendert

Was nicht mehr das Primaermodell sein darf:
- Benutzer manuell anlegen
- Benutzer lokal als fuehrende Wahrheit pflegen
- Gruppen und Rollen vollstaendig manuell bauen

---

## 7. Betriebsmodell

Die App soll on-prem per Docker betrieben werden.

Produktiv bedeutet dabei mindestens:
- TLS vor der Anwendung
- Reverse Proxy
- getrennte Konfiguration fuer Dev und Prod
- keine Demo-Authentifizierung in Prod
- keine Klartext-Secrets in der Datenbank
- Logging
- Monitoring
- Health-Checks
- Backup- und Restore-Prozess
- klarer Update- und Migrationsprozess

Wichtige Betriebsentscheidung:
- On-prem Hosting schliesst Entra-Login nicht aus.
- Die Anwendung kann on-prem laufen und trotzdem moderne Cloud-Authentifizierung nutzen.

---

## 8. Ziel fuer Mailversand

Mailversand darf produktiv nicht auf einem in der DB gespeicherten Klartext-Secret und Demo-Links basieren.

Soll-Zustand:
- produktive Authentifizierung gegen Microsoft Graph
- Secret-Verwaltung ueber sichere Laufzeitmechanismen
- keine Demo-Zugriffslinks in Mails
- stabile Links auf regulare App-Routen

Je nach Betriebsumgebung:
- bevorzugt Zertifikat oder andere sichere Secret-Verwaltung
- mindestens keine Ablage sensibler Secrets als freier DB-Klartextwert

---

## 9. Migrationspfad ohne Big Bang

### Phase 1: Sicherheits- und Produktivblocker entfernen
- Demo-Auth produktiv deaktivieren
- Demo-Links aus Benachrichtigungen entfernen
- produktive Auth-Richtung festziehen
- Klartext-Secret-Modell ablösen

### Phase 2: Produktive Identitaetsbasis schaffen
- Entra-Login integrieren
- API auf echtes Token-Handling umstellen
- stabile externe Identifikatoren im Datenmodell einfuehren

### Phase 3: Rechte auf Gruppenmodell umstellen
- Gruppenprojektion einfuehren
- Rollen aus Gruppen ableiten
- lokale Sonderzuordnungen separat halten

### Phase 4: Fachmodell bereinigen
- `Person`/`Employee` von technischer Identitaet entkoppeln
- historische Zuordnungen sauber migrieren
- stringbasierte Zuordnung ueber Namen ablösen

### Phase 5: Admin-Oberflaeche umbauen
- User-CRUD reduzieren oder entfernen
- Sync- und Mapping-UI aufbauen
- Audit und Konfliktbehandlung sichtbar machen

### Phase 6: Architektur und Betrieb haerten
- Repository-Grenzen aufraeumen
- Health-Checks und Observability erweitern
- produktiven Migrationsprozess etablieren

---

## 10. Klare Entscheidungen

Fuer die weitere Entwicklung gelten diese Entscheidungen:
- Die App ist nicht das fuehrende Benutzersystem.
- On-prem AD ist fachlich die Quelle, Entra ist die produktive Authentifizierungs- und Integrationsschicht.
- Benutzer werden produktiv nicht manuell in der App angelegt.
- Gruppenbasierte Rechte sind der Standard.
- Lokale Zuordnungen sind nur fuer app-spezifische Fachlogik gedacht.
- `Employee` und `Identity` sind getrennte Konzepte.
- Produktivbetrieb braucht ein Betriebsmodell, nicht nur einen funktionierenden Docker-Start.
