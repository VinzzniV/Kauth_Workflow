# Produktive Zielarchitektur

Dieses Dokument beschreibt das Sollbild fuer den produktiven Ausbau des Projekts und verortet kurz, was davon im Repo bereits sichtbar ist. Es ersetzt keine Umsetzungsdetails, definiert aber die Leitplanken fuer weitere Architekturarbeit.

## Statusbild April 2026

Bereits im Repo sichtbar:
- Entra-basierte Produktivrichtung ist technisch verankert
- lokaler `dev-sim` nutzt synchronisierte Verzeichnisidentitaeten statt alter Demo-Benutzer
- Directory-Projektion, Gruppen-Mapping und Audit-Tabellen existieren
- Admin-UI deckt bereits Directory-Sync, Gruppen-Mapping, Permissions, Mail-Runtime-Konfiguration und read-only Graph-Status ab
- mehrere Prozessarten und Workflow-Verknuepfungen sind im Datenmodell vorhanden

Noch nicht am Ziel:
- Security- und Betriebsmodell sind weiter in Haertung, auch wenn Lint, Tests, Swagger-Gating und minimale CI inzwischen stehen
- User- und Gruppenkonfiguration enthaelt weiterhin Uebergangsanteile von lokalem CRUD
- Release-/CI-Haertung ist eingefuehrt, aber noch nicht das vollstaendige Betriebsendmodell

---

## 1. Zielbild

Die Anwendung soll produktiv als internes Employee-Lifecycle-System betrieben werden.

Rahmenbedingungen:
- fuehrendes Identitaetssystem ist das on-prem Active Directory
- AD wird nach Entra synchronisiert
- die Anwendung laeuft on-prem in Docker
- Entra ist die produktive Authentifizierungs- und Integrationsschicht

Kernprinzip:
- Identitaet kommt aus AD/Entra
- die App projiziert, mappt und erweitert
- fachliche Prozesslogik, Verantwortlichkeiten, Ausnahmen und Workflow-Daten bleiben in der App

---

## 2. Authentifizierung

Produktiv darf die Anwendung nicht auf lokale Demo- oder Pseudo-Auth bauen.

Sollzustand:
- Frontend meldet Benutzer ueber Microsoft Entra ID an
- API validiert Access Tokens serverseitig
- Benutzerkontext wird aus stabilen externen Identitaetsmerkmalen aufgebaut
- lokaler `dev-sim` bleibt nur Entwicklungsmodus

Pflichtfelder im Identitaetskontext:
- `entra_object_id`
- `user_principal_name`
- `display_name`
- `mail`
- serverseitig aufgeloeste Gruppen- oder Mappinginformationen

Regeln:
- kein produktiver Login ueber Simulations-Endpunkte
- keine Header-basierte Pseudo-Authentifizierung
- keine Mail- oder Deep-Link-Mechanik, die implizit Sessions erzeugt

---

## 3. Rollen, Permissions und Verantwortlichkeiten

Die App verwendet ein hybrides Berechtigungsmodell.

### Basiszugang

Steuert, wer die Anwendung ueberhaupt verwenden darf, typischerweise ueber Entra-Gruppen.

### Standardrollen

Zugriffsrollen kommen standardmaessig aus Gruppen-Mappings.
Beispiele:
- HR
- Manager
- Worker
- Reader
- Admin

### App-spezifische Fachzuordnungen

Lokal in der App bleiben:
- Verantwortlichkeiten
- Freigabeverantwortungen
- Vertretungen
- Ausnahmen
- ggf. bereichsbezogene Scopes auf Permissions oder Rollen

Regeln:
- Rollen = Zugriff
- Verantwortlichkeiten = fachliche Ownership
- Standardrechte aus Gruppen
- lokale Sonderfaelle bleiben Ausnahmen, nicht Primarmodell

---

## 4. Mapping-Modell

Empfohlenes Zielbild:
- externe Gruppen werden in `directory_groups` projiziert
- Gruppenmitgliedschaften werden lokal gespiegelt
- Gruppen werden ueber Mappingtabellen auf App-Rollen und ggf. Scopes abgebildet
- lokale Permission-Overrides bleiben moeglich, aber selten

Damit gilt:
- die App ist nicht die fuehrende Benutzerquelle
- Benutzer sollen produktiv nicht manuell als Normalfall angelegt werden
- lokale Pflege dient Mapping, Ausnahmen und Fachanreicherung

---

## 5. Datenmodell-Soll

### Identitaet

Technische Identitaet aus AD/Entra, z. B. in `directory_identities`.

Wichtige Merkmale:
- `entra_object_id`
- `user_principal_name`
- `mail`
- `display_name`
- `account_enabled`
- `last_synced_at`

### Mitarbeiter / Person

Fachlicher Mitarbeiterdatensatz bleibt getrennt von technischer Identitaet.

