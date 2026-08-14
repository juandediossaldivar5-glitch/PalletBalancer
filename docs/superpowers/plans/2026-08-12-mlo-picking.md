# MLO Upload + Picking Recalculado Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow uploading an MLO (.xls) per FDO, link it to the container loading plan, and generate a reordered picking PDF that tells the warehouse operator exactly which CASEs to pick and in which order so the staging area is ready to load.

**Architecture:** New `Mlo`/`MloLinea` models stored in PostgreSQL via EF Core. A new `MloXlsParser` service reads the XLS. A `MloPickingService` recalculates the picking order from the container plan positions and the MLO lines, matching by ModelNo and accumulating CASEs into pallets using the `Item.SpPiezasPorPallet` standard pack from the existing catalog. The frontend uploads MLOs per FDO and renders a print-only picking section with one row per CASE.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, ExcelDataReader 3.x (new), Alpine.js 3 (existing frontend in `wwwroot/app.html`)

## Global Constraints

- Target framework: net10.0 — do not downgrade.
- All new C# files go in `src/PalletBalancer.Api/` following existing namespaces (`PalletBalancer.Api.Models`, `.DTOs`, `.Services`, `.Controllers`).
- EF migrations must be created with `dotnet ef migrations add` from `src/PalletBalancer.Api/`.
- No authentication changes — MLO endpoints are public (same pattern as existing FDO/Contenedor endpoints, no `[Authorize]`).
- XLS column mapping (0-indexed, from `MLO00302753.xls`): 0=SlipNo(FDO), 1=ModelNo, 3=Class, 5=CaseNo, 6=FromLocation, 7=FromQty, 10=Check('C'|''), 15=Descripcion, 17=ToLocation. Row 0 = header (skip). Last row starts with '-->' (skip).
- `FromLocation` format: `MEAX-FG1-56-17-02` — sort numerically by the last three dash-separated segments (rack, position, level).
- Standard pack = `Item.SpPiezasPorPallet` from existing catalog. One pallet = one group of CASEs whose cumulative FromQty reaches SpPiezasPorPallet.
- Picking order: ascending container row (row 1 = deepest = picks first) → within same row and model: ascending location (closest rack first).
- Frontend state variable names: `mlosPorFdo` (dict fdoId→mlo data), `pickingResult` (picking response). No renaming.
- Push to `origin main` after final task to trigger Railway deploy.

---

## File Structure

**New files:**
- `Models/Mlo.cs` — Mlo entity
- `Models/MloLinea.cs` — MloLinea entity
- `DTOs/MloDto.cs` — MloDto, MloLineaDto, PickingLineaDto, PickingResultadoDto
- `Services/MloXlsParser.cs` — XLS → Mlo+lines
- `Services/MloPickingService.cs` — picking order logic
- `Controllers/MloController.cs` — upload, query, picking endpoints

**Modified files:**
- `PalletBalancer.Api.csproj` — add ExcelDataReader packages
- `Data/AppDbContext.cs` — add DbSet<Mlo>, DbSet<MloLinea>
- `wwwroot/app.html` — upload UI, picking print section

---

### Task 1: Models, DbContext and EF Migration

**Files:**
- Create: `src/PalletBalancer.Api/Models/Mlo.cs`
- Create: `src/PalletBalancer.Api/Models/MloLinea.cs`
- Modify: `src/PalletBalancer.Api/Data/AppDbContext.cs`
- Create: migration via `dotnet ef migrations add AgregarMlo`

**Interfaces:**
- Produces: `Mlo` with `Id`, `MloNo`, `FdoId`, `FechaEntrega`, `Lineas`; `MloLinea` with `Id`, `MloId`, `SlipNo`, `ModelNo`, `Class`, `CaseNo`, `FromLocation`, `FromQty`, `Check`, `Descripcion`, `ToLocation`.

- [ ] **Step 1: Create `Mlo.cs`**

```csharp
// src/PalletBalancer.Api/Models/Mlo.cs
namespace PalletBalancer.Api.Models;

public class Mlo
{
    public int    Id          { get; set; }
    public string MloNo       { get; set; } = string.Empty;  // e.g. "MLO00302753"
    public int    FdoId       { get; set; }
    public Fdo    Fdo         { get; set; } = null!;
    public DateOnly FechaEntrega { get; set; }
    public DateTime CreadoEn  { get; set; } = DateTime.UtcNow;

    public List<MloLinea> Lineas { get; set; } = [];
}
```

