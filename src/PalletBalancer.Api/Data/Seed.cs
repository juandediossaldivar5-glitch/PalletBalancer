using System.Text.Json;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Data;

public static class Seed
{
    public static async Task CargarItemsDesdeJson(AppDbContext db, string rutaJson)
    {
        if (await db.Items.AnyAsync()) return;
        if (!File.Exists(rutaJson)) return;

        var json = await File.ReadAllTextAsync(rutaJson);
        var catalogo = JsonSerializer.Deserialize<CatalogoJson>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (catalogo?.Items is null) return;

        foreach (var e in catalogo.Items)
        {
            db.Items.Add(new Item
            {
                ModelNo           = e.ModelNo,
                Descripcion       = e.Descripcion,
                SpPiezasPorPallet = e.StandardPack.PiezasPorPallet,
                SpPesoKg          = e.StandardPack.Peso_Kg,
                SpLargoCm         = e.StandardPack.Largo_Cm,
                SpAnchoCm         = e.StandardPack.Ancho_Cm,
                SpAltoCm          = e.StandardPack.Alto_Cm,
                CajaPiezasPorCaja = e.Caja.PiezasPorCaja,
                CajaPesoKg        = e.Caja.Peso_Kg,
                CajaLargoCm       = e.Caja.Largo_Cm,
                CajaAnchoCm       = e.Caja.Ancho_Cm,
                CajaAltoCm        = e.Caja.Alto_Cm,
                PiezaPesoKg       = e.Pieza.Peso_Kg,
                PiezaLargoCm      = e.Pieza.Largo_Cm,
                PiezaAnchoCm      = e.Pieza.Ancho_Cm,
                PiezaAltoCm       = e.Pieza.Alto_Cm
            });
        }
        await db.SaveChangesAsync();
    }

    public static async Task SeedUsuarioAdminAsync(AppDbContext db)
    {
        if (await db.Usuarios.AnyAsync()) return;

        db.Usuarios.Add(new Usuario
        {
            Username     = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234!"),
            Rol          = "ADM",
            Activo       = true
        });
        await db.SaveChangesAsync();
    }

    private class CatalogoJson { public List<EntradaJson> Items { get; set; } = []; }
    private class EntradaJson
    {
        public string    ModelNo      { get; set; } = string.Empty;
        public string    Descripcion  { get; set; } = string.Empty;
        public SpJson    StandardPack { get; set; } = new();
        public CajaJson  Caja         { get; set; } = new();
        public PiezaJson Pieza        { get; set; } = new();
    }
    private class SpJson
    {
        public int    PiezasPorPallet { get; set; }
        public double Peso_Kg  { get; set; }
        public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; }
        public double Alto_Cm  { get; set; }
    }
    private class CajaJson
    {
        public int    PiezasPorCaja { get; set; }
        public double Peso_Kg  { get; set; }
        public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; }
        public double Alto_Cm  { get; set; }
    }
    private class PiezaJson
    {
        public double Peso_Kg  { get; set; }
        public double Largo_Cm { get; set; }
        public double Ancho_Cm { get; set; }
        public double Alto_Cm  { get; set; }
    }
}
