# Code-Landkarte

#code-landkarte #index

Brücke zwischen fachlichem Verständnis (was tut der Bereich) und technischer Datei-Topologie (wo liegt der Code). Ergänzt die rein technischen Doku-Einstiege ([[01-Die-grosse-Landkarte]], `PROJECT_STRUCTURE.md`).

**Wann hilft die Code-Landkarte:** „Ich will an Department-Change ran — wo schaue ich?" / „Wo liegt das, was Mailbox-Provisioning macht?" / „Wo sind die Tests für Workflow-Validation?"

**Wann nicht:** Wenn du dich technisch durchklicken willst (Endpoint → Service → Repository), nimm direkt den Code oder [[02-Anfrage-durch-den-Code]].

---

## Bereiche

| Karte | Was steckt drin |
|---|---|
| [[Workflow-Builder]] | Workflow-Definition, Versionierung, Builder-UI, Publish-Validation |
| [[Workflow-Runtime]] | Engine, Lifecycle-Service, Plan-/Apply-Loop, Node-Aktivierung |
| [[Tasks-und-Approvals]] | Task-Generierung, Task-Lifecycle, Supervisor-Bridge-Skip |
| [[Workflow-Typen]] | Onboarding / Offboarding / Department-Change / Name-Change — Definition-Seeds, fachliche Eigenheiten |
| [[Rotation]] | Department-Rotation, Pläne, Stationen, Cycle-Generierung |
| [[People-und-360-Karte]] | Personen, Directory-Sync (Entra), Mitarbeiterakte |
| [[Automation]] | Handler, Worker, Mapping-Sources, Vault, Retry-Policy |
| [[Notifications-und-Mail]] | Mail-Templates, Dispatch, Notification-Catalog |
| [[Auth-und-Permissions]] | Login (Entra / Dev-Sim), Policies, Rollen, Responsibilities |
| [[Querschnitt]] | Sweeper, System-Events, Audit-Log, Background-Services |

---

## Konventionen

- Jede Karte listet **primäre** Files für den Bereich. Files mit Cross-Cutting-Charakter (z. B. `WorkflowLifecycleService.cs` für Runtime + Tasks) erscheinen in der Karte, in die sie primär gehören, und werden aus anderen Karten **verlinkt** statt dupliziert.
- Datei-Pfade sind relativ zum Repo-Root.
- DB-Tabellen sind ohne `public.`-Präfix.
- Tests-Patterns nutzen Glob-Notation (`api/API.Tests/*Workflow*Tests.cs`).
- Cross-Links: Domänen-Doku (das **Was**) ↔ Code-Karte (das **Wo**).

## Pflege

Beim Slice-Abschluss aktualisiert die KI die betroffene Code-Karte mit. Neue Service-/Repository-Datei → in passende Karte rein. Umbenannte Datei → in der Karte umbenennen. Reine internal Helper-Files brauchen nicht in die Karte (sie würden sie zumüllen — die Karte zeigt die **Einstiegspunkte**, nicht jeden Helper).

## Verwandte Notizen

- [[01-Die-grosse-Landkarte]] — Technische Schichten (Browser → Server → DB)
- [[02-Anfrage-durch-den-Code]] — Konkrete Tour durch einen Endpoint
- `PROJECT_STRUCTURE.md` (Repo-Root) — Verzeichnis-Layout
