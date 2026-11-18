using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Organizacion;
using GestionTurnos.Api.Presentation.Contracts.Requests.Organizacion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Organizacion;

/// <summary>
/// Catálogos de estructura organizacional: Sucursales, Áreas y Cargos (Sección 5 - RF-02).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/organizacion")]
public sealed class OrganizacionController : BaseApiController
{
    public OrganizacionController(ISender sender) : base(sender)
    {
    }

    [HttpGet("sucursales")]
    public async Task<IActionResult> ListarSucursales(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarSucursalesQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("sucursales")]
    public async Task<IActionResult> CrearSucursal(
        [FromBody] GuardarSucursalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarSucursalCommand(null, request.Nombre, request.Direccion, request.Ciudad, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("sucursales/{idSucursal:int}")]
    public async Task<IActionResult> ActualizarSucursal(
        int idSucursal,
        [FromBody] GuardarSucursalRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarSucursalCommand(idSucursal, request.Nombre, request.Direccion, request.Ciudad, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("areas")]
    public async Task<IActionResult> ListarAreas(
        [FromQuery] int? idSucursal,
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarAreasQuery(idSucursal, soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("areas")]
    public async Task<IActionResult> CrearArea(
        [FromBody] GuardarAreaRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarAreaCommand(null, request.IdSucursal, request.Nombre, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("areas/{idArea:int}")]
    public async Task<IActionResult> ActualizarArea(
        int idArea,
        [FromBody] GuardarAreaRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarAreaCommand(idArea, request.IdSucursal, request.Nombre, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("cargos")]
    public async Task<IActionResult> ListarCargos(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarCargosQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("cargos")]
    public async Task<IActionResult> CrearCargo(
        [FromBody] GuardarCargoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarCargoCommand(null, request.Nombre, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpPut("cargos/{idCargo:int}")]
    public async Task<IActionResult> ActualizarCargo(
        int idCargo,
        [FromBody] GuardarCargoRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarCargoCommand(idCargo, request.Nombre, request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

