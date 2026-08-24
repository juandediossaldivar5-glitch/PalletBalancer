using System.Text;
using System.Text.Json.Serialization;
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

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var rawConnection = Environment.GetEnvironmentVariable("DATABASE_URL")
                 ?? Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL")
                 ?? Environment.GetEnvironmentVariable("POSTGRES_URL")
                 ?? builder.Configuration.GetConnectionString("Default")
                 ?? "";

// Normalizar: quitar espacios y comillas accidentales del env var
rawConnection = rawConnection.Trim().Trim('"', '\'');

string connectionString;
try
{
    connectionString = rawConnection.StartsWith("postgresql://") || rawConnection.StartsWith("postgres://")
        ? ConvertirUrlAConexion(rawConnection)
        : rawConnection;
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Error parseando DATABASE_URL: {ex.Message}");
    Console.WriteLine($"⚠ Longitud={rawConnection.Length}, prefijo='{(rawConnection.Length > 20 ? rawConnection[..20] : rawConnection)}'");
    connectionString = rawConnection;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// JWT
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key no configurado. Configura la variable JWT__Key en Railway.");
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

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "v5" }));

app.MapGet("/debug/env", () =>
{
    var db  = Environment.GetEnvironmentVariable("DATABASE_URL");
    var dbp = Environment.GetEnvironmentVariable("DATABASE_PRIVATE_URL");
    var pg  = Environment.GetEnvironmentVariable("POSTGRES_URL");
    static string Preview(string? v) =>
        string.IsNullOrEmpty(v) ? "(no set)"
        : $"len={v.Length}, prefijo='{(v.Length > 20 ? v[..20] : v)}...'";
    return Results.Ok(new
    {
        DATABASE_URL         = Preview(db),
        DATABASE_PRIVATE_URL = Preview(dbp),
        POSTGRES_URL         = Preview(pg),
    });
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
