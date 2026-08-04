# PalletBalancer Web API + PostgreSQL — Plan Fase 1

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crear un backend ASP.NET Core Web API con PostgreSQL que exponga endpoints REST para catálogo de items y FDOs, accesible desde cualquier dispositivo.

**Architecture:** El proyecto `PalletBalancer.Api` se agrega a la solución existente y referencia `PalletBalancer.Core` para reutilizar la lógica de negocio. La base de datos PostgreSQL corre en Railway. El frontend HTML/JS se construye en Fase 2.

**Tech Stack:** .NET 8, ASP.NET Core Web API, Entity Framework Core 8, Npgsql, PostgreSQL, Railway

## Global Constraints
- C# .NET 8; nombres de clases, métodos y variables en español
- Unidades base en la DB: kg y cm
- Sin autenticación en Fase 1
- Proyecto raíz: `/Users/jd/Desktop/PalletBalancer/`

---

### Tarea 1: Crear proyecto PalletBalancer.Api

**Files:**
- Create: `src/PalletBalancer.Api/PalletBalancer.Api.csproj`
- Create: `src/PalletBalancer.Api/Program.cs`
- Modify: `PalletBalancer.sln`

**Interfaces:**
- Produces: API corriendo en `http://localhost:5000`, Swagger en `http://localhost:5000/swagger`

- [ ] **Paso 1: Crear el proyecto Web API**

```bash
cd /Users/jd/Desktop/PalletBalancer
dotnet new webapi -n PalletBalancer.Api -o src/PalletBalancer.Api --no-https
```

- [ ] **Paso 2: Agregar a la solución y referenciar Core**

```bash
dotnet sln add src/PalletBalancer.Api/PalletBalancer.Api.csproj
dotnet add src/PalletBalancer.Api/PalletBalancer.Api.csproj reference src/PalletBalancer.Core/PalletBalancer.Core.csproj
```

- [ ] **Paso 3: Eliminar archivos de ejemplo del template**

Borrar `WeatherForecast.cs` y `Controllers/WeatherForecastController.cs`.

- [ ] **Paso 4: Reemplazar Program.cs con configuración base**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
```

- [ ] **Paso 5: Verificar que compila y corre**

```bash
dotnet run --project src/PalletBalancer.Api
```

Abrir `http://localhost:5000/swagger` — debe aparecer la UI sin endpoints aún.

- [ ] **Paso 6: Commit**

```bash
git add src/PalletBalancer.Api/ PalletBalancer.sln
git commit -m "feat: agregar proyecto PalletBalancer.Api"
```

---

### Tarea 2: Instalar EF Core + Npgsql + AppDbContext

**Files:**
- Modify: `src/PalletBalancer.Api/PalletBalancer.Api.csproj`
- Create: `src/PalletBalancer.Api/Data/AppDbContext.cs`
- Modify: `src/PalletBalancer.Api/appsettings.json`
- Modify: `src/PalletBalancer.Api/Program.cs`

**Interfaces:**
- Produces: `AppDbContext` inyectable, conectado a PostgreSQL local

- [ ] **Paso 1: Instalar paquetes NuGet**

```bash
dotnet add src/PalletBalancer.Api/PalletBalancer.Api.csproj package Npgsql.EntityFrameworkCore.PostgreSQL --version 8.0.4
dotnet add src/PalletBalancer.Api/PalletBalancer.Api.csproj package Microsoft.EntityFrameworkCore.Design --version 8.0.8
```

- [ ] **Paso 2: Crear AppDbContext vacío**

Crear `src/PalletBalancer.Api/Data/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace PalletBalancer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
```

