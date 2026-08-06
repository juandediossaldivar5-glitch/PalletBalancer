using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PalletBalancer.Api.Data;
using PalletBalancer.Api.DTOs;

namespace PalletBalancer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext   _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.Activo);

        if (usuario is null || !BCrypt.Net.BCrypt.Verify(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        var llave   = Encoding.UTF8.GetBytes(_config["Jwt:Key"]!);
        var expira  = DateTime.UtcNow.AddHours(_config.GetValue<int>("Jwt:ExpiresHours", 8));

        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer:   _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims:
                [
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name,           usuario.Username),
                    new Claim(ClaimTypes.Role,           usuario.Rol)
                ],
                expires:            expira,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(llave),
                    SecurityAlgorithms.HmacSha256)
            ));

        return Ok(new LoginResponse
        {
            Token    = token,
            Username = usuario.Username,
            Rol      = usuario.Rol
        });
    }
}
