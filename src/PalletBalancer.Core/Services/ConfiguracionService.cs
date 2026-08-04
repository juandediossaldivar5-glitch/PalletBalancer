using System.Text.Json;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Services;

public static class ConfiguracionService
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Carga configuración desde un archivo JSON.
    /// Si el archivo no existe o falla la deserialización, devuelve valores por defecto.
    /// </summary>
    public static ConfiguracionCarga Cargar(string rutaJson)
    {
        if (!File.Exists(rutaJson))
            return new ConfiguracionCarga();

        try
        {
            var json = File.ReadAllText(rutaJson);
            return JsonSerializer.Deserialize<ConfiguracionCarga>(json, Opts) ?? new ConfiguracionCarga();
        }
        catch
        {
            return new ConfiguracionCarga();
        }
    }
}