- [ ] **Paso 3: Agregar cadena de conexión a appsettings.json**

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=palletbalancer;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Paso 4: Registrar DbContext en Program.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                    ?? builder.Configuration.GetConnectionString("Default");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
```

- [ ] **Paso 5: Commit**

```bash
git add src/PalletBalancer.Api/
git commit -m "feat: configurar EF Core con Npgsql"
```

---

### Tarea 3: Entidad Item + Primera Migración

**Files:**
- Create: `src/PalletBalancer.Api/Models/Item.cs`
- Modify: `src/PalletBalancer.Api/Data/AppDbContext.cs`
- Create: `src/PalletBalancer.Api/Data/Migrations/` (auto-generado por EF)

**Interfaces:**
- Produces: tabla `Items` en PostgreSQL con columnas para standardPack, caja y pieza

- [ ] **Paso 1: Crear entidad Item**

Crear `src/PalletBalancer.Api/Models/Item.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Item
{
    [Key]
    public string ModelNo { get; set; } = string.Empty;

    [Required]
    public string Descripcion { get; set; } = string.Empty;

    // Standard Pack (pallet completo)
    public int    SpPiezasPorPallet { get; set; }
    public double SpPesoKg          { get; set; }
    public double SpLargoCm         { get; set; }
    public double SpAnchoCm         { get; set; }
    public double SpAltoCm          { get; set; }

    // Caja (unidad intermedia)
    public int    CajaPiezasPorCaja { get; set; }
    public double CajaPesoKg        { get; set; }
    public double CajaLargoCm       { get; set; }
    public double CajaAnchoCm       { get; set; }
    public double CajaAltoCm        { get; set; }

    // Pieza (unidad mínima)
    public double PiezaPesoKg  { get; set; }
    public double PiezaLargoCm { get; set; }
    public double PiezaAnchoCm { get; set; }
    public double PiezaAltoCm  { get; set; }
}
```

- [ ] **Paso 2: Registrar en AppDbContext**

```csharp
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
}
```

- [ ] **Paso 3: Crear la migración**

```bash
dotnet ef migrations add CrearTablaItems \
  --project src/PalletBalancer.Api \
  --startup-project src/PalletBalancer.Api
```

Revisar el archivo generado en `Data/Migrations/` — debe tener columnas para todas las propiedades de `Item`.

- [ ] **Paso 4: Aplicar migración a la base de datos**

> Requiere PostgreSQL corriendo localmente. Instalación rápida con Docker:
> `docker run --name pg-pallet -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=palletbalancer -p 5432:5432 -d postgres:16`

```bash
dotnet ef database update \
  --project src/PalletBalancer.Api \
  --startup-project src/PalletBalancer.Api
```

- [ ] **Paso 5: Commit**

```bash
git add src/PalletBalancer.Api/
git commit -m "feat: entidad Item y primera migración EF Core"
```

---

### Tarea 4: ItemsController — CRUD completo

**Files:**
- Create: `src/PalletBalancer.Api/DTOs/ItemDto.cs`
- Create: `src/PalletBalancer.Api/Controllers/ItemsController.cs`

**Interfaces:**
- Consumes: `AppDbContext` (inyectado), `Item` (Tarea 3)
- Produces:
  - `GET /api/items` → `List<Item>`
  - `GET /api/items/{modelNo}` → `Item`
  - `POST /api/items` → `Item` creado (201)
  - `PUT /api/items/{modelNo}` → `Item` actualizado
  - `DELETE /api/items/{modelNo}` → 204

- [ ] **Paso 1: Crear ItemDto**

Crear `src/PalletBalancer.Api/DTOs/ItemDto.cs`:

```csharp
namespace PalletBalancer.Api.DTOs;

