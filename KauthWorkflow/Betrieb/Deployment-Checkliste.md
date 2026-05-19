# Deployment-Checkliste

#betrieb #deployment #checkliste

Ausführbare Checkliste für ein sauberes Production-Deployment.
Primärquelle im Repo war: `PRODUCTION_CHECKLIST.md` (in Vault migriert)

Detailbeschreibungen → [[Setup]]
Vollständige Variablen-Übersicht → [[Konfiguration]]

---

## 1 — Entra-App-Registration

- [ ] App Registration angelegt
- [ ] `ENTRA_CLIENT_ID` notiert
- [ ] `ENTRA_TENANT_ID` notiert
- [ ] `ENTRA_CLIENT_SECRET` erstellt
- [ ] `ENTRA_AUDIENCE` gesetzt
- [ ] Redirect URI als SPA eingetragen und exakt auf `PUBLIC_BASE_URL` abgestimmt
- [ ] API Permission `<audience>/access_as_user` freigegeben
- [ ] Microsoft-Graph-Application-Permissions (Admin-Consent) freigegeben: `Mail.Send`, `User.Read.All`, `LicenseAssignment.ReadWrite.All`, `Group.Read.All` (letzteres für `reference_user.groups`-Mapping)
- [ ] **Optional claim `auth_time` für Access Tokens** im Manifest aktiviert (Token configuration → Add optional claim → Token type: **Access** → `auth_time`). Pflicht für Admin-Gated-Automation-Approvals: der `AutomationReauthFreshnessGate` lehnt sonst jeden Re-Auth mit `422 reauth_unconfigured` ab und kein Plan kann freigegeben werden.
- [ ] Gruppen für Rollen-Mapping vorbereitet

---

## 2 — Host-Voraussetzungen

- [ ] Docker Engine installiert
- [ ] `docker compose` verfügbar
- [ ] DNS für `PUBLIC_HOSTNAME` gesetzt
- [ ] Ports 80 und 443 offen
- [ ] Repo oder Handoff-ZIP bereitgestellt

---

## 3 — Konfiguration

```bash
cp .env.prod.example .env.prod
```

Pflichtfelder setzen:
- `PUBLIC_HOSTNAME`
- `PUBLIC_BASE_URL` (muss `https://` beginnen)
- `POSTGRES_PASSWORD`
- `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_AUDIENCE`, `ENTRA_CLIENT_SECRET`

Regeln:
- `SWAGGER_ENABLED=true` ist in Production **verboten**
- Secrets kommen aus der Umgebung, nicht aus der DB

---

## 4 — Stack starten

```bash
chmod +x scripts/start-vm.sh
./scripts/start-vm.sh prod
```

Danach prüfen:

```bash
./scripts/start-vm.sh prod status
./scripts/start-vm.sh prod logs
```

---

## 5 — Smoke-Tests

```bash
curl -k https://<PUBLIC_HOSTNAME>/api/health/live
curl -k https://<PUBLIC_HOSTNAME>/api/health/ready
curl -k https://<PUBLIC_HOSTNAME>/api/health
curl -k https://<PUBLIC_HOSTNAME>/api/auth/provider-info
curl -k -i https://<PUBLIC_HOSTNAME>/api/me
```

Erwartung:
- `/api/health/live` → `200`
- `/api/health/ready` → `200` bei erreichbarer DB
- `/api/health` → Deep-Health-JSON
- `/api/auth/provider-info` → `mode=entra`
- `/api/me` ohne Login → `401`

Browser:
- [ ] `https://<PUBLIC_HOSTNAME>` öffnet
- [ ] Entra-Login funktioniert
- [ ] **Approval-Re-Auth-Smoke:** Admin öffnet eine Automation-Task mit Bundle, klickt „Plan bestätigen", sieht den MSAL-Popup mit `prompt: 'login'`, schließt ihn ab und der Plan läuft. Sieht der Admin stattdessen die Fehlermeldung „auth_time-Claim fehlt im Access-Token", ist der optional claim `auth_time` in der App-Registration nicht gesetzt (Schritt 1).

---

## 6 — Directory-Sync prüfen

- [ ] Erster Sync läuft ohne Fehler
- [ ] Benutzer und Gruppen sind im Admin-Bereich sichtbar

---

## 7 — TLS

- [ ] Clients vertrauen dem verwendeten Zertifikat
- [ ] Redirects und Login funktionieren ohne Browser-Warnungen

---

## 8 — Secret-Rotation

1. Neues Secret in `.env.prod` setzen
2. API-Container neu starten
3. Smoke-Tests wiederholen

---

## Verwandte Notizen

- [[Setup]] — Details zu Konfiguration und Deployment
