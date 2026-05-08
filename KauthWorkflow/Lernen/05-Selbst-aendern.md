# Einheit 5 — Selbst Änderungen machen

> Der Loop: Code lesen → Ändern → Bauen → Testen → Sehen → wiederholen.

## Phase 1: Lokal starten

| Was | Befehl | Wo |
|---|---|---|
| Datenbank | `docker compose -f compose.dev-db.yml up -d` | Hintergrund |
| Backend | `cd api/API; dotnet run` | Konsole |
| Frontend | `cd web; npm run dev` | zweite Konsole |

Oder kurz: `.\start.ps1 dev`

Browser → `http://localhost:8080/` oder den naechsten freien Vite-Port

### Wenn was nicht läuft
- Backend startet nicht → DB nicht da, oder Port belegt
- Frontend startet nicht → `npm install` vergessen?
- Frontend läuft, Daten leer → Backend nicht erreichbar (F12 → Browser-Konsole)

## Phase 2: Eine kleine Änderung

### Beispiel: einen UI-Text ändern

**Schritt 1 — finden:**
Strg+Shift+F im VS Code → globale Suche → Text suchen → klicken.

**Schritt 2 — ändern:**
```tsx
// vorher
if (isLoading) return <p>Lade…</p>;

// nachher
if (isLoading) return <p>Daten werden geladen…</p>;
```

**Schritt 3 — speichern:** Strg+S
Vite hat **Hot Module Reloading** → Browser lädt automatisch nach.

**Schritt 4 — rückgängig:**
```bash
git status              # zeigt geänderte Dateien
git diff                # zeigt was geändert wurde
git checkout <file>     # macht Änderung rückgängig
```
Oder VS Code Source-Control-Tab → Discard Changes.

## Phase 3: Vor dem Committen — Bauen und Testen

### Backend-Änderung
```powershell
dotnet build api/API/API.sln
cd api/API.Tests
dotnet test
```

### Frontend-Änderung
```powershell
cd web
npx tsc --noEmit       # Compile-Check
npm test               # Tests
```

→ Beide grün = sicher zum Committen.
→ Rot = Fehlermeldung **konkret** lesen, sie sagt fast alles.

## Phase 4: Übungsaufgabe — Vertikale Änderung

> **"Im Dashboard eine zusätzliche Spalte 'Erstellt am' anzeigen."**

Schichten, die du anfasst:
1. **Backend-DTO** in `api/API/Contracts/...Dtos.cs` → Feld `CreatedAt`
2. **Backend-Repository** → SQL-Query erweitern
3. **Frontend-Typ** in `web/src/types/...` → Spiegelfeld
4. **Frontend-Mapper** → Übersetzung
5. **Frontend-Component** → neue Tabellen-Spalte
6. **Build & Tests** beide Seiten

Klassische "Feature-Schicht": eine Änderung wandert durch die ganze Vertikale.

## Phase 5: Wenn du steckenbleibst

1. Fehlermeldung **genau** lesen
2. Datei + Zeile öffnen
3. `git diff` — was habe ich verändert?
4. Konkret fragen:
   - Schlecht: "Funktioniert nicht."
   - Gut: "Beim `npm test` kommt: `[Fehler]`. Datei `XYZ.tsx` Zeile 42 geändert: `[Diff]`. Was läuft falsch?"

## Phase 6: Der Loop

```
1. PROBLEM:     "Was soll am Ende anders sein?"
2. CODE LESEN:  "Wo passiert das gerade?"
3. ÄNDERN:      "Welche Zeile muss anders aussehen?"
4. BAUEN:       "Compiliert es noch?"
5. TESTEN:      "Bestehende Tests noch grün?"
6. AUSPROBIEREN:"Macht es wirklich, was ich wollte?"
7. COMMITTEN:   "Mit klarer Beschreibung in Git."
```

**Klein und oft.** Zehn winzige Änderungen mit Build & Test > eine große.

## Was du noch nicht brauchst

Migrations, Authorization-Policies, Caddy, CI/CD, Deployment — alles spätere Themen. Wenn du draufstößt, frag gezielt.

## Merksätze

> **Starten:** DB + Backend + Frontend, alle drei parallel.
>
> **Loop:** Lesen → Ändern → Bauen → Testen → Sehen.
>
> **Klein bleiben.** Test/Build nach jeder Mini-Änderung.
>
> **Bei Fehlern:** Konkrete Frage + Snippet + Fehlermeldung.
>
> **Hot Reload:** Vite lädt im Browser von selbst nach.
