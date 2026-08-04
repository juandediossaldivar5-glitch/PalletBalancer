using Microsoft.Data.Sqlite;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Data;

/// <summary>Persiste el resultado de un balanceo (histórico de cargas armadas).</summary>
public class CargaRepository
{
    private readonly string _cadenaConexion;

    public CargaRepository(string cadenaConexion)
    {
        _cadenaConexion = cadenaConexion;
    }

    public long GuardarCarga(ResultadoBalanceo resultado, string contenedorTipo)
    {
        using var conexion = new SqliteConnection(_cadenaConexion);
        conexion.Open();
        using var transaccion = conexion.BeginTransaction();

        var comandoCarga = conexion.CreateCommand();
        comandoCarga.Transaction = transaccion;
        comandoCarga.CommandText = """
            INSERT INTO Carga (FechaCreacion, ContenedorTipo, PesoTotalKg, PesoLadoIzquierdoKg, PesoLadoDerechoKg, DiferenciaPorcentual)
            VALUES ($fecha, $tipo, $total, $izq, $der, $dif);
            SELECT last_insert_rowid();
            """;
        comandoCarga.Parameters.AddWithValue("$fecha", DateTime.Now.ToString("o"));
        comandoCarga.Parameters.AddWithValue("$tipo", contenedorTipo);
        comandoCarga.Parameters.AddWithValue("$total", resultado.PesoTotalKg);
        comandoCarga.Parameters.AddWithValue("$izq", resultado.PesoLadoIzquierdoKg);
        comandoCarga.Parameters.AddWithValue("$der", resultado.PesoLadoDerechoKg);
        comandoCarga.Parameters.AddWithValue("$dif", resultado.DiferenciaPorcentual);

        var cargaId = (long)comandoCarga.ExecuteScalar()!;

        foreach (var posicion in resultado.Posiciones)
        {
            for (int capa = 0; capa < posicion.Capas.Count; capa++)
            {
                var p = posicion.Capas[capa];
                var cmd = conexion.CreateCommand();
                cmd.Transaction = transaccion;
                cmd.CommandText = """
                    INSERT INTO CargaPallet (CargaId, CodigoEscaneado, Sku, CantidadPiezas, PesoTotalKg, AltoCm, Lado, Fila, Capa)
                    VALUES ($cargaId, $codigo, $sku, $cantidad, $peso, $alto, $lado, $fila, $capa);
                    """;
                cmd.Parameters.AddWithValue("$cargaId", cargaId);
                cmd.Parameters.AddWithValue("$codigo", p.CodigoEscaneado);
                cmd.Parameters.AddWithValue("$sku", p.Sku);
                cmd.Parameters.AddWithValue("$cantidad", p.CantidadPiezas);
                cmd.Parameters.AddWithValue("$peso", p.PesoTotalKg);
                cmd.Parameters.AddWithValue("$alto", p.AltoCm);
                cmd.Parameters.AddWithValue("$lado", posicion.Lado.ToString());
                cmd.Parameters.AddWithValue("$fila", posicion.Fila);
                cmd.Parameters.AddWithValue("$capa", capa);
                cmd.ExecuteNonQuery();
            }
        }

        transaccion.Commit();
        return cargaId;
    }
}
