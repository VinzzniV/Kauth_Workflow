# Migrationspfad

#architektur #migration

Wo das Projekt jetzt steht und wohin es geht. Diese Datei hält das grobe Bild fest — die laufende Slice-Arbeit lebt in `CODE_REVIEW.md` / `TODO.md` und im git log.

---

## Stand

Die Plattform ist als versionierte Workflow-Engine produktiv aufgesetzt. Definition Layer + Runtime existieren parallel und sind für die Kern-Workflows (`onboarding`, `offboarding`, `department_change`) verdrahtet. Rotation/Abteilungsdurchlauf läuft. Automation Layer hat einen produktiv-tauglichen Pfad für AD-/Mailbox-Provisioning über einen Windows-Worker plus Linux-API-Handler für Graph-Calls; Welcome-Mail mit echtem Initial-Passwort funktioniert End-to-End.

Was noch fehlt: ERP-Anbindung (`CreateErpEmployee`), Builder-UI-Komfort für Output-Bedingungen, Hygiene-Slices (Vault-Cleanup, Key-Rotation, License-Pool-Monitoring).

## Reihenfolge der Migration

| Etappe | Inhalt | Stand |
|--------|--------|-------|
| 1 | Artefakt- und Secret-Hygiene | abgeschlossen |
| 2 | Produktkern ent-onboarden (Plattform statt Onboarding-Tool) | laufend |
| 3 | Zielarchitektur dokumentieren, Begriffe harmonisieren | abgeschlossen |
| 4 | Definition Layer einführen | abgeschlossen |
| 5 | Runtime parallel einführen | abgeschlossen |
| 6 | Bestehende Workflows mappen | für Kern-3 abgeschlossen |
| 7 | Task-System an Node-Runtime anbinden | abgeschlossen |
| 8 | Generische DAG-Validierung + Publish-Guard | abgeschlossen |
| 9 | Automation Layer (Basis) | abgeschlossen |
| 9a | Schreibender Automation-Layer (Windows-Worker, AD on-prem führt) | produktiv-tauglich; ERP als Folge-Slice |
| 10 | Guided Builder ausbauen | Basis da, Komfort-Slices offen |
| 11 | Altwelt gezielt zurückbauen | nach Parität |

## Etappe 9a · Schreibender Automation-Layer

Architekturrichtung entschieden 2026-05-12: **AD on-prem führt**, schreibende Lifecycle-Aktionen laufen über einen dedizierten domain-joined Windows-Worker (gMSA, LDAPS). Linux-API behält Read-only-Pfade plus Graph-App-only-Calls (Mail, Mailbox-Lizenz). Details + verworfene Alternativen in [[Entscheidungen]] § „Hybrid-Worker-Sub-Architektur" und [[Hybrid-Worker-Sub-Architektur]].

Was die Etappe konkret liefert:

- **Worker-Skeleton + Transport**: `worker/Worker.sln` mit Core/Host/Tests; DB-Polling auf zentrale Postgres mit `target_runtime`-Diskriminator und Lease-/Heartbeat-Spalten; Linux-API-Sweep für stale Claims.
- **Failure-Klassifikation**: `automation_job_attempts.failure_kind` (`permanent`/`transient`) entscheidet, ob die Retry-Policy weiter versucht oder sofort final-fail liefert. Per-Action-Override-Spalten erlauben langlaufenden Handlern (Entra-Connect-Sync-Wait) ein größeres Retry-Budget, ohne dass alle anderen mit-laufen.
- **AD-Handler im Worker**: `CreateAdUserLdaps` (Pre-Search-Idempotenz, Random-Passwort mit Force-Change) und `AssignGroupsLdaps` (AlreadyMember = idempotent). DPAPI für Worker-Secrets.
- **Graph-Handler in der Linux-API**: `SendWelcomeMailGraph` (App-only) und `CreateMailboxGraph` (Exchange-Lizenz-Assignment mit SMTP-Strictness — kein UPN-Fallback, MailboxProvisioningInProgress als eigene Transient-Variante).
- **Temporary-Credentials-Vault**: pgcrypto-Symmetric-Encryption für das Initial-Passwort. Worker schreibt Vault-Insert + Output-Patch + Job-Success in einer Tx (atomar, idempotent). Decrypt nur in `SendWelcomeMailGraph`; das Klartext-Passwort lebt nirgendwo in `payload_json` oder `output_json`.
- **Output-Verkettung**: enge Mapping-Sources `created_ad_user` (`distinguishedName`, `userPrincipalName`, `credentialVaultId`) und `created_mailbox` (`primarySmtpAddress`). Vault-Grenze ist im Code — keine Mapping-Source kann sensible Felder exponieren.
- **Workflow-Engine-Branching auf Automation-Output**: Decision-Nodes verzweigen jetzt auch über `automation_output`-Bedingungen ihres direkten Predecessors (`referenceKind: 'answer' | 'automation_output'`-Discriminator in `workflow_edges.condition_expression`). Initiale Whitelist `CreateAdUserLdaps.alreadyExisted` — macht den AlreadyExists-Fall zu einem gültigen Workflow-Pfad statt einem Permanent-Failure.

Offen in 9a: `CreateErpEmployee` (ERP-Auswahl wartet auf Stakeholder; Ziel wäre InforLN). Folge-Hygiene: Vault-Cleanup-Sweeper, Key-Rotation, License-Pool-Monitoring, Builder-UI für Output-Bedingungen.

## Parallelzustand Legacy + Neu

Der große Parallelzustand ist abgebaut. `process_types`, alte Onboarding-Alias-Endpunkte, das harte Permission-Array, `task_templates`/`legacyTemplateKey`-Pfad sind durch. Restbrücken: `legacyProcessTypeKey` in Builder-/Publish-Validierung und in Teilen der Master-Data-Leser; interner `WorkflowLegacyStatus` als Übergangsstatus in Engine/Apply; weitere Repository-Schnitte (Runtime/Automation/Audit/Notification noch im Monolith — Rotation ist herausgeschnitten).

## Verwandte Notizen

- [[Zielarchitektur]] — Sollbild der Plattform
- [[Entscheidungen]] — Warum Migration statt Big Bang, getroffene Architektur-Entscheidungen
- [[Hybrid-Worker-Sub-Architektur]] — Detail zur Etappe-9a-Architektur
