# TODO.md

## Zweck

- aktive Arbeitsplanung fuer Review-Nacharbeit
- nur offene oder unmittelbar relevante Arbeit

## Primaerquelle fuer

- naechsten Arbeitsschritt
- Reihenfolge der offenen Slices

## Nicht verwenden fuer

- lange Historie abgeschlossener Slices
- Architekturargumentation
- Session-Notizen

## Wann aktualisieren

- wenn ein neuer Zyklus startet
- wenn sich Priorisierung aendert
- wenn eine Aufgabe abgeschlossen oder deferred wird
- wenn ein aktiver Zyklus beendet wird; abgeschlossene Detailbloecke danach aus dieser Datei entfernen oder ins Archiv verweisen

## Verwandte Dateien

- `CODE_REVIEW.md`
- `MEMORY.md`
- `CODEX_SYNC.md`
- `FRONTEND_TODO.md`

---

## Pflicht vor jeder Aufgabe

Vor jeder Aufgabe muss die KI zuerst `CODE_REVIEW.md` lesen.

Zusaetzlich immer mitlesen:
- `DOCS_CONTROL.md`
- `PROJECT_CONTEXT.md`
- `MEMORY.md`

Schreibregel: jedes neue Review-Finding / jeder Slice muss neben dem technischen Befund kurz erklaeren, was er praktisch bedeutet, warum es sich lohnt, ihn anzugehen, und was dadurch besser, sicherer, schneller oder wartbarer wird. Detail in `CODE_REVIEW.md` § „Schreibregel" und `CLAUDE_CONTROL.md`.

---

## Aktueller Review-Status

**Z18 abgeschlossen (2026-05-08).** Alle 9 Findings erledigt. Kein aktiver Zyklus offen. Naechster Schritt: Codex priorisiert neuen Zyklus.

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Geplante Features (Z19-Kandidaten)

Analysiert 2026-05-08. Noch kein aktiver Zyklus gestartet. Reihenfolge ist Empfehlung, nicht fest.

---

### Hintergrund: Warum diese Features jetzt entstehen

kauth_workflow wird in ein Unternehmen eingeführt, das bereits eine vollständige Belegschaft hat. Die Mehrheit der bisherigen Onboardings lief manuell, bevor kauth_workflow existierte. Das bedeutet:

- Es gibt bereits 20–50+ Mitarbeitende mit Entra/AD-Konten, Computern und teils langen Betriebszugehörigkeiten.
- Diese Personen haben **keine `people`-Records** in kauth_workflow und damit **keine Mitarbeiterkarte**.
- Einige von ihnen haben bereits kauth_workflow-Accounts (als Admin, HR, Supervisor oder Worker), um das System zu bedienen — aber das ist eine andere Identität als „Mitarbeiter dieses Unternehmens".
- Für zukünftige Vorgänge (Offboarding, Stellenwechsel, Automatisierungen) muss kauth_workflow wissen wer diese Personen sind.

**Das zentrale Architekturproblem:**

Im System gibt es drei verschiedene Identitäten einer Person:

```
[Entra/AD]                [kauth_workflow Login]    [kauth_workflow Subjekt]
directory_identities  →   app_users             →   people
─────────────────────     ──────────────────────     ──────────────────────
Wer hat ein AD-Konto?     Wer loggt sich ein?        Über wen laufen Vorgänge?
```

Aktuell entsteht ein `people`-Record fast ausschließlich durch das Anlegen eines Onboarding-Vorgangs. Für alle die vor kauth_workflow schon da waren, fehlt dieser Record. Der bestehende Import-Pfad (`/admin/directory/import`) ist ungeeignet, weil er für jeden importierten Entra-Nutzer einen Login-Account anlegt — das ist für normale Mitarbeitende weder gewollt noch sinnvoll.

**Zusätzlicher Sonderfall:** Mitarbeitende die nachträglich eine Supervisor- oder Admin-Rolle bekommen (weil sich Zuständigkeiten ändern) brauchen sowohl einen `app_user` (Login) als auch einen `people`-Record (Mitarbeiterkarte). Aktuell existieren diese unkontrolliert nebeneinander ohne Verknüpfung.

**Was diese Features zusammen lösen:**
1. Alle bestehenden Entra-Mitarbeitenden retroaktiv mit vollständiger Mitarbeiterkarte ins System holen — ohne Login-Accounts zu vergeben.
2. Bestehende `app_users` automatisch mit ihrem `people`-Record verbinden.
3. Fehlende Stammdaten (Eintrittsdatum, Ausweisnummer) nachträglich auf der Mitarbeiterkarte erfassbar machen.
4. Entra-Berufsbezeichnungen direkt als Abteilungs-Stellen übernehmen können.
5. Noch nicht importierte Entra-Personen im Mitarbeiter-Verzeichnis sichtbar machen.

---

### Feature A – Retroaktiver Mitarbeiter-Import aus Entra

**Kontext:** kauth_workflow wird in eine bestehende Belegschaft eingeführt. Mitarbeitende existieren bereits in Entra/AD, haben aber keine `people`-Records und damit keine Mitarbeiterkarte. Ziel: alle bestehenden Entra-Mitarbeitenden retroaktiv anlegen, damit zukünftige Offboarding-, Änderungs- und Automatisierungs-Vorgänge möglich sind — ohne ihnen einen kauth_workflow-Login zu geben. Bestehende `app_users` (z.B. IT-Admin mit Supervisor-Rolle) werden dabei mit dem jeweiligen `people`-Record verbunden.

