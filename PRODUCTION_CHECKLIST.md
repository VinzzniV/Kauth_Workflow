# Production Checklist

Knappe, ausführbare Checkliste für ein sauberes Production-Deployment.
Detailbeschreibungen stehen in `SETUP.md`.

---

## 1 – Entra-App-Registration (einmalig, im Azure-Portal)

- [ ] App Registration angelegt
- [ ] **Application (client) ID** notiert → `ENTRA_CLIENT_ID`
- [ ] **Directory (tenant) ID** notiert → `ENTRA_TENANT_ID`
- [ ] **Client Secret** erstellt → `ENTRA_CLIENT_SECRET`
- [ ] **App ID URI** gesetzt, z. B. `api://<client-id>` → `ENTRA_AUDIENCE`
- [ ] **Redirect URI** als *Single-Page Application* eingetragen, exakt gleich wie `PUBLIC_BASE_URL`
- [ ] API Permission `<audience>/access_as_user` als delegierte Berechtigung eingetragen und per Admin Consent freigegeben
- [ ] Ggf. Sicherheitsgruppen für Rollen (`DIRECTORY_GROUP_PREFIX`) angelegt

---

## 2 – VM / Host-Voraussetzungen

- [ ] Docker Engine ≥ 24 installiert
- [ ] Docker Compose Plugin verfügbar (`docker compose version`)
- [ ] DNS-Eintrag für `PUBLIC_HOSTNAME` gesetzt und erreichbar
- [ ] Ports 80 und 443 offen (firewall/UFW)
- [ ] Repo ausgecheckt (oder Handoff-ZIP entpackt)

---

## 3 – Konfiguration

```bash
cp .env.prod.example .env.prod
```

Pflichtfelder setzen:

| Variable | Bedeutung |
|---|---|
| `PUBLIC_HOSTNAME` | FQDN, z. B. `onboarding.example.local` |
| `PUBLIC_BASE_URL` | Exakte HTTPS-URL, muss mit Entra Redirect URI übereinstimmen |
| `POSTGRES_PASSWORD` | Sicheres Passwort setzen – **nicht `change_me` lassen** |
| `ENTRA_TENANT_ID` | Aus Schritt 1 |
| `ENTRA_CLIENT_ID` | Aus Schritt 1 |
| `ENTRA_AUDIENCE` | App ID URI, z. B. `api://<client-id>` |
| `ENTRA_CLIENT_SECRET` | Aus Schritt 1 |

Optionale Felder prüfen:

- `DIRECTORY_GROUP_PREFIX` – Präfix der Entra-Sicherheitsgruppen für Rollen-Mapping
- `DIRECTORY_EXPLICIT_GROUP_IDS` – Explizite Gruppen-IDs, wenn kein gemeinsamer Präfix
- `DIRECTORY_SYNC_INTERVAL_MINUTES` – Sync-Takt (Standard: 15)
- `SWAGGER_ENABLED` – muss `false` bleiben oder weggelassen werden

Wichtige Regeln:
- `PUBLIC_BASE_URL` muss `https://` beginnen, sonst bricht der Startup ab
- `SWAGGER_ENABLED=true` in Production führt bewusst zu einem Startup-Abbruch
- `POSTGRES_DB` und `POSTGRES_USER` können auf den Defaults (`appdb`, `app`) bleiben

---

## 4 – Stack starten

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

Status prüfen:

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f
```

Alle vier Services (`db`, `api`, `web`, `proxy`) müssen `healthy` erreichen.

---

## 5 – Smoke-Tests nach Deployment

```bash
curl -k https://<PUBLIC_HOSTNAME>/api/health/live
curl -k https://<PUBLIC_HOSTNAME>/api/health/ready
curl -k https://<PUBLIC_HOSTNAME>/api/health
curl -k https://<PUBLIC_HOSTNAME>/api/auth/provider-info
curl -k -i https://<PUBLIC_HOSTNAME>/api/me
```

Erwartungen:

| Endpoint | Erwartung |
|---|---|
| `/api/health/live` | HTTP 200, `{"status":"ok"}` |
| `/api/health/ready` | HTTP 200, `{"status":"ok","database":"ok"}` |
| `/api/health` | HTTP 200, JSON mit DB- und Auth-Status |
| `/api/auth/provider-info` | JSON mit `"mode":"entra"` |
| `/api/me` | HTTP 401 (kein Token), aber nicht 404 oder 500 |

Browser-Test:
- [ ] `https://<PUBLIC_HOSTNAME>` öffnen → Entra-Login erscheint
- [ ] Login mit einem Benutzer der richtigen Gruppe funktioniert

---

## 6 – Erster Directory-Sync prüfen

Nach dem ersten Start läuft der Directory-Sync automatisch (wenn `DIRECTORY_SYNC_SCHEDULED=true`).

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs api | grep -i "sync"
```

- [ ] Sync läuft ohne Fehler durch
- [ ] Im Admin-Bereich sind Benutzer und Gruppen sichtbar

Hinweis: Ohne erfolgreichen Sync ist die Benutzerliste leer.

---

## 7 – TLS / Zertifikat

Caddy verwendet standardmäßig `tls internal` (selbstsigniertes Zertifikat).

- [ ] Clients im Netz vertrauen dem internen Caddy-Root-Zertifikat, oder
- [ ] Caddyfile wurde für ein externes Zertifikat angepasst

Ohne Zertifikatsvertrauen erscheinen Browser-Warnungen und MSAL kann den Redirect ablehnen.

---

## 8 – Secret-Rotation (bei Bedarf)

Secrets kommen ausschließlich aus der Umgebung – nichts liegt in der Datenbank.

Rotation-Ablauf:
1. Neues Secret in `ENTRA_CLIENT_SECRET` (oder `GRAPH_CLIENT_SECRET`) in `.env.prod` setzen
2. API-Container neu starten: `docker compose ... restart api`
3. Smoke-Tests aus Schritt 5 wiederholen

---

## Schnellreferenz: häufige Fehler

| Symptom | Ursache | Lösung |
|---|---|---|
| API startet nicht | `PUBLIC_BASE_URL` ohne `https://` | In `.env.prod` korrigieren |
| API startet nicht | `SWAGGER_ENABLED=true` in Production | Entfernen oder auf `false` setzen |
| Login schlägt fehl | Redirect URI in Entra stimmt nicht mit `PUBLIC_BASE_URL` überein | Im Azure-Portal anpassen |
| DB `unhealthy` | `POSTGRES_PASSWORD` nicht gesetzt oder DB noch nicht initialisiert | Logs prüfen, Stack neu starten |
| Benutzerliste leer | Kein erfolgreicher Directory-Sync | `ENTRA_CLIENT_SECRET` und Gruppenfilter prüfen |
| Browser-Zertifikatsfehler | Caddy-Root-Zertifikat nicht vertraut | Zertifikat auf Client importieren |
