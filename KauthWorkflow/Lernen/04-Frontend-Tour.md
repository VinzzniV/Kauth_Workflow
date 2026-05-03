# Einheit 4 — Frontend-Tour (React + TypeScript)

> Was im Browser läuft. Components, Hooks, Pages, Services. Wie das Frontend mit dem Backend redet.

## TypeScript

**= JavaScript mit Typen.**

JavaScript hat keine Typen — Variablen können den Typ wechseln. TypeScript zwingt zu Typen wie C# oder Java:

```typescript
let x: number = 5;
x = "hallo";    // ❌ Compile-Fehler
```

Beim Bauen wird TypeScript zu JavaScript übersetzt (der Browser versteht nur JS).

## React

**Bibliothek, um UI aus wiederverwendbaren Bausteinen (Components) zu bauen.**

Components sind Funktionen, die HTML zurückgeben:

```tsx
function Begrüßung() {
    return <h1>Hallo Welt</h1>;
}
```

`<h1>` im Code = **JSX** (bzw. **TSX** mit TypeScript). Trick, der HTML in JS erlaubt. Datei endet auf `.tsx`.

## Ordnerstruktur

```
web/src/
├── App.tsx          ← Wurzel-Component, Routing
├── main.tsx         ← Einstiegspunkt
├── pages/           ← eine Datei pro Seite
├── components/      ← wiederverwendbare Bausteine
├── hooks/           ← eigene Hooks (Logik-Kapselung)
├── services/        ← redet mit Backend
├── types/           ← TypeScript-Typen (DTO-Gegenstück)
├── auth/, navigation/, styles/, theme/, utils/
```

Die wichtigsten: **pages**, **components**, **services**. Plus **hooks** als Bindeglied.

## Component-Anatomie

```tsx
function NutzerKarte() {
    return (
        <div className="card">
            <h2>Max Müller</h2>
        </div>
    );
}
```

- `className` statt `class` (`class` in JS reserviert).
- Verwendung: `<NutzerKarte />`.

### Mit Props (Parameter)

```tsx
type NutzerKarteProps = {
    name: string;
    abteilung: string;
};

function NutzerKarte({ name, abteilung }: NutzerKarteProps) {
    return (
        <div className="card">
            <h2>{name}</h2>
            <p>{abteilung}</p>
        </div>
    );
}

<NutzerKarte name="Max" abteilung="IT" />
```

- `NutzerKarteProps` = Typ für Props (DTO-artig).
- `{ name, abteilung }` = **Destructuring** der Props.
- `{name}` im JSX = Variable einsetzen.

## State & Hooks

### useState — Zustand in einer Component

```tsx
import { useState } from "react";

function Zähler() {
    const [zahl, setZahl] = useState(0);
    //     ^Wert ^Setter    ^Startwert

    return (
        <div>
            <p>{zahl}</p>
            <button onClick={() => setZahl(zahl + 1)}>+1</button>
        </div>
    );
}
```

`setZahl(...)` aufrufen → React **rendert neu**. UI = Funktion(State).

### useEffect — Nebeneffekte beim Erscheinen

```tsx
useEffect(() => {
    console.log("Component erschienen");
}, []);
```

`[]` = nur einmal beim ersten Erscheinen. `[zahl]` = jedes Mal, wenn `zahl` sich ändert.

In modernem Code mit React Query brauchst du es selten direkt für Daten-Laden.

### Custom Hooks (selbstgebaut)

Funktionen, die mit `use...` anfangen und andere Hooks intern verwenden:

```tsx
function useDoubleCounter() {
    const [zahl, setZahl] = useState(0);
    function plusZwei() { setZahl(zahl + 2); }
    return { zahl, plusZwei };
}
```

Kapseln Logik, die mehrere Components teilen. → Inhalt von `web/src/hooks/`.

## Frontend ↔ Backend

```
Component → Hook → Service → fetch(URL) → Backend
                                           │
Component ← Hook ← Service ← JSON ← ───────┘
```

### Schicht A: Service-Funktion

`web/src/services/lookupApi.ts:17-19`

```typescript
export async function getDepartments(): Promise<Department[]> {
    return requestJson<BackendDepartmentDto[]>("/departments");
}
```

- Async-Funktion, macht HTTP-GET.
- `Promise<T>` = TypeScript-Pendant zu C#s `Task<T>`.

### Schicht B: Query-Hook (mit Caching)

`web/src/services/queries/roleQueries.ts:13-19`

```typescript
export function useDepartments() {
    return useQuery({
        queryKey: queryKeys.departments(),
        queryFn: getDepartments,
        staleTime: 5 * 60 * 1000,
    });
}
```

`useQuery` (TanStack Query / React Query):
- ruft `getDepartments()` auf
- **cachet** das Ergebnis
- gibt Status zurück: `isLoading`, `error`, `data`

`staleTime` = wie lange Daten als frisch gelten.

### Schicht C: Component

```tsx
function AbteilungsListe() {
    const { data, isLoading, error } = useDepartments();

    if (isLoading) return <p>Lade…</p>;
    if (error) return <p>Fehler!</p>;
    if (!data) return null;

    return (
        <ul>
            {data.map(dept => <li key={dept.id}>{dept.name}</li>)}
        </ul>
    );
}
```

Erst Lade-Anzeige, dann Daten erscheinen, Component rendert neu.

`data.map(...)` = wie `.Select(...)` in LINQ.

## Die komplette Reise

```
1. <AbteilungsListe />
   ↓ ruft Hook
2. useDepartments()
   ↓ ruft Service
3. getDepartments()
   ↓ HTTP
4. fetch("GET /departments")
   --- Netzwerk ---
5. ASP.NET Endpoint
6. → Service
7. → Repository
8. → SQL
   ↓ JSON zurück
   --- Netzwerk ---
9. Hook bekommt Daten, cachet
10. Component rendert neu
```

## Pages vs Components

- **`pages/`** = ganze Seiten, eine pro URL.
- **`components/`** = wiederverwendbare Bausteine.

Technisch beides Components. Pages werden vom Routing geladen (`App.tsx`):

```tsx
<Route path="/dashboard" element={<DashboardPage />} />
```

## TypeScript-Spezialitäten

| Syntax | Bedeutung |
|---|---|
| `type Foo = { ... }` | Eigener Typ |
| `Foo[]` | Liste von Foo |
| `Foo \| null` | Foo oder null (Union-Typ) |
| `Foo?` als Property | Optional |
| `as Foo` | Typ-Cast |
| `import { x } from "..."` | Etwas holen |
| `export ...` | Etwas nach außen sichtbar |

Frontend-DTOs liegen in `web/src/types/`. Spiegelbild zu `api/API/Contracts/`.

## Merksätze

> **React** = Components (Funktionen, die JSX zurückgeben).
> **State** in Hooks. State ändert sich → Component rendert neu.
>
> **Backend-Aufruf in 3 Schichten:**
> Component → Hook (`useQuery`) → Service-Funktion (`fetch`).
> Spiegelbild zu Endpoint → Service → Repository.
>
> **TypeScript** = JavaScript mit Typen.
>
> **Pages** = Seiten an URLs. **Components** = Bausteine.
