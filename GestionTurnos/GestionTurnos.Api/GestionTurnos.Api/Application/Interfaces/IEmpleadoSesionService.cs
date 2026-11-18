using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Resuelve el id_empleado activo asociado al id_usuario de la sesión actual desde la base de datos (Sección 2 - RG-08).
/// Regla de oro: el id_empleado del empleado nunca llega del cliente.
/// </summary>
public interface IEmpleadoSesionService
{
    Task<Result<long>> ObtenerIdEmpleadoActivoAsync(CancellationToken cancellationToken = default);
}

