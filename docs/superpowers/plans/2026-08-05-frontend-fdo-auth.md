# PalletBalancer Frontend Phase 1 — Auth + FDO Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Añadir autenticación JWT con 5 roles y un frontend HTML/JS completo para importar FDOs desde PDF, verlos, y editar cantidades con permiso AMG/ADM.

**Architecture:** Static files en `wwwroot/` dentro del proyecto API (ASP.NET Core los sirve directamente). Alpine.js 3 + Bootstrap 5 via CDN, sin build step. Backend añade tabla `Usuarios`, JWT, endpoint PDF import con PdfPig, y PATCH de cantidades.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core 10, Npgsql, BCrypt.Net-Next 4, Microsoft.AspNetCore.Authentication.JwtBearer 10, UglyToad.PdfPig 0.1.9, Alpine.js 3, Bootstrap 5, PostgreSQL en Railway.

## Global Constraints

- Nombres de clases, métodos y variables: en español (excepción: DTOs que reflejan nombres del PDF como `FdoSlipNo`, `CustomerPoNo`, `ModelNo`)
- Unidades en DB: kg y cm
- .NET 10 / C# 13 — target framework `net10.0`
- Alpine.js y Bootstrap via CDN, sin npm ni build step
- JWT guardado en `localStorage` con clave `pb_token`; datos de usuario en `pb_usuario`
- Credenciales iniciales del admin: usuario `admin`, contraseña `Admin1234!`
- Railway env var para JWT key: `JWT__Key` (mínimo 32 caracteres)

---

## File Map

**Crear:**
- `src/PalletBalancer.Api/Models/Usuario.cs`
- `src/PalletBalancer.Api/DTOs/AuthDto.cs`
- `src/PalletBalancer.Api/DTOs/FdoImportadoDto.cs`
- `src/PalletBalancer.Api/DTOs/PatchCantidadDto.cs`
- `src/PalletBalancer.Api/Controllers/AuthController.cs`
- `src/PalletBalancer.Api/Services/PdfFdoParser.cs`
- `src/PalletBalancer.Api/wwwroot/index.html`
- `src/PalletBalancer.Api/wwwroot/app.html`

**Modificar:**
- `src/PalletBalancer.Api/PalletBalancer.Api.csproj` — nuevos paquetes NuGet
- `src/PalletBalancer.Api/appsettings.json` — sección Jwt
- `src/PalletBalancer.Api/Data/AppDbContext.cs` — DbSet<Usuario>
- `src/PalletBalancer.Api/Data/Seed.cs` — seed usuario ADM
- `src/PalletBalancer.Api/Program.cs` — JWT auth, static files, seed admin
- `src/PalletBalancer.Api/Controllers/FdosController.cs` — endpoints importar + PATCH
- `tests/PalletBalancer.Core.Tests/*.csproj` — referencia a PalletBalancer.Api

**Migración:**
- `src/PalletBalancer.Api/Migrations/` — nueva migración `AgregarUsuarios`

---

### Task 1: NuGet packages + JWT config + static files middleware

**Files:**
- Modify: `src/PalletBalancer.Api/PalletBalancer.Api.csproj`
- Modify: `src/PalletBalancer.Api/appsettings.json`
- Modify: `src/PalletBalancer.Api/Program.cs`

**Interfaces:**
- Produces: JWT authentication middleware disponible para AuthController (Task 3) y endpoints protegidos (Tasks 4, 5). Archivos estáticos en `wwwroot/` servidos automáticamente.

- [ ] **Step 1: Agregar paquetes NuGet al .csproj**

Abre `src/PalletBalancer.Api/PalletBalancer.Api.csproj` y agrega dentro del primer `<ItemGroup>`:

```xml
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.8" />
<PackageReference Include="UglyToad.PdfPig" Version="0.1.9" />
```

- [ ] **Step 2: Restaurar paquetes y verificar que compila**

```bash
cd /Users/jd/Desktop/PalletBalancer
dotnet restore src/PalletBalancer.Api/PalletBalancer.Api.csproj
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

Esperado: `Build succeeded.`

- [ ] **Step 3: Agregar sección Jwt a appsettings.json**

Reemplaza el contenido de `src/PalletBalancer.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "REEMPLAZAR_CON_URL_DE_RAILWAY"
  },
  "Jwt": {
    "Key": "REEMPLAZAR_CON_ENV_VAR_EN_RAILWAY_JWT__Key",
    "Issuer": "PalletBalancer",
    "Audience": "PalletBalancer",
    "ExpiresHours": 8
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

- [ ] **Step 4: Actualizar Program.cs — agregar JWT auth y static files**

Reemplaza el contenido de `src/PalletBalancer.Api/Program.cs`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PalletBalancer.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rawConnection = Environment.GetEnvironmentVariable("DATABASE_URL")
                 ?? builder.Configuration.GetConnectionString("Default")
                 ?? "";

var connectionString = rawConnection.StartsWith("postgresql://") || rawConnection.StartsWith("postgres://")
    ? ConvertirUrlAConexion(rawConnection)
    : rawConnection;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key no configurado.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = Encoding.UTF8.GetBytes(jwtKey);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(key),
            ValidateIssuer          = true,
            ValidIssuer             = builder.Configuration["Jwt:Issuer"],
            ValidateAudience        = true,
            ValidAudience           = builder.Configuration["Jwt:Audience"],
            ClockSkew               = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

static string ConvertirUrlAConexion(string url)
{
    var uri  = new Uri(url);
    var info = uri.UserInfo.Split(':');
    return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};" +
           $"Database={uri.AbsolutePath.TrimStart('/')};" +
           $"Username={Uri.UnescapeDataString(info[0])};" +
           $"Password={Uri.UnescapeDataString(info[1])};" +
           $"SSL Mode=Require;Trust Server Certificate=true";
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "v4" }));

