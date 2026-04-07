# Production Checklist

Knappe, ausfuehrbare Checkliste fuer ein sauberes Production-Deployment.
Details stehen in `SETUP.md`.

---

## 1 - Entra-App-Registration

- [ ] App Registration angelegt
- [ ] `ENTRA_CLIENT_ID` notiert
- [ ] `ENTRA_TENANT_ID` notiert
- [ ] `ENTRA_CLIENT_SECRET` erstellt
- [ ] `ENTRA_AUDIENCE` gesetzt
- [ ] Redirect URI als SPA eingetragen und exakt auf `PUBLIC_BASE_URL` abgestimmt
- [ ] API Permission `<audience>/access_as_user` freigegeben
- [ ] Gruppen fuer Rollen-Mapping vorbereitet

---

## 2 - Host-Voraussetzungen

- [ ] Docker Engine installiert
- [ ] `docker compose` verfuegbar
- [ ] DNS fuer `PUBLIC_HOSTNAME` gesetzt
- [ ] Ports 80 und 443 offen
- [ ] Repo oder Handoff-ZIP bereitgestellt

---

## 3 - Konfiguration

```bash
cp .env.prod.example .env.prod
```

Pflichtfelder setzen:
- `PUBLIC_HOSTNAME`
- `PUBLIC_BASE_URL`
- `POSTGRES_PASSWORD`
- `ENTRA_TENANT_ID`
- `ENTRA_CLIENT_ID`
- `ENTRA_AUDIENCE`
- `ENTRA_CLIENT_SECRET`

Wichtige Regeln:
- `PUBLIC_BASE_URL` muss mit `https://` beginnen
- `SWAGGER_ENABLED=true` ist in Production verboten
- Secrets kommen aus der Umgebung, nicht aus der Datenbank

---

## 4 - Stack starten

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml up -d --build
```

Danach:

```bash
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml ps
docker compose --env-file .env.prod -f compose.yml -f compose.prod.yml logs -f
```

---

## 5 - Smoke-Tests

```bash
curl -k https://<PUBLIC_HOSTNAME>/api/health/live
curl -k https://<PUBLIC_HOSTNAME>/api/health/ready
curl -k https://<PUBLIC_HOSTNAME>/api/health
curl -k https://<PUBLIC_HOSTNAME>/api/auth/provider-info
curl -k -i https://<PUBLIC_HOSTNAME>/api/me
```

Erwartung:
- `/api/health/live` -> `200`
- `/api/health/ready` -> `200` bei erreichbarer DB
- `/api/health` -> Deep-Health-JSON
- `/api/auth/provider-info` -> `mode=entra`
- `/api/me` ohne Login typischerweise `401`

Browser-Test:
- [ ] `https://<PUBLIC_HOSTNAME>` oeffnet
- [ ] Entra-Login funktioniert

---

## 6 - Directory-Sync pruefen

- [ ] erster Sync laeuft ohne Fehler
- [ ] Benutzer und Gruppen sind im Admin-Bereich sichtbar

---

## 7 - TLS

- [ ] Clients vertrauen dem verwendeten Zertifikat
- [ ] Redirects und Login funktionieren ohne Browser-Warnungen

---

## 8 - Secret-Rotation

1. Neues Secret in `.env.prod` setzen
2. API-Container neu starten
3. Smoke-Tests wiederholen
