namespace PalletBalancer.Api.Models;

/// <summary>
/// Pesos de tara por eje con rango de incertidumbre operativa (RF-08).
/// Min = tanque vacío, sin conductor.
/// Max = tanque lleno (~300 L diesel × 0.85 kg/L ≈ 255 kg) + conductor pesado (~120 kg) + DEF lleno (~40 kg).
/// </summary>
public record TractocamionSpec(
    string Tipo,
    int    WheelbaseCm,               // eje delantero → eje trasero del tractor
    int    QuintaRuedaCm,             // distancia quinta rueda ADELANTE del eje trasero
    double TaraEjeDelanteroMinKg,     // W1 mínimo — tanque vacío, sin conductor
    double TaraEjeDelanteroMaxKg,     // W1 máximo — tanque lleno, conductor pesado
    double TaraEjeTraseroMinKg,       // W2 mínimo — tanque vacío, DEF vacío
    double TaraEjeTraseroMaxKg);      // W2 máximo — tanque lleno, DEF lleno

public static class TractocamionSpecs
{
    // ΔW1 ≈ +120 kg (conductor 120 kg — todo en eje delantero / cabina)
    // ΔW2 ≈ +300 kg (tanque lleno 255 kg + DEF 44 kg — en bastidor, sobre eje trasero)
    public static readonly Dictionary<string, TractocamionSpec> Todos =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["T3-S2 Estándar"]     = new("T3-S2 Estándar",     400,  91, 4_200, 4_320, 4_300, 4_600),
        ["T3-S2 Cabina Larga"] = new("T3-S2 Cabina Larga", 450, 102, 4_400, 4_520, 4_500, 4_850),
        ["T3-S2 Day Cab"]      = new("T3-S2 Day Cab",      360,  76, 4_000, 4_120, 4_100, 4_400),
        // Truck americano típico para drayage en frontera Laredo/El Paso (Class 8 Day Cab, 6×4)
        // WB 228" (579 cm), 5th wheel ~42" del tándem. Tara liviana ~7,500-8,200 kg.
        ["US Class 8 Day Cab"] = new("US Class 8 Day Cab", 579, 107, 2_800, 2_950, 4_700, 5_250),
    };

    public static TractocamionSpec Default => Todos["T3-S2 Estándar"];

    public static TractocamionSpec Get(string? tipo) =>
        tipo != null && Todos.TryGetValue(tipo, out var s) ? s : Default;
}
