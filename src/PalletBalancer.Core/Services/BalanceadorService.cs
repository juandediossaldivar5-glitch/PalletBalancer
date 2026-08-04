using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Services;

/// <summary>
/// Algoritmo de balanceo con soporte de apilado vertical:
///  Fase 1 — APILADO: agrupa las unidades en stacks respetando alto del contenedor
///            y footprint (la unidad de encima no puede ser más ancha/larga que la base).
///            Las unidades se procesan de mayor a menor peso, por lo que la base siempre
///            es la más pesada del stack (máxima estabilidad).
///  Fase 2 — BALANCE LATERAL: distribuye los stacks entre izquierdo/derecho usando
///            el mismo greedy de menor-peso-acumulado.
///  Fase 3 — CENTRADO LONGITUDINAL: los stacks más pesados de cada lado van a las
///            filas más cercanas al centro del contenedor.
///
///  Si PiezasPorPalletCompleto = 0 en la config, el apilado está deshabilitado
///  y cada unidad ocupa su propia posición (comportamiento original).
/// </summary>
public class BalanceadorService : IBalanceadorService
{
    public ResultadoBalanceo Balancear(IEnumerable<Pallet> pallets, ConfiguracionCarga config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));
        var lista = (pallets ?? throw new ArgumentNullException(nameof(pallets))).ToList();

        var resultado = new ResultadoBalanceo();
        if (lista.Count == 0)
        {
            resultado.Advertencias.Add("No hay pallets para acomodar.");
            return resultado;
        }

        // Fase 1: Formar stacks
        var stacks = FormarStacks(lista, config);

        int capacidadTotal = config.Contenedor.FilasDisponibles * 2;
        if (stacks.Count > capacidadTotal)
        {
            resultado.Advertencias.Add(
                $"La carga requiere {stacks.Count} posiciones pero el contenedor solo tiene {capacidadTotal} disponibles.");
        }

        // Fase 2: Balance lateral (greedy por peso total del stack)
        var ordenados = stacks.OrderByDescending(s => s.Sum(p => p.PesoTotalKg)).ToList();
        var ladoIzq = new List<List<Pallet>>();
        var ladoDer = new List<List<Pallet>>();
        double pesoIzq = 0, pesoDer = 0;

        foreach (var stack in ordenados)
        {
            double w = stack.Sum(p => p.PesoTotalKg);
            if (pesoIzq <= pesoDer) { ladoIzq.Add(stack); pesoIzq += w; }
            else { ladoDer.Add(stack); pesoDer += w; }
        }

        // Fase 3: Centrado longitudinal
        var posiciones = new List<PosicionPallet>();
        posiciones.AddRange(AsignarFilasCentradas(ladoIzq, LadoContenedor.Izquierdo, config.Contenedor.FilasDisponibles, resultado));
        posiciones.AddRange(AsignarFilasCentradas(ladoDer, LadoContenedor.Derecho, config.Contenedor.FilasDisponibles, resultado));

        resultado.Posiciones = posiciones.OrderBy(p => p.Fila).ThenBy(p => p.Lado).ToList();
        resultado.PesoLadoIzquierdoKg = Math.Round(pesoIzq, 2);
        resultado.PesoLadoDerechoKg = Math.Round(pesoDer, 2);
        resultado.PesoTotalKg = Math.Round(pesoIzq + pesoDer, 2);

        double mayor = Math.Max(pesoIzq, pesoDer);
        double menor = Math.Min(pesoIzq, pesoDer);
        resultado.DiferenciaPorcentual = mayor == 0 ? 0 : Math.Round((mayor - menor) / mayor * 100, 2);
        resultado.DentroDeTolerancia = resultado.DiferenciaPorcentual <= config.Reglas.ToleranciaBalanceoPorc;

        if (!resultado.DentroDeTolerancia)
        {
            resultado.Advertencias.Add(
                $"Diferencia de peso entre lados ({resultado.DiferenciaPorcentual}%) excede la tolerancia ({config.Reglas.ToleranciaBalanceoPorc}%).");
        }

        if (resultado.PesoTotalKg > config.Contenedor.CapacidadMaximaKg)
        {
            resultado.Advertencias.Add(
                $"El peso total ({resultado.PesoTotalKg} kg) excede la capacidad máxima del contenedor ({config.Contenedor.CapacidadMaximaKg} kg).");
        }

        return resultado;
    }

    /// <summary>
    /// Forma stacks respetando alto del contenedor y footprint.
    /// Procesa pallets de mayor a menor peso, garantizando que la base sea siempre la más pesada.
    /// Si la config no tiene PiezasPorPalletCompleto > 0 o AltoCm > 0, cada pallet va solo.
    /// </summary>
    private static List<List<Pallet>> FormarStacks(List<Pallet> pallets, ConfiguracionCarga config)
    {
        double altoCont = config.Contenedor.AltoCm;
        int pxp = config.PalletEstandar.PiezasPorPalletCompleto;

        if (pxp <= 0 || altoCont <= 0)
            return pallets.Select(p => new List<Pallet> { p }).ToList();

        var ordenados = pallets.OrderByDescending(p => p.PesoTotalKg).ToList();
        var stacks = new List<List<Pallet>>();

        foreach (var pallet in ordenados)
        {
            bool apilado = false;
            foreach (var stack in stacks)
            {
                double alturaActual = stack.Sum(p => p.AltoCm);
                var baseStack = stack[0];

                // Alto dentro del contenedor
                bool cabeAlto = alturaActual + pallet.AltoCm <= altoCont;
                // Footprint: la unidad nueva no puede sobresalir de la base
                // Si la base tiene dimensiones 0 (no configuradas) se omite esa verificación
                bool cabeLargo = baseStack.LargoCm <= 0 || pallet.LargoCm <= baseStack.LargoCm;
                bool cabeAncho = baseStack.AnchoCm <= 0 || pallet.AnchoCm <= baseStack.AnchoCm;

                if (cabeAlto && cabeLargo && cabeAncho)
                {
                    stack.Add(pallet);
                    apilado = true;
                    break;
                }
            }

            if (!apilado)
                stacks.Add(new List<Pallet> { pallet });
        }

        return stacks;
    }

    private static List<PosicionPallet> AsignarFilasCentradas(
        List<List<Pallet>> stacksDelLado, LadoContenedor lado, int filasDisponibles, ResultadoBalanceo resultado)
    {
        var posiciones = new List<PosicionPallet>();
        if (stacksDelLado.Count == 0) return posiciones;

        if (stacksDelLado.Count > filasDisponibles)
        {
            resultado.Advertencias.Add(
                $"El lado {lado} requiere {stacksDelLado.Count} posiciones pero solo hay {filasDisponibles} disponibles.");
        }

        var porPeso = stacksDelLado.OrderByDescending(s => s.Sum(p => p.PesoTotalKg)).ToList();
        var ordenFilas = GenerarOrdenDesdeCentro(filasDisponibles);

        double acumulado = 0;
        int cantidad = Math.Min(porPeso.Count, ordenFilas.Count);
        for (int i = 0; i < cantidad; i++)
        {
            double pesoStack = porPeso[i].Sum(p => p.PesoTotalKg);
            acumulado += pesoStack;
            posiciones.Add(new PosicionPallet
            {
                Capas = porPeso[i],
                Lado = lado,
                Fila = ordenFilas[i],
                PesoAcumuladoLadoKg = Math.Round(acumulado, 2)
            });
        }

        return posiciones;
    }

    private static List<int> GenerarOrdenDesdeCentro(int filas)
    {
        double centro = (filas - 1) / 2.0;
        return Enumerable.Range(0, filas)
            .OrderBy(i => Math.Abs(i - centro))
            .ThenBy(i => i)
            .Select(i => i + 1)
            .ToList();
    }
}