- [ ] **Step 2: Create `MloLinea.cs`**

```csharp
// src/PalletBalancer.Api/Models/MloLinea.cs
namespace PalletBalancer.Api.Models;

public class MloLinea
{
    public int    Id           { get; set; }
    public int    MloId        { get; set; }
    public Mlo    Mlo          { get; set; } = null!;

    public string SlipNo       { get; set; } = string.Empty;  // col 0 — FDO ref
    public string ModelNo      { get; set; } = string.Empty;  // col 1
    public string Class        { get; set; } = string.Empty;  // col 3
    public string CaseNo       { get; set; } = string.Empty;  // col 5
    public string FromLocation { get; set; } = string.Empty;  // col 6 e.g. MEAX-FG1-56-17-02
    public int    FromQty      { get; set; }                  // col 7
    public string Check        { get; set; } = string.Empty;  // col 10 "C" or ""
    public string Descripcion  { get; set; } = string.Empty;  // col 15
    public string ToLocation   { get; set; } = string.Empty;  // col 17 e.g. MEAX-FGVIA
}
```

- [ ] **Step 3: Add DbSets to `AppDbContext.cs`**

Add these two lines inside the `AppDbContext` class body, after `public DbSet<Usuario> Usuarios`:

```csharp
public DbSet<Mlo>      Mlos      => Set<Mlo>();
public DbSet<MloLinea> MloLineas => Set<MloLinea>();
```

- [ ] **Step 4: Create EF migration**

Run from `src/PalletBalancer.Api/`:
```bash
dotnet ef migrations add AgregarMlo
```

Expected: new file `Migrations/<timestamp>_AgregarMlo.cs` created with `CreateTable` for `Mlos` and `MloLineas`.

- [ ] **Step 5: Verify build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/PalletBalancer.Api/Models/Mlo.cs \
        src/PalletBalancer.Api/Models/MloLinea.cs \
        src/PalletBalancer.Api/Data/AppDbContext.cs \
        src/PalletBalancer.Api/Migrations/