app.MapGet("/debug/env", () =>
{
    var raw = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "(no DATABASE_URL)";
    var preview = raw.Length > 30 ? raw[..30] + "..." : raw;
    return Results.Ok(new { DATABASE_URL_preview = preview, longitud = raw.Length });
});

app.MapGet("/debug/db", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        return Results.Ok(new { conectado = canConnect, migracionesPendientes = pending });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = ex.Message });
    }
});

app.MapControllers();

_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        var rutaJson = Path.Combine(AppContext.BaseDirectory, "catalogo_items.json");
        await Seed.CargarItemsDesdeJson(db, rutaJson);
        await Seed.SeedUsuarioAdminAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error en migración/seed: {ex.Message}");
    }
});

app.Run();
```

- [ ] **Step 5: Verificar que compila**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

Esperado: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PalletBalancer.Api/PalletBalancer.Api.csproj \
        src/PalletBalancer.Api/appsettings.json \
        src/PalletBalancer.Api/Program.cs
git commit -m "feat: agregar JWT auth y static files middleware"
```

---

### Task 2: Modelo Usuario + migración + seed ADM

**Files:**
- Create: `src/PalletBalancer.Api/Models/Usuario.cs`
- Modify: `src/PalletBalancer.Api/Data/AppDbContext.cs`
- Modify: `src/PalletBalancer.Api/Data/Seed.cs`
- New migration via CLI

**Interfaces:**
- Produces: tabla `Usuarios` en DB; `Seed.SeedUsuarioAdminAsync(AppDbContext)` disponible para Program.cs (ya añadido en Task 1).

- [ ] **Step 1: Crear Models/Usuario.cs**

```csharp
using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Rol { get; set; } = string.Empty;  // OPE | MKT | SV | AMG | ADM

    public bool Activo { get; set; } = true;
}
```

- [ ] **Step 2: Actualizar AppDbContext.cs**

Agrega `DbSet<Usuario>`:

```csharp
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item>     Items     => Set<Item>();
    public DbSet<Fdo>      Fdos      => Set<Fdo>();
    public DbSet<FdoLinea> FdoLineas => Set<FdoLinea>();
    public DbSet<Usuario>  Usuarios  => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
```

- [ ] **Step 3: Agregar SeedUsuarioAdminAsync a Seed.cs**

Al final de `Seed.cs`, dentro de la clase `Seed`, agrega:

```csharp
public static async Task SeedUsuarioAdminAsync(AppDbContext db)
{
    if (await db.Usuarios.AnyAsync()) return;

    db.Usuarios.Add(new Usuario
    {
        Username     = "admin",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
        Rol          = "ADM",
        Activo       = true
    });
    await db.SaveChangesAsync();
}
```

Agrega el using al inicio del archivo Seed.cs:
```csharp
using BCrypt.Net;
```

- [ ] **Step 4: Generar migración**

```bash
export PATH="$PATH:/Users/jd/.dotnet/tools"
cd /Users/jd/Desktop/PalletBalancer
dotnet ef migrations add AgregarUsuarios \
  --project src/PalletBalancer.Api \
  --startup-project src/PalletBalancer.Api
```

Esperado: tres archivos nuevos en `Migrations/`.

- [ ] **Step 5: Verificar build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

- [ ] **Step 6: Commit**

```bash
git add src/PalletBalancer.Api/Models/Usuario.cs \
        src/PalletBalancer.Api/Data/AppDbContext.cs \
        src/PalletBalancer.Api/Data/Seed.cs \
        src/PalletBalancer.Api/Migrations/
git commit -m "feat: modelo Usuario, migración y seed admin"
```

---

### Task 3: AuthController — login con JWT

**Files:**
- Create: `src/PalletBalancer.Api/DTOs/AuthDto.cs`
- Create: `src/PalletBalancer.Api/Controllers/AuthController.cs`

**Interfaces:**
- Consumes: `AppDbContext.Usuarios`, configuración `Jwt:*` de appsettings, `BCrypt.Net.BCrypt.Verify()`
- Produces: `POST /api/auth/login` → `{ token, username, rol }`

- [ ] **Step 1: Crear DTOs/AuthDto.cs**

