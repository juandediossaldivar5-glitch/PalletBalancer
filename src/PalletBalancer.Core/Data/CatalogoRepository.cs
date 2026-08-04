using Microsoft.Data.Sqlite;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Data;

/// <summary>Catálogo de productos: peso unitario y medidas por SKU.</summary>
public class CatalogoRepository
{
    private readonly string _cadenaConexion;

    public CatalogoRepository(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public void GuardarOActualizar(Producto producto)
    {
        using var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        var comando = conexion.CreateCommand();
        comando.CommandText = """
            INSERT INTO Producto (Sku, Nombre, PesoUnitarioKg, LargoCm, AnchoCm, AltoCm)
            VALUES ($sku, $nombre, $peso, $largo, $ancho, $alto)
            ON CONFLICT(Sku) DO UPDATE SET
                Nombre = excluded.Nombre,
                PesoUnitarioKg = excluded.PesoUnitarioKg,
                LargoCm = excluded.LargoCm,
                AnchoCm = excluded.AnchoCm,
                AltoCm = excluded.AltoCm;
            """;
        comando.Parameters.AddWithValue("$sku", producto.Sku);
        comando.Parameters.AddWithValue("$nombre", producto.Nombre);
        comando.Parameters.AddWithValue("$peso", producto.PesoUnitarioKg);
        comando.Parameters.AddWithValue("$largo", producto.LargoCm);
        comando.Parameters.AddWithValue("$ancho", producto.AnchoCm);
        comando.Parameters.AddWithValue("$alto", producto.AltoCm);
        comando.ExecuteNonQuery();
    }

    public Producto? ObtenerPorSku(string sku)
    {
        using var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Sku, Nombre, PesoUnitarioKg, LargoCm, AnchoCm, AltoCm FROM Producto WHERE Sku = $sku;";
        comando.Parameters.AddWithValue("$sku", sku);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;

        return LeerProducto(lector);
    }

    public List<Producto> ObtenerTodos()
    {
        using var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT Sku, Nombre, PesoUnitarioKg, LargoCm, AnchoCm, AltoCm FROM Producto ORDER BY Sku;";

        var resultado = new List<Producto>();
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            resultado.Add(LeerProducto(lector));
        }
        return resultado;
    }

    private static Producto LeerProducto(SqliteDataReader lector) => new()
    {
        Sku = lector.GetString(0),
        Nombre = lector.GetString(1),
        PesoUnitarioKg = lector.GetDouble(2),
        LargoCm = lector.GetDouble(3),
        AnchoCm = lector.GetDouble(4),
        AltoCm = lector.GetDouble(5)
    };
}