git commit -m "feat(mlo): add Mlo/MloLinea models and EF migration"
```

---

### Task 2: ExcelDataReader Package + XLS Parser Service

**Files:**
- Modify: `src/PalletBalancer.Api/PalletBalancer.Api.csproj`
- Create: `src/PalletBalancer.Api/Services/MloXlsParser.cs`

**Interfaces:**
- Consumes: `Mlo`, `MloLinea` from Task 1.
- Produces: `MloXlsParser.Parse(Stream stream, string fileName, int fdoId) → Mlo` — static method, no DI needed.

- [ ] **Step 1: Add NuGet packages**

Run from `src/PalletBalancer.Api/`:
```bash
dotnet add package ExcelDataReader --version 3.7.0
dotnet add package ExcelDataReader.DataSet --version 3.7.0
dotnet add package System.Text.Encoding.CodePages --version 9.0.0
```

- [ ] **Step 2: Create `MloXlsParser.cs`**

```csharp
// src/PalletBalancer.Api/Services/MloXlsParser.cs
using System.Data;
using System.Text;
using ExcelDataReader;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloXlsParser
{
    public static Mlo Parse(Stream stream, string fileName, int fdoId)
    {
        // Required for .xls (BIFF format)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var sheet = dataSet.Tables[0];
        var mloNo = Path.GetFileNameWithoutExtension(fileName); // "MLO00302753"

        // Parse FechaEntrega from filename digits if present, else today
        var fechaEntrega = DateOnly.FromDateTime(DateTime.UtcNow);

        var mlo = new Mlo
        {
            MloNo        = mloNo,
            FdoId        = fdoId,
            FechaEntrega = fechaEntrega,
        };

        foreach (DataRow row in sheet.Rows)
        {
            var col0 = row[0]?.ToString()?.Trim() ?? "";

            // Skip header row (col0 = "FG Model") and footer (col0 starts with "-->")
            if (col0 == "FG Model" || col0.StartsWith("-->") || col0 == "")
                continue;

            var caseNo = row[5]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(caseNo))
                continue;

            var fromQtyStr = row[7]?.ToString()?.Trim() ?? "0";
            if (!int.TryParse(fromQtyStr, out var fromQty))
                fromQty = (int)(double.TryParse(fromQtyStr, out var d) ? d : 0);

            mlo.Lineas.Add(new MloLinea
            {
                SlipNo       = col0,
                ModelNo      = row[1]?.ToString()?.Trim() ?? "",
                Class        = row[3]?.ToString()?.Trim() ?? "",
                CaseNo       = caseNo,
                FromLocation = row[6]?.ToString()?.Trim() ?? "",
                FromQty      = fromQty,
                Check        = row[10]?.ToString()?.Trim() ?? "",
                Descripcion  = row[15]?.ToString()?.Trim() ?? "",
                ToLocation   = row[17]?.ToString()?.Trim() ?? "",
            });
        }

        return mlo;
    }

    /// <summary>
    /// Parses the numeric location segments for proximity sorting.
    /// "MEAX-FG1-56-17-02" → (56, 17, 2)
    /// </summary>
    public static (int Rack, int Pos, int Level) ParseLocation(string loc)
    {
        var parts = loc.Split('-');
        if (parts.Length < 3) return (0, 0, 0);
        // Last 3 numeric segments
        int.TryParse(parts[^3], out var rack);
        int.TryParse(parts[^2], out var pos);
        int.TryParse(parts[^1], out var lvl);
        return (rack, pos, lvl);
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/PalletBalancer.Api/PalletBalancer.Api.csproj \
        src/PalletBalancer.Api/Services/MloXlsParser.cs
git commit -m "feat(mlo): add ExcelDataReader and XLS parser service"
```

---

### Task 3: DTOs, Picking Service and MLO Controller

**Files:**
- Create: `src/PalletBalancer.Api/DTOs/MloDto.cs`
- Create: `src/PalletBalancer.Api/Services/MloPickingService.cs`
- Create: `src/PalletBalancer.Api/Controllers/MloController.cs`

**Interfaces:**
- Consumes: `MloXlsParser.Parse` and `MloXlsParser.ParseLocation` from Task 2; `Mlo`, `MloLinea` from Task 1; existing `ContenedorService`, `AppDbContext`, `Fdo`, `Item`.
- Produces:
  - `POST /api/mlo/upload` → `MloResumenDto`
  - `GET /api/mlo/fdo/{fdoId}` → `MloDto`
  - `POST /api/mlo/picking` body: `PickingRequestDto` → `PickingResultadoDto`

- [ ] **Step 1: Create `DTOs/MloDto.cs`**

```csharp
// src/PalletBalancer.Api/DTOs/MloDto.cs
namespace PalletBalancer.Api.DTOs;

public class MloResumenDto
{
    public int    Id      { get; set; }
    public string MloNo   { get; set; } = "";
    public int    FdoId   { get; set; }
    public int    TotalLineas { get; set; }
}

public class MloDto
{
    public int    Id      { get; set; }
    public string MloNo   { get; set; } = "";
    public int    FdoId   { get; set; }
    public string FdoSlipNo { get; set; } = "";
    public List<MloLineaDto> Lineas { get; set; } = [];
}

public class MloLineaDto
{
    public int    Id           { get; set; }
    public string CaseNo       { get; set; } = "";
    public string ModelNo      { get; set; } = "";
    public string Descripcion  { get; set; } = "";
    public string FromLocation { get; set; } = "";
    public int    FromQty      { get; set; }
    public string Check        { get; set; } = "";
}

// Request for picking endpoint
public class PickingRequestDto
{
    public List<int>    FdoIds          { get; set; } = [];
    public string?      TipoContenedor  { get; set; }
    public string?      TipoTractocamion { get; set; }
    public List<string>? OrdenDescarga  { get; set; }
}

// One line in the picking list
public class PickingLineaDto
{
    public int    OrdenPicking    { get; set; }  // 1-based, ascending = pick first
    public int    FilaContenedor  { get; set; }
    public string LadoContenedor  { get; set; } = "";
    public int    CapaContenedor  { get; set; }
    public string Consignee       { get; set; } = "";
    public string FdoSlipNo       { get; set; } = "";
    public string MloNo           { get; set; } = "";
    public string CaseNo          { get; set; } = "";
    public string FromLocation    { get; set; } = "";
    public string ModelNo         { get; set; } = "";
    public string Descripcion     { get; set; } = "";
    public int    Qty             { get; set; }
    public bool   EsParcial       { get; set; }
}

public class PickingResultadoDto
{
    public List<PickingLineaDto> Lineas  { get; set; } = [];
    public List<string> Advertencias    { get; set; } = [];
}
```

- [ ] **Step 2: Create `Services/MloPickingService.cs`**

```csharp
// src/PalletBalancer.Api/Services/MloPickingService.cs
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloPickingService
{
    /// <summary>
    /// Given the container plan positions and the MLOs for each FDO,
    /// returns an ordered picking list (row 1 first = deepest in container).
    /// </summary>
    public static PickingResultadoDto Calcular(
        ContenedorResultadoDto plan,
        List<(Fdo Fdo, Mlo? Mlo, Item? Item)> fdoData)
    {
        var advertencias = new List<string>();
        var lineas       = new List<PickingLineaDto>();

        // Build lookup: modelNo → pool of CASEs sorted by location (closest first)
        // Key: modelNo, Value: queue of MloLineas ordered by location ascending
        var casePool = new Dictionary<string, Queue<MloLinea>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fdo, mlo, _) in fdoData)
        {
            if (mlo == null) continue;
            foreach (var linea in mlo.Lineas)
            {
                if (!casePool.ContainsKey(linea.ModelNo))
                    casePool[linea.ModelNo] = new Queue<MloLinea>();
                // Items will be added sorted; we sort after building all
            }
        }

        // Sort each model's lines by location, then enqueue
        var poolRaw = new Dictionary<string, List<MloLinea>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fdo, mlo, _) in fdoData)
        {
            if (mlo == null) continue;
            foreach (var linea in mlo.Lineas)
            {
                if (!poolRaw.ContainsKey(linea.ModelNo))
                    poolRaw[linea.ModelNo] = [];
                poolRaw[linea.ModelNo].Add(linea);
            }
        }

        foreach (var (modelNo, list) in poolRaw)
        {
            var sorted = list
                .OrderBy(l => MloXlsParser.ParseLocation(l.FromLocation).Rack)
                .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Pos)
                .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Level)
                .ToList();
            casePool[modelNo] = new Queue<MloLinea>(sorted);
        }

        // Build FDO lookup for SlipNo and consignee
        var fdoById = fdoData.ToDictionary(x => x.Fdo.Id, x => x.Fdo);
        var mloByFdoSlip = fdoData
            .Where(x => x.Mlo != null)
            .ToDictionary(x => x.Fdo.FdoSlipNo, x => x.Mlo!, StringComparer.OrdinalIgnoreCase);

        // Positions ordered: row ascending (1 = deepest = pick first), then lado, then capa
        var posOrdenadas = plan.Posiciones
            .OrderBy(p => p.Fila)
            .ThenBy(p => p.Lado)
            .ThenBy(p => p.Capa)
            .ToList();

        int orden = 1;
        foreach (var pos in posOrdenadas)
        {
            if (!casePool.TryGetValue(pos.ModelNo, out var pool) || pool.Count == 0)
            {
                advertencias.Add($"Sin CASE en MLO para modelo {pos.ModelNo} (Fila {pos.Fila}, {pos.Lado})");
                lineas.Add(new PickingLineaDto
                {
                    OrdenPicking   = orden++,
                    FilaContenedor = pos.Fila,
                    LadoContenedor = pos.Lado,
                    CapaContenedor = pos.Capa,
                    Consignee      = pos.Destino,
                    ModelNo        = pos.ModelNo,
                    Descripcion    = pos.Descripcion,
                    Qty            = pos.Piezas,
                    EsParcial      = pos.EsParcial,
                    CaseNo         = "(sin MLO)",
                    FromLocation   = "(sin MLO)",
                });
                continue;
            }

            var case_ = pool.Dequeue();

            // Find which FDO/MLO this case belongs to
            var fdoSlip = case_.SlipNo;
            mloByFdoSlip.TryGetValue(fdoSlip, out var mloRef);

            lineas.Add(new PickingLineaDto
            {
                OrdenPicking   = orden++,
                FilaContenedor = pos.Fila,
                LadoContenedor = pos.Lado,
                CapaContenedor = pos.Capa,
                Consignee      = pos.Destino,
                FdoSlipNo      = fdoSlip,
                MloNo          = mloRef?.MloNo ?? "",
                CaseNo         = case_.CaseNo,
                FromLocation   = case_.FromLocation,
                ModelNo        = pos.ModelNo,
                Descripcion    = pos.Descripcion,
                Qty            = pos.Piezas,
                EsParcial      = pos.EsParcial,
            });
        }

        return new PickingResultadoDto { Lineas = lineas, Advertencias = advertencias };
    }
}
```

- [ ] **Step 3: Create `Controllers/MloController.cs`**

```csharp
// src/PalletBalancer.Api/Controllers/MloController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Api.Services;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MloController : ControllerBase
{
    private readonly AppDbContext _db;
    public MloController(AppDbContext db) => _db = db;

    /// <summary>
    /// Upload an MLO XLS file and link it to the FDO identified by fdoSlipNo.
    /// Replaces any existing MLO for that FDO.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile archivo, [FromForm] string fdoSlipNo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Se requiere un archivo XLS." });
        if (string.IsNullOrWhiteSpace(fdoSlipNo))
            return BadRequest(new { mensaje = "Se requiere fdoSlipNo." });

        var fdo = await _db.Fdos.FirstOrDefaultAsync(f => f.FdoSlipNo == fdoSlipNo);
        if (fdo == null)
            return NotFound(new { mensaje = $"FDO '{fdoSlipNo}' no encontrado." });

        // Remove existing MLO for this FDO if any
        var existing = await _db.Mlos
            .Include(m => m.Lineas)
            .FirstOrDefaultAsync(m => m.FdoId == fdo.Id);
        if (existing != null)
        {
            _db.MloLineas.RemoveRange(existing.Lineas);
            _db.Mlos.Remove(existing);
        }

        Mlo mlo;
        using (var stream = archivo.OpenReadStream())
            mlo = MloXlsParser.Parse(stream, archivo.FileName, fdo.Id);

        _db.Mlos.Add(mlo);
        await _db.SaveChangesAsync();

        return Ok(new MloResumenDto
        {
            Id         = mlo.Id,
            MloNo      = mlo.MloNo,
            FdoId      = fdo.Id,
            TotalLineas = mlo.Lineas.Count,
        });
    }

    /// <summary>Returns the MLO linked to a given FDO id.</summary>
    [HttpGet("fdo/{fdoId:int}")]
    public async Task<IActionResult> GetByFdo(int fdoId)
    {
        var mlo = await _db.Mlos
            .Include(m => m.Lineas)
            .Include(m => m.Fdo)
            .FirstOrDefaultAsync(m => m.FdoId == fdoId);

        if (mlo == null) return NotFound();

        return Ok(new MloDto
        {
            Id        = mlo.Id,
            MloNo     = mlo.MloNo,
            FdoId     = mlo.FdoId,
            FdoSlipNo = mlo.Fdo.FdoSlipNo,
            Lineas    = mlo.Lineas.Select(l => new MloLineaDto
            {
                Id           = l.Id,
                CaseNo       = l.CaseNo,
                ModelNo      = l.ModelNo,
                Descripcion  = l.Descripcion,
                FromLocation = l.FromLocation,
                FromQty      = l.FromQty,
                Check        = l.Check,
            }).ToList(),
        });
    }

    /// <summary>
    /// Given the same inputs as /api/contenedor/calcular plus FDO ids,
    /// recalculates the container plan and overlays picking order from MLOs.
    /// </summary>
    [HttpPost("picking")]
    public async Task<IActionResult> Picking(PickingRequestDto dto)
    {
        if (dto.FdoIds == null || dto.FdoIds.Count == 0)
            return BadRequest(new { mensaje = "Se requiere al menos un FDO." });

        var fdos = await _db.Fdos
            .Include(f => f.Lineas)
            .Where(f => dto.FdoIds.Contains(f.Id))
            .ToListAsync();

        if (fdos.Count == 0) return NotFound();

        // Load items into FDO lines (same pattern as ContenedorController)
        var modelNos = fdos.SelectMany(f => f.Lineas).Select(l => l.ModelNo).ToHashSet();
        var items = await _db.Items
            .Where(i => modelNos.Contains(i.ModelNo))
            .ToDictionaryAsync(i => i.ModelNo);
        foreach (var linea in fdos.SelectMany(f => f.Lineas))
            linea.Item = items.GetValueOrDefault(linea.ModelNo);

        // Load MLOs for each FDO
        var mlos = await _db.Mlos
            .Include(m => m.Lineas)
            .Where(m => dto.FdoIds.Contains(m.FdoId))
            .ToDictionaryAsync(m => m.FdoId);

        // Calculate container plan
        var plan = new ContenedorService().Calcular(
            fdos, dto.OrdenDescarga, dto.TipoContenedor, dto.TipoTractocamion);

        // Build fdoData tuples
        var fdoData = fdos.Select(f => (
            Fdo:  f,
            Mlo:  mlos.GetValueOrDefault(f.Id),
            Item: (Item?)null
        )).ToList();

        var resultado = MloPickingService.Calcular(plan, fdoData);
        return Ok(resultado);
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/PalletBalancer.Api/DTOs/MloDto.cs \
        src/PalletBalancer.Api/Services/MloPickingService.cs \
        src/PalletBalancer.Api/Controllers/MloController.cs
git commit -m "feat(mlo): DTOs, picking service and MLO controller"
```

---

### Task 4: Frontend — MLO Upload UI + Picking Print Section

**Files:**
- Modify: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Consumes: `POST /api/mlo/upload`, `GET /api/mlo/fdo/{fdoId}`, `POST /api/mlo/picking` from Task 3.
- Produces: Alpine.js state `mlosPorFdo` (object), `pickingResult` (object|null); print-only section `<div class="print-only pr-picking-page">`.

There are 5 distinct edits to `app.html`. Apply them in order.

---

#### Edit A — Add Alpine.js state variables

Find the line that begins the `data()` return object. It looks like:

```javascript
      contenedor: null,
```

Add two new lines immediately after it:

```javascript
      mlosPorFdo:   {},   // fdoId → { mloNo, lineas[] }
      pickingResult: null,
```

---

#### Edit B — Add `cargarMlo()` and `generarPicking()` methods

Find the method `puedeEditar()` in the Alpine.js methods section:

```javascript
    puedeEditar() {
```

Add these two methods immediately before it:

```javascript
    async cargarMlo(fdoId, archivo) {
      if (!archivo) return;
      const fdo = this.fdos.find(f => f.id === fdoId);
      if (!fdo) return;
      const form = new FormData();
      form.append('archivo', archivo);
      form.append('fdoSlipNo', fdo.fdoSlipNo);
      const r = await fetch('/api/mlo/upload', { method:'POST', body: form });
      if (!r.ok) { alert('Error al subir MLO'); return; }
      const data = await r.json();
      // Reload MLO lines
      const r2 = await fetch(`/api/mlo/fdo/${fdoId}`);
      if (r2.ok) {
        const mlo = await r2.json();
        this.mlosPorFdo = { ...this.mlosPorFdo, [fdoId]: mlo };
      }
      alert(`MLO ${data.mloNo} cargado — ${data.totalLineas} líneas`);
    },

    async generarPicking() {
      if (!this.contenedor) { alert('Primero calcula el contenedor.'); return; }
      const selIds = this.fdosSeleccionados.map(f => f.id);
      const r = await fetch('/api/mlo/picking', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fdoIds:          selIds,
          tipoContenedor:  this.tipoContenedor,
          tipoTractocamion: this.tipoTractocamion,
          ordenDescarga:   null,
        })
      });
      if (!r.ok) { alert('Error al generar picking'); return; }
      this.pickingResult = await r.json();
    },

