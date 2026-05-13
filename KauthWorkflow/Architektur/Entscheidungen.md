# Entscheidungen

#architektur #entscheidungen #stabil

Langfristige Architektur- und Produktentscheidungen. Diese ändern sich selten — wenn doch, ist das eine bewusste Richtungskorrektur. Slice-für-Slice-Implementierungsverlauf lebt in `CODE_REVIEW.md` und im git log, nicht hier.

---

## Produktrichtung

### Die App ist eine Workflow-Plattform, kein Onboarding-Tool

Onboarding ist fachlich wichtig, darf aber kein versteckter Produktkern sein. Neue Kernlogik wird am generischen Plattformkern gemessen, nicht daran, ob sie dem Onboarding-Flow hilft. Langfristig laufen viele verschiedene interne Workflows auf derselben Basis.

---

## Backend-Architektur

### Backend ist Source of Truth

Frontend ist Darstellung und Bedienoberfläche. Validierungs- und Businessregeln werden nicht in das Frontend dupliziert; UI-Regeln sind aus dem Backend ableitbar.

### Rollen ≠ Responsibilities

| Begriff | Bedeutung |
|---------|-----------|
| **Rolle** | Technischer Zugriff — wer darf was aufrufen |
| **Responsibility** | Fachliche Ownership — wer ist für was zuständig |

`assignment_type` bleibt strikt: `user` = persönlich, `responsibility` = geteilte Zuständigkeit. Keine impliziten Abkürzungen.

### Workflow-Definition ist eigener Kern

Task-Templates und Prozessarten allein sind nicht das Endmodell. Die Plattform braucht einen expliziten Definition Layer mit Nodes, Edges und Versionierung — sonst können Nicht-Entwickler Workflows nicht ohne Codeänderung pflegen.

### Versionierung ist Pflicht

`workflow_instances` referenzieren immer eine feste Definition-Version. Admin-Änderungen erzeugen neue Versionen; laufende Instanzen brechen nicht.

### Runtime ist mehr als Task-Generierung

Die Runtime aktiviert Nodes, wertet Decision-Bedingungen aus, startet Automationen. Tasks sind nur eine Laufzeitwirkung von mehreren.

---

## Migrationsstrategie

### Migration statt Big Bang

Neue Architektur wird parallel zur Altwelt eingeführt. Altlogik erst nach Stabilität und Parität zurückbauen. Keine voreiligen Löschungen.

---

## Sicherheit & Kontrolle

### Keine freie technische Magie für Admins

Admins sind Fachanwender, keine Entwickler. Freie PowerShell, SQL oder HTTP-Requests mit Secrets wären ein unkontrollierbares Sicherheitsrisiko. Nur freigegebene, validierte Actions; kein freies Scripting.

### Actions sind kontrollierte Produktelemente

Technische Actions wie `CreateAdUser` oder `SendWelcomeMail` sind definierte Bausteine mit bekanntem Verhalten, Retry-Logik und Logging — keine losen Skripte.

---

## Datenmodell & Konsistenz

- Ein Workflow gilt erst als abgeschlossen, wenn alle relevanten Laufzeitpfade sauber beendet sind. Keine UI-Abkürzungen.
- `cancelled` wird nicht auf `completed` gemappt.
- `skipped` ist keine Reparatur für falsch generierte Tasks.
- Parallelität bleibt erlaubt — mehrere Teams oder Rollen können parallel arbeiten.

---

## Identity & Directory

### Die App ist nicht das führende Benutzersystem

AD / Entra liefern die technische Identität. Person und technische Identity bleiben getrennte Konzepte; Details in [[Identity]].

### Gruppen sind Standard für Zugriff

Standardzugriff kommt über Gruppen-Mapping. Lokale Sonderfälle bleiben Ausnahme.

### Entra-Sync setzt keine Zuständigkeiten automatisch

Der Directory-Sync-Zyklus aktualisiert ausschließlich Identitätsdaten (`directory_identities`, `directory_groups`, `app_users`-Felder). Zuständigkeiten (z. B. `department_settings.department_lead_person_id`) werden nicht automatisch aus Entra-Gruppen abgeleitet — sie werden manuell zugewiesen.

**Warum:** Manuelle Admin-Zuweisungen wurden vom Sync überschrieben. Das Modell vermischte „wer ist im System bekannt" (Identität) mit „wer ist für was zuständig" (Responsibility).

### AD/Entra-Schreibrichtung: on-prem AD führt via Windows-Worker

Schreibende Lifecycle-Aktionen (Konto anlegen/deaktivieren, Gruppenmitgliedschaften pflegen, Postfach steuern) laufen ausschließlich gegen **on-prem AD**, ausgeführt von einem **dedizierten Windows-Worker-Service**. Entra-ID wird über AD Connect nachgeführt — die App schreibt **nicht** direkt gegen Microsoft Graph (`EntraGraphClient` bleibt strikt read-only).

**Warum:** Die IT-Landschaft hat on-prem-Primat. Ein Cloud-only-Schreibpfad würde die Schreibhoheit umkehren und ist organisatorisch nicht tragbar. Beidseitige Spiegelung hält in der Praxis nie sauber „beides führend".

**Konsequenz:** Der Stack hat eine zweite Laufzeitkomponente (Windows-Server-VM, domain-joined). Der `WorkflowAutomationHostedService` (Linux-API) bleibt **Orchestrator** und delegiert schreibende Jobs an den Worker — er wird nicht zu einem Linux→AD-Schreiber umgebaut.

### Hybrid-Worker-Sub-Architektur (Detail)

