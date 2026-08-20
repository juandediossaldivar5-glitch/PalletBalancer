using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Api.Services;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContenedorController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContenedorController(AppDbContext db) => _db = db;

    /// <summary>
    /// Calcula el plan de estiba para uno o varios FDOs.
    /// OrdenDescarga es la lista de consignees en orden de descarga (índice 0 = primero en descargar).
    /// Solo MKT/ADM deben enviar OrdenDescarga; el resto lo recibe automático.
    /// </summary>
    [HttpPost("calcular")]
    public async Task<IActionResult> Calcular(CalcularContenedorDto dto)
    {
        if (dto.FdoIds == null || dto.FdoIds.Count == 0)
            return BadRequest(new { mensaje = "Se requiere al menos un FDO." });

        var fdos = await _db.Fdos
            .Include(f => f.Lineas)
            .Where(f => dto.FdoIds.Contains(f.Id))
            .ToListAsync();

        if (fdos.Count == 0) return NotFound();

        await CargarItemsEnFdos(fdos);

        var resultado = new ContenedorService().Calcular(fdos, dto.OrdenDescarga,
            dto.TipoContenedor, dto.TipoTractocamion);
        return Ok(resultado);
    }

    /// <summary>
    /// Evalúa las 15 combinaciones contenedor×tractocamión y devuelve las viables ordenadas
    /// de más eficiente (contenedor más pequeño) a menos.
    /// </summary>
    [HttpPost("recomendar")]
    public async Task<IActionResult> Recomendar(RecomendarDto dto)
    {
        if (dto.FdoIds == null || dto.FdoIds.Count == 0)
            return BadRequest(new { mensaje = "Se requiere al menos un FDO." });

        var fdos = await _db.Fdos
            .Include(f => f.Lineas)
            .Where(f => dto.FdoIds.Contains(f.Id))
            .ToListAsync();

        if (fdos.Count == 0) return NotFound();
        await CargarItemsEnFdos(fdos);

        string[] contenedores   = ["20ft", "40ft", "40ft HC", "45ft HC", "53ft"];
        string[] tractocamiones = ["T3-S2 Estándar", "T3-S2 Cabina Larga", "T3-S2 Day Cab"];

        var resultados = new List<RecomendacionItemDto>();

        foreach (var cont in contenedores)
        {
            foreach (var trac in tractocamiones)
            {
                var r = new ContenedorService().Calcular(fdos, null, cont, trac);

                var problemas = new List<string>();
                if (r.Advertencias.Any(a => a.Contains("posiciones")))
                    problemas.Add("La carga no cabe en el contenedor.");
                // Solo fallos NOM-012 y exceso de payload descalifican.
                // FHWA aplica al truck americano (distinto vehículo) → queda informativo, no bloquea.
                problemas.AddRange(r.Advertencias
                    .Where(a => (a.Contains("⛔") && a.Contains("NOM-012")) || a.Contains("capacidad de carga")));

                int filasUsadas = r.Destinos.Count > 0
                    ? r.Destinos.Max(d => d.FilaFin) : 0;

                resultados.Add(new RecomendacionItemDto
                {
                    Contenedor       = cont,
                    Tractocamion     = trac,
                    Viable           = problemas.Count == 0,
                    GVWKg            = r.PesoTotalGVWKg,
                    W1Kg             = r.PesoEjeDelanteroKg,
                    W2Kg             = r.PesoEjeTractorKg,
                    WrKg             = r.PesoEjeRemolqueKg,
                    FilasUsadas      = filasUsadas,
                    FilasDisponibles = r.FilasDisponibles,
                    BalancePct       = r.DiferenciaPorcentual,
                    Problemas        = problemas,
                });
            }
        }

        var orden = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { ["20ft"]=0, ["40ft"]=1, ["40ft HC"]=2, ["45ft HC"]=3, ["53ft"]=4 };

        var viables = resultados
            .Where(r => r.Viable)
            .OrderBy(r => orden.GetValueOrDefault(r.Contenedor, 9))
            .ThenBy(r => r.GVWKg)
            .ToList();

        // Si ninguna combinación es viable, devolver todas para que el usuario vea el motivo
        var salida = viables.Count > 0 ? viables : resultados
            .OrderBy(r => orden.GetValueOrDefault(r.Contenedor, 9))
            .ToList();

        return Ok(salida);
    }

    private async Task CargarItemsEnFdos(List<Fdo> fdos)
    {
        var modelNos = fdos.SelectMany(f => f.Lineas)
                           .Select(l => l.ModelNo)
                           .ToHashSet();
        var items = await _db.Items
            .Where(i => modelNos.Contains(i.ModelNo))
            .ToDictionaryAsync(i => i.ModelNo);
        foreach (var linea in fdos.SelectMany(f => f.Lineas))
            linea.Item = items.GetValueOrDefault(linea.ModelNo);
    }
}