```

---

#### Edit C — Add MLO upload button per FDO in the FDO list

Find the FDO list section in the screen UI. Search for the element that shows each FDO's slip number and consignee — it is inside an `x-for="fdo in fdosSeleccionados"` or similar loop showing FDO info. Look for text like `x-text="fdo.fdoSlipNo"` or `fdo.consignee`.

Add the following block immediately after the FDO slip number display inside the FDO card/row (inside the same `x-for` loop):

```html
<!-- MLO upload -->
<div style="margin-top:6px;display:flex;align-items:center;gap:8px;flex-wrap:wrap">
  <label :for="'mlo-'+fdo.id" style="cursor:pointer;font-size:12px;color:var(--accent);border:1px solid var(--accent);border-radius:6px;padding:3px 10px">
    <span x-text="mlosPorFdo[fdo.id] ? '✓ MLO ' + mlosPorFdo[fdo.id].mloNo : '+ Cargar MLO'"></span>
  </label>
  <input :id="'mlo-'+fdo.id" type="file" accept=".xls,.xlsx" style="display:none"
         @change="cargarMlo(fdo.id, $event.target.files[0])">
  <span x-show="mlosPorFdo[fdo.id]" style="font-size:11px;color:var(--muted)"
        x-text="mlosPorFdo[fdo.id]?.lineas?.length + ' CASEs'"></span>