```csharp
namespace PalletBalancer.Api.DTOs;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token    { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Rol      { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Crear Controllers/AuthController.cs**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext  _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        var key     = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        var expira  = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:ExpiresHours", 8));

        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer:   _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims:
                [
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name,           usuario.Username),
                    new Claim(ClaimTypes.Role,           usuario.Rol)
                ],
                expires:            expira,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256)
            ));

        return Ok(new LoginResponse
        {
            Token    = token,
            Username = usuario.Username,
            Rol      = usuario.Rol
        });
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

- [ ] **Step 4: Probar login localmente via Swagger**

Ejecuta el proyecto localmente con la connection string local (o comenta la excepción de Jwt:Key temporalmente con un valor hardcoded). Abre `https://localhost:5001/swagger`, prueba `POST /api/auth/login` con:
```json
{ "username": "admin", "password": "Admin1234!" }
```
Esperado: `200 OK` con `{ "token": "eyJ...", "username": "admin", "rol": "ADM" }`.

> **Nota Railway:** Antes de deploy, agrega en Railway la variable de entorno `JWT__Key` con un string de mínimo 32 caracteres, por ejemplo: `PalletBalancer2026SecretKeyXYZ!!`

- [ ] **Step 5: Commit**

```bash
git add src/PalletBalancer.Api/DTOs/AuthDto.cs \
        src/PalletBalancer.Api/Controllers/AuthController.cs
git commit -m "feat: AuthController con login JWT"
```

---

### Task 4: PDF parser + endpoint /api/fdos/importar

**Files:**
- Create: `src/PalletBalancer.Api/DTOs/FdoImportadoDto.cs`
- Create: `src/PalletBalancer.Api/Services/PdfFdoParser.cs`
- Modify: `src/PalletBalancer.Api/Controllers/FdosController.cs`
- Modify: `tests/PalletBalancer.Core.Tests/PalletBalancer.Core.Tests.csproj` (agregar ref a Api)
- Create: `tests/PalletBalancer.Core.Tests/PdfFdoParserTests.cs`

**Interfaces:**
- Consumes: `UglyToad.PdfPig`, `FdoLineaDto` (ya existe en DTOs/FdoDto.cs)
- Produces: `PdfFdoParser.ParsearLineas(IReadOnlyList<string>)` → `FdoImportadoDto`; `POST /api/fdos/importar` → `FdoImportadoDto`

- [ ] **Step 1: Crear DTOs/FdoImportadoDto.cs**

`FdoImportadoDto` es idéntica a `FdoDto` — reutilízala como alias para dejar claro el flujo (importar devuelve sin guardar, confirmar guarda con FdoDto). Crea el archivo:

```csharp
namespace PalletBalancer.Api.DTOs;

// Resultado del parser PDF — mismos campos que FdoDto, sin guardar en DB.
// El frontend permite editar antes de confirmar con POST /api/fdos.
public class FdoImportadoDto : FdoDto { }
```

- [ ] **Step 2: Crear Services/PdfFdoParser.cs**

```csharp
using UglyToad.PdfPig;
using PalletBalancer.Api.DTOs;

namespace PalletBalancer.Api.Services;

public static class PdfFdoParser
{
    public static FdoImportadoDto Parsear(Stream pdfStream)
    {
        var lineas = ExtraerLineas(pdfStream);
        return ParsearLineas(lineas);
    }

    // Internal parsing — public for testability
    public static FdoImportadoDto ParsearLineas(IReadOnlyList<string> lineas)
    {
        var dto = new FdoImportadoDto();

        foreach (var linea in lineas)
        {
            if (TryExtraerValor(linea, "FDO Slip No",       out var v)) dto.FdoSlipNo = v;
            else if (TryExtraerValor(linea, "Disbursement Date", out v)) dto.DsbDate   = NormalizarFecha(v);
            else if (TryExtraerValor(linea, "Ship Date",         out v)) dto.ShipDate  = NormalizarFecha(v);
            else if (TryExtraerValor(linea, "Customer",          out v)) dto.Customer  = v;
            else if (TryExtraerValor(linea, "Consignee",         out v)) dto.Consignee = v;
        }

        dto.Lineas = ParsearLineasProducto(lineas);
        return dto;
    }

    private static List<string> ExtraerLineas(Stream pdfStream)
    {
        using var pdf   = PdfDocument.Open(pdfStream);
        var       todas = new List<string>();

        foreach (var pagina in pdf.GetPages())
        {
            var porFila = pagina.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ",
                    g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            todas.AddRange(porFila);
        }
        return todas;
    }

    private static bool TryExtraerValor(string linea, string etiqueta, out string valor)
    {
        var idx = linea.IndexOf(etiqueta, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { valor = ""; return false; }
        valor = linea[(idx + etiqueta.Length)..].TrimStart(':', ' ').Trim();
        return !string.IsNullOrWhiteSpace(valor);
    }

    private static string NormalizarFecha(string raw)
    {
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }

    private static List<FdoLineaDto> ParsearLineasProducto(IReadOnlyList<string> lineas)
    {
        var resultado = new List<FdoLineaDto>();

        foreach (var linea in lineas)
        {
            var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Línea de producto: mínimo 3 tokens, segundo token parece código de modelo
            // (empieza con letra, ≥8 chars), último token es número entero
            if (partes.Length >= 3
                && partes[1].Length >= 6
                && char.IsLetter(partes[1][0])
                && int.TryParse(partes[^1], out var qty)
                && qty > 0)
            {
                resultado.Add(new FdoLineaDto
                {
                    CustomerPoNo = partes[0],
                    ModelNo      = partes[1],
                    ReqQty       = qty
                });
            }
        }
        return resultado;
    }
}
```