public class ItemDto
{
    public string ModelNo    { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public int    SpPiezasPorPallet { get; set; }
    public double SpPesoKg          { get; set; }
    public double SpLargoCm         { get; set; }
    public double SpAnchoCm         { get; set; }
    public double SpAltoCm          { get; set; }

    public int    CajaPiezasPorCaja { get; set; }
    public double CajaPesoKg        { get; set; }
    public double CajaLargoCm       { get; set; }
    public double CajaAnchoCm       { get; set; }
    public double CajaAltoCm        { get; set; }

    public double PiezaPesoKg  { get; set; }
    public double PiezaLargoCm { get; set; }
    public double PiezaAnchoCm { get; set; }
    public double PiezaAltoCm  { get; set; }
}
```

- [ ] **Paso 2: Crear ItemsController**

Crear `src/PalletBalancer.Api/Controllers/ItemsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ItemsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos() =>
        Ok(await _db.Items.OrderBy(i => i.ModelNo).ToListAsync());

    [HttpGet("{modelNo}")]
    public async Task<IActionResult> ObtenerPorModelNo(string modelNo)
    {
        var item = await _db.Items.FindAsync(modelNo);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(ItemDto dto)
    {
        if (await _db.Items.AnyAsync(i => i.ModelNo == dto.ModelNo))
            return Conflict($"Ya existe un item con ModelNo '{dto.ModelNo}'.");

        var item = MapearDesdeDto(dto);
        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorModelNo), new { modelNo = item.ModelNo }, item);
    }

    [HttpPut("{modelNo}")]
    public async Task<IActionResult> Actualizar(string modelNo, ItemDto dto)
    {
        var item = await _db.Items.FindAsync(modelNo);
        if (item is null) return NotFound();
        ActualizarDesdeDto(item, dto);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{modelNo}")]
    public async Task<IActionResult> Eliminar(string modelNo)
    {
        var item = await _db.Items.FindAsync(modelNo);
        if (item is null) return NotFound();
        _db.Items.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static Item MapearDesdeDto(ItemDto d) => new()
    {
        ModelNo          = d.ModelNo,    Descripcion      = d.Descripcion,
        SpPiezasPorPallet = d.SpPiezasPorPallet, SpPesoKg = d.SpPesoKg,
        SpLargoCm        = d.SpLargoCm,  SpAnchoCm        = d.SpAnchoCm,  SpAltoCm  = d.SpAltoCm,
        CajaPiezasPorCaja = d.CajaPiezasPorCaja, CajaPesoKg = d.CajaPesoKg,
        CajaLargoCm      = d.CajaLargoCm, CajaAnchoCm     = d.CajaAnchoCm, CajaAltoCm = d.CajaAltoCm,
        PiezaPesoKg      = d.PiezaPesoKg, PiezaLargoCm    = d.PiezaLargoCm,
        PiezaAnchoCm     = d.PiezaAnchoCm, PiezaAltoCm    = d.PiezaAltoCm
    };

    private static void ActualizarDesdeDto(Item i, ItemDto d)
    {
        i.Descripcion       = d.Descripcion;
        i.SpPiezasPorPallet = d.SpPiezasPorPallet; i.SpPesoKg   = d.SpPesoKg;
        i.SpLargoCm         = d.SpLargoCm;  i.SpAnchoCm         = d.SpAnchoCm;  i.SpAltoCm   = d.SpAltoCm;
        i.CajaPiezasPorCaja = d.CajaPiezasPorCaja; i.CajaPesoKg = d.CajaPesoKg;
        i.CajaLargoCm       = d.CajaLargoCm; i.CajaAnchoCm      = d.CajaAnchoCm; i.CajaAltoCm = d.CajaAltoCm;
        i.PiezaPesoKg       = d.PiezaPesoKg; i.PiezaLargoCm     = d.PiezaLargoCm;
        i.PiezaAnchoCm      = d.PiezaAnchoCm; i.PiezaAltoCm     = d.PiezaAltoCm;
    }
}
```

- [ ] **Paso 3: Probar en Swagger**

```bash
dotnet run --project src/PalletBalancer.Api
```

En `http://localhost:5000/swagger`:
1. `POST /api/items` — ingresar el item K006T91071XB
2. `GET /api/items` — verificar que aparece
3. `GET /api/items/K006T91071XB` — verificar detalle
4. `DELETE /api/items/K006T91071XB` — verificar 204

- [ ] **Paso 4: Commit**

```bash
git add src/PalletBalancer.Api/
git commit -m "feat: CRUD completo de Items via REST API"
```

---

### Tarea 5: Seed — cargar catalogo_items.json a la base de datos

**Files:**
- Create: `src/PalletBalancer.Api/Data/Seed.cs`
- Modify: `src/PalletBalancer.Api/Program.cs`

**Interfaces:**
- Consumes: `src/PalletBalancer.App/catalogo_items.json`, `AppDbContext`
- Produces: tabla `Items` pre-cargada al arrancar la API si está vacía

- [ ] **Paso 1: Crear Seed.cs**

Crear `src/PalletBalancer.Api/Data/Seed.cs`:

```csharp
using System.Text.Json;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Data;

public static class Seed
{
    public static async Task CargarItemsDesdeJson(AppDbContext db, string rutaJson)
    {
        if (await db.Items.AnyAsync()) return;
        if (!File.Exists(rutaJson)) return;

        var json = await File.ReadAllTextAsync(rutaJson);
        var catalogo = JsonSerializer.Deserialize<CatalogoJson>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (catalogo?.Items is null) return;

        foreach (var e in catalogo.Items)
        {
            db.Items.Add(new Item
            {
                ModelNo           = e.ModelNo,
                Descripcion       = e.Descripcion,
                SpPiezasPorPallet = e.StandardPack.PiezasPorPallet,
                SpPesoKg          = e.StandardPack.Peso_Kg,
                SpLargoCm         = e.StandardPack.Largo_Cm,
                SpAnchoCm         = e.StandardPack.Ancho_Cm,
                SpAltoCm          = e.StandardPack.Alto_Cm,
                CajaPiezasPorCaja = e.Caja.PiezasPorCaja,
                CajaPesoKg        = e.Caja.Peso_Kg,
                CajaLargoCm       = e.Caja.Largo_Cm,
                CajaAnchoCm       = e.Caja.Ancho_Cm,
                CajaAltoCm        = e.Caja.Alto_Cm,
                PiezaPesoKg       = e.Pieza.Peso_Kg,
                PiezaLargoCm      = e.Pieza.Largo_Cm,
                PiezaAnchoCm      = e.Pieza.Ancho_Cm,
                PiezaAltoCm       = e.Pieza.Alto_Cm
            });
        }
        await db.SaveChangesAsync();
    }

    private class CatalogoJson  { public List<EntradaJson> Items { get; set; } = []; }
    private class EntradaJson
    {
        public string ModelNo    { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public SpJson     StandardPack { get; set; } = new();
        public CajaJson   Caja         { get; set; } = new();
        public PiezaJson  Pieza        { get; set; } = new();
    }
    private class SpJson
    {
        public int    PiezasPorPallet { get; set; }
        public double Peso_Kg { get; set; } public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; } public double Alto_Cm { get; set; }
    }
    private class CajaJson
    {
        public int    PiezasPorCaja { get; set; }
        public double Peso_Kg { get; set; } public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; } public double Alto_Cm { get; set; }
    }
    private class PiezaJson
    {
        public double Peso_Kg { get; set; } public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; } public double Alto_Cm { get; set; }
    }
}
```

- [ ] **Paso 2: Llamar al Seed en Program.cs al arrancar**

Agregar después de `var app = builder.Build();`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    var rutaJson = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
                     "PalletBalancer.App", "catalogo_items.json"));
    await Seed.CargarItemsDesdeJson(db, rutaJson);
}
```

- [ ] **Paso 3: Verificar**

```bash
dotnet run --project src/PalletBalancer.Api
```

`GET /api/items` debe devolver `K006T91071XB` sin haberlo ingresado manualmente.

- [ ] **Paso 4: Commit**

```bash
git add src/PalletBalancer.Api/
git commit -m "feat: seed automático desde catalogo_items.json"
```

---

### Tarea 6: Entidades Fdo + FdoLinea + FdosController

**Files:**
- Create: `src/PalletBalancer.Api/Models/Fdo.cs`
- Create: `src/PalletBalancer.Api/Models/FdoLinea.cs`
- Modify: `src/PalletBalancer.Api/Data/AppDbContext.cs`
- Create: `src/PalletBalancer.Api/DTOs/FdoDto.cs`
- Create: `src/PalletBalancer.Api/Controllers/FdosController.cs`
- Create: migración (auto)

**Interfaces:**
- Produces:
  - tabla `Fdos` (Id, FdoSlipNo, DsbDate, ShipDate, Customer, Consignee, CreadoEn)
  - tabla `FdoLineas` (Id, FdoId, CustomerPoNo, ModelNo, ReqQty)
  - `GET /api/fdos` → lista
  - `GET /api/fdos/{id}` → FDO con líneas e items
  - `POST /api/fdos` → crear FDO con líneas

- [ ] **Paso 1: Crear Fdo.cs**

```csharp
using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Fdo
{
    public int Id { get; set; }

    [Required]
    public string FdoSlipNo { get; set; } = string.Empty;

    public DateOnly DsbDate   { get; set; }
    public DateOnly ShipDate  { get; set; }
    public string   Customer  { get; set; } = string.Empty;
    public string   Consignee { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public List<FdoLinea> Lineas { get; set; } = [];
}
```

- [ ] **Paso 2: Crear FdoLinea.cs**

```csharp
namespace PalletBalancer.Api.Models;

public class FdoLinea
{
    public int Id { get; set; }

    public Fdo  Fdo   { get; set; } = null!;
    public int  FdoId { get; set; }

    public string CustomerPoNo { get; set; } = string.Empty;
    public string ModelNo      { get; set; } = string.Empty;
    public Item?  Item         { get; set; }
    public int    ReqQty       { get; set; }
}
```

- [ ] **Paso 3: Registrar en AppDbContext**

```csharp
public DbSet<Item>     Items     => Set<Item>();
public DbSet<Fdo>      Fdos      => Set<Fdo>();
public DbSet<FdoLinea> FdoLineas => Set<FdoLinea>();
```

- [ ] **Paso 4: Crear y aplicar migración**

```bash
dotnet ef migrations add AgregarFdoYFdoLinea \
  --project src/PalletBalancer.Api \
  --startup-project src/PalletBalancer.Api

dotnet ef database update \
  --project src/PalletBalancer.Api \
  --startup-project src/PalletBalancer.Api
```

- [ ] **Paso 5: Crear FdoDto.cs**

```csharp
namespace PalletBalancer.Api.DTOs;

public class FdoDto
{
    public string FdoSlipNo  { get; set; } = string.Empty;
    public string DsbDate    { get; set; } = string.Empty;   // "2026-08-03"
    public string ShipDate   { get; set; } = string.Empty;
    public string Customer   { get; set; } = string.Empty;
    public string Consignee  { get; set; } = string.Empty;
    public List<FdoLineaDto> Lineas { get; set; } = [];
}

public class FdoLineaDto
{
    public string CustomerPoNo { get; set; } = string.Empty;
    public string ModelNo      { get; set; } = string.Empty;
    public int    ReqQty       { get; set; }
}
```

- [ ] **Paso 6: Crear FdosController.cs**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FdosController : ControllerBase
{
    private readonly AppDbContext _db;
    public FdosController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos() =>
        Ok(await _db.Fdos.OrderByDescending(f => f.CreadoEn).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var fdo = await _db.Fdos
            .Include(f => f.Lineas).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(f => f.Id == id);
        return fdo is null ? NotFound() : Ok(fdo);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(FdoDto dto)
    {
        if (await _db.Fdos.AnyAsync(f => f.FdoSlipNo == dto.FdoSlipNo))
            return Conflict($"FDO '{dto.FdoSlipNo}' ya existe.");

        var fdo = new Fdo
        {
            FdoSlipNo = dto.FdoSlipNo,
            DsbDate   = DateOnly.Parse(dto.DsbDate),
            ShipDate  = DateOnly.Parse(dto.ShipDate),
            Customer  = dto.Customer,
            Consignee = dto.Consignee,
            Lineas    = dto.Lineas.Select(l => new FdoLinea
            {
                CustomerPoNo = l.CustomerPoNo,
                ModelNo      = l.ModelNo,
                ReqQty       = l.ReqQty
            }).ToList()
        };

        _db.Fdos.Add(fdo);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = fdo.Id }, fdo);
    }
}
```

- [ ] **Paso 7: Probar en Swagger**

`POST /api/fdos`:
```json
{
  "fdoSlipNo": "2612481",
  "dsbDate": "2026-08-03",
  "shipDate": "2026-08-03",
  "customer": "FORD CEP Mitsubishi Electric Automotive America, Inc.",
  "consignee": "FORD CE Ford Cleveland Engine Plant 1",
  "lineas": [
    { "customerPoNo": "5700173375", "modelNo": "K006T91071XB", "reqQty": 1152 },
    { "customerPoNo": "5700173353", "modelNo": "K006T91072XB", "reqQty": 1728 }
  ]
}
```

`GET /api/fdos/1` — debe devolver el FDO con sus líneas.

- [ ] **Paso 8: Commit**

```bash
git add src/PalletBalancer.Api/
git commit -m "feat: FDO y FdoLinea — modelo, migración y endpoints"
```

---

### Tarea 7: CORS + Deploy a Railway

**Files:**
- Modify: `src/PalletBalancer.Api/Program.cs`
- Create: `railway.json`

**Interfaces:**
- Produces: API accesible desde cualquier dispositivo via URL pública de Railway

- [ ] **Paso 1: Habilitar CORS en Program.cs**

Agregar antes de `builder.Services.AddControllers()`:

```csharp
builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
```

Agregar después de `var app = builder.Build();`:

```csharp
app.UseCors();
```

- [ ] **Paso 2: Crear railway.json en la raíz del proyecto**

```json
{
  "$schema": "https://railway.app/railway-schema.json",
  "build": { "builder": "NIXPACKS" },
  "deploy": {
    "startCommand": "dotnet PalletBalancer.Api.dll",
    "healthcheckPath": "/health"
  }
}
```

- [ ] **Paso 3: Deploy en Railway**

1. Ir a `railway.app` → New Project → Deploy from GitHub repo
2. Seleccionar el repositorio de PalletBalancer
3. Agregar servicio PostgreSQL al proyecto
4. En variables del servicio API: agregar `DATABASE_URL` con la URL de PostgreSQL de Railway (Railway la genera automáticamente)
5. Railway detecta .NET y hace el build automáticamente

- [ ] **Paso 4: Verificar en producción**

`GET https://tu-api.railway.app/health` → `{"status":"ok"}`
`GET https://tu-api.railway.app/api/items` → catálogo desde cualquier dispositivo

- [ ] **Paso 5: Commit**

```bash
git add .
git commit -m "feat: CORS habilitado + configuración Railway deploy"
```

---

## Cobertura del spec

| Requisito | Estado |
|---|---|
| ASP.NET Core Web API | ✅ Tarea 1 |
| PostgreSQL con EF Core | ✅ Tarea 2-3 |
| Catálogo Items CRUD | ✅ Tarea 4 |
| Seed desde JSON existente | ✅ Tarea 5 |
| FDO + líneas en DB | ✅ Tarea 6 |
| Accesible multi-dispositivo | ✅ Tarea 7 |
| Frontend HTML/JS | ⏳ Fase 2 |
| Importar FDO desde PDF | ⏳ Fase 2 |
| Cálculo VAN-PLAN ejes | ⏳ Fase 3 |