</div>
```

---

#### Edit D — Add "Generar MLO de Picking" button in the results section

Find the button or area that shows after the container calculation result — look for the "Imprimir / PDF" or `window.print()` button. It likely looks like:

```html
@click="window.print()"
```

Add the following button immediately after that print button:

```html
<button @click="generarPicking()"
        x-show="contenedor && Object.keys(mlosPorFdo).length > 0"
        style="background:rgba(77,216,132,0.15);border:1px solid var(--accent);color:var(--accent);padding:8px 18px;border-radius:8px;cursor:pointer;font-weight:700;font-size:14px">
  📋 MLO de Picking
</button>
```

---

#### Edit E — Add print-only picking section

Find the closing line of the print section:

```html
</div><!-- /print-only -->
```

Insert the following complete block immediately before that closing div:

```html
  <!-- ═══ PÁGINA PICKING: MLO Recalculada ═══ -->
  <div class="pr-page" style="page-break-before:always" x-show="pickingResult && pickingResult.lineas.length > 0">

    <div class="pr-header">
      <img src="assets/logo-melco.jpg" class="pr-header-logo" alt="MELCO">
      <div class="pr-header-center">
        <h1>MLO RECALCULADA — ORDEN DE PICKING</h1>
        <p>Orden de recolección para carga de contenedor · Uso exclusivo operador de almacén</p>
      </div>
      <div class="pr-header-meta">
        <div x-text="new Date().toLocaleDateString('es-MX',{day:'2-digit',month:'long',year:'numeric'})"></div>
        <div x-text="'FDOs: ' + fdosDelContenedor().map(f=>f.fdoSlipNo).join(', ')"></div>
        <div x-text="contenedor?.contenedorTipo + ' · ' + contenedor?.tractocamionTipo"></div>
      </div>
    </div>

    <template x-if="pickingResult?.advertencias?.length > 0">
      <div style="margin-bottom:8pt">
        <template x-for="adv in pickingResult.advertencias">
          <div class="pr-warn">⚠ <span x-text="adv"></span></div>
        </template>
      </div>
    </template>

    <table class="pr-table" style="font-size:7.5pt">
      <thead>
        <tr>
          <th style="width:22pt">#</th>
          <th style="width:26pt">Fila</th>
          <th style="width:20pt">Lado</th>
          <th style="width:18pt">Capa</th>
          <th style="width:55pt">CASE No.</th>
          <th style="width:80pt">Ubicación</th>
          <th style="width:60pt">Modelo</th>
          <th>Descripción</th>
          <th style="width:28pt">Qty</th>
          <th style="width:38pt">Consignee</th>
          <th style="width:22pt">FDO</th>
          <th style="width:22pt">✓</th>
        </tr>
      </thead>
      <tbody>
        <template x-for="l in pickingResult.lineas" :key="l.ordenPicking">
          <tr :style="l.esParcial ? 'background:#fff8dc' : ''">
            <td style="text-align:center;font-weight:800" x-text="l.ordenPicking"></td>
            <td style="text-align:center;font-weight:700" x-text="l.filaContenedor"></td>
            <td style="text-align:center;font-size:9px" x-text="l.ladoContenedor === 'Izquierdo' ? 'IZQ' : 'DER'"></td>
            <td style="text-align:center" x-text="l.capaContenedor"></td>
            <td style="font-family:monospace;font-size:7pt" x-text="l.caseNo"></td>
            <td style="font-family:monospace;font-size:7pt;font-weight:700;color:#1a3a6b" x-text="l.fromLocation"></td>
            <td style="font-family:monospace" x-text="l.modelNo"></td>
            <td x-text="l.descripcion + (l.esParcial ? ' [PARCIAL]' : '')"></td>
            <td style="text-align:right;font-weight:700" x-text="l.qty"></td>
            <td style="font-size:7pt" x-text="l.consignee"></td>
            <td style="font-size:7pt;font-family:monospace" x-text="l.fdoSlipNo"></td>
            <td style="border:0.5pt solid #999;width:18pt"></td>
          </tr>
        </template>
      </tbody>
    </table>

    <div class="pr-sign" style="margin-top:20pt">
      <div class="pr-sign-box">Picker — Nombre:<br><br>___________________________</div>
      <div class="pr-sign-box">Verificó — Nombre:<br><br>___________________________</div>
      <div class="pr-sign-box">Firma / Hora:<br><br>___________________________</div>
    </div>

  </div>
