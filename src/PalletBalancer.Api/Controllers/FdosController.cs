using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

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
            .Include(f => f.Lineas).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(f => f.Id == id);
        return fdo is null ? NotFound() : Ok(fdo);
    }

    [HttpPost]
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
}
