using GestionTurnos.Api.Application.CQRS.Turnos;

namespace GestionTurnos.Api.Presentation.Contracts.Requests.Turnos;

public sealed record GuardarTurnoRequest(
    string Nombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int MinutosDescanso,
    string? Color,
    bool Activo = true);

public sealed record ProgramarTurnosRequest(
    IReadOnlyList<ItemProgramacionTurnoDto> Asignaciones);

public sealed record ModificarAsignacionTurnoRequest(
    int IdTurno,
    DateOnly Fecha,
    string? Observacion);

public sealed record AnularAsignacionTurnoRequest(
    string? Observacion);

public sealed record PublicarAsignacionesTurnoRequest(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    int? IdArea = null,
    long? IdEmpleado = null);

public sealed record ProgramarDiaDescansoRequest(
    long IdEmpleado,
    DateOnly Fecha);

public sealed record SolicitarCambioTurnoRequest(
    long IdAsignacionSolicitante,
    long IdAsignacionCompanero,
    string? Motivo);

public sealed record ResolverCambioTurnoRequest(
    bool Aprobar,
    string? MotivoRechazo = null);