```

---

#### Verification (Edit A–E combined)

- [ ] **Step 1: Apply Edit A** (add `mlosPorFdo` and `pickingResult` to data())
- [ ] **Step 2: Apply Edit B** (add `cargarMlo` and `generarPicking` methods)
- [ ] **Step 3: Apply Edit C** (MLO upload button per FDO in list)
- [ ] **Step 4: Apply Edit D** (Generar Picking button after print button)
- [ ] **Step 5: Apply Edit E** (print-only picking section before /print-only)

- [ ] **Step 6: Smoke-test in browser**

Start the API locally:
```bash
cd src/PalletBalancer.Api && dotnet run
```
Open `http://localhost:8080`.
- Load the page — no JS errors in console.
- Open browser DevTools → Console — should be clean.
- Verify the FDO list shows a "+ Cargar MLO" label per FDO.
- Verify the picking button is hidden (appears only when contenedor is set AND mlosPorFdo has entries).

- [ ] **Step 7: Commit**

```bash
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat(mlo): frontend upload UI, picking button and print picking section"
```

---

### Task 5: End-to-End Test + Deploy

**Files:** No new files. Validates the full flow and pushes to Railway.

- [ ] **Step 1: Run the API locally**

```bash
cd src/PalletBalancer.Api && dotnet run
```

