using Microsoft.Data.Sqlite;

namespace PalletBalancer.Core.Data;

public static class DbInitializer
{
    public static void Inicializar(string cadenaConexion)
    {
        using var conexion = new SqliteConnection(cadenaConexion);
        conexion.Open();

        var comando = conexion.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS Producto (
                Sku TEXT PRIMARY KEY,
                Nombre TEXT NOT NULL,
                PesoUnitarioKg REAL NOT NULL,
                LargoCm REAL NOT NULL,
                AnchoCm REAL NOT NULL,
                AltoCm REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Carga (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FechaCreacion TEXT NOT NULL,
                ContenedorTipo TEXT NOT NULL,
                PesoTotalKg REAL NOT NULL,
                PesoLadoIzquierdoKg REAL NOT NULL,
                PesoLadoDerechoKg REAL NOT NULL,
                DiferenciaPorcentual REAL NOT NULL
            );

            CREATE TABLE IF NOT EXISTS CargaPallet (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CargaId INTEGER NOT NULL,
                CodigoEscaneado TEXT NOT NULL,
                Sku TEXT NOT NULL,
                CantidadPiezas INTEGER NOT NULL,
                PesoTotalKg REAL NOT NULL,
                AltoCm REAL NOT NULL DEFAULT 0,
                Lado TEXT NOT NULL,
                Fila INTEGER NOT NULL,
                Capa INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (CargaId) REFERENCES Carga(Id)
            );
            """;
        comando.ExecuteNonQuery();

        // Migración: agrega columnas nuevas si la tabla ya existía con el esquema anterior
        EjecutarSilencioso(conexion, "ALTER TABLE CargaPallet ADD COLUMN AltoCm REAL NOT NULL DEFAULT 0");
        EjecutarSilencioso(conexion, "ALTER TABLE CargaPallet ADD COLUMN Capa INTEGER NOT NULL DEFAULT 0");
    }

    private static void EjecutarSilencioso(SqliteConnection conexion, string sql)
    {
        try
        {
            var cmd = conexion.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch { }
    }
}
