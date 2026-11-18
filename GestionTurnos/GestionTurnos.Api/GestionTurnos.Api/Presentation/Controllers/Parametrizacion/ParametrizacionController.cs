using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Parametrizacion;
using GestionTurnos.Api.Presentation.Contracts.Requests.Parametrizacion;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Parametrizacion;

/// <summary>
/// Catálogos y reglas de negocio configurables con vigencia (Secciones 5 y 6.2 - RG-10).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/parametrizacion")]
public sealed class ParametrizacionController : BaseApiController
{
    public ParametrizacionController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Lista tipos de documento (público para el formulario de solicitud de registro).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("tipos-documento")]
    public async Task<IActionResult> ListarTiposDocumento(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposDocumentoQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("tipos-documento")]
    public async Task<IActionResult> GuardarTipoDocumento(
        [FromBody] GuardarCatalogoSimpleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GuardarTipoDocumentoCommand(request.Codigo, request.Nombre, request.Activo), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("tipos-contrato")]
    public async Task<IActionResult> ListarTiposContrato(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposContratoQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("tipos-contrato")]
    public async Task<IActionResult> GuardarTipoContrato(
        [FromBody] GuardarCatalogoSimpleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GuardarTipoContratoCommand(request.Codigo, request.Nombre, request.Activo), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("tipos-novedad")]
    public async Task<IActionResult> ListarTiposNovedad(
        [FromQuery] bool soloActivos = true,
        [FromQuery] bool soloPermitidosEmpleado = false,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarTiposNovedadQuery(soloActivos, soloPermitidosEmpleado), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("tipos-novedad")]
    public async Task<IActionResult> GuardarTipoNovedad(
        [FromBody] GuardarTipoNovedadRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarTipoNovedadCommand(
            request.CodTipoNovedad,
            request.Nombre,
            request.RemuneradoDefecto,
            request.AfectaHorasDefecto,
            request.RequiereAprobacion,
            request.RequiereSoporte,
            request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("categorias-hora")]
    public async Task<IActionResult> ListarCategoriasHora(
        [FromQuery] bool soloActivos = true,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarCategoriasHoraQuery(soloActivos), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("categorias-hora")]
    public async Task<IActionResult> GuardarCategoriaHora(
        [FromBody] GuardarCategoriaHoraRequest request,
        CancellationToken cancellationToken)
    {
        var command = new GuardarCategoriaHoraCommand(
            request.CodCategoriaHora,
            request.Nombre,
            request.FactorPago,
            request.Activo);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("dias-festivos")]
    public async Task<IActionResult> ListarDiasFestivos(
        [FromQuery] int? anio,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarDiasFestivosQuery(anio), cancellationToken);
        return HandleResult(result);
    }

    [HttpPost("dias-festivos")]
    public async Task<IActionResult> GuardarDiaFestivo(
        [FromBody] GuardarDiaFestivoRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GuardarDiaFestivoCommand(request.Fecha, request.Nombre), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Borra un día festivo (única entidad de negocio que se borra físicamente). Fecha en formato yyyy-MM-dd.
    /// </summary>
    [HttpDelete("dias-festivos/{fecha}")]
    public async Task<IActionResult> EliminarDiaFestivo(
        DateOnly fecha,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new EliminarDiaFestivoCommand(fecha), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("parametros")]
    public async Task<IActionResult> ListarParametros(
        [FromQuery] DateOnly? fechaReferencia,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ListarParametrosQuery(fechaReferencia), cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("parametros/{codParametro}/historial")]
    public async Task<IActionResult> ListarHistorialParametro(
        string codParametro,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ListarHistorialParametroQuery(codParametro), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Cierra el valor vigente de un parámetro editable e inserta la nueva vigencia en una sola transacción (Sección 6.2).
    /// </summary>
    [HttpPost("parametros/{codParametro}/valores")]
    public async Task<IActionResult> CambiarValorParametro(
        string codParametro,
        [FromBody] CambiarValorParametroRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CambiarValorParametroCommand(codParametro, request.Valor, request.VigenteDesde);
        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

