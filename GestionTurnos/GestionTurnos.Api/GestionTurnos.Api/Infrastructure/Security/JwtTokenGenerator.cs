using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GestionTurnos.Api.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTimeOffset ExpiraEn) GenerateToken(
        long idUsuario,
        string correo,
        string rolNombre,
        long? idEmpleado,
        string? nombreCompleto,
        IEnumerable<string> permisos)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["Key"] ?? "ClaveSeguraTemporalParaTokensJwt2026!GestionTurnos";
        var issuer = jwtSettings["Issuer"] ?? "GestionTurnosApi";
        var audience = jwtSettings["Audience"] ?? "AngularApp";
        int expirationMinutes = int.TryParse(jwtSettings["AccessTokenExpirationMinutes"], out int parsedMin)
            ? parsedMin
            : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, idUsuario.ToString()),
            new("usuario_id", idUsuario.ToString()),
            new(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new(ClaimTypes.Email, correo),
            new(ClaimTypes.Role, rolNombre),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(nombreCompleto))
        {
            claims.Add(new Claim(ClaimTypes.Name, nombreCompleto));
        }

        if (idEmpleado.HasValue)
        {
            claims.Add(new Claim("empleado_id", idEmpleado.Value.ToString()));
        }

        foreach (var permiso in permisos.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permiso", permiso));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiresAt);
    }
}