- [ ] **Step 2: Upload an MLO via curl**

```bash
curl -X POST http://localhost:8080/api/mlo/upload \
  -F "archivo=@/Users/jd/Desktop/MLO00302753.xls" \
  -F "fdoSlipNo=2612492-1"
```

Expected response:
```json
{ "id": 1, "mloNo": "MLO00302753", "fdoId": <N>, "totalLineas": 8 }
```

If FDO `2612492-1` doesn't exist yet, create it first via `POST /api/fdos` or use an existing FDO from the DB. Check available FDOs: `GET http://localhost:8080/api/fdos`.

- [ ] **Step 3: Verify MLO stored**

```bash
curl http://localhost:8080/api/mlo/fdo/<fdoId>
```
Expected: JSON with `lineas` array of 8 items, each with `caseNo`, `fromLocation`, `modelNo`, etc.

- [ ] **Step 4: Verify picking endpoint**

```bash
curl -X POST http://localhost:8080/api/mlo/picking \
  -H "Content-Type: application/json" \
  -d '{"fdoIds":[<fdoId>],"tipoContenedor":"40ft HC","tipoTractocamion":"T3-S2 Estándar"}'
```
Expected: `{"lineas":[...],"advertencias":[]}` where `lineas` are ordered by `filaContenedor` ascending.