> **Nota importante:** Este parser usa heurísticas basadas en el formato esperado del PDF de MD Logis. Después de probar con el PDF real, puede necesitar ajustar las etiquetas exactas (ej. `"FDO Slip No"` vs `"FDO SLIP NO"`) y el patrón de líneas de producto. La pantalla de confirmación es el safety net — el usuario corrige lo que el parser no leyó bien.

- [ ] **Step 3: Escribir el test que falla**

Primero agrega referencia a PalletBalancer.Api en el proyecto de tests. Abre `tests/PalletBalancer.Core.Tests/PalletBalancer.Core.Tests.csproj` y agrega dentro de un `<ItemGroup>`:

```xml
<ProjectReference Include="..\..\src\PalletBalancer.Api\PalletBalancer.Api.csproj" />
```

Luego crea `tests/PalletBalancer.Core.Tests/PdfFdoParserTests.cs`:

```csharp
using PalletBalancer.Api.Services;
using Xunit;

namespace PalletBalancer.Core.Tests;

public class PdfFdoParserTests
{
    [Fact]
    public void ParsearLineas_ExtraeCamposDeEncabezado()
    {
        var lineas = new[]
        {
            "FDO Slip No: 2612481",
            "Disbursement Date: 2026-08-03",
            "Ship Date: 2026-08-15",
            "Customer: MD LOGIS SA",
            "Consignee: MITSUBISHI MOTORS"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal("2612481",          dto.FdoSlipNo);
        Assert.Equal("2026-08-03",       dto.DsbDate);
        Assert.Equal("2026-08-15",       dto.ShipDate);
        Assert.Equal("MD LOGIS SA",      dto.Customer);
        Assert.Equal("MITSUBISHI MOTORS", dto.Consignee);
    }

    [Fact]
    public void ParsearLineas_ExtraeLineasProducto()
    {
        var lineas = new[]
        {
            "PO-001 K006T91071XB 120",
            "PO-002 K006T91072XB 240"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal(2, dto.Lineas.Count);
        Assert.Equal("PO-001",        dto.Lineas[0].CustomerPoNo);
        Assert.Equal("K006T91071XB",  dto.Lineas[0].ModelNo);
        Assert.Equal(120,             dto.Lineas[0].ReqQty);
    }

    [Fact]
    public void ParsearLineas_CamposAusentes_RetornaStringVacio()
    {
        var lineas = new[] { "Texto irrelevante sin campos conocidos" };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal("", dto.FdoSlipNo);
        Assert.Empty(dto.Lineas);
    }
}
```

- [ ] **Step 4: Ejecutar test para verificar que falla**

```bash
dotnet test tests/PalletBalancer.Core.Tests/
```

Esperado: FAIL — `PdfFdoParser` no existe aún (creado en Step 2 pero el test se ejecuta antes de compilar correctamente si el parser tiene errores).

- [ ] **Step 5: Ejecutar test para verificar que pasa**

```bash
dotnet build tests/PalletBalancer.Core.Tests/
dotnet test tests/PalletBalancer.Core.Tests/ --filter "PdfFdoParserTests"
```

Esperado: 3 tests PASS.

- [ ] **Step 6: Agregar endpoint a FdosController.cs**

Agrega el using al inicio del archivo:
```csharp
using Microsoft.AspNetCore.Authorization;
using PalletBalancer.Api.Services;
```

Agrega este método dentro de la clase `FdosController`:

```csharp
[HttpPost("importar")]
[Authorize]
public async Task<IActionResult> Importar(IFormFile archivo)
{
    if (archivo is null || archivo.Length == 0)
        return BadRequest(new { mensaje = "Se requiere un archivo PDF." });

    using var stream = archivo.OpenReadStream();
    var dto = PdfFdoParser.Parsear(stream);
    return Ok(dto);
}
```

- [ ] **Step 7: Build y verificar**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

- [ ] **Step 8: Commit**

```bash
git add src/PalletBalancer.Api/DTOs/FdoImportadoDto.cs \
        src/PalletBalancer.Api/Services/PdfFdoParser.cs \
        src/PalletBalancer.Api/Controllers/FdosController.cs \
        tests/PalletBalancer.Core.Tests/
git commit -m "feat: PDF parser y endpoint POST /api/fdos/importar"
```

---

### Task 5: PATCH /api/fdos/{id}/lineas/{lineaId} — editar cantidades (AMG/ADM)

**Files:**
- Create: `src/PalletBalancer.Api/DTOs/PatchCantidadDto.cs`
- Modify: `src/PalletBalancer.Api/Controllers/FdosController.cs`

**Interfaces:**
- Consumes: `AppDbContext.FdoLineas`, JWT con rol AMG o ADM
- Produces: `PATCH /api/fdos/{id}/lineas/{lineaId}` → `FdoLinea` actualizada

- [ ] **Step 1: Crear DTOs/PatchCantidadDto.cs**