#### A1 – Backend-Fundament ✅ abgeschlossen (2026-05-08)

`POST /admin/people/import-from-directory` und `GET /admin/directory/unlinked-identities` implementiert.
Dedup via `directory_identity_id`, `employee_number` und `app_user_id`. Auto-Link zu bestehendem `app_user` via `entra_object_id`. Audit-Log in `person_match_audit_log`. Build: 0 CS-Fehler, 424 Tests gruen. Bereits im Repo gestaged (inkl. `job_title`-WIP-Durchleitung).

#### A2 – Import-UI (Admin) ✅ abgeschlossen (2026-05-08)

Neue Admin-Sektion „Aus Entra importieren" unter `personen_zugriff`. Selbstständige Komponente `AdminEntraImportSection` mit useQuery/useMutation. Abteilungs-Gruppierung, per-Gruppe und global Select-All, Vorschau (Name, UPN, Stelle, Konto-Status), Toggle für deaktivierte Konten (standardmäßig ausgeblendet), Import-Button mit Ergebnisbanner. Build: 0 TS-Fehler, 280 Tests grün.

#### A3 – Mitarbeiterkarte: fehlende Felder + retroaktiver Status

| Feld | Wert |
|------|------|
| **Was** | `PersonWorkflowHistoryPage` erweitern: Inline-Bearbeitung fehlender Felder (Eintrittsdatum, Ausweisnummer) direkt auf der Karte. Lücken-Warnung wenn Pflichtfelder leer. Neuer Status-Badge „Retroaktiv importiert" wenn kein Onboarding-Vorgang vorhanden. Leerer Vorgangs-Bereich mit erklärendem Text statt leerem Panel. |
| **Warum** | Ohne Bearbeitungsmöglichkeit entstehen dauerhaft unvollständige Karten. Der Status verhindert Verwechslung mit regulär ongeboardeten Mitarbeitenden. |
| **Effort** | `medium` — bestehende Page erweitern, neue API-Mutation für Inline-Edit (PATCH /admin/people/{personId}), kein neues Konzept |
| **Modell** | `claude-sonnet-4-6` |
| **CLI** | `--model claude-sonnet-4-6 --effort medium` |

---

### Feature B – Entra-Stellenbezeichnungen in Abteilungs-Stellen importieren

**Kontext:** `directory_identities.job_title` enthält die Entra-Berufsbezeichnungen. „Stellen dieser Abteilung" (`app_roles` mit `role_kind = 'position'`) werden aktuell nur manuell angelegt. Der Admin soll mit einem Klick alle eindeutigen Entra-Bezeichnungen der Abteilung als Vorschlag sehen und per Checkbox importieren.

| Feld | Wert |
|------|------|
| **Was** | Neuer Endpunkt `GET /admin/master-data/departments/{id}/entra-job-titles` (DISTINCT job_title per department_name-Match) · Neuer Endpunkt `POST /admin/master-data/departments/{id}/positions/import-from-entra` · Checkbox-UI in `AdminOrganizationDepartmentEditor.tsx`, bereits vorhandene als markiert |
| **Warum** | Manuelle Erfassung ist fehleranfällig, besonders beim retroaktiven Bootstrapping. Die Daten liegen bereits vor. |
| **Effort** | `low` — klar abgegrenzter Scope, nutzt bestehende `CreateDepartmentPosition`-Logik intern |
| **Modell** | `claude-sonnet-4-6` |
| **CLI** | `--model claude-sonnet-4-6 --effort low` |

---

### Feature C – Mitarbeiter-Verzeichnis: Entra-only-Personen sichtbar machen

**Kontext:** `GetPeopleDirectory` zeigt nur `people`-Records. Entra-Mitarbeitende ohne Import bleiben unsichtbar. Als Übergangslösung (vor oder statt Feature A) sollen diese über einen UNION sichtbar sein — mit Inline-Import-Button pro Person.

| Feld | Wert |
|------|------|
| **Was** | `GetPeopleDirectory`-SQL um `UNION ALL` auf `directory_identities` (ohne people-Record, `account_enabled = true`) erweitern. Status `directory_only` einführen. Kein `personId` → statt Karten-Link ein Inline-„Importieren"-Button der `POST /admin/people/import-from-directory` für genau diese Identity aufruft. |
| **Warum** | Sofortige Sichtbarkeit ohne Schema-Eingriff. Nützlich als Schnelllösung oder als Ergänzung zu A2. |
| **Effort** | `medium` — UNION ändert Abfrage-Semantik (Ordinal-Verschiebung, nullable personId), Frontend muss zwei Record-Typen unterscheiden |
| **Modell** | `claude-sonnet-4-6` |
| **CLI** | `--model claude-sonnet-4-6 --effort medium` |
| **Abhängigkeit** | Setzt A1 voraus (Import-Endpoint muss existieren) |

---

## Abgeschlossene Zyklen

- Zyklen 7 bis 16 sind abgeschlossen.
- Kurzfassungen und Begruendungen stehen in `CODE_REVIEW.md`.
- Detailspiegel stehen in `CODE_REVIEW_ARCHIVE.md`, `CODEX_SYNC.md` und `KauthWorkflow/Stand/Code-Review-Status.md`.

---

## Arbeitsregel

Vor dem Start einer Aufgabe immer explizit nennen:
1. welche Aufgabe als naechstes ansteht
2. welches Reasoning sinnvoll ist
3. welches Modell empfohlen ist
4. wenn Claude per CLI laeuft: `--model` und `--effort` explizit setzen, nicht nur im Prompt empfehlen
