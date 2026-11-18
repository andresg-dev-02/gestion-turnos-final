namespace GestionTurnos.Api.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiraEn) GenerateToken(
        long idUsuario,
        string correo,
        string rolNombre,
        long? idEmpleado,
        string? nombreCompleto,
        IEnumerable<string> permisos);
}