```csharp
namespace PalletBalancer.Api.DTOs;

public class PatchCantidadDto
{
    public int ReqQty { get; set; }
}
```

- [ ] **Step 2: Agregar endpoint PATCH a FdosController.cs**

Agrega dentro de la clase `FdosController`:

```csharp
[HttpPatch("{id:int}/lineas/{lineaId:int}")]
[Authorize(Roles = "AMG,ADM")]
public async Task<IActionResult> PatchCantidad(int id, int lineaId, PatchCantidadDto dto)
{
    var linea = await _db.FdoLineas
        .FirstOrDefaultAsync(l => l.Id == lineaId && l.FdoId == id);

    if (linea is null) return NotFound();

    linea.ReqQty = dto.ReqQty;
    await _db.SaveChangesAsync();
    return Ok(linea);
}
```

- [ ] **Step 3: Verificar que FdosController.cs tiene los usings necesarios**

Asegúrate de que al inicio del archivo estén:
```csharp
using Microsoft.AspNetCore.Authorization;
```
(ya se agregó en Task 4, Step 6)

- [ ] **Step 4: Build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

- [ ] **Step 5: Commit**

```bash
git add src/PalletBalancer.Api/DTOs/PatchCantidadDto.cs \
        src/PalletBalancer.Api/Controllers/FdosController.cs
git commit -m "feat: PATCH /api/fdos/{id}/lineas/{lineaId} para AMG y ADM"
```

---

### Task 6: index.html — página de login

**Files:**
- Create: `src/PalletBalancer.Api/wwwroot/index.html`

**Interfaces:**
- Consumes: `POST /api/auth/login` → guarda `pb_token` y `pb_usuario` en localStorage
- Produces: página de login que redirige a `app.html` tras autenticación exitosa

- [ ] **Step 1: Crear carpeta wwwroot**

```bash
mkdir -p src/PalletBalancer.Api/wwwroot
```

- [ ] **Step 2: Crear wwwroot/index.html**

```html
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PalletBalancer — Acceso</title>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
</head>
<body class="bg-light d-flex align-items-center" style="min-height:100vh">

<div class="container" x-data="loginApp()" x-init="init()">
  <div class="row justify-content-center">
    <div class="col-md-4 col-sm-8">
      <div class="card shadow-sm">
        <div class="card-body p-4">
          <h4 class="card-title text-center mb-1">PalletBalancer</h4>
          <p class="text-center text-muted small mb-4">MD Logis</p>

          <div x-show="error" class="alert alert-danger py-2" x-text="error"></div>

          <form @submit.prevent="login()">
            <div class="mb-3">
              <label class="form-label">Usuario</label>
              <input type="text" class="form-control" x-model="username" required autofocus>
            </div>
            <div class="mb-3">
              <label class="form-label">Contraseña</label>
              <input type="password" class="form-control" x-model="password" required>
            </div>
            <button type="submit" class="btn btn-primary w-100" :disabled="cargando">
              <span x-show="cargando" class="spinner-border spinner-border-sm me-1"></span>
              <span x-text="cargando ? 'Verificando...' : 'Ingresar'"></span>
            </button>
          </form>
        </div>
      </div>
    </div>
  </div>
</div>

<script src="https://cdn.jsdelivr.net/npm/alpinejs@3.14.1/dist/cdn.min.js" defer></script>
<script>
function loginApp() {
  return {
    username: '',
    password: '',
    error:    '',
    cargando: false,

    init() {
      if (localStorage.getItem('pb_token')) {
        window.location.href = 'app.html';
      }
    },

    async login() {
      this.cargando = true;
      this.error    = '';
      try {
        const r = await fetch('/api/auth/login', {
          method:  'POST',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ username: this.username, password: this.password })
        });
        if (!r.ok) {
          this.error = 'Usuario o contraseña incorrectos.';
          return;
        }
        const data = await r.json();
        localStorage.setItem('pb_token',   data.token);
        localStorage.setItem('pb_usuario', JSON.stringify({ username: data.username, rol: data.rol }));
        window.location.href = 'app.html';
      } catch {
        this.error = 'Error de conexión con el servidor.';
      } finally {
        this.cargando = false;
      }
    }
  };
}
</script>
</body>
</html>
```

- [ ] **Step 3: Build y verificar que wwwroot se incluye en publish**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

Los archivos en `wwwroot/` se copian automáticamente al publish porque ASP.NET Core Web SDK los incluye por default.

- [ ] **Step 4: Commit**

```bash
git add src/PalletBalancer.Api/wwwroot/index.html
git commit -m "feat: index.html — página de login con Alpine.js"
```

---

### Task 7: app.html — aplicación principal (lista, detalle, importar)

**Files:**
- Create: `src/PalletBalancer.Api/wwwroot/app.html`

**Interfaces:**
- Consumes: `GET /api/fdos`, `GET /api/fdos/{id}`, `POST /api/fdos/importar`, `POST /api/fdos`, `PATCH /api/fdos/{id}/lineas/{lineaId}`; `pb_token` y `pb_usuario` desde localStorage
- Produces: SPA completa con tres vistas: lista, detalle, importar

