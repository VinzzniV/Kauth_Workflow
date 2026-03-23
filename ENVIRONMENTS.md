# Demo / Dev / Prod Separation

Dieses Repository ist noch prototypisch, aber einige Grenzen muessen klar bleiben.

## Grundsatz

- Demo ist fuer Vorfuehrung und sichere Beispiel-Daten.
- Dev ist fuer lokale Entwicklung und Tests.
- Prod ist spaeter ein eigener Betriebsmodus mit echter Identitaet, echter Mail-Konfiguration und kontrollierten Datenwegen.

Business-Logik bleibt in allen Umgebungen im Backend. Unterschiede zwischen Umgebungen duerfen nur Konfiguration, Demo-Zugriff und Betriebsoptionen betreffen.

## Demo

- Auth: nur Demo-Login ueber die API-Endpunkte `/auth/demo-users`, `/auth/demo-login`, `/auth/demo-logout`
- Frontend: spricht gegen eine explizit gesetzte API-Basis (`VITE_API_BASE`)
- Daten: nur Demo-/Seed-Daten, keine echten produktiven Personen oder Empfaenger
- Mail: `NotificationEmail.Enabled` muss deaktiviert bleiben, solange keine explizit sichere Demo-Mail-Konfiguration vorliegt
- Archive/Handoff: keine Build-Artefakte, keine `node_modules`, keine lokalen `.env`-Dateien

Geeignet fuer:
- Screenshots
- Demo-Durchlaeufe
- Review/Handoff

Nicht geeignet fuer:
- echte SSO-Anbindung
- produktive Empfaenger
- produktive Mail-Zustellung

## Dev

- Auth: aktuell weiter Demo-Login, bis reale Identity kommt
- API: `ASPNETCORE_ENVIRONMENT=Development`
- Datenbank: lokale Docker-/Entwicklungsdatenbank
- Frontend: lokale `.env.local` auf Basis von [`web/.env.example`](/C:/OnBoarding/OnBoarding/web/.env.example)
- Mail: standardmaessig deaktiviert; echte Provider-Credentials gehoeren nicht ins Repository

Empfohlene lokale Startkonvention:

```bash
docker compose up --build
```

Dabei gilt aktuell:
- Web: `http://localhost:5173`
- API: `http://localhost:5001`
- Postgres: `localhost:23456`

## Prod

- kein Demo-Login
- keine Demo-Seeds
- keine lokalen `.env.local`-Dateien aus Entwickler-Rechnern
- Mail nur mit expliziter, validierter Konfiguration
- echte Secrets nur ueber sichere Betriebswege, nie ueber eingecheckte Dateien

Produktionsreife ist noch kein aktuelles Ziel des Repos. Diese Trennung soll aber verhindern, dass Demo-/Dev-Annahmen spaeter versehentlich als Produktionsstandard behandelt werden.

## Seeds

- Demo-Seeds gehoeren nur in `db/*.sql`
- Seed-Daten muessen klar als Demo erkennbar sein
- Demo-E-Mail-Adressen sollen auf sichere Demo-Domaenen wie `@demo.local` zeigen
- Seed-Dateien sind keine Stelle fuer produktive Empfaenger oder geheime Zugangsdaten

## Mail

Aktueller sicherer Default in [`api/API/appsettings.json`](/C:/OnBoarding/OnBoarding/api/API/appsettings.json):

- `NotificationEmail.Enabled = false`

Konvention:
- Demo/Dev starten mit deaktivierter Mail
- echte Mail-Konfiguration nur ausserhalb des Repositories setzen
- Testempfaenger fuer spaetere Aktivierung muessen explizit umgebungsspezifisch gepflegt werden

## Auth Boundaries

Aktueller Zustand:
- Frontend nutzt bewusst einen Demo-Identity-Provider in [`web/src/auth/IdentityProvider.ts`](/C:/OnBoarding/OnBoarding/web/src/auth/IdentityProvider.ts)
- API besitzt bewusst Demo-Auth-Endpunkte in [`api/API/Endpoints/AuthEndpoints.cs`](/C:/OnBoarding/OnBoarding/api/API/Endpoints/AuthEndpoints.cs)

Das ist fuer Demo/Dev ok, aber kein Produktionsmodell. Reale Auth muss spaeter dieselben fachlichen Backend-Regeln verwenden, ohne Demo-Endpunkte als Betriebsweg mitzuschleppen.

## Review / Handoff Hygiene

Vor Weitergabe oder Review:

- keine lokalen `.env`-Dateien einchecken
- keine Build-Ausgaben einchecken
- keine `handoff/*.zip` als Source of Truth behandeln
- Lockfiles, Quellcode und explizite Beispiel-Konfigurationen beibehalten

## Minimale Checkliste

Fuer Demo:
- Mail deaktiviert
- nur Demo-Nutzer
- API-URL explizit gesetzt

Fuer Dev:
- lokale `.env.local` genutzt
- Docker-/lokale DB getrennt von fremden Instanzen
- keine echten Secrets im Repo

Fuer spaeteres Prod:
- Demo-Auth entfernt/abgeloest
- Demo-Seeds nicht verwendet
- Mail und Secrets extern verwaltet
