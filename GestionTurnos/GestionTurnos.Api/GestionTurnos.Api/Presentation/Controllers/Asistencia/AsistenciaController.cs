using Asp.Versioning;
using GestionTurnos.Api.Application.CQRS.Asistencia;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Presentation.Contracts.Requests.Asistencia;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GestionTurnos.Api.Presentation.Controllers.Asistencia;

/// <summary>
/// Marcación de entrada/salida, liquidación de horas y consultas de asistencia (Secciones 3.4, 3.6 y 6.3 - RF-05, CU-02, CU-05).
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/asistencia")]
public sealed class AsistenciaController : BaseApiController
{
    public AsistenciaController(ISender sender) : base(sender)
    {
    }

    /// <summary>
    /// Registra la entrada del empleado en sesión usando la hora del servidor now() (Sección 3.6 - CU-02).
    /// </summary>
    [HttpPost("mi-entrada")]
    public async Task<IActionResult> RegistrarMiEntrada(
        [FromBody] RegistrarMiEntradaRequest? request,
        CancellationToken cancellationToken)
    {
        var fuente = request?.CodFuenteMarcacion ?? FuentesMarcacionCodigos.Web;
        var result = await Sender.Send(new RegistrarMiEntradaCommand(fuente), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra la salida del empleado en sesión usando now() y liquida las horas en la misma transacción (Sección 3.6 y 6.3).
    /// </summary>
    [HttpPost("mi-salida")]
    public async Task<IActionResult> RegistrarMiSalida(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new RegistrarMiSalidaCommand(), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Obtiene el token rotativo actual (tipo TOTP / Authenticator) para la pantalla tótem o estación física de entrada.
    /// Se refresca dinámicamente cada ciclo (ej. 20s) e invalida los códigos anteriores.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("estacion-qr/token")]
    public async Task<IActionResult> ObtenerTokenEstacionQr(
        [FromQuery] string? estacion,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new ObtenerTokenQrEstacionQuery(estacion), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registra la asistencia (Entrada, Salida o detección automática) escaneando el código QR rotativo de la estación.
    /// Valida vigencia temporal del token (TOTP) y previene adulteraciones o fotos estáticas.
    /// </summary>
    [HttpPost("mi-marcar-qr")]
    public async Task<IActionResult> MarcarAsistenciaPorQr(
        [FromBody] MarcarAsistenciaQrRequest request,
        CancellationToken cancellationToken)
    {
        var command = new MarcarAsistenciaPorQrCommand(
            request.TokenQr,
            request.Accion,
            request.Latitud,
            request.Longitud);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Detalle de horas programadas vs trabajadas por turno desde asistencia.v_horas_por_turno (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/turnos")]
    public async Task<IActionResult> ObtenerMisHorasPorTurno(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMisHorasPorTurnoQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consolidado de horas agrupado por grano ('day', 'week', 'month', 'year') desde asistencia.v_horas_por_turno (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/consolidado")]
    public async Task<IActionResult> ObtenerMiConsolidadoHoras(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        [FromQuery] string grano = "week",
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMiConsolidadoHorasQuery(desde, hasta, grano), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Suma de horas por categoría (ordinaria, extra, recargos) desde asistencia.detalle_horas (Sección 3.4).
    /// </summary>
    [HttpGet("mis-horas/categorias")]
    public async Task<IActionResult> ObtenerMisHorasPorCategoria(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerMisHorasPorCategoriaQuery(desde, hasta), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Panel del día (sección 10): programado contra real, tardanza y turnos sin asistencia, con filtro por área.
    /// Sin fecha usa el día actual en la zona horaria de la empresa (permiso: asistencia.consultar).
    /// </summary>
    [HttpGet("panel-dia")]
    public async Task<IActionResult> ObtenerPanelDia(
        [FromQuery] DateOnly? fecha,
        [FromQuery] int? idArea,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new ObtenerPanelAsistenciaDiaQuery(fecha, idArea), cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Consulta administrativa de los registros de asistencia de todos los empleados (permiso: asistencia.consultar).
    /// </summary>
    [HttpGet("registros")]
    public async Task<IActionResult> ListarRegistrosAdmin(
        [FromQuery] DateTimeOffset desde,
        [FromQuery] DateTimeOffset hasta,
        [FromQuery] long? idEmpleado,
        [FromQuery] int? idArea,
        [FromQuery] string? fuente,
        [FromQuery] bool? soloAbiertos,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ListarRegistrosAsistenciaAdminQuery(
            desde,
            hasta,
            idEmpleado,
            idArea,
            fuente,
            soloAbiertos,
            pageNumber,
            pageSize);

        var result = await Sender.Send(query, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Registro manual de asistencia por el administrador con reliquidación automática (Sección 3.6 - permiso: asistencia.registrar_manual).
    /// </summary>
    [HttpPost("manual")]
    public async Task<IActionResult> RegistrarManual(
        [FromBody] RegistrarAsistenciaManualRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarOCorregirAsistenciaManualCommand(
            null,
            request.IdEmpleado,
            request.IdAsignacion,
            request.HoraEntrada,
            request.HoraSalida,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Corrección manual de un registro de asistencia existente con reliquidación automática (Sección 3.6 - permiso: asistencia.registrar_manual).
    /// </summary>
    [HttpPut("manual/{idRegistro:long}")]
    public async Task<IActionResult> CorregirManual(
        long idRegistro,
        [FromBody] RegistrarAsistenciaManualRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegistrarOCorregirAsistenciaManualCommand(
            idRegistro,
            request.IdEmpleado,
            request.IdAsignacion,
            request.HoraEntrada,
            request.HoraSalida,
            request.Observacion);

        var result = await Sender.Send(command, cancellationToken);
        return HandleResult(result);
    }
}

