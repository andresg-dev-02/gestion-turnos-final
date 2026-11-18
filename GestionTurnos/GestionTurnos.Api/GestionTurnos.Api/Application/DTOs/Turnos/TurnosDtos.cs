namespace GestionTurnos.Api.Application.DTOs.Turnos;

public sealed record TurnoDto(
    int IdTurno,
    string Nombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int MinutosDescanso,
    bool CruzaMedianoche,
    int DuracionMinutos,
    string? Color,
    bool Activo);

public sealed record AsignacionTurnoDto(
    long IdAsignacion,
    long IdEmpleado,
    string NombreEmpleado,
    string NumeroDocumento,
    int IdArea,
    string NombreArea,
    int IdTurno,
    string NombreTurno,
    string? ColorTurno,
    DateOnly Fecha,
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFin,
    string CodEstadoAsignacion,
    string NombreEstadoAsignacion,
    string? Observacion,
    long CreadoPor,
    DateTimeOffset CreadoEn);

public sealed record DiaDescansoDto(
    long IdDiaDescanso,
    long IdEmpleado,
    string NombreEmpleado,
    DateOnly Fecha,
    long CreadoPor,
    DateTimeOffset CreadoEn);

/// <summary>
/// Evento del calendario unificado del empleado (Sección 3.4 - turnos.v_calendario).
/// </summary>
public sealed record EventoCalendarioEmpleadoDto(
    string Tipo,
    string Detalle,
    DateTimeOffset Inicio,
    DateTimeOffset Fin);

/// <summary>
/// Evento del calendario general administrativo (Sección 5 - RF-09).
/// </summary>
public sealed record EventoCalendarioGeneralDto(
    long IdEmpleado,
    string NombreEmpleado,
    int IdArea,
    string NombreArea,
    string Tipo,
    string Detalle,
    DateTimeOffset Inicio,
    DateTimeOffset Fin);

/// <summary>
/// Historial de turnos pasados del empleado (Sección 3.4: fecha &lt; current_date).
/// </summary>
public sealed record HistorialTurnoEmpleadoDto(
    long IdAsignacion,
    DateOnly Fecha,
    int IdTurno,
    string NombreTurno,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int MinutosDescanso,
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFin,
    string CodEstadoAsignacion,
    string? Observacion);

/// <summary>
/// Solicitud de cambio de turno entre compañeros (Sección 3.9 - OPC-03).
/// </summary>
public sealed record SolicitudCambioTurnoDto(
    long IdSolicitudCambio,
    long IdAsignacionSolicitante,
    long IdEmpleadoSolicitante,
    string NombreEmpleadoSolicitante,
    DateOnly FechaTurnoSolicitante,
    string NombreTurnoSolicitante,
    long IdAsignacionCompanero,
    long IdEmpleadoCompanero,
    string NombreEmpleadoCompanero,
    DateOnly FechaTurnoCompanero,
    string NombreTurnoCompanero,
    string? Motivo,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    DateTimeOffset SolicitadoEn,
    long? ResueltoPor,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);


/// <summary>
/// Conflicto encontrado al revisar una asignación (sección 9.1).
/// <c>Bloquea</c> es siempre verdadero para cruces, vacaciones, novedades y descansos; para descanso mínimo
/// y jornada máxima depende del parámetro CONFLICTO_BLOQUEA (si es falso, es solo una advertencia).
/// </summary>
public sealed record ConflictoTurnoDto(
    string Tipo,
    string Mensaje,
    bool Bloquea);

/// <summary>
/// Resultado de la revisión previa de conflictos de una asignación (sección 9.1).
/// </summary>
public sealed record RevisionConflictosTurnoDto(
    long IdEmpleado,
    int IdTurno,
    string NombreTurno,
    DateOnly Fecha,
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFin,
    bool PuedeAsignarse,
    IReadOnlyList<ConflictoTurnoDto> Conflictos);

/// <summary>
/// Turno y periodo de una asignación, para mostrar el cambio actual contra nuevo (sección 9.4).
/// </summary>
public sealed record DatosAsignacionDto(
    int IdTurno,
    string NombreTurno,
    DateOnly Fecha,
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFin);

/// <summary>
/// Vista previa de una modificación: asignación actual, propuesta y conflictos de la propuesta (sección 9.4).
/// </summary>
public sealed record PrevisualizacionModificacionAsignacionDto(
    long IdAsignacion,
    long IdEmpleado,
    DatosAsignacionDto Actual,
    DatosAsignacionDto Nuevo,
    bool PuedeModificarse,
    IReadOnlyList<ConflictoTurnoDto> Conflictos);