- [ ] **Step 1: Crear wwwroot/app.html**

```html
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>PalletBalancer</title>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">
  <style>[x-cloak] { display: none !important; }</style>
</head>
<body x-data="app()" x-init="init()">

<!-- NAVBAR -->
<nav class="navbar navbar-dark bg-dark px-3 mb-4">
  <span class="navbar-brand fw-bold">PalletBalancer</span>
  <div class="d-flex gap-2 align-items-center flex-wrap">
    <button class="btn btn-sm btn-outline-light" @click="irALista()">FDOs</button>
    <button class="btn btn-sm btn-outline-light" @click="vista='importar'; importado=null">
      Importar PDF
    </button>
    <span class="badge bg-secondary ms-2" x-text="usuario?.rol"></span>
    <span class="text-light small" x-text="usuario?.username"></span>
    <button class="btn btn-sm btn-outline-danger" @click="salir()">Salir</button>
  </div>
</nav>

<div class="container">

  <!-- ALERTA GLOBAL -->
  <div x-show="alerta" x-cloak class="alert alert-danger alert-dismissible mb-3" role="alert">
    <span x-text="alerta"></span>
    <button type="button" class="btn-close" @click="alerta=''"></button>
  </div>

  <!-- ============================================================ -->
  <!-- VISTA: LISTA                                                  -->
  <!-- ============================================================ -->
  <div x-show="vista==='lista'">
    <div class="d-flex justify-content-between align-items-center mb-3">
      <h5 class="mb-0">FDOs registrados</h5>
      <button class="btn btn-primary btn-sm" @click="vista='importar'; importado=null">
        + Importar PDF
      </button>
    </div>

    <div x-show="cargandoLista" class="text-muted mb-3">
      <span class="spinner-border spinner-border-sm"></span> Cargando...
    </div>

    <table class="table table-hover table-bordered" x-show="!cargandoLista">
      <thead class="table-dark">
        <tr>
          <th>FDO Slip No</th>
          <th>Cliente</th>
          <th>Consignatario</th>
          <th>Fecha Embarque</th>
        </tr>
      </thead>
      <tbody>
        <template x-for="f in fdos" :key="f.id">
          <tr style="cursor:pointer" @click="verDetalle(f.id)">
            <td x-text="f.fdoSlipNo"></td>
            <td x-text="f.customer"></td>
            <td x-text="f.consignee"></td>
            <td x-text="f.shipDate"></td>
          </tr>
        </template>
        <tr x-show="fdos.length === 0 && !cargandoLista">
          <td colspan="4" class="text-center text-muted py-3">
            Sin FDOs registrados. Importa el primero con el botón de arriba.
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- ============================================================ -->
  <!-- VISTA: DETALLE                                                -->
  <!-- ============================================================ -->
  <div x-show="vista==='detalle'" x-cloak>
    <button class="btn btn-secondary btn-sm mb-3" @click="irALista()">← Volver</button>

    <template x-if="fdoActual">
      <div>
        <!-- Encabezado FDO -->
        <div class="card mb-3">
          <div class="card-body">
            <div class="row g-2">
              <div class="col-6 col-md-3">
                <small class="text-muted d-block">FDO Slip No</small>
                <strong x-text="fdoActual.fdoSlipNo"></strong>
              </div>
              <div class="col-6 col-md-3">
                <small class="text-muted d-block">DSB Date</small>
                <span x-text="fdoActual.dsbDate"></span>
              </div>
              <div class="col-6 col-md-3">
                <small class="text-muted d-block">Ship Date</small>
                <span x-text="fdoActual.shipDate"></span>
              </div>
              <div class="col-6 col-md-3">
                <small class="text-muted d-block">Cliente</small>
                <span x-text="fdoActual.customer"></span>
              </div>
              <div class="col-6 col-md-3 mt-2">
                <small class="text-muted d-block">Consignatario</small>
                <span x-text="fdoActual.consignee"></span>
              </div>
            </div>
          </div>
        </div>

        <!-- Líneas -->
        <table class="table table-bordered table-sm">
          <thead class="table-secondary">
            <tr>
              <th>Customer PO No</th>
              <th>Model No</th>
              <th>Req Qty</th>
              <th x-show="puedeEditar()">Acción</th>
            </tr>
          </thead>
          <tbody>
            <template x-for="l in fdoActual.lineas" :key="l.id">
              <tr>
                <td x-text="l.customerPoNo"></td>
                <td x-text="l.modelNo"></td>
                <td>
                  <span x-show="editandoLinea !== l.id" x-text="l.reqQty"></span>
                  <input x-show="editandoLinea === l.id"
                         x-cloak
                         type="number" min="1"
                         class="form-control form-control-sm d-inline-block"
                         style="width:90px"
                         x-model.number="editQty">
                </td>
                <td x-show="puedeEditar()">
                  <button x-show="editandoLinea !== l.id"
                          class="btn btn-outline-primary btn-sm"
                          @click="editandoLinea = l.id; editQty = l.reqQty">
                    Editar
                  </button>
                  <template x-if="editandoLinea === l.id">
                    <span>
                      <button class="btn btn-success btn-sm" @click="guardarCantidad(l)">
                        Guardar
                      </button>
                      <button class="btn btn-secondary btn-sm ms-1"
                              @click="editandoLinea = null">
                        Cancelar
                      </button>
                    </span>
                  </template>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </template>
  </div>

  <!-- ============================================================ -->
  <!-- VISTA: IMPORTAR                                               -->
  <!-- ============================================================ -->
  <div x-show="vista==='importar'" x-cloak>
    <h5 class="mb-3">Importar FDO desde PDF</h5>

    <!-- Paso 1: seleccionar archivo -->
    <div x-show="!importado">
      <div class="mb-3" style="max-width:400px">
        <label class="form-label">Selecciona el PDF del FDO</label>
        <input type="file" class="form-control" accept=".pdf"
               @change="importarPdf($event)" :disabled="cargandoImport"
               x-ref="inputPdf">
      </div>
      <div x-show="cargandoImport" class="text-muted mb-3">
        <span class="spinner-border spinner-border-sm"></span> Procesando PDF...
      </div>
      <button class="btn btn-secondary btn-sm" @click="irALista()">Cancelar</button>
    </div>

    <!-- Paso 2: confirmar datos parseados -->
    <div x-show="importado" x-cloak>
      <div class="alert alert-info py-2 mb-3">
        Revisa y corrige los datos antes de guardar. Los campos en blanco no se pudieron leer del PDF.
      </div>

      <div class="row g-3 mb-3">
        <div class="col-6 col-md-3">
          <label class="form-label">FDO Slip No</label>
          <input class="form-control" x-model="importado.fdoSlipNo" required>
        </div>
        <div class="col-6 col-md-3">
          <label class="form-label">DSB Date</label>
          <input class="form-control" type="date" x-model="importado.dsbDate" required>
        </div>
        <div class="col-6 col-md-3">
          <label class="form-label">Ship Date</label>
          <input class="form-control" type="date" x-model="importado.shipDate" required>
        </div>
        <div class="col-6 col-md-3">
          <label class="form-label">Cliente</label>
          <input class="form-control" x-model="importado.customer">
        </div>
        <div class="col-6 col-md-3">
          <label class="form-label">Consignatario</label>
          <input class="form-control" x-model="importado.consignee">
        </div>
      </div>

      <table class="table table-bordered table-sm mb-3">
        <thead class="table-secondary">
          <tr><th>Customer PO No</th><th>Model No</th><th>Req Qty</th></tr>
        </thead>
        <tbody>
          <template x-for="(l, i) in importado.lineas" :key="i">
            <tr>
              <td><input class="form-control form-control-sm" x-model="l.customerPoNo"></td>
              <td><input class="form-control form-control-sm" x-model="l.modelNo"></td>
              <td><input class="form-control form-control-sm" type="number" min="1"
                         x-model.number="l.reqQty"></td>
            </tr>
          </template>
          <tr x-show="importado.lineas.length === 0">
            <td colspan="3" class="text-muted text-center">
              No se detectaron líneas de producto. Agrégalas manualmente.
            </td>
          </tr>
        </tbody>
      </table>

      <div class="d-flex gap-2">
        <button class="btn btn-success" @click="confirmarImport()" :disabled="guardando">
          <span x-show="guardando" class="spinner-border spinner-border-sm me-1"></span>
          Confirmar y Guardar
        </button>
        <button class="btn btn-outline-secondary" @click="importado = null; $refs.inputPdf.value = ''">
          Volver a seleccionar
        </button>
        <button class="btn btn-secondary" @click="irALista()">Cancelar</button>
      </div>
    </div>
  </div>

</div><!-- /container -->

<script src="https://cdn.jsdelivr.net/npm/alpinejs@3.14.1/dist/cdn.min.js" defer></script>
<script>
function app() {
  return {
    vista:          'lista',
    usuario:        null,
    fdos:           [],
    fdoActual:      null,
    importado:      null,
    alerta:         '',
    cargandoLista:  false,
    cargandoImport: false,
    guardando:      false,
    editandoLinea:  null,
    editQty:        0,

    init() {
      const token = localStorage.getItem('pb_token');
      if (!token) { window.location.href = 'index.html'; return; }

      // Verificar expiración del token (campo "exp" en payload JWT)
      try {
        const payload = JSON.parse(atob(token.split('.')[1]));
        if (payload.exp * 1000 < Date.now()) { this.salir(); return; }
      } catch {
        this.salir(); return;
      }

      this.usuario = JSON.parse(localStorage.getItem('pb_usuario') || '{}');
      this.cargarFdos();
    },

    token() { return localStorage.getItem('pb_token'); },

    // Helper para fetch con Authorization header; redirige a login si 401
    async api(url, opts = {}) {
      const headers = {
        'Authorization': 'Bearer ' + this.token(),
        ...(opts.headers || {})
      };
      const r = await fetch(url, { ...opts, headers });
      if (r.status === 401) { this.salir(); return null; }
      return r;
    },

    async cargarFdos() {
      this.cargandoLista = true;
      const r = await this.api('/api/fdos');
      this.cargandoLista = false;
      if (!r || !r.ok) { this.alerta = 'Error al cargar FDOs.'; return; }
      this.fdos = await r.json();
    },

    irALista() {
      this.vista     = 'lista';
      this.importado = null;
      this.alerta    = '';
      this.cargarFdos();
    },

    async verDetalle(id) {
      const r = await this.api(`/api/fdos/${id}`);
      if (!r || !r.ok) { this.alerta = 'Error al cargar el FDO.'; return; }
      this.fdoActual    = await r.json();
      this.editandoLinea = null;
      this.vista         = 'detalle';
    },

    puedeEditar() {
      return this.usuario?.rol === 'AMG' || this.usuario?.rol === 'ADM';
    },

    async guardarCantidad(linea) {
      const r = await this.api(
        `/api/fdos/${this.fdoActual.id}/lineas/${linea.id}`,
        {
          method:  'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body:    JSON.stringify({ reqQty: this.editQty })
        }
      );
      if (!r || !r.ok) { this.alerta = 'Error al guardar la cantidad.'; return; }
      linea.reqQty       = this.editQty;
      this.editandoLinea = null;
    },

    async importarPdf(event) {
      const archivo = event.target.files[0];
      if (!archivo) return;
      this.cargandoImport = true;
      this.alerta         = '';

      const form = new FormData();
      form.append('archivo', archivo);

      const r = await this.api('/api/fdos/importar', { method: 'POST', body: form });
      this.cargandoImport = false;

      if (!r || !r.ok) {
        this.alerta = 'No se pudo procesar el PDF. Verifica que sea un FDO válido.';
        return;
      }
      this.importado = await r.json();
    },

    async confirmarImport() {
      this.guardando = true;
      this.alerta    = '';

      const r = await this.api('/api/fdos', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body:    JSON.stringify(this.importado)
      });
      this.guardando = false;

      if (!r || !r.ok) {
        const txt = await r?.text().catch(() => '');
        this.alerta = txt || 'Error al guardar el FDO.';
        return;
      }
      this.importado = null;
      this.irALista();
    },

    salir() {
      localStorage.removeItem('pb_token');
      localStorage.removeItem('pb_usuario');
      window.location.href = 'index.html';
    }
  };
}
</script>
</body>
</html>
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PalletBalancer.Api/PalletBalancer.Api.csproj
```

