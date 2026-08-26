namespace PalletBalancer.Api.DTOs;

public class ContenedorResultadoDto
{
    public List<PosicionResultadoDto> Posiciones      { get; set; } = [];
    public List<DestinoInfoDto>       Destinos        { get; set; } = [];
    public double  PesoIzquierdoKg                   { get; set; }
    public double  PesoDerechoKg                     { get; set; }
    public double  PesoTotalKg                       { get; set; }
    public double  DiferenciaPorcentual               { get; set; }
    public bool    DentroDeTolerancia                { get; set; }
    public List<string> Advertencias                { get; set; } = [];
    public int     TotalPallets                     { get; set; }
    public List<string> ModelosSinDatos             { get; set; } = [];

    // Contenedor seleccionado
    public string ContenedorTipo             { get; set; } = "";
    public int    ContenedorLargoCm          { get; set; }
    public int    ContenedorAnchoCm          { get; set; }
    public int    ContenedorAltoCm           { get; set; }
    public int    FilasDisponibles           { get; set; }

    // Dimensión de tarima usada para el cálculo de filas
    public double PalletLargoCm             { get; set; }
    public double PalletAnchoCm             { get; set; }

    // Tractocamión y cargas por eje
    public string TractocamionTipo          { get; set; } = "";
    public double CgLongitudinalCm          { get; set; }  // desde king pin
    public double CgLongitudinalPct         { get; set; }  // % de king pin a eje remolque
    public double PesoEjeDelanteroKg        { get; set; }  // W1 peor caso (= Max)
    public double PesoEjeTractorKg          { get; set; }  // W2 peor caso (= Max)
    public double PesoEjeRemolqueKg         { get; set; }  // Wr — determinístico
    public double PesoTotalGVWKg            { get; set; }  // GVW peor caso (= Max)

    // Rango por eje (RF-08) — min=tanque vacío / max=tanque lleno + conductor pesado
    public double PesoEjeDelanteroMinKg     { get; set; }
    public double PesoEjeDelanteroMaxKg     { get; set; }
    public double PesoEjeTractorMinKg       { get; set; }
    public double PesoEjeTractorMaxKg       { get; set; }
    // Wr es determinístico: no depende del estado del tractor
    public double PesoTotalGVWMinKg         { get; set; }
    public double PesoTotalGVWMaxKg         { get; set; }

    // Estado de cumplimiento por eje y norma: "Seguro" | "Condicional" | "Falla"
    public string EstadoNomW1               { get; set; } = "";
    public string EstadoNomW2               { get; set; } = "";
    public string EstadoNomWr               { get; set; } = "";
    public string EstadoFhwaW1              { get; set; } = "";
    public string EstadoFhwaW2              { get; set; } = "";
    public string EstadoFhwaWr              { get; set; } = "";
    public double MargenSeguridadPct        { get; set; }
    // Incertidumbre acumulada del peso total (N pallets × ±X kg/pallet)
    public double TolerancePesoTotalKg      { get; set; }
    // Posición del tandem del remolque (KP → eje) usada en el cálculo
    public int    PosicionTandemCm          { get; set; }

    // Ejes con US Class 8 Day Cab — el tractor que hace drayage en frontera EE.UU.
    // Se evalúan aparte porque FHWA aplica a este tractor, no al T3-S2 mexicano
    public double PesoEjeDelanteroUsMinKg   { get; set; }
    public double PesoEjeDelanteroUsMaxKg   { get; set; }
    public double PesoEjeTractorUsMinKg     { get; set; }
    public double PesoEjeTractorUsMaxKg     { get; set; }
    public double PesoTotalGVWUsMinKg       { get; set; }
    public double PesoTotalGVWUsMaxKg       { get; set; }
}

public class PosicionResultadoDto
{
    public int    Fila        { get; set; }
    public string Lado        { get; set; } = "";
    public string ModelNo     { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Destino     { get; set; } = "";
    public double PesoKg      { get; set; }
    public int    Piezas      { get; set; }
    public int    Capa        { get; set; }  // 1 = piso, 2 = encima
    public double AltoCm      { get; set; }
    public double LargoCm     { get; set; }
    public double AnchoCm     { get; set; }
    public bool   EsParcial   { get; set; }  // true = pallet con cantidad incompleta
    public bool   Apilable    { get; set; }  // false = no se puede estibar (no poner encima)
}

public class DestinoInfoDto
{
    public string Consignee     { get; set; } = "";
    public int    OrdenDescarga { get; set; } // 1 = primero en descargar (puertas)
    public int    TotalPallets  { get; set; }
    public int    FilaInicio    { get; set; }
    public int    FilaFin       { get; set; }
}
