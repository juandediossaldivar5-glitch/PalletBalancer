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
public class FdosController : ControllerBase
{
    private readonly AppDbContext _db;
    public FdosController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos() =>
        Ok(await _db.Fdos.OrderByDescending(f => f.CreadoEn).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var fdo = await _db.Fdos
            .Include(f => f.Lineas)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (fdo is null) return NotFound();
        await CargarItemsEnLineas(fdo.Lineas);
        return Ok(fdo);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Crear(FdoDto dto)
    {
        if (await _db.Fdos.AnyAsync(f => f.FdoSlipNo == dto.FdoSlipNo))
            return Conflict($"FDO '{dto.FdoSlipNo}' ya existe.");

        var fdo = new Fdo
        {
            FdoSlipNo = dto.FdoSlipNo,
            DsbDate   = DateOnly.Parse(dto.DsbDate),
            ShipDate  = DateOnly.Parse(dto.ShipDate),
            Customer  = dto.Customer,
            Consignee = dto.Consignee,
            Lineas    = dto.Lineas.Select(l => new FdoLinea
            {
                CustomerPoNo = l.CustomerPoNo,
                ModelNo      = l.ModelNo,
                ReqQty       = l.ReqQty
            }).ToList()
        };

        _db.Fdos.Add(fdo);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorId), new { id = fdo.Id }, fdo);
    }

    [HttpPost("importar")]
    [Authorize]
    public IActionResult Importar(IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Se requiere un archivo PDF." });

        using var stream = archivo.OpenReadStream();
        var dto = PdfFdoParser.Parsear(stream);
        return Ok(dto);
    }

    [HttpPost("importar/debug")]
    [Authorize]
    public IActionResult ImportarDebug(IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Se requiere un archivo PDF." });

        using var stream = archivo.OpenReadStream();
        var lineas = PdfFdoParser.ExtraerLineasPublico(stream);
        return Ok(new { totalLineas = lineas.Count, lineas });
    }

    [HttpGet("{id:int}/contenedor")]
    public async Task<IActionResult> CalcularContenedor(int id)
    {
        var fdo = await _db.Fdos
            .Include(f => f.Lineas)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (fdo is null) return NotFound();
        await CargarItemsEnLineas(fdo.Lineas);

        var resultado = new ContenedorService().Calcular(fdo);
        return Ok(resultado);
    }

    private async Task CargarItemsEnLineas(IEnumerable<FdoLinea> lineas)
    {
        var modelNos = lineas.Select(l => l.ModelNo).ToHashSet();
        var items    = await _db.Items
            .Where(i => modelNos.Contains(i.ModelNo))
            .ToDictionaryAsync(i => i.ModelNo);
        foreach (var linea in lineas)
            linea.Item = items.GetValueOrDefault(linea.ModelNo);
    }

    [HttpPatch("{id:int}/lineas/{lineaId:int}")]
    [Authorize(Roles = "AMG,ADM")]
    public async Task<IActionResult> PatchCantidad(int id, int lineaId, PatchCantidadDto dto)
    {
        var linea = await _db.FdoLineas
            .FirstOrDefaultAsync(l => l.Id == lineaId && l.FdoId == id);

        if (linea is null) return NotFound();

        linea.ReqQty = dto.ReqQty;
        await _db.SaveChangesAsync();
        return Ok(linea);
    }
}
