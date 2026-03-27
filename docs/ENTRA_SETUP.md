# Entra ID Authentication Setup

## Voraussetzung

Eine Azure App Registration muss im Entra ID (Azure AD) des Unternehmens vorhanden sein.

---

## Backend Umgebungsvariablen

| Variable | Beschreibung | Beispiel |
|---|---|---|
| `ENTRA_AUTH_ENABLED` | Entra-Authentifizierung aktivieren | `true` |
| `ENTRA_TENANT_ID` | Azure Tenant ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `ENTRA_CLIENT_ID` | App Registration Client ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `ENTRA_AUDIENCE` | Erwartete Audience im JWT | `api://<client-id>` |
| `ENTRA_CLIENT_SECRET` | Client Secret fuer Graph/Directory-Sync | `<secret>` |
| `DIRECTORY_GROUP_PREFIX` | Optionaler Filter fuer zu synchronisierende Gruppen | `Onboarding-App-` |
| `DEMO_ENDPOINTS_ENABLED` | Demo-Login deaktivieren (Prod) | `false` |
| `ASPNETCORE_ENVIRONMENT` | Production setzt Demo automatisch ab | `Production` |

---

## Frontend Umgebungsvariablen

| Variable | Beschreibung | Beispiel |
|---|---|---|
| `VITE_AUTH_MODE` | Auth-Modus (`demo` oder `entra`) | `entra` |
| `VITE_ENTRA_CLIENT_ID` | App Registration Client ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `VITE_ENTRA_TENANT_ID` | Azure Tenant ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `VITE_ENTRA_REDIRECT_URI` | Redirect URI nach Login | `https://lifecycle.example.com` |

---

## Azure App Registration Konfiguration

### Plattform

- Typ: **Single-Page Application (SPA)**
- Redirect URIs:
  - Dev: `http://localhost:5173`
  - Prod: `https://<your-domain>`

### API-Berechtigungen

- `openid` (delegiert)
- `profile` (delegiert)
- `email` (delegiert)

### API exponieren (Expose an API)

- Application ID URI: `api://<client-id>`
- Scope: `access_as_user` (Consent: Admins and users)

### Token-Konfiguration

- Access Token: v2
- Optional Claims: `email`, `preferred_username`

---

## Betriebsmodi

### Demo-Modus (Entwicklung)

```env
# Backend
ASPNETCORE_ENVIRONMENT=Development
DEMO_ENDPOINTS_ENABLED=true

# Frontend
VITE_AUTH_MODE=demo
```

### Entra-Modus (Produktion)

```env
# Backend
ASPNETCORE_ENVIRONMENT=Production
DEMO_ENDPOINTS_ENABLED=false
ENTRA_AUTH_ENABLED=true
ENTRA_TENANT_ID=<tenant-id>
ENTRA_CLIENT_ID=<client-id>
ENTRA_AUDIENCE=api://<client-id>
ENTRA_CLIENT_SECRET=<client-secret>
DIRECTORY_GROUP_PREFIX=Onboarding-App-

# Frontend
VITE_AUTH_MODE=entra
VITE_ENTRA_CLIENT_ID=<client-id>
VITE_ENTRA_TENANT_ID=<tenant-id>
VITE_ENTRA_REDIRECT_URI=https://<domain>
```

### Dual-Modus (Entwicklung mit Entra)

```env
# Backend
ASPNETCORE_ENVIRONMENT=Development
DEMO_ENDPOINTS_ENABLED=true
ENTRA_AUTH_ENABLED=true
ENTRA_TENANT_ID=<tenant-id>
ENTRA_CLIENT_ID=<client-id>
ENTRA_AUDIENCE=api://<client-id>

# Frontend (waehle einen Modus)
VITE_AUTH_MODE=entra
```

Im Dual-Modus akzeptiert das Backend sowohl Entra-Tokens als auch Demo-Sessions. Das Frontend zeigt je nach `VITE_AUTH_MODE` den entsprechenden Login.

---

## Sicherheitshinweise

- In `ASPNETCORE_ENVIRONMENT=Production` werden Demo-Endpoints **immer** deaktiviert, unabhaengig von `DEMO_ENDPOINTS_ENABLED`.
- Demo-Resolver (Session-Token, Header-Auth) werden im Production-Modus gar nicht im DI-Container registriert.
- Demo-Zugriffslinks in E-Mail-Benachrichtigungen werden in Production automatisch unterdrueckt.
- Auto-Provisioning: Entra-Benutzer ohne bestehenden `app_users`-Eintrag erhalten automatisch einen beim ersten Login. Rollen/Gruppen muessen danach zugewiesen werden.
- Wenn `ENTRA_AUTH_ENABLED=true` gesetzt ist, prueft der Startup-Check die Pflichtwerte `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID` und `ENTRA_AUDIENCE`. In Production startet die API ohne diese Werte nicht.
- Bei `401` versucht das Frontend im Entra-Modus zuerst einen stillen Token-Refresh und wiederholt den Request einmal, bevor auf erneute Anmeldung gewechselt wird.
- Der Directory-Sync kann ueber `DIRECTORY_GROUP_PREFIX` auf App-spezifische Gruppen begrenzt werden, z. B. `Onboarding-App-`.

---

## Monitoring und Betrieb

- `GET /health` prueft:
  - Datenbank erreichbar
  - Entra-Modus und Konfigurationsstatus
  - Erreichbarkeit des OpenID-Discovery-Endpunkts von Microsoft, wenn Entra aktiv ist
- Erwartung in Production: HTTP `200` und `status = "ok"`.
- Bei Konfigurations- oder Verbindungsproblemen liefert der Endpoint HTTP `503` mit Detailstatus fuer Datenbank und Auth.

---

## Docker Deployment

```bash
# Development (Demo-Modus, Standard)
docker compose up

# Production (Entra-Modus)
docker compose -f docker-compose.yml -f docker-compose.prod.yml up
```

Die Entra-Env-Vars muessen in `docker-compose.prod.yml` oder ueber die Deployment-Umgebung gesetzt werden.
