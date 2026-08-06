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
