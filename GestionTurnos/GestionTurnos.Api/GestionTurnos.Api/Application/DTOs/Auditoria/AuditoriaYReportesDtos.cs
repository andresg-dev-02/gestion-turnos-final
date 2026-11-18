namespace GestionTurnos.Api.Application.DTOs.Auditoria;

/// <summary>
/// Registro de la bitácora de auditoría de negocio (Sección 5 - RF-08, RG-09).
/// </summary>
public sealed record BitacoraNegocioDto(
    long IdBitacora,
    DateTimeOffset FechaHora,
    long? IdUsuario,
    string? CorreoUsuario,
    string CodTipoAccion,
    string NombreTipoAccion,
    string Esquema,
    string Tabla,
    string IdRegistro,
    long? IdEmpleado,
    string? NombreEmpleado,
    string? ValorAnterior,
    string? ValorNuevo,
    string? Descripcion,
    string? IpOrigen);

/// <summary>
/// Reporte operativo de cumplimiento de turnos y asistencia (Sección 5 - RF-10).
/// </summary>
public sealed record ReporteCumplimientoTurnoDto(
    long IdAsignacion,
    DateOnly Fecha,
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    int IdArea,
    string NombreArea,
    int IdCargo,
    string NombreCargo,
    int IdTurno,
    string NombreTurno,
    int MinutosProgramados,
    int MinutosTrabajados,
    int DiferenciaMinutos,
    DateTimeOffset? HoraEntrada,
    DateTimeOffset? HoraSalida,
    int? MinutosTarde,
    bool SinAsistencia);

/// <summary>
/// Reporte consolidado de horas por empleado (Sección 5 - RF-10).
/// </summary>
public sealed record ReporteConsolidadoEmpleadoDto(
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    int TotalTurnosProgramados,
    int TotalTurnosSinAsistencia,
    int TotalMinutosProgramados,
    int TotalMinutosTrabajados,
    int DiferenciaMinutos,
    int TotalMinutosTarde);


/// <summary>
/// Reporte de horas por categoría (ordinaria, extra, recargos) por empleado (Sección 14).
/// </summary>
public sealed record ReporteHorasCategoriaDto(
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    string CodCategoriaHora,
    string NombreCategoria,
    decimal FactorPago,
    int TotalMinutos,
    decimal TotalHoras);

/// <summary>
/// Reporte de saldo de vacaciones por empleado con los días solicitados dentro del rango (Sección 14).
/// El saldo se lee de novedades.v_saldo_vacaciones.
/// </summary>
public sealed record ReporteSaldoVacacionesDto(
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    string CodEstadoEmpleado,
    decimal DiasOtorgados,
    decimal DiasUtilizados,
    decimal DiasEnSolicitud,
    decimal DiasDisponibles,
    decimal DiasAprobadosEnRango,
    decimal DiasPendientesEnRango);

/// <summary>
/// Reporte de solicitudes de vacaciones que se cruzan con el rango consultado (Sección 14).
/// </summary>
public sealed record ReporteSolicitudVacacionesDto(
    long IdSolicitudVacaciones,
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    decimal DiasSolicitados,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    DateTimeOffset SolicitadoEn,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);

/// <summary>
/// Reporte de permisos, incapacidades y ausencias que se cruzan con el rango consultado (Sección 14).
/// </summary>
public sealed record ReportePermisoAusenciaDto(
    long IdNovedad,
    long IdEmpleado,
    string NumeroDocumento,
    string NombreEmpleado,
    string NombreArea,
    string NombreCargo,
    string CodTipoNovedad,
    string NombreTipoNovedad,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    int DuracionMinutos,
    bool Remunerado,
    bool AfectaHoras,
    string CodEstadoSolicitud,
    string NombreEstadoSolicitud,
    DateTimeOffset RegistradoEn,
    DateTimeOffset? ResueltoEn,
    string? MotivoRechazo);
