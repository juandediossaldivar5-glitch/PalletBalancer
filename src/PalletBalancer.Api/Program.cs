using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Railway inyecta PORT; ASP.NET Core lo lee de ASPNETCORE_HTTP_PORTS
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

// Railway entrega la URL como postgresql://user:pass@host:port/db
// Npgsql necesita formato Host=...;Username=...
var connectionString = rawConnection.StartsWith("postgresql://") || rawConnection.StartsWith("postgres://")
    ? ConvertirUrlAConexion(rawConnection)
    : rawConnection;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "v4" }));

// Muestra qué prefijo tiene la connection string (sin exponer credenciales)
app.MapGet("/debug/env", () =>
{
    var raw = Environment.GetEnvironmentVariable("DATABASE_URL") ?? "(no DATABASE_URL)";
    var preview = raw.Length > 30 ? raw[..30] + "..." : raw;
    return Results.Ok(new { DATABASE_URL_preview = preview, longitud = raw.Length });
});

// Endpoint temporal de diagnóstico — muestra error de DB si hay uno
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

// Migración y seed en background para no bloquear el arranque
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // espera a que el server esté escuchando
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await db.Database.MigrateAsync();
        var rutaJson = Path.Combine(AppContext.BaseDirectory, "catalogo_items.json");
        await Seed.CargarItemsDesdeJson(db, rutaJson);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error en migración: {ex.Message}");
    }
});

app.Run();