Regeln:
- Person bzw. Employee ist nicht synonym zu Login
- historische Workflow-Daten duerfen nicht an mutable Strings wie Anzeigenamen gekoppelt werden
- Verknuepfung zwischen Person und Identitaet ist moeglich, aber nicht identisch

### Gruppen

Externe Gruppen werden als Projektion gefuehrt und lokal mit Rollen/Scopes verknuepft.

### Workflow-Daten

Workflow-, Aufgaben-, Antwort- und Audit-Daten bleiben Fachobjekte der Anwendung.
Sie duerfen nicht ueber Auth-Abkuerzungen oder UI-Hilfsannahmen modelliert werden.

---

## 6. Admin-UI-Soll

Die Admin-Oberflaeche ist langfristig kein User-CRUD-Werkzeug, sondern ein Steuerungsbereich fuer:

- Organisationspflege
- Aufgabenlogik und Antwortdefinitionen
- Rollen, Permissions und Gruppen
- Directory-Sync und Gruppen-Mapping
- System- und Mail-Konfiguration
- Bulk-Operationen und Betriebswarnungen

Was langfristig nicht das Primarmodell sein soll:
- Benutzer lokal manuell als Standardweg anlegen
- Gruppen vollstaendig in der App nachbauen
- technische Identitaetsfuehrung in der App halten

---

## 7. Betriebsmodell

Produktiv bedeutet mindestens:
- TLS vor der Anwendung
- Reverse Proxy
- getrennte Dev-/Prod-Konfiguration
- Entra-Login statt lokaler Simulationsauth
- kein produktiver Klartext-Secret-Standard in DB
- Logging, Monitoring und Health-Checks
- reproduzierbare DB-Initialisierung und Migration
- klarer Update- und Restore-Pfad

Health-Modell:
- Liveness prueft nur den Prozess
- Readiness prueft nur lokale Betriebsfaehigkeit wie die Datenbank
- Deep Health darf externe Provider wie Entra einbeziehen, darf aber keine Container-Restarts ausloesen

Konfigurationsmodell:
- Production nutzt nur explizite `https://`-basierte Origins fuer `PUBLIC_BASE_URL` und CORS
- Das Web bekommt seinen Auth-Modus und Entra-Werte ueber Runtime-Config statt implizite Build-Defaults
- Der Entra-SPA-Redirect wird produktiv explizit auf die oeffentliche Basis-URL gespiegelt, nicht aus Frontend-Fallbacks erraten

Wichtige Betriebsentscheidung:
- On-prem Hosting rechtfertigt keine proprietaeren Auth-Abkuerzungen
- on-prem und moderne Cloud-Authentifizierung schliessen sich nicht aus

---

## 8. Mail und Graph

Sollzustand:
- produktive Authentifizierung gegen Microsoft Graph
- Secret-Verwaltung ueber sichere Laufzeitmechanismen oder Secret Store
- keine Demo-Links oder Session-Abkuerzungen in Benachrichtigungen
- stabile Links auf regulaere App-Routen

Aktueller Architekturhinweis:
- Die Admin-Oberflaeche hat weiterhin Mail-Konfiguration und einen read-only Graph-Status
- produktive Graph-/Mail-Secrets kommen aus Runtime-Konfiguration, nicht mehr aus DB-Persistenz

---

## 9. Migrationspfad Ohne Big Bang

### Phase 1: Produktivblocker haerten

- Swagger produktiv absichern oder deaktivieren
- Klartext-/DB-Secret-Modell fuer weitere potenzielle Secret-Pfade konsequent vermeiden
- Release-Checks und CI einfuehren

### Phase 2: Identitaetsbasis festziehen

- Entra-Login und Token-Handling weiter haerten
- stabile externe Identifikatoren konsequent verwenden
- lokale Simulationswelt nur als Entwicklungsmodus behandeln

### Phase 3: Gruppen- und Permission-Modell konsolidieren

- Gruppenprojektion und Mapping als Standard
- lokale Sonderrechte explizit und auditierbar halten
- User-CRUD weiter zur Ausnahme zurueckdruecken

### Phase 4: Fachmodell weiter bereinigen

- People/Employee sauber von technischer Identitaet getrennt halten
- historische Zuordnungen robust machen
- stringbasierte Aufloesung ueber Namen vermeiden

### Phase 5: Betrieb haerten

- Health-Modell scharf trennen
- Observability erweitern
- Migrations- und Deploymentprozess standardisieren

---

## 10. Klare Entscheidungen

Fuer die weitere Entwicklung gelten diese Entscheidungen:
- die App ist nicht das fuehrende Benutzersystem
- AD bzw. Entra liefern Identitaet und Gruppenbasis
- gruppenbasierte Rechte sind der Standard
- lokale Zuordnungen bleiben fuer Fachlogik, Ausnahmen und Scopes
- `Employee` bzw. Person und technische Identitaet sind getrennte Konzepte
- Produktionsbetrieb braucht ein Betriebsmodell, nicht nur einen funktionierenden Docker-Start
