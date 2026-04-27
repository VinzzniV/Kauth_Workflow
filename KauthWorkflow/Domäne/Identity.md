# Identity & Rollen

#domäne #identity #rollen

Wie Identität, Personen, Rollen und Zuständigkeiten in diesem System modelliert sind.

---

## Zwei getrennte Konzepte: Person ≠ technische Identity

| Konzept | Was es ist | Woher |
|---------|-----------|-------|
| **Person** | Fachliche/kanonische Identität — der Mitarbeiter als Person | Aus dem System gepflegt, aus Onboarding entstanden |
| **technische Identity** | AD/Entra-Account — technischer Zugang zum System | Aus Entra synchronisiert |

Diese Trennung ist bewusst und darf nicht aufgehoben werden. Die App ist **nicht** das führende Benutzersystem — Entra ist die Quelle der technischen Identität.

---

## Person als fachlicher Anker

Seit dem mitarbeiterzentrierten Lifecycle-Schnitt ist `people` der fachliche Primäranker:

- Neue Onboardings legen zuerst einen `people`-Datensatz an
- Danach startet der Workflow mit `targetPersonId`
- Workflow-Felder halten nur noch einen Snapshot

Wichtige Endpoints:
- `POST /people` — neue Person anlegen
- `GET /people/search` — Personen suchen
- `GET /people/{personId}/workflow-history` — Lifecycle-Verlauf
- `GET /people/rotation-eligible` — für Rotation geeignete Personen

---

## Rollen vs. Responsibilities — Nie vermischen

| Begriff | Bedeutung | Wofür |
|---------|-----------|-------|
| **Rolle** | Technischer Zugriff | Wer darf welchen API-Endpunkt aufrufen |
| **Responsibility** | Fachliche Ownership | Wer ist für welche Aufgaben zuständig |

**Beispiel:** Die IT-Gruppe hat die Rolle `it_staff` (Zugriff) und die Responsibility `laptop_provisioning` (Zuständigkeit für Laptop-Tasks). Das sind zwei verschiedene Konzepte.

Assignment-Typen in Tasks:
- `user` = genau diese Person ist persönlich zuständig
- `responsibility` = geteilte fachliche Zuständigkeit einer Gruppe

Keine impliziten Abkürzungen, keine Vermischung.

---

## Entra-Integration

### Directory-Sync

- Entra liefert technische Identitäten per Sync
- `directory_synced=TRUE` User werden aktiv überwacht
- Entra-gelöschte User werden deaktiviert (soft-delete) und im `system_event_log` protokolliert
- Manuell angelegte User (`directory_synced=FALSE`) bleiben unberührt

### Abteilungsleitung aus Entra

Pro Abteilung wird aus synchronisierten `auth_manager`-Benutzern mit synchronisierter Abteilung genau eine Abteilungsleitung gesucht:

- **Genau ein Treffer:** → `department_lead_person_id` + `requirement_approver_person_id` gesetzt
- **Kein Treffer:** → beide Felder geleert, Audit-Eintrag
- **Mehrere Treffer (Konflikt):** → beide Felder geleert, Konflikt im Audit

Die Admin-Ansicht zeigt: Quelle (`entra_managed` / `manual`), Sync-Status (`resolved` / `missing` / `conflict`).

---

## Abteilungsleiter-Sicht (Berechtigungsmodell)

Abteilungsleiter sehen **nur** explizit aufgelöste, beobachtbare Abteilungen aus:
- `department_settings`
- `department_lead`-Responsibilities
- Department-scoped Permission Grants

**Nicht** durch:
- Globale `auth_manager`-Rolle
- Globale `workflows.view_department`-Berechtigung

Kein abteilungsübergreifender Blick durch Rollen allein.

---

## Gruppen-Mapping

Standard für Systemzugang: Gruppen-Mapping aus Entra.
Lokale Sonderfälle (manuelle Rollen ohne Gruppen-Basis) bleiben Ausnahme.

---

## Wichtige Backend-Dateien

| Datei | Zweck |
|-------|-------|
| `api/API/Services/EntraDirectorySyncService.cs` | Sync-Logik, User-Deaktivierung |
| `api/API/Authorization/AuthorizationPolicyService.cs` | Policy-Auflösung |
| `api/API/Repositories/.../PersonLifecycleOperations.cs` | Personen-Lifecycle |
| `db/01_schema.sql` | Konsolidiertes Schema inkl. People-/Identity-Erweiterungen |

---

## Verwandte Notizen

- [[Workflow]] — Assignment und Task-Sichtbarkeit
- [[Rotation]] — Rotation-Eligibility, Responsibilities
- [[Zielarchitektur]] — Identity- und Betriebsmodell