1. **Worker-Deployment**: dedizierte Windows-Server-VM, domain-joined. (Container und Azure-Hybrid-Worker verworfen wegen gMSA-Komplexität.)
2. **Transport API↔Worker**: DB-Polling auf zentrale Postgres mit `target_runtime`-Diskriminator. (HTTPS-Pull und Message-Queue verworfen.)
3. **AD-Schreibmechanik**: `System.DirectoryServices.Protocols` (LDAPS) auf .NET 8. Worker läuft unter gMSA-Kontext mit `AuthType.Negotiate` — keine expliziten Credentials. (PowerShell-Modul und ADSI verworfen.)
4. **Domänen-Auth**: gMSA. Löst ausschließlich die AD-Authentisierung, nicht die DB-Auth.
5. **Audit-Rückkanal**: Worker schreibt direkt in zentrale `automation_jobs` + `automation_job_attempts`. (HTTP-Callback und lokales Logfile + Sync verworfen.)
6. **Postgres-Auth des Workers**: eigener DB-Login `kauth_worker`, Passwort in DPAPI-geschützter Konfig auf der Worker-VM (maschinen- und service-user-gebunden). Postgres-GSSAPI als denkbarer Folge-Slice.
7. **Job-Claim-Sicherheit**: `SELECT … FOR UPDATE SKIP LOCKED` + Lease-Spalten (`claimed_at`/`claimed_by`/`heartbeat_at`). Plus Linux-API-`StaleWorkerClaimSweeper` als Belt-and-Suspenders.

Detail mit Stakeholder-Inputs und verworfenen Varianten: [[Hybrid-Worker-Sub-Architektur]].

### Vault-Grenze ist im Code, nicht in Doku

Sensible temporäre Credentials (z. B. AD-Initial-Passwort) leben verschlüsselt in `temporary_credentials` (pgcrypto). Es gibt **keine Mapping-Source**, die Klartext beim Payload-Build entschlüsseln kann — Konsumenten lesen das Plain-Secret nur Mikrosekunden zur Run-time im Handler-Heap (`ITemporaryCredentialRepository.ReadAdInitialPasswordByVaultIdAsync`). Output-Mapping exponiert ausschließlich die nicht-sensible `credentialVaultId` (UUID).

### Mailbox-SMTP-Strictness: keine UPN-Fallbacks

`CreateMailboxGraph` liefert nur dann `Provisioned`-Success, wenn Graph eine echte SMTP-Adresse aus `proxyAddresses` (uppercase `SMTP:` = primary) oder `mail` liefert — **kein UPN-Fallback**. Wenn Exchange Online die Mailbox nach `assignLicense` noch nicht voll provisioniert hat, kommt `MailboxProvisioningInProgress`-Transient zurück und der Retry liest später nach. Verhindert Welcome-Mails an nicht-existente Adressen.

### Workflow-Engine-Branching auf Automation-Output

Decision-Nodes können über `automation_output`-Bedingungen ihres direkten Predecessors verzweigen, nicht nur über Formular-Antworten. JSON-Discriminator `referenceKind: 'answer' | 'automation_output'` in `workflow_edges.condition_expression`; Bestandsform ohne Discriminator bleibt als `answer`-Default gültig (kein Migration-Skript). Drei Validation-Schranken erzwingen den Scope „nur direkter Predecessor" technisch: Property-/Operator-Union-Schnellcheck im Parser, Direct-Predecessor- und Action-Key-Schranke im Plan-Lauf gegen den Graph. Damit ist der AlreadyExists-Fall ein gültiger Workflow-Pfad, kein Permanent-Failure.

---

## API-Kompatibilität & Naming

### `workflowDefinitionKey` ist der kanonische Name

`workflowDefinitionKey` ist der aktive fachliche Anker überall im System: Builder, Runtime-Engine, Validatoren, Gatekeeper-Regeln, Repositories und Notification-/Link-DTOs. `legacyProcessTypeKey` existiert nur noch als read-only-Fallback für bereits persistierte Workflow-Definitionen im Altbestand — neue Definitionen müssen `workflowDefinitionKey` tragen.

### Action-Mapping-Editor bleibt flach

Der `WorkflowBuilderActionMappingEditor` rendert das Eingabe-Mapping ausschließlich als flache Liste `paramName → {source, value}`. Das Backend kann theoretisch verschachtelte Strukturen, aber keine produktive Action nutzt das. Falls eine Action mit nested-Schema gebraucht wird, gibt es den Power-Modus (raw JSON). Tree-Eingabe wird nicht spekulativ gebaut.

---

## Mitarbeiterakte & Personenverzeichnis

### `/people` als eigener Navigationsbereich

Person ist nicht Anhang eines Workflows, sondern eigenständiger fachlicher Kern. Die 360°-Akte `/people/:personId` hat einen eigenen Listen-/Sucheinstieg für HR und Admin (FE-Feature `peopleDirectory`, BE-Policy `CanAccessPeopleDirectory`).

---

## Dokumentation & Prozess

- Zielbild, Migration und Begriffe müssen im Repo nachvollziehbar sein. Architekturarbeit ohne Doku gilt nicht als fertig.
- Keine großen unstrukturierten Refactors nebenbei. Bei Kernumbauten zuerst Zielmodell und Migrationsschnitt klären.

---

## Verwandte Notizen

- [[Zielarchitektur]] — Was gebaut wird
- [[Migrationspfad]] — In welcher Reihenfolge
- [[Hybrid-Worker-Sub-Architektur]] — Worker-Architektur im Detail
- [[Identity]] — Identity-Modell
- [[Automation]] — Automation Layer
