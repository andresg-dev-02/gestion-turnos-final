namespace GestionTurnos.Api.Application.DTOs.Novedades;

/// <summary>
/// Saldo de vacaciones desde novedades.v_saldo_vacaciones (Sección 3.4 - RF-06).
/// </summary>
public sealed record SaldoVacacionesDto(
    long IdEmpleado,
    decimal DiasOtorgados,
    decimal DiasUtilizados,
    decimal DiasEnSolicitud,
    decimal DiasDisponibles,
    decimal DiasDisponiblesParaNuevaSolicitud);

public sealed record ResultadoSolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    decimal DiasSolicitados,
    IReadOnlyList<DateOnly> FechasTurnosEnConflicto,
    string? AdvertenciaConflictos);

public sealed record SolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasSolicitados,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    string? Observacion,
    long SolicitadoPor,
    DateTimeOffset SolicitadoEn,
    long? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);

public sealed record PeriodoVacacionalDto(
    long IdPeriodoVacacional,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasOtorgados);

public sealed record NovedadDto(
    long IdNovedad,
    long IdEmpleado,
    string NombreEmpleado,
    string CodTipoNovedad,
    string NombreTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    bool Remunerado,
    bool AfectaHoras,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    string? Observaciones,
    string? UrlSoporte,
    long RegistradoPor,
    DateTimeOffset RegistradoEn,
    long? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);

/// <summary>
/// Turno programado que choca con unas vacaciones o una novedad (secciones 11 y 12).
/// Un turno con asistencia impide la aprobación.
/// </summary>
public sealed record TurnoEnConflictoDto(
    long IdAsignacion,
    DateOnly Fecha,
    int IdTurno,
    string NombreTurno,
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFin,
    string CodEstadoAsignacion,
    bool TieneAsistencia);

/// <summary>
/// Resultado de la consulta de turnos que chocan con una solicitud de vacaciones (sección 11).
/// </summary>
public sealed record ConflictosSolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    long IdEmpleado,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool ConflictoBloquea,
    IReadOnlyList<TurnoEnConflictoDto> Turnos);

/// <summary>
/// Solicitud de vacaciones en la bandeja del administrador, con el saldo del empleado (sección 11).
/// El saldo se lee siempre de novedades.v_saldo_vacaciones; nunca se guarda.
/// </summary>
public sealed record SolicitudVacacionesBandejaDto(
    SolicitudVacacionesDto Solicitud,
    decimal DiasDisponibles,
    decimal DiasEnSolicitud,
    decimal SaldoEfectivo);
