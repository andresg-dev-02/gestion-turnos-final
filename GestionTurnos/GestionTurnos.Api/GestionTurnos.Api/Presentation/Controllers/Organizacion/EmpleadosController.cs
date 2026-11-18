using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Organizacion;
using GestionTurnos.Api.Presentation.Contracts.Requests.Organizacion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Organizacion;

/// <summary>
/// Gestión administrativa de empleados (Sección 5 - RF-02, RG-06 - permiso: empleados.gestionar).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/empleados")]
public sealed class EmpleadosController : BaseApiController
{
    public EmpleadosController(ISender sender) : base(sender)
    {
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? idArea,
        [FromQuery] int? idSucursal,
        [FromQuery] int? idCargo,
        [FromQuery] string? estado,
        [FromQuery] string? busqueda,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarEmpleadosQuery(idArea, idSucursal, idCargo, estado, busqueda, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("{idEmpleado:long}")]
    public async Task<IActionResult> ObtenerPorId(long idEmpleado, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerEmpleadoPorIdQuery(idEmpleado), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] CrearEmpleadoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CrearEmpleadoCommand(
            request.CodTipoDocumento,
            request.NumeroDocumento,
            request.Nombres,
            request.Apellidos,
            request.CorreoContacto,
            request.Telefono,
            request.Direccion,
            request.IdArea,
            request.IdCargo,
            request.CodTipoContrato,
            request.FechaIngreso,
            request.IdUsuarioExistente,
            request.CorreoAccesoNuevoUsuario,
            request.ContrasenaNuevoUsuario);

        var result = await Sender.Send(command, cancellationToken);
        return HandleCreatedResult(result, nameof(ObtenerPorId), new { idEmpleado = result.IsSuccess ? result.Value : 0 });
    }

    [HttpPut("{idEmpleado:long}")]
    public async Task<IActionResult> Actualizar(
        long idEmpleado,
        [FromBody] ActualizarEmpleadoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ActualizarEmpleadoCommand(
            idEmpleado,
            request.CodTipoDocumento,
            request.NumeroDocumento,
            request.Nombres,
            request.Apellidos,
            request.CorreoContacto,
            request.Telefono,
            request.Direccion,
            request.IdArea,
            request.IdCargo,
            request.CodTipoContrato,
            request.FechaIngreso);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Desactiva un empleado fijando cod_estado_empleado = 'INACTIVO', fecha_retiro y, en la misma transacción,
    /// usuario.cod_estado_usuario = 'INACTIVO'. Nunca borra físicamente (Sección 5 - RF-02, RG-06).
    /// </summary>
    [HttpPost("{idEmpleado:long}/desactivar")]
    public async Task<IActionResult> Desactivar(
        long idEmpleado,
        [FromBody] DesactivarEmpleadoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DesactivarEmpleadoCommand(idEmpleado, request.FechaRetiro);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Reactiva un empleado y su cuenta de usuario en la misma transacción (permiso: empleados.gestionar).
    /// </summary>
    [HttpPost("{idEmpleado:long}/reactivar")]
    public async Task<IActionResult> Reactivar(
        long idEmpleado,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ReactivarEmpleadoCommand(idEmpleado), cancellationToken);
        return HandleResult(result);
    }
}

