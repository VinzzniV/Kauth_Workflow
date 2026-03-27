# ENVIRONMENTS.md

## Ziel

Klare Trennung zwischen Demo, lokaler Entwicklung und spaeterem Produktivbetrieb.

Wichtig:
- Backend bleibt Source of Truth.
- Demo-Helfer sind fuer Demo/Dev gedacht, nicht fuer Produktion.
- Seeds und Mail-Verhalten muessen bewusst zur jeweiligen Umgebung passen.

---

## Demo

Gedacht fuer:
- Vorfuehrungen
- schnelles manuelles Testen
- handgefuehrte Handoffs

Regeln:
- Demo-Login ueber die vorhandenen Demo-Auth-Endpunkte ist erlaubt.
- Demo-Seed-Daten aus [`db/02_seed.sql`](db/02_seed.sql) sind hier erwartbar.
- Im eingecheckten Demo-/Dev-Setup ist Mailversand standardmaessig deaktiviert (`enabled = false`).
- Falls Mailversand fuer Demo-Zwecke bewusst aktiviert wird, dann nur mit `sandbox_redirect_email`.
- Keine echten Empfaengeradressen in Demo-Konfigurationen verwenden.

---

## Dev

Gedacht fuer:
- lokale Entwicklung
- automatisierte und manuelle Fachtests

Regeln:
- `web/.env.local` setzt lokal `VITE_API_BASE`; Vorlage ist [`web/.env.example`](web/.env.example).
- Demo-Login darf lokal weiter genutzt werden, solange klar ist, dass es kein Produktionsmodell ist.
- Demo-Seeds sind fuer lokale Testdaten ok, aber bewusst als Demo-Daten zu behandeln.
- Mailversand lokal standardmaessig deaktiviert; bei bewusstem Aktivieren nur mit Sandbox-Redirect.
- Build- und Laufzeitartefakte gehoeren nicht in Git und nicht in Handoff-Zips.

---

## Prod

Gedacht fuer:
- reale Benutzer
- reale Workflows

Regeln:
- Authentifizierung ueber produktive Entra-Integration.
- Kein Demo-Login.
- Keine Demo-Seed-Daten und keine `@demo.local`-Adressen.
- Mailversand nur mit produktiver Konfiguration und ohne Sandbox-Redirect.
- Secrets und Umgebungswerte kommen aus der Laufzeitumgebung, nicht aus eingecheckten Dateien.
- Keine Demo-Zugriffslinks in produktiven Benachrichtigungen.
- Keine Header-basierte Pseudo-Authentifizierung.
- Benutzer und Gruppen kommen aus dem fuehrenden Verzeichnis, nicht aus manueller In-App-Anlage.
- Rollen werden bevorzugt aus externen Gruppen abgeleitet; lokale Ausnahmen muessen bewusst und nachvollziehbar sein.
- Produktivbetrieb braucht TLS, Logging, Monitoring, Backup und einen definierten Migrationsprozess.
- Der Endpoint `/health` muss im Produktivbetrieb gruen sein und sowohl Datenbank als auch Entra-Erreichbarkeit erfolgreich melden.
- Wenn `ENTRA_AUTH_ENABLED=true` aktiv ist, muessen `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID` und `ENTRA_AUDIENCE` gesetzt sein; fehlende Werte verhindern den Start in Production.

---

## Praktische Leitplanken

- `db/02_seed.sql` ist Demo-/Dev-Hilfe, kein Produktions-Setup.
- [`scripts/Prepare-Handoff.ps1`](scripts/Prepare-Handoff.ps1) muss Handoff-Artefakte ohne Build-/Runtime-Reste erzeugen.
- Wenn unklar ist, ob eine Einstellung Demo oder Prod betrifft, konservativ bleiben: lieber deaktiviert oder Sandbox statt echter Wirkung.
- Siehe auch [`PRODUCTIVE_TARGET_ARCHITECTURE.md`](PRODUCTIVE_TARGET_ARCHITECTURE.md) fuer das produktive Zielbild.
