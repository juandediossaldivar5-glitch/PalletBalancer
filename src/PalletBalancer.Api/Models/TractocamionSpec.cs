namespace PalletBalancer.Api.Models;

public record TractocamionSpec(
    string Tipo,
    int    WheelbaseCm,            // eje delantero → eje trasero del tractor
    int    QuintaRuedaCm,          // distancia quinta rueda ADELANTE del eje trasero
    double TaraEjeDelanteroKg,     // peso en vacío sobre eje delantero
    double TaraEjeTraseroKg);      // peso en vacío sobre eje trasero (tándem)

public static class TractocamionSpecs
{
    public static readonly Dictionary<string, TractocamionSpec> Todos =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["T3-S2 Estándar"]     = new("T3-S2 Estándar",     400, 91, 4_200, 4_300),
        ["T3-S2 Cabina Larga"] = new("T3-S2 Cabina Larga", 450, 102, 4_400, 4_500),
        ["T3-S2 Day Cab"]      = new("T3-S2 Day Cab",      360, 76, 4_000, 4_100),
    };

    public static TractocamionSpec Default => Todos["T3-S2 Estándar"];

    public static TractocamionSpec Get(string? tipo) =>
        tipo != null && Todos.TryGetValue(tipo, out var s) ? s : Default;
}
