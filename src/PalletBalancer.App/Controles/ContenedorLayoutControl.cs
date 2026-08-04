using System.Drawing.Drawing2D;
using PalletBalancer.Core.Models;

namespace PalletBalancer.App.Controles;

public class ContenedorLayoutControl : UserControl
{
    private ResultadoBalanceo? _resultado;
    private Contenedor? _contenedor;

    private const int MargenIzq = 10;
    private const int MargenDer = 10;
    private const int MargenSup = 14;
    private const int AlturaCelda = 96;
    private const int AlturaTitulo = 20;
    private const int EspacioEntreLados = 18;
    private const int AlturaEje = 26;

    public ContenedorLayoutControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
    }

    public void MostrarResultado(ResultadoBalanceo resultado, Contenedor contenedor)
    {
        _resultado = resultado;
        _contenedor = contenedor;
        Invalidate();
    }

    public void LimpiarLayout()
    {
        _resultado = null;
        _contenedor = null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (_resultado is null || _contenedor is null)
        {
            using var grayBrush = new SolidBrush(Color.Silver);
            g.DrawString(
                "Presione «Balancear carga» para ver el layout del contenedor.",
                new Font("Segoe UI", 10f), grayBrush,
                new PointF(MargenIzq, Height / 2f - 10));
            return;
        }

        int filas = _contenedor.FilasDisponibles;
        float anchoDisp = Width - MargenIzq - MargenDer;
        float anchoCelda = anchoDisp / filas;

        // Peso máximo entre todos los pallets individuales (para escala de color por capa)
        double pesoMax = _resultado.Posiciones.Count > 0
            ? _resultado.Posiciones.SelectMany(p => p.Capas).Max(p => p.PesoTotalKg)
            : 1.0;

        var mapIzq = _resultado.Posiciones
            .Where(p => p.Lado == LadoContenedor.Izquierdo)
            .ToDictionary(p => p.Fila);
        var mapDer = _resultado.Posiciones
            .Where(p => p.Lado == LadoContenedor.Derecho)
            .ToDictionary(p => p.Fila);

        float yIzqTit = MargenSup;
        float yIzqCel = yIzqTit + AlturaTitulo;
        float yDerTit = yIzqCel + AlturaCelda + EspacioEntreLados;
        float yDerCel = yDerTit + AlturaTitulo;
        float yEje = yDerCel + AlturaCelda + 6;

        using var fuenteTit = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var fuenteCapa = new Font("Segoe UI", 7f);
        using var fuenteEje = new Font("Segoe UI", 8f);
        using var brushTxt = new SolidBrush(Color.FromArgb(20, 20, 20));
        using var brushTit = new SolidBrush(Color.FromArgb(50, 50, 110));
        using var brushEje = new SolidBrush(Color.Gray);
        using var brushVacio = new SolidBrush(Color.FromArgb(238, 238, 238));
        using var penBorde = new Pen(Color.FromArgb(160, 160, 160), 1f);
        using var penSep = new Pen(Color.FromArgb(110, 110, 110), 0.5f);

        g.DrawString("IZQUIERDO", fuenteTit, brushTit, MargenIzq, yIzqTit);
        g.DrawString("DERECHO", fuenteTit, brushTit, MargenIzq, yDerTit);

        // Etiquetas eje longitudinal
        string lblCabina = "← Frente (cabina)";
        string lblPuertas = "Puertas (fondo) →";
        float wPuertas = g.MeasureString(lblPuertas, fuenteEje).Width;
        g.DrawLine(penBorde, MargenIzq, yEje - 3, MargenIzq + anchoDisp, yEje - 3);
        g.DrawString(lblCabina, fuenteEje, brushEje, MargenIzq, yEje);
        g.DrawString(lblPuertas, fuenteEje, brushEje, MargenIzq + anchoDisp - wPuertas, yEje);

        var sfCenter = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        DibujarFila(g, mapIzq, filas, MargenIzq, yIzqCel, anchoCelda, AlturaCelda,
            pesoMax, fuenteCapa, brushTxt, brushVacio, penBorde, penSep, sfCenter);
        DibujarFila(g, mapDer, filas, MargenIzq, yDerCel, anchoCelda, AlturaCelda,
            pesoMax, fuenteCapa, brushTxt, brushVacio, penBorde, penSep, sfCenter);
    }

    private static void DibujarFila(
        Graphics g,
        Dictionary<int, PosicionPallet> mapa,
        int filas, float x0, float y,
        float anchoCelda, float alto,
        double pesoMax,
        Font fuente, SolidBrush brushTxt, SolidBrush brushVacio,
        Pen penBorde, Pen penSep,
        StringFormat sf)
    {
        for (int fila = 1; fila <= filas; fila++)
        {
            float x = x0 + (fila - 1) * anchoCelda;
            var rect = new RectangleF(x + 1f, y, anchoCelda - 2f, alto);

            if (mapa.TryGetValue(fila, out var pos))
            {
                int n = pos.Capas.Count;
                float hCapa = alto / n;

                // Pintar cada capa de abajo hacia arriba
                for (int c = 0; c < n; c++)
                {
                    float yCapa = y + (n - 1 - c) * hCapa; // c=0 es base (abajo)
                    var pallet = pos.Capas[c];
                    double ratio = pesoMax > 0 ? pallet.PesoTotalKg / pesoMax : 0;

                    using var brushFill = new SolidBrush(ColorPorPeso(ratio));
                    g.FillRectangle(brushFill, rect.X, yCapa, rect.Width, hCapa);

                    DrawCapaText(g, pallet, fuente, brushTxt, sf, rect.X, yCapa, rect.Width, hCapa);
                }

                // Separadores entre capas
                for (int c = 1; c < n; c++)
                {
                    float ySepLine = y + (n - c) * hCapa;
                    g.DrawLine(penSep, rect.X, ySepLine, rect.X + rect.Width, ySepLine);
                }
            }
            else
            {
                g.FillRectangle(brushVacio, rect);
            }

            g.DrawRectangle(penBorde, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    private static void DrawCapaText(Graphics g, Pallet pallet, Font fuente, SolidBrush brushTxt,
        StringFormat sf, float x, float yCapa, float width, float hCapa)
    {
        string sku = pallet.Sku.Length > 9 ? pallet.Sku[..9] : pallet.Sku;
        string peso = $"{pallet.PesoTotalKg:F0}kg";

        if (hCapa >= 28)
        {
            var rSku = new RectangleF(x + 1, yCapa + 2, width - 2, hCapa * 0.52f);
            var rPeso = new RectangleF(x + 1, yCapa + hCapa * 0.52f, width - 2, hCapa * 0.45f);
            g.DrawString(sku, fuente, brushTxt, rSku, sf);
            g.DrawString(peso, fuente, brushTxt, rPeso, sf);
        }
        else
        {
            var r = new RectangleF(x + 1, yCapa + 1, width - 2, hCapa - 2);
            g.DrawString(peso, fuente, brushTxt, r, sf);
        }
    }

    // Verde suave (ligero) → naranja → rojo (pesado)
    private static Color ColorPorPeso(double ratio)
    {
        int r = (int)(160 + ratio * (220 - 160));
        int gv = (int)(220 - ratio * (220 - 40));
        int b = (int)(130 - ratio * 130);
        return Color.FromArgb(
            Math.Clamp(r, 0, 255),
            Math.Clamp(gv, 0, 255),
            Math.Clamp(b, 0, 255));
    }
}