- [ ] **Step 3: Commit**

```bash
git add src/PalletBalancer.Api/wwwroot/app.html
git commit -m "feat: app.html — SPA con lista, detalle e importar FDO"
```

---

### Task 8: Deploy a Railway + configurar JWT__Key

**Files:** ninguno — configuración en Railway dashboard

**Interfaces:**
- Consumes: commits de Tasks 1-7 en `main`
- Produces: app live en `https://palletbalancer-production.up.railway.app`

- [ ] **Step 1: Push a GitHub**

```bash
git remote set-url origin https://TU_TOKEN@github.com/juandediossaldivar5-glitch/PalletBalancer.git
git push origin main
git remote set-url origin https://github.com/juandediossaldivar5-glitch/PalletBalancer.git
```

- [ ] **Step 2: Agregar variable JWT__Key en Railway**

En Railway → tu proyecto → pestaña **Variables**, agrega:
```
JWT__Key = PalletBalancer2026SecretKeyXYZ!!
```
(mínimo 32 caracteres, cualquier string aleatorio seguro)

- [ ] **Step 3: Esperar deploy y verificar**

Railway redesplegará automáticamente al detectar el push.

1. `https://palletbalancer-production.up.railway.app/` → debe mostrar página de login
2. Ingresar con `admin` / `Admin1234!` → debe redirigir a `app.html`
3. Lista de FDOs carga vacía → normal
4. `https://palletbalancer-production.up.railway.app/swagger` → debe mostrar nuevos endpoints: `/api/auth/login`, `/api/fdos/importar`, `/api/fdos/{id}/lineas/{lineaId}`

