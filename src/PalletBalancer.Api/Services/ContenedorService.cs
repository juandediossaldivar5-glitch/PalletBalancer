using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Core.Models;
using PalletBalancer.Core.Services;

namespace PalletBalancer.Api.Services;

public class ContenedorService
{
    private readonly BalanceadorService _balanceador = new();

    public ContenedorResultadoDto Calcular(Fdo fdo)
    {
        var pallets  = new List<Pallet>();
        var sinDatos = new List<string>();

        var descMap = fdo.Lineas
            .Where(l => l.Item != null)
            .ToDictionary(l => l.ModelNo, l => l.Item!.Descripcion);

        foreach (var linea in fdo.Lineas)
        {
            if (linea.Item == null || linea.Item.SpPiezasPorPallet == 0)
            {
                if (!sinDatos.Contains(linea.ModelNo))
                    sinDatos.Add(linea.ModelNo);
                continue;
            }

            var item = linea.Item;
            int pxp  = item.SpPiezasPorPallet;
            int full = linea.ReqQty / pxp;
            int rest = linea.ReqQty % pxp;

            for (int i = 0; i < full; i++)
                pallets.Add(Build(linea.ModelNo, pxp, item.SpPesoKg,
                    item.SpLargoCm, item.SpAnchoCm, item.SpAltoCm));

            if (rest > 0)
            {
                double pesoParc = Math.Round((double)rest / pxp * item.SpPesoKg, 2);
                double altoParc = Math.Round((double)rest / pxp * item.SpAltoCm, 1);
                pallets.Add(Build(linea.ModelNo, rest, pesoParc,
                    item.SpLargoCm, item.SpAnchoCm,
                    altoParc > 0 ? altoParc : item.SpAltoCm));
            }
        }

        var config   = new ConfiguracionCarga(); // defaults: 53ft, sin apilado
        var resultado = _balanceador.Balancear(pallets, config);

        return new ContenedorResultadoDto
        {
            PesoIzquierdoKg     = resultado.PesoLadoIzquierdoKg,
            PesoDerechoKg       = resultado.PesoLadoDerechoKg,
            PesoTotalKg         = resultado.PesoTotalKg,
            DiferenciaPorcentual = resultado.DiferenciaPorcentual,
            DentroDeTolerancia  = resultado.DentroDeTolerancia,
            Advertencias        = resultado.Advertencias,
            TotalPallets        = pallets.Count,
            ModelosSinDatos     = sinDatos,
            Posiciones          = resultado.Posiciones.Select(p =>
            {
                string sku = p.Capas.FirstOrDefault()?.Sku ?? "";
                return new PosicionResultadoDto
                {
                    Fila        = p.Fila,
                    Lado        = p.Lado.ToString(),
                    ModelNo     = sku,
                    Descripcion = descMap.TryGetValue(sku, out var d) ? d : "",
                    PesoKg      = Math.Round(p.PesoTotalKg, 2),
                    Piezas      = p.Capas.Sum(c => c.CantidadPiezas),
                    Capas       = p.Capas.Count,
                };
            }).ToList()
        };
    }

    private static Pallet Build(string sku, int piezas, double pesoKg,
        double largoCm, double anchoCm, double altoCm) =>
        new()
        {
            CodigoEscaneado = sku,
            Sku             = sku,
            CantidadPiezas  = piezas,
            PesoTotalKg     = pesoKg,
            LargoCm         = largoCm > 0 ? largoCm : 120,
            AnchoCm         = anchoCm > 0 ? anchoCm : 120,
            AltoCm          = altoCm  > 0 ? altoCm  : 66,
        };
}
