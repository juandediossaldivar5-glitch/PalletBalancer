namespace PalletBalancer.Api.Models;

public record ContenedorSpec(
    string Tipo,
    int    LargoCm,
    int    AnchoCm,
    int    AltoCm,
    int    KingPinOffsetCm,    // distancia frente del contenedor → king pin
    int    KingPinAEjeCm,      // distancia king pin → eje del remolque
    double TaraChassisKg,      // tara del chassis/semirremolque
    double TaraContenedorKg,   // tara del contenedor vacío
    double PayloadMaxKg);      // carga útil máxima ISO/Maersk

public static class ContenedorSpecs
{
    public static readonly Dictionary<string, ContenedorSpec> Todos =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["20ft"]    = new("20ft",       589, 235, 239, 91,   549, 2_800, 2_200, 28_300),
        ["40ft"]    = new("40ft",     1_200, 235, 239, 91, 1_082, 4_500, 3_900, 28_870),
        ["40ft HC"] = new("40ft HC",  1_200, 235, 269, 91, 1_082, 4_500, 3_900, 28_690),
        ["45ft HC"] = new("45ft HC",  1_351, 235, 269, 91, 1_219, 4_800, 4_200, 27_650),
        ["53ft"]    = new("53ft",     1_600, 260, 274, 91, 1_372, 4_760, 3_900, 24_000),
        // 53ft Dry Van (trailer integrado): equipo estándar de drayage US.
        // Chasis + caja como una sola unidad ~6,500 kg (aluminio moderno).
        // Tandem posición default 40ft California legal (KP→eje = 1,220 cm).
        ["53ft Dry Van"] = new("53ft Dry Van", 1_600, 260, 274, 91, 1_220, 3_500, 3_000, 20_400),
    };

    public static ContenedorSpec Default => Todos["40ft HC"];

    public static ContenedorSpec Get(string? tipo) =>
        tipo != null && Todos.TryGetValue(tipo, out var s) ? s : Default;
}
