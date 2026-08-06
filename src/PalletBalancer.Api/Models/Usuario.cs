using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(10)]
    public string Rol { get; set; } = string.Empty;  // OPE | MKT | SV | AMG | ADM

    public bool Activo { get; set; } = true;
}
