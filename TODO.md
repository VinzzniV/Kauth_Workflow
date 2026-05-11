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

**Z20 eroeffnet 2026-05-11.** Admin/Directory/Runtime Read Contracts Phase 2 — die nach Z10/Z11 bewusst zurueckgestellten Read-/Listen-Vertraege (Block A, C, E, F, G, H) werden als ein breiter Folgeblock geschnitten. **Z20-S1 (Inventur + Vertrags-Skizze + Slice-Plan) ist Doku-only abgeschlossen.** Bundles B1..B4 stehen zur Umsetzung an.

**Praktisch:** P1-/P2-/P3-Adapter sind seit Z11 etabliert; einzelne Mikro-Slices je Endpunkt waeren reine Wiederholungsarbeit. **Lohnenswert:** vier thematisch enge Bundles loesen den gesamten Restblock auf, statt sieben Mini-Zyklen mit identischem Doku-Overhead. **Nutzen:** konvergenter Admin-/Directory-/Runtime-Read-Vertrag; FE konvergiert auf zwei Hulls + ein P3-Lookup-Pattern.

Z19 vollstaendig abgeschlossen (2026-05-11). Z18 + Entra-Retrofit-Block (A1, A2, A3, B, C) abgeschlossen am 2026-05-08.

### Aktive Slices Z20

| Slice | Inhalt | Prio | Modell/Effort | Status |
|-------|--------|------|---------------|--------|
| Z20-S1 | Inventur + Vertrags-Skizze + Slice-Plan (Doku-only) | HIGH | `claude-opus-4-7` + `--effort high` | **done 2026-05-11** |
| Z20-B1 | Block H + Block E Preview-Lookups: `GET /workflow-definitions/startable` + `GET /admin/notification-templates/preview-targets/{workflows,rotation-plans}` auf P3-Lookup-Adapter (Server-`search`/`limit`, FE-Wrapper). | HIGH | `claude-sonnet-4-6` + `--effort medium` | offen — naechster Schritt |
| Z20-B2 | Block C: `GET /admin/directory/identities` auf P1; **Composite-Split** `/admin/directory/responsibility-gaps` + `/admin/directory/pending-imports`. | HIGH | `claude-opus-4-7` + `--effort high` | offen |
| Z20-B3 | Block E (Liste) + F + G: `GET /admin/notification-templates` P1, `GET /admin/rotation/action-templates` P1, `GET /admin/runtime/workflow-instances/{uid}/events` P2/Cursor, `GET /admin/runtime/workflow-instances/{uid}/automation-jobs` P2/Cursor. | HIGH | `claude-sonnet-4-6` + `--effort medium` | offen |
| Z20-B4 | Block A: `/admin/auth/{users,groups,permissions,roles}` auf P1 + `/admin/directory/unlinked-identities` P1. | MEDIUM | `claude-sonnet-4-6` + `--effort medium` | offen |

Detail in `CODE_REVIEW.md` § „Aktiver Zyklus 20" (S1-Findings, Reihenfolge-Begruendung, Test-Erwartung).

**Bewusst NICHT in Z20:** Schreibpfade der genannten Endpunkte; breite Architektur-Umbauten am Definition-/Runtime-/Automation-Layer; Berechtigungsmodell-Aenderungen; Mobile-/Tablet-Layout (R10); Z16-S4 Automation-Snapshot-Vertrag (deferred); Z8-3.2/#8 `RegenerateDepartmentPlansAsync` (deferred); `/admin/people` Vertragsaenderung (Z16-S2-Anker bleibt unveraendert); Filterzustand-URL-Persistenz fuer Builder-Tabs (Restgrenze aus Z11-F3).

## Zyklusuebergreifend offen

| ID | Aufgabe | Quelle | Status |
|----|---------|--------|--------|
| R8 | Browser-Verifikation Form-Editor (alle 12 Schritt-Typen) | L7 | offen — Nutzer-Aufgabe |
| R10 | Handy/Tablet-Layout fuer Form-Editor (≥1024px aktuell) | L7-Backlog | backlog — kein konkreter Bedarf |
| L2 | Datenbereinigung fuer Drafts/abgebrochene Plaene/stornierte Aufgaben | Zyklus 1 | deferred — wartet auf Produkt-Entscheidung |
| Z8-3.2/#8 | `RotationTaskGenerationService.RegenerateDepartmentPlansAsync` Schleife | Zyklus 8 | deferred — admin-getriggert, kein kleiner SQL-Hebel |

---

## Geplante Features (Z19-Kandidaten)

Analysiert und umgesetzt am 2026-05-08. Der Feature-Block A1-A3/B/C ist abgeschlossen.

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

#### A3 – Mitarbeiterkarte: fehlende Felder + retroaktiver Status ✅ abgeschlossen (2026-05-08)

`PATCH /admin/people/{personId}` implementiert (entry_date, badge_number). Inline-Edit in `PersonOverviewSection` (Admin-only). Lücken-Warnung wenn Felder leer. Badge „Retroaktiv importiert" wenn kein Onboarding vorhanden. Erklärender Text im leeren Vorgangsbereich. Build: 0 CS-Fehler, 0 TS-Fehler, 280 FE-Tests grün.

---

### Feature B – Entra-Stellenbezeichnungen in Abteilungs-Stellen importieren ✅ abgeschlossen (2026-05-08)

`GET /admin/master-data/departments/{id}/entra-job-titles` und `POST …/positions/import-from-entra` implementiert. Checkbox-UI als `AdminDepartmentEntraImportSection` in `AdminOrganizationDepartmentEditor` eingebettet; bereits vorhandene Stellen markiert und deaktiviert. Build: 0 BE-Fehler, 0 FE-Fehler. Tests: 494 BE grün, 280 FE grün.

---

### Feature C – Mitarbeiter-Verzeichnis: Entra-only-Personen sichtbar machen ✅ abgeschlossen (2026-05-08)

`GetPeopleDirectory` liefert jetzt per `UNION ALL` auch aktive `directory_identities` ohne `people`-Record. Diese Einträge erscheinen als `directory_only` mit `personId = null` und `directoryIdentityId`.
`PeopleDirectoryPage` unterscheidet echte Mitarbeiterkarten von Verzeichnis-only-Einträgen sauber: kein Karten-Link ohne `personId`, stattdessen Inline-Import-Button pro Zeile/Karte via `POST /admin/people/import-from-directory`. Build: 0 BE-Fehler, 0 FE-Fehler. Backend-Tests: 494/494 grün. FE-Build: grün. Voller FE-Testlauf: 1 vorliegender Fehler in `tests/MyTasksPage.test.tsx`, außerhalb des C-Slices.

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
