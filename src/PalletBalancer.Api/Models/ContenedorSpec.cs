namespace PalletBalancer.Api.Models;

public record ContenedorSpec(string Tipo, int LargoCm, int AnchoCm, int AltoCm);

public static class ContenedorSpecs
{
    public static readonly Dictionary<string, ContenedorSpec> Todos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["20ft"]    = new("20ft",     589, 235, 239),
        ["40ft"]    = new("40ft",   1_200, 235, 239),
        ["40ft HC"] = new("40ft HC",1_200, 235, 269),
        ["45ft HC"] = new("45ft HC",1_351, 235, 269),
        ["53ft"]    = new("53ft",   1_600, 260, 274),
    };

    public static ContenedorSpec Default => Todos["40ft HC"];

    public static ContenedorSpec Get(string? tipo) =>
        tipo != null && Todos.TryGetValue(tipo, out var s) ? s : Default;
}
