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

        var resultado = new ContenedorService().Calcular(fdos, dto.OrdenDescarga, dto.TipoContenedor);
        return Ok(resultado);
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
