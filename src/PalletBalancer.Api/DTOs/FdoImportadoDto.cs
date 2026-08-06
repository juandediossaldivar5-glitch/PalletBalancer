namespace PalletBalancer.Api.DTOs;

// Resultado del parser PDF — mismos campos que FdoDto, sin guardar en DB.
// El frontend permite editar antes de confirmar con POST /api/fdos.
public class FdoImportadoDto : FdoDto { }
