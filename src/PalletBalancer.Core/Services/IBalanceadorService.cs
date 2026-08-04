using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Services;

public interface IBalanceadorService
{
    ResultadoBalanceo Balancear(IEnumerable<Pallet> pallets, ConfiguracionCarga config);
}