---

## Self-Review

**Spec coverage:**
- ✓ Static files en wwwroot / Alpine.js + Bootstrap — Task 1, 6, 7
- ✓ Tabla Usuarios + bcrypt + seed ADM — Task 2
- ✓ JWT 8h con rol en claim — Task 3
- ✓ POST /api/auth/login — Task 3
- ✓ POST /api/fdos/importar con PdfPig — Task 4
- ✓ FdoImportadoDto retornado sin guardar — Task 4
- ✓ Confirmación editable antes de guardar — Task 7
- ✓ PATCH /api/fdos/{id}/lineas/{lineaId} solo AMG/ADM — Task 5
- ✓ index.html login con redirección — Task 6
- ✓ app.html: navbar, lista, detalle, importar — Task 7
- ✓ puedeEditar() solo AMG/ADM en frontend — Task 7
- ✓ JWT expiry check en frontend — Task 7
- ✓ Deploy Railway con JWT__Key env var — Task 8

**Sin placeholders:** todas las clases, métodos y HTML están escritos completos.

**Consistencia de tipos:** `FdoImportadoDto extends FdoDto` — `POST /api/fdos` acepta `FdoDto`, el frontend envía `importado` que es `FdoImportadoDto` (compatible). `FdoLineaDto` usada consistentemente en parser y en el DTO base.
