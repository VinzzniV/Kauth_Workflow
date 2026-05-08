# Einheit 1 — Die große Landkarte

> Erste Lerneinheit aus der Projekt-Tour. Ziel: Verstehen, woraus die Anwendung besteht und wie die Teile zusammenspielen.

## Was ist die Anwendung?

Eine **Webanwendung** mit drei Schichten:

```
[Browser] <---HTTP---> [Server] <---SQL---> [Datenbank]
 (Frontend)            (Backend)             (Storage)
```

- **Frontend** = läuft im Browser. HTML, CSS, JavaScript. Zeigt die UI.
- **Backend** = Programm auf einem Server. Nimmt Anfragen entgegen, führt Logik aus, redet mit der Datenbank.
- **Datenbank** = persistenter Speicher. Bleibt erhalten, auch wenn der Server neu startet.

Kommunikation:
- Browser ↔ Backend: **HTTP** (gleiches Protokoll wie beim Aufrufen einer Website)
- Backend ↔ Datenbank: **SQL** (eigene Sprache für Daten-Operationen)

## Die drei Schichten im Repo

```
kauth_workflow/
├── api/              ← Backend (C# / ASP.NET Core)
├── web/              ← Frontend (React / TypeScript)
├── db/               ← Datenbank-Schema (SQL-Dateien)
├── KauthWorkflow/    ← Doku (Obsidian-Vault, KEIN Code)
└── *.md              ← Steuerungs-Dateien für die KIs
```

## Was läuft wo?

| Ordner | Sprache | Was läuft das? | Wer startet es? |
|---|---|---|---|
| `api/` | C# | ASP.NET Core Web-Server | `dotnet run` |
| `web/` | TypeScript + React | Vite Dev-Server (ab Port 8080, mit Fallback) | `npm run dev` |
| `db/` | SQL | PostgreSQL-Datenbank | Docker / lokal installiert |

Lokal entwickeln = **3 Prozesse parallel**: Backend, Frontend, Datenbank.

## Beispiel-Datenfluss: "Workflows anzeigen"

1. **Browser**: Klick → React macht `GET http://localhost:5xxx/workflows`
2. **Backend**: Endpoint nimmt Anfrage an → ruft Service → ruft Repository
3. **Repository**: SQL-Abfrage `SELECT * FROM workflows ...`
4. **Datenbank**: Antwortet mit Daten
5. **Backend**: Verpackt als JSON, schickt zurück
6. **Frontend**: Empfängt JSON, rendert Tabelle

→ **Jede Aktion in der App folgt diesem Muster.**

## Steuerungs-Dateien im Root

Kein Code, sondern Doku/Steuerung für die KIs:

| Datei | Wofür |
|---|---|
| `CLAUDE.md` | Anweisungen an Claude |
| `CLAUDE_CONTROL.md` | operative Claude-Regeln unter Codex-Orchestrierung |
| `TODO.md` | Aktuelle Aufgabenliste |
| `CODE_REVIEW.md` | Code-Review-Befunde |
| `CODE_REVIEW_ARCHIVE.md` | Detailarchiv abgeschlossener Review-Zyklen |
| `CODEX_SYNC.md` | Handoff-Protokoll Codex ↔ Claude |
| `CODEX_SYNC_ARCHIVE.md` | aeltere Handoff-Historie |
| `MEMORY.md` | kurzer aktueller Session-Fokus |
| `PROJECT_CONTEXT.md` | Übergeordneter Projekt-Kontext |
| `DOCS_CONTROL.md` | Welche Doku wann zu lesen ist |

## Der Vault `KauthWorkflow/`

Sammlung von Markdown-Notizen (Obsidian).

- `Architektur/` = Wie das System aufgebaut sein soll
- `Domäne/Begriffe.md` = Glossar wichtiger Begriffe
- `Domäne/` = Fachliche Konzepte (Workflow, Rotation, Identity, ...)
- `Stand/` = Aktueller Code-Review-Stand
- `Arbeit/` = Wie KI und Mensch zusammenarbeiten
- `Betrieb/` = Deployment, Monitoring
- `Lernen/` = Diese Lerneinheiten

## Merksätze

> **Drei Schichten:** Frontend (`web/`), Backend (`api/`), Datenbank (`db/`).
> Sie reden über HTTP und SQL.
>
> **Jede Aktion folgt demselben Muster:** Klick → HTTP-Anfrage → Endpoint → Service → Repository → Datenbank → zurück.
>
> **Doku ist getrennt vom Code:** Vault unter `KauthWorkflow/`, Steuerungs-MDs im Root.

## Vorab-Definition: Endpoint, Service, Repository

```
Endpoint     → "HTTP-Anfrage kommt rein, ich nehme sie entgegen"
   ↓
Service      → "Ich entscheide, WAS getan werden muss" (Logik, Regeln)
   ↓
Repository   → "Ich rede mit der Datenbank" (nur SQL, keine Logik)
```

- **Endpoint** = HTTP-Türsteher (Anfrage rein, Antwort raus). Eine URL, die das Backend anbietet.
- **Service** = Denker. Geschäftslogik: Berechtigungen, Validierung, Workflow-Regeln, Datenumformung.
- **Repository** = Datenholer. Führt SQL aus, liefert Rohdaten zurück.

Beispiel `GET /workflows`:
1. Endpoint: nimmt Anfrage entgegen, ruft Service
2. Service: prüft Berechtigung, filtert nach Sichtbarkeit, ruft Repository
3. Repository: macht `SELECT * FROM workflows ...`, liefert Rows
4. Service: wandelt Rows in DTO um
5. Endpoint: schickt DTO als JSON zurück

Prinzip dahinter: **Separation of Concerns** — jede Schicht hat genau eine Verantwortung.

## Offene Begriffe (kommen in Einheit 2 mit Code)

- DTO
- JSON
- Routing
- Dependency Injection
