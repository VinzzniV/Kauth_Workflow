# Einheit 2 — Eine echte Anfrage durch deinen Code

> Tour an einem konkreten Endpoint (`GET /departments`). Erklärt: Endpoint, Service, Repository, DTO, JSON, Routing, Dependency Injection.

## Der Use-Case

Frontend ruft `GET http://localhost:5xxx/departments` auf. Was passiert?

## Schicht 1: Der Endpoint

`api/API/Endpoints/WorkflowMasterDataEndpoints.cs:12-27`

```csharp
app.MapGet("/departments", async (
    IWorkflowCatalogService workflowCatalogService,
    IUserContext userContext,
    IAuthorizationPolicyService authorizationPolicy) =>
{
    var access = await EndpointSupport.RequireAuthorization(
        userContext,
        authorizationPolicy.CanCreateWorkflow,
        "HR, Abteilungsleitung oder Admin role is required.");
    if (access.Error is not null)
    {
        return access.Error;
    }

    return Results.Ok(await workflowCatalogService.GetDepartmentsAsync());
}).Produces<List<DepartmentDto>>(StatusCodes.Status200OK);
```

- `app.MapGet("/departments", ...)` = **Routing**: URL → Funktion.
- Lambda mit Parametern (Services) = **Dependency Injection**.
- `EndpointSupport.RequireAuthorization` = Berechtigungs-Check.
- `Results.Ok(...)` = HTTP 200 + JSON-Antwort.
- `await ...GetDepartmentsAsync()` = Service rufen.

→ Endpoint hat **keine Logik**. Nur HTTP-Türsteher.

## Schicht 2: Der Service

`api/API/Services/WorkflowCatalogService.cs:44-47`

```csharp
public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(
    CancellationToken cancellationToken = default)
{
    return await repository.GetDepartments();
}
```

Bei Departments gibt es nichts zu prüfen → reicht durch. Bei komplexeren Endpoints (z.B. `CreateWorkflow`) macht der Service viel mehr.

→ Service ist die **Schaltzentrale**. Nicht jeder Service muss komplex sein.

## Schicht 3: Das Repository

`api/API/Repositories/PostgresWorkflowRepository.MasterDataOperations.cs:9-33`

```csharp
public async Task<List<DepartmentDto>> GetDepartments()
{
    await using var connection = new NpgsqlConnection(GetConnectionString());
    await connection.OpenAsync();

    const string sql = @"
SELECT id, name
FROM departments
ORDER BY name;";

    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();

    var departments = new List<DepartmentDto>();
    while (await reader.ReadAsync())
    {
        departments.Add(new DepartmentDto
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1)
        });
    }

    return departments;
}
```

1. Verbindung zur DB öffnen (`NpgsqlConnection` = PostgreSQL).
2. SQL-Statement definieren.
3. Ausführen → `reader` läuft Zeile für Zeile.
4. Pro Row ein DTO bauen.
5. Liste zurück.

→ Repository hat **keine Logik**. Nur SQL + DTO-Mapping.

## Schicht 4: Das DTO

`api/API/Contracts/WorkflowDtos.cs:4-8`

```csharp
public sealed class DepartmentDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}
```

**DTO** = Data Transfer Object. Klasse, die nur Daten enthält, keine Logik.

Im Browser landet das als JSON:

```json
[
  { "id": 1, "name": "IT" },
  { "id": 2, "name": "Vertrieb" }
]
```

ASP.NET wandelt C#-Objekte automatisch in JSON (und zurück).

## Begriffe

### DTO
Klasse ohne Logik, nur Daten. Vokabular zwischen Schichten. Bei dir alle in `api/API/Contracts/`.

### JSON
Text-Format zum Datenaustausch. `{ "key": "value", "andere": 42 }`. Universal verstanden.

### Routing
Zuordnung URL → Funktion. `app.MapGet("/departments", ...)` registriert sie.

HTTP-Methoden:
- `GET` = lesen (`MapGet`)
- `POST` = erzeugen (`MapPost`)
- `PUT` / `PATCH` = ändern (`MapPut`, `MapPatch`)
- `DELETE` = löschen (`MapDelete`)

### Dependency Injection (DI)
Du sagst, **was** du brauchst (`IWorkflowCatalogService`). ASP.NET gibt dir eine **konkrete Implementierung**. Entkoppelt Schichten.

Im `Program.cs` registriert:
```csharp
builder.Services.AddScoped<IWorkflowCatalogService, WorkflowCatalogService>();
```

→ "Wenn jemand `IWorkflowCatalogService` braucht, gib `WorkflowCatalogService`."

### Interface-Konvention
- `I...` = Interface (Vertrag, "diese Methoden gibt es")
- ohne `I` = konkrete Klasse (Umsetzung)

Beispiel: `IWorkflowCatalogService` (Interface) und `WorkflowCatalogService` (Klasse).

## C#-Syntax (Vorschau auf Einheit 3)

| Syntax | Bedeutung |
|---|---|
| `async` / `await` | Asynchron, ohne zu blockieren |
| `Task<T>` | Rückgabetyp asynchroner Methoden |
| `=>` | Lambda / Pfeil-Funktion |
| `required` | Property muss beim Erzeugen gesetzt werden |
| `init` | Property nur bei Erstellung setzbar (immutable danach) |
| `IReadOnlyList<T>` | Nur lesbare Liste |
| `var` | Typ vom Compiler erraten |
| `?.` | Null-Safe-Zugriff |

## Der Zusammenhang grafisch

```
GET /departments
        │
        ▼
┌─────────────────────────────────────────┐
│ ENDPOINT                                 │
│ - Berechtigung prüfen                    │
│ - Service rufen                          │
│ - Ergebnis als JSON zurückgeben          │
└─────────────────────────────────────────┘
        │  ruft Service
        ▼
┌─────────────────────────────────────────┐
│ SERVICE                                  │
│ - Logik (hier: nichts, nur weiterreichen)│
└─────────────────────────────────────────┘
        │  ruft Repository
        ▼
┌─────────────────────────────────────────┐
│ REPOSITORY                               │
│ - SQL ausführen                          │
│ - Rows → DTOs                            │
└─────────────────────────────────────────┘
        │  SQL
        ▼
   POSTGRES
        │  Rows zurück
        ▼
   DTO-Liste → JSON → Browser
```

## Merksätze

> **Endpoint** = HTTP rein/raus, Berechtigung prüfen, Service rufen.
> **Service** = Logik.
> **Repository** = SQL.
> **DTO** = reines Datenobjekt, Vokabular zwischen Schichten.
>
> **JSON** = Text-Format für Datenübertragung. Automatisch aus DTOs.
>
> **Dependency Injection** = du sagst *was*, das System gibt dir *eine Implementierung*. Entkoppelt.
>
> **`I...`-Konvention** = Interface (Vertrag). Klasse ohne `I` = konkrete Umsetzung.
