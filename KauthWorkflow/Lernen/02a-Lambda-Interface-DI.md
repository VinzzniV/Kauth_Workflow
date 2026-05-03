# Einheit 2a — Lambda, Interface, Dependency Injection (vertieft)

> Vertiefung der drei kniffligen Konzepte aus Einheit 2. Mit Alltagsanalogien.

## Lambda

Eine **Lambda** ist nur eine **kurze Schreibweise für eine Funktion ohne Namen**.

### Normal vs. Lambda

```csharp
// Normal:
int Verdoppeln(int x)
{
    return x * 2;
}

// Als Lambda:
var verdoppeln = (int x) => x * 2;
```

Die `=>` trennt Parameter (links) von Rückgabe (rechts).

### Wozu?

Funktion, die **nur an einer Stelle** gebraucht wird → Wegwerf-Funktion inline.

```csharp
var zahlen = new List<int> { 1, 2, 3, 4, 5 };
var gerade = zahlen.Where(z => z % 2 == 0);
//                        ^^^^^^^^^^^^^^^
//                        Lambda als Filter
```

`Where` will eine Funktion bekommen, die "rein oder raus" entscheidet.

### Im Endpoint

```csharp
app.MapGet("/departments", async (...) => { ... });
//                         ^^^^^^^^^^^^^^^^^^^^^^^
//                         die Funktion, die bei /departments läuft
```

> **Lambda = anonyme Funktion in Kurzschrift.** `=>` trennt Parameter und Logik.

---

## Interface

Ein **Interface** ist eine **Liste von Methoden, die jemand anbieten muss** — ohne zu sagen, wie er sie umsetzt.

### Alltagsanalogie: Stellenanzeige

> Gesucht: Kaffeemacher. Muss können: `KaffeKochen()`, `MilchAufschäumen()`, `BecherAusspülen()`.

Die Anzeige sagt **was**, nicht **wie**. Siebträger und Vollautomat erfüllen sie unterschiedlich.

```csharp
public interface IKaffeemacher
{
    void KaffeKochen();
    void MilchAufschäumen();
    void BecherAusspülen();
}
```

Keine Implementierung — nur die Methodenköpfe.

### Klassen, die den Vertrag erfüllen

```csharp
public class Siebträger : IKaffeemacher    { ... }
public class Vollautomat : IKaffeemacher   { ... }
```

Beide haben alle drei Methoden, aber konkret unterschiedlich umgesetzt.

### Code, der "egal welche Maschine" sagt

```csharp
void MachMirEinenKaffee(IKaffeemacher maschine)
{
    maschine.KaffeKochen();
}
```

Die Funktion akzeptiert jede Klasse, die `IKaffeemacher` implementiert.

### Wozu?

- **Austauschbarkeit** ohne den Code, der die Maschine nutzt, anzufassen.
- **Tests** mit Fake-Implementierungen.

### In deinem Code

- `IWorkflowCatalogService` (Interface) = Vertrag.
- `WorkflowCatalogService` (Klasse) = Umsetzung.

> **Interface = Liste von Methoden ohne Inhalt, ein Vertrag.**
> Klassen, die ihn erfüllen, müssen alle Methoden anbieten.

### Warum Interface, wenn man auch direkt die Klasse nehmen könnte?

Drei konkrete Vorteile:

**1. Implementierung austauschbar.**
Mit Interface änderst du eine Zeile in `Program.cs`, um z.B. von `PostgresWorkflowRepository` auf eine andere Implementierung zu wechseln. Ohne Interface musst du jeden Aufrufer ändern.

**2. Tests werden möglich.**
Eine Fake-Implementierung (`StubWorkflowRepository : IWorkflowRepository`) kann anstelle der echten reingegeben werden. Test läuft ohne DB, in Millisekunden, reproduzierbar. → genau deshalb hat dein Projekt überhaupt Tests.

**3. Mehrere Implementierungen können koexistieren.**
`List<IKaffeemacher>` kann Vollautomat, Siebträger und Filterkaffee gemischt aufnehmen.

### Trade-off

Interface = eine Datei mehr. Lohnt sich bei Services, Repositories und allem, was austauschbar oder mockbar sein soll. Bei reinen Helpern (`static int Maximum(...)`) Overkill.

**Faustregel in ASP.NET:** Services und Repositories haben fast immer ein Interface.

---

## Dependency Injection (DI)

### Ohne DI: Klasse macht sich ihre Abhängigkeiten selbst

```csharp
public class KaffeeService
{
    private Vollautomat maschine;

    public KaffeeService()
    {
        maschine = new Vollautomat();   // selbst gebaut
    }

    public void Bestellen() => maschine.KaffeKochen();
}
```

Probleme:
1. Fest an `Vollautomat` gekoppelt.
2. Schwer testbar (kann keine Fake-Maschine reinschummeln).
3. Wenn `Vollautomat` selbst Abhängigkeiten hat, muss `KaffeeService` die alle kennen.

### Mit DI: Klasse bekommt Abhängigkeiten reingereicht

```csharp
public class KaffeeService
{
    private IKaffeemacher maschine;

    public KaffeeService(IKaffeemacher maschine)   // bekommt sie als Parameter
    {
        this.maschine = maschine;
    }

    public void Bestellen() => maschine.KaffeKochen();
}
```

Drei Änderungen:
1. Typ ist `IKaffeemacher` (Interface).
2. Konstruktor nimmt Maschine als Parameter.
3. Service baut **nichts** mehr selbst.

Aufruf:
```csharp
var service = new KaffeeService(new Vollautomat());
// oder im Test:
var service = new KaffeeService(new TestKaffeemacher());
```

### Begriffe
- **Dependency** = Abhängigkeit. Was die Klasse braucht.
- **Injection** = Einspritzen. Von außen reingegeben.

### Der DI-Container

Bei vielen Services wird das Verkabeln-von-Hand ekelig. ASP.NET hat einen Container. In `Program.cs`:

```csharp
builder.Services.AddScoped<IKaffeemacher, Vollautomat>();
builder.Services.AddScoped<IKaffeeService, KaffeeService>();
```

→ "Wenn jemand `IKaffeemacher` braucht, gib `Vollautomat`."

Wenn irgendwo ein `KaffeeService` gebraucht wird, baut der Container ihn **und reicht alles automatisch rein**, was der Konstruktor verlangt.

### Im Endpoint

```csharp
app.MapGet("/departments", async (
    IWorkflowCatalogService workflowCatalogService,    // Container reicht
    IUserContext userContext,                          // alles automatisch
    IAuthorizationPolicyService authorizationPolicy)   // rein
    => { ... });
```

Du schreibst nirgends `new WorkflowCatalogService(...)`.

### Aha-Moment

> **DI ist die Antwort auf "Wer baut die Abhängigkeiten meiner Klasse?"**
>
> Vor DI: "Die Klasse selbst." → enge Kopplung, schwer testbar.
> Mit DI: "Jemand anders. Mir wird es reingereicht." → lose Kopplung, testbar, austauschbar.
>
> Der DI-Container automatisiert das Reinreichen.

---

## Zusammenhang der drei

- **Interface** sagt: "Diese Methoden gibt es."
- **DI-Container** sagt: "Wenn jemand das Interface braucht, kriegt er diese Klasse."
- **Lambda** ist die Kurzschreibweise, mit der du Funktionen direkt an Stellen wie `MapGet` einreichst — und die Lambda-Parameter (`IWorkflowCatalogService catalog, ...`) zeigen dem DI-Container, was sie braucht.

Drei einzelne Konzepte, in deinem Endpoint zusammen am Werk.