- [ ] **Step 5: Test the UI flow**

Open `http://localhost:8080`:
1. Log in.
2. Select the FDO linked to the MLO.
3. Click "+ Cargar MLO" → upload `MLO00302753.xls` → confirm alert shows lines count.
4. Select container type, calculate.
5. Click "📋 MLO de Picking" → button should appear.
6. Open print preview → picking page appears as the last page with the ordered table.

- [ ] **Step 6: Push to Railway**

```bash
git push origin main
```

Wait ~2 minutes, verify at `https://palletbalancer-production.up.railway.app`.

---

## Self-Review Notes

**Spec coverage check:**
- ✅ XLS parser (Task 2) — columns 0,1,3,5,6,7,10,15,17; skips header and footer
- ✅ Standard pack from `Item.SpPiezasPorPallet` — used in MloPickingService via ContenedorResultadoDto positions
- ✅ Location sort by rack→pos→level (ParseLocation helper)
- ✅ Container row ascending = pick first (deepest = row 1)
- ✅ Multiple FDOs + multiple MLOs per container
- ✅ FdoSlipNo linkage (col 0 of XLS = Fdo.FdoSlipNo)
- ✅ Replaces existing MLO on re-upload (upsert logic in controller)
- ✅ Picking PDF with checkbox column for operator sign-off
- ✅ Warnings when a model has no CASE in any MLO

**Note for implementer:** The `MloPickingService` uses one CASE per container position (one pallet = one position). If `Item.SpPiezasPorPallet > pos.Piezas` (partial pallet), the position's `EsParcial=true` flag is already set by `ContenedorService` — no extra grouping logic needed at the picking layer.
