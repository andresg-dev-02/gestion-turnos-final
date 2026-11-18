namespace GestionTurnos.Api.Presentation.Contracts.Requests.Novedades;

public sealed record SolicitarMisVacacionesRequest(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion);

public sealed record RegistrarPeriodoVacacionalRequest(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal? DiasOtorgados);

public sealed record ResolverSolicitudRequest(
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false);

public sealed record SolicitarMiNovedadRequest(
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string? Observaciones,
    string? UrlSoporte);

public sealed record RegistrarNovedadAdminRequest(
    long IdEmpleado,
    string CodTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool? Remunerado,
    bool? AfectaHoras,
    string? Observaciones,
    string? UrlSoporte,
    bool ConfirmarAnulacionTurnos = false);

/// <summary>
/// Resolución de una novedad (sección 12). Al aprobar se puede ajustar si es remunerada y si afecta horas.
/// </summary>
public sealed record ResolverNovedadRequest(
    bool Aprobar,
    string? MotivoRechazo = null,
    bool ConfirmarAnulacionTurnos = false,
    bool? Remunerado = null,
    bool? AfectaHoras = null);

/// <summary>
/// Solicitud de vacaciones creada por el administrador a nombre del empleado (sección 11).
/// </summary>
public sealed record RegistrarSolicitudVacacionesAdminRequest(
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    string? Observacion);

/// <summary>
/// Anulación de vacaciones aprobadas que todavía no inician (sección 11).
/// </summary>
public sealed record AnularVacacionesRequest(
    string Motivo);
