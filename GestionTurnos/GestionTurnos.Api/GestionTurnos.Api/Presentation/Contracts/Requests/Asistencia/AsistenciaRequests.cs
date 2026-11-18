using GestionTurnos.Api.Domain.Enums;

namespace GestionTurnos.Api.Presentation.Contracts.Requests.Asistencia;

public sealed record RegistrarMiEntradaRequest(
    string CodFuenteMarcacion = FuentesMarcacionCodigos.Web);

public sealed record RegistrarAsistenciaManualRequest(
    long IdEmpleado,
    long? IdAsignacion,
    DateTimeOffset HoraEntrada,
    DateTimeOffset? HoraSalida,
    string Observacion);

public sealed record MarcarAsistenciaQrRequest(
    string TokenQr,
    string Accion = "AUTO",
    double? Latitud = null,
    double? Longitud = null);

