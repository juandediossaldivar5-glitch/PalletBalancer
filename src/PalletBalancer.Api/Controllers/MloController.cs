using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Api.Services;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MloController : ControllerBase
{
    private readonly AppDbContext _db;
    public MloController(AppDbContext db) => _db = db;

    /// <summary>
    /// Upload an MLO XLS file and link it to the FDO identified by fdoSlipNo.
    /// Replaces any existing MLO for that FDO.
    /// fechaEntrega defaults to the FDO's ShipDate when not provided.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile archivo,
        [FromForm] string fdoSlipNo,
        [FromForm] string? fechaEntrega = null)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Se requiere un archivo XLS." });
        if (string.IsNullOrWhiteSpace(fdoSlipNo))
            return BadRequest(new { mensaje = "Se requiere fdoSlipNo." });

        var fdo = await _db.Fdos.FirstOrDefaultAsync(f => f.FdoSlipNo == fdoSlipNo);
        if (fdo == null)
            return NotFound(new { mensaje = $"FDO '{fdoSlipNo}' no encontrado." });

        // Resolve fecha: prefer explicit parameter, fall back to FDO ShipDate
        DateOnly fecha = fechaEntrega is not null && DateOnly.TryParse(fechaEntrega, out var parsed)
            ? parsed
            : fdo.ShipDate;

        // Remove existing MLO for this FDO if any
        var existing = await _db.Mlos
            .Include(m => m.Lineas)
            .FirstOrDefaultAsync(m => m.FdoId == fdo.Id);
        if (existing != null)
        {
            _db.MloLineas.RemoveRange(existing.Lineas);
            _db.Mlos.Remove(existing);
        }

        Mlo mlo;
        using (var stream = archivo.OpenReadStream())
            mlo = MloXlsParser.Parse(stream, archivo.FileName, fdo.Id, fecha);

        _db.Mlos.Add(mlo);
        await _db.SaveChangesAsync();

        return Ok(new MloResumenDto
        {
            Id          = mlo.Id,
            MloNo       = mlo.MloNo,
            FdoId       = fdo.Id,
            TotalLineas = mlo.Lineas.Count,
        });
    }

    /// <summary>Returns the MLO linked to a given FDO id.</summary>
    [HttpGet("fdo/{fdoId:int}")]
    public async Task<IActionResult> GetByFdo(int fdoId)
    {
        var mlo = await _db.Mlos
            .Include(m => m.Lineas)
            .Include(m => m.Fdo)
            .FirstOrDefaultAsync(m => m.FdoId == fdoId);

        if (mlo == null) return NotFound();

        return Ok(new MloDto
        {
            Id        = mlo.Id,
            MloNo     = mlo.MloNo,
            FdoId     = mlo.FdoId,
            FdoSlipNo = mlo.Fdo.FdoSlipNo,
            Lineas    = mlo.Lineas.Select(l => new MloLineaDto
            {
                Id           = l.Id,
                CaseNo       = l.CaseNo,
                ModelNo      = l.ModelNo,
                Descripcion  = l.Descripcion,
                FromLocation = l.FromLocation,
                FromQty      = l.FromQty,
                Check        = l.Check,
            }).ToList(),
        });
    }

    /// <summary>
    /// Given the same inputs as /api/contenedor/calcular plus FDO ids,
    /// recalculates the container plan and overlays picking order from MLOs.
    /// </summary>
    [HttpPost("picking")]
    public async Task<IActionResult> Picking(PickingRequestDto dto)
    {
        if (dto.FdoIds == null || dto.FdoIds.Count == 0)
            return BadRequest(new { mensaje = "Se requiere al menos un FDO." });

        var fdos = await _db.Fdos
            .Include(f => f.Lineas)
            .Where(f => dto.FdoIds.Contains(f.Id))
            .ToListAsync();

        if (fdos.Count == 0) return NotFound();

        // Load items into FDO lines (same pattern as ContenedorController)
        var modelNos = fdos.SelectMany(f => f.Lineas).Select(l => l.ModelNo).ToHashSet();
        var items = await _db.Items
            .Where(i => modelNos.Contains(i.ModelNo))
            .ToDictionaryAsync(i => i.ModelNo);
        foreach (var linea in fdos.SelectMany(f => f.Lineas))
            linea.Item = items.GetValueOrDefault(linea.ModelNo);

        // Load MLOs for each FDO
        var mlos = await _db.Mlos
            .Include(m => m.Lineas)
            .Where(m => dto.FdoIds.Contains(m.FdoId))
            .ToDictionaryAsync(m => m.FdoId);

        // Calculate container plan
        var plan = new ContenedorService().Calcular(
            fdos, dto.OrdenDescarga, dto.TipoContenedor, dto.TipoTractocamion);

        // Build fdoData tuples
        var fdoData = fdos.Select(f => (
            Fdo:  f,
            Mlo:  mlos.GetValueOrDefault(f.Id),
            Item: (Item?)null
        )).ToList();

        var resultado = MloPickingService.Calcular(plan, fdoData);
        return Ok(resultado);
    }
}
