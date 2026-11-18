namespace GestionTurnos.Api.Application.DTOs.Asistencia;

public sealed record RegistroAsistenciaDto(
    long IdRegistro,
    long IdEmpleado,
    string NombreEmpleado,
    long? IdAsignacion,
    DateTimeOffset HoraEntrada,
    DateTimeOffset? HoraSalida,
    string CodFuenteMarcacion,
    string NombreFuenteMarcacion,
    string? Observacion,
    long? RegistradoPor,
    DateTimeOffset CreadoEn);

public sealed record HorasPorTurnoEmpleadoDto(
    long IdAsignacion,
    DateOnly Fecha,
    int IdTurno,
    string NombreTurno,
    int MinutosProgramados,
    int MinutosTrabajados,
    int DiferenciaMinutos,
    DateTimeOffset? HoraEntrada,
    DateTimeOffset? HoraSalida,
    int? MinutosTarde,
    bool SinAsistencia,
    string? Novedad = null,
    bool EsJustificado = false);

public sealed record ConsolidadoHorasEmpleadoDto(
    DateOnly Periodo,
    int MinutosProgramados,
    int MinutosTrabajados,
    int Diferencia,
    int TurnosSinAsistencia);

public sealed record HorasPorCategoriaDto(
    string CodCategoriaHora,
    string NombreCategoria,
    decimal FactorPago,
    int TotalMinutos,
    decimal TotalHoras);

/// <summary>
/// Fila del panel de asistencia del día: programado contra real (sección 10).
/// </summary>
public sealed record PanelAsistenciaTurnoDto(
    long IdAsignacion,
    long IdEmpleado,
    string NombreEmpleado,
    int IdArea,
    string NombreArea,
    int IdTurno,
    string NombreTurno,
    DateTimeOffset ProgramadoInicio,
    DateTimeOffset ProgramadoFin,
    long? IdRegistro,
    DateTimeOffset? HoraEntrada,
    DateTimeOffset? HoraSalida,
    int MinutosTarde,
    bool Tarde,
    string Estado,
    string? NovedadQueExplica);

/// <summary>
/// Panel de asistencia del día (sección 10): resumen y detalle por turno.
/// </summary>
public sealed record PanelAsistenciaDiaDto(
    DateOnly Fecha,
    int ToleranciaEntradaMinutos,
    int TurnosProgramados,
    int ConAsistencia,
    int EnCurso,
    int Tardanzas,
    int SinAsistencia,
    IReadOnlyList<PanelAsistenciaTurnoDto> Turnos);

/// <summary>
/// Resultado del marcaje de asistencia realizado mediante escaneo de código QR de estación.
/// </summary>
public sealed record ResultadoMarcacionQrDto(
    long IdRegistro,
    string TipoAccion,
    string Mensaje,
    DateTimeOffset Hora,
    string CodigoEstacion);
