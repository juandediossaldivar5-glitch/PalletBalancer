using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ItemsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos() =>
        Ok(await _db.Items.OrderBy(i => i.ModelNo).ToListAsync());

    [HttpGet("{modelNo}")]
    public async Task<IActionResult> ObtenerPorModelNo(string modelNo)
    {
        var item = await _db.Items.FindAsync(modelNo);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(ItemDto dto)
    {
        if (await _db.Items.AnyAsync(i => i.ModelNo == dto.ModelNo))
            return Conflict($"Ya existe un item con ModelNo '{dto.ModelNo}'.");

        var item = MapearDesdeDto(dto);
        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(ObtenerPorModelNo), new { modelNo = item.ModelNo }, item);
    }

    [HttpPut("{modelNo}")]
    public async Task<IActionResult> Actualizar(string modelNo, ItemDto dto)
    {
        var item = await _db.Items.FindAsync(modelNo);
        if (item is null) return NotFound();
        ActualizarDesdeDto(item, dto);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{modelNo}")]
    public async Task<IActionResult> Eliminar(string modelNo)
    {
        var item = await _db.Items.FindAsync(modelNo);
        if (item is null) return NotFound();
        _db.Items.Remove(item);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static Item MapearDesdeDto(ItemDto d) => new()
    {
        ModelNo           = d.ModelNo,           Descripcion       = d.Descripcion,
        SpPiezasPorPallet = d.SpPiezasPorPallet,  SpPesoKg          = d.SpPesoKg,
        SpLargoCm         = d.SpLargoCm,          SpAnchoCm         = d.SpAnchoCm,   SpAltoCm  = d.SpAltoCm,
        CajaPiezasPorCaja = d.CajaPiezasPorCaja,  CajaPesoKg        = d.CajaPesoKg,
        CajaLargoCm       = d.CajaLargoCm,        CajaAnchoCm       = d.CajaAnchoCm, CajaAltoCm = d.CajaAltoCm,
        PiezaPesoKg       = d.PiezaPesoKg,        PiezaLargoCm      = d.PiezaLargoCm,
        PiezaAnchoCm      = d.PiezaAnchoCm,       PiezaAltoCm       = d.PiezaAltoCm
    };

    private static void ActualizarDesdeDto(Item i, ItemDto d)
    {
        i.Descripcion       = d.Descripcion;
        i.SpPiezasPorPallet = d.SpPiezasPorPallet;  i.SpPesoKg   = d.SpPesoKg;
        i.SpLargoCm         = d.SpLargoCm;           i.SpAnchoCm  = d.SpAnchoCm;   i.SpAltoCm   = d.SpAltoCm;
        i.CajaPiezasPorCaja = d.CajaPiezasPorCaja;   i.CajaPesoKg = d.CajaPesoKg;
        i.CajaLargoCm       = d.CajaLargoCm;         i.CajaAnchoCm = d.CajaAnchoCm; i.CajaAltoCm = d.CajaAltoCm;
        i.PiezaPesoKg       = d.PiezaPesoKg;         i.PiezaLargoCm = d.PiezaLargoCm;
        i.PiezaAnchoCm      = d.PiezaAnchoCm;        i.PiezaAltoCm  = d.PiezaAltoCm;
    }
}
