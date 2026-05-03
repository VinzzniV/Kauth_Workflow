# Einheit 3 — C#-Syntax jenseits der Basics

> Alles, was über C#-Grundlagen hinausgeht und im Backend ständig auftaucht. Ziel: jede Zeile lesen können.

## 1. Properties

Java-Getter/Setter in einer Zeile:

```csharp
public string Name { get; set; }       // lesbar + schreibbar
public string Name { get; init; }      // schreibbar NUR bei Erstellung (immutable danach)
public string Name { get; }            // nur im Konstruktor setzbar
public required string Name { get; init; }  // Compiler erzwingt Setzen beim Erstellen
```

`init` macht das Objekt nach Erstellung **unveränderlich**. Gut für DTOs.

`required` = Compile-Fehler, wenn nicht beim `new` gesetzt.

## 2. async / await / Task<T>

### Problem
Datenbank-Aufruf dauert 50ms. Synchron blockiert der Server. Asynchron bedient er währenddessen andere Anfragen.

### Syntax
```csharp
public async Task<List<DepartmentDto>> GetDepartments()
{
    var rows = await datenbank.Query("SELECT ...");
    return Mappe(rows);
}
```

- `async` = "darf await benutzen"
- `Task<T>` = Versprechen auf einen Wert vom Typ T
- `await` = "warte auf den Wert, ohne zu blockieren"

### Faustregel
- Methode mit DB/Netzwerk/File → `async Task<...>`
- Aufruf einer async-Methode → `await` davor
- Ohne Rückgabe → `Task` statt `Task<T>`

Sobald du `Task<...>` als Rückgabetyp siehst, ist die Methode async.

## 3. Generics: <T>

`<T>` = Platzhalter für einen Typ. Beim Aufruf konkretisiert.

```csharp
List<int>             // Liste von ints
List<DepartmentDto>   // Liste von DTOs
Task<int>             // Versprechen auf int
IReadOnlyList<T>      // nur lesbare Liste
```

Eine Klasse, beliebig viele Typ-Anwendungen. Ohne Generics: eine Klasse pro Typ.

## 4. var

```csharp
var x = 5;                              // Compiler weiß: int
var liste = new List<DepartmentDto>();  // Compiler weiß: List<DepartmentDto>
```

**Statisch typisiert** (nicht wie JS). Typ steht beim Compilieren fest, `var` spart nur das Tippen.

## 5. Null-Safety

C# unterscheidet `string` (nicht null) und `string?` (kann null sein).

### `?.` — Null-Safe-Zugriff
```csharp
int? laenge = name?.Length;
// wenn name null: laenge ist null. Kein Crash.

var stadt = person?.Adresse?.Stadt?.Name;
// Kette bricht beim ersten null ab
```

### `??` — Null-Coalescing
```csharp
string anzeige = name ?? "unbekannt";
// wenn name null: "unbekannt"
```

### `??=` — nur zuweisen, wenn null
```csharp
list ??= new List<int>();
```

## 6. Expression-bodied Members (`=>` bei Methoden)

`=>` hat **zwei** Bedeutungen:

### A. Lambda (Funktion ohne Namen)
```csharp
zahlen.Where(z => z % 2 == 0);
```

### B. Methode mit nur einem Ausdruck
```csharp
public int Verdoppeln(int x) => x * 2;
// gleiche wie:
public int Verdoppeln(int x) { return x * 2; }
```

Aus dem Kontext erkennbar: bei Methodendeklaration → expression-bodied. Sonst Lambda.

## 7. using / await using

Ressourcen, die freigegeben werden müssen (DB-Verbindungen, Files):

```csharp
await using var connection = new NpgsqlConnection(...);
await connection.OpenAsync();
// connection.DisposeAsync() läuft automatisch beim Methodenende
```

Ohne `using` müsstest du selbst `Dispose()` aufrufen und garantieren, dass es auch bei Fehlern passiert.

## 8. LINQ

Methoden für Listen, mit Lambdas als Filter/Transformer.

| Methode | Wofür |
|---|---|
| `.Where(x => ...)` | Filtern |
| `.Select(x => ...)` | Transformieren |
| `.First()` / `.FirstOrDefault()` | Erstes Element |
| `.Any(x => ...)` | "Gibt's eins, das passt?" |
| `.All(x => ...)` | "Gilt es für alle?" |
| `.Count()` | Anzahl |
| `.OrderBy(x => ...)` | Sortieren |
| `.ToList()` | In `List<T>` packen |

```csharp
var ergebnis = zahlen.Where(z => z > 2).Select(z => z * 10).ToList();
// 30, 40, 50
```

## 9. record vs class

```csharp
public class Person
{
    public required string Name { get; init; }
    public required int Alter { get; init; }
}

public record Person(string Name, int Alter);
```

Beide speichern dieselben Daten. `record`:
- Kurz-Syntax (eine Zeile)
- **Wert-Vergleich**: zwei Records mit gleichen Feldern sind gleich
- Immutable per default

Faustregel: `record` für Wert-Objekte (DTO-artig). `class` für alles mit Identität.

## 10. string? vs string

```csharp
string name = "Max";    // darf NICHT null sein
string? name = null;    // darf null sein
```

Compiler warnt bei null-Zuweisung an non-nullable. Heißt **Nullable-Reference-Types** (C# 8+).

## Zusammenführung: eine Methode entschlüsseln

```csharp
public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(
    CancellationToken cancellationToken = default)
{
    return await repository.GetDepartments();
}
```

- `public async` = öffentliche, asynchrone Methode
- `Task<IReadOnlyList<DepartmentDto>>` = Versprechen auf nur-lesbare Liste von DTOs
- `GetDepartmentsAsync` = Methodenname (Konvention: `Async`-Suffix)
- `CancellationToken cancellationToken = default` = optionaler Abbruch-Parameter
- `return await ...` = warte aufs Repository, gib weiter

## Merksätze

> **`async` + `await` + `Task<T>`** lösen Blockaden — Server bedient andere Anfragen während er wartet.
>
> **`<T>`** = Platzhalter für einen Typ. Generic = "funktioniert für jeden Typ."
>
> **`?.`** = Null-Safe-Zugriff. **`??`** = Fallback bei null.
>
> **`=>`** = Lambda **oder** expression-bodied member, je nach Kontext.
>
> **`using`** = Ressource wird automatisch freigegeben.
>
> **LINQ** (`.Where`, `.Select`, ...) = Daten-Transformationen mit Lambdas.
>
> **`record`** = Wert-Objekt mit Auto-Equality. **`class`** = alles andere.
