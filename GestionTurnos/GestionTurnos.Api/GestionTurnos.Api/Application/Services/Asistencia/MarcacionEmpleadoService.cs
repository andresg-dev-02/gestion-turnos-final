using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Interfaces;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Persistence;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.Services.Asistencia;

public enum AccionMarcacion
{
    /// <summary>Entrada si no hay jornada abierta; salida si la hay.</summary>
    Auto,
    Entrada,
    Salida
}

public sealed record ResultadoMarcacion(
    long IdRegistro,
    string TipoAccion,
    DateTimeOffset Hora,
    string HoraLocal,
    string? Aviso);

/// <summary>
/// Reglas comunes del marcaje del propio empleado (web, móvil y QR):
/// <list type="bullet">
/// <item>Bloquea la fila del empleado: dos marcaciones simultáneas (doble escaneo) se procesan en serie.</item>
/// <item>Jornada vencida: una entrada sin salida que superó el fin de su turno + margen (o la duración
///   máxima si no tenía turno) se cierra como "salida no registrada" para revisión del administrador,
///   en lugar de tomar la marcación de hoy como su salida.</item>
/// <item>Intervalo mínimo entre entrada y salida para evitar que un segundo escaneo accidental cierre la jornada.</item>
/// </list>
/// </summary>
public sealed class MarcacionEmpleadoService
{
    public const string TipoEntrada = "ENTRADA";
    public const string TipoSalida = "SALIDA";

    private const int LongitudMaximaObservacion = 300;

    private readonly GestionTurnosContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILiquidacionHorasCalculator _liquidacionCalculator;
    private readonly IConfiguration _configuration;

    public MarcacionEmpleadoService(
        GestionTurnosContext context,
        IUnitOfWork unitOfWork,
        ILiquidacionHorasCalculator liquidacionCalculator,
        IConfiguration configuration)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _liquidacionCalculator = liquidacionCalculator;
        _configuration = configuration;
    }

    private int MinutosMinimosEntreMarcaciones =>
        Math.Max(0, _configuration.GetValue("AppSettings:MinutosMinimosEntreMarcaciones", 5));

    private int HorasMargenCierreTurno =>
        Math.Max(1, _configuration.GetValue("AppSettings:HorasMargenCierreTurno", 4));

    private int HorasMaximasJornadaSinTurno =>
        Math.Max(1, _configuration.GetValue("AppSettings:HorasMaximasJornadaSinTurno", 16));

    public async Task<Result<ResultadoMarcacion>> MarcarAsync(
        long idEmpleado,
        AccionMarcacion accion,
        string codFuenteMarcacion,
        string? observacion,
        CancellationToken cancellationToken)
    {
        var zona = TimeZoneHelper.ResolveEmpresaTimeZone(_configuration);
        var ahora = DateTimeOffset.UtcNow;

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Serializa las marcaciones del empleado (doble escaneo, dos pestañas, reintentos de red).
            await _context.BloquearPorIdAsync<Empleado>(idEmpleado, cancellationToken);

            var abierto = await _context.RegistrosAsistencia
                .Include(r => r.Asignacion)
                .Where(r => r.IdEmpleado == idEmpleado && r.HoraSalida == null && !r.SalidaNoRegistrada)
                .OrderByDescending(r => r.HoraEntrada)
                .FirstOrDefaultAsync(cancellationToken);

            string? aviso = null;
            if (abierto is not null && JornadaVencida(abierto, ahora))
            {
                aviso = $"Tu entrada del {Local(abierto.HoraEntrada, zona):dd/MM/yyyy hh:mm tt} quedó sin salida. " +
                        "Se cerró como \"salida no registrada\" y el administrador la revisará.";
                abierto.SalidaNoRegistrada = true;
                abierto.Observacion = Anexar(abierto.Observacion,
                    $"Salida no registrada: cerrada automáticamente el {Local(ahora, zona):dd/MM/yyyy hh:mm tt}");
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                abierto = null;
            }

            bool marcarSalida = accion == AccionMarcacion.Salida || (accion == AccionMarcacion.Auto && abierto is not null);

            Result<ResultadoMarcacion> resultado = marcarSalida
                ? await RegistrarSalidaAsync(abierto, ahora, zona, observacion, aviso, cancellationToken)
                : await RegistrarEntradaAsync(idEmpleado, abierto, ahora, zona, codFuenteMarcacion, observacion, aviso, cancellationToken);

            // El cierre de una jornada vencida se conserva aunque la marcación pedida falle.
            if (resultado.IsSuccess || aviso is not null)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            else
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result.Failure<ResultadoMarcacion>(DatabaseErrorTranslator.Translate(ex));
        }
    }

    private async Task<Result<ResultadoMarcacion>> RegistrarSalidaAsync(
        RegistroAsistencia? abierto,
        DateTimeOffset ahora,
        TimeZoneInfo zona,
        string? observacion,
        string? aviso,
        CancellationToken cancellationToken)
    {
        if (abierto is null)
        {
            return Result.Failure<ResultadoMarcacion>(new Error(
                "Asistencia.SinEntradaAbierta",
                aviso is null
                    ? "No tienes un registro de entrada pendiente por cerrar."
                    : $"{aviso} Registra una nueva entrada cuando inicies tu turno.",
                ErrorType.NotFound));
        }

        var habilitadaDesde = abierto.HoraEntrada.AddMinutes(MinutosMinimosEntreMarcaciones);
        if (ahora < habilitadaDesde)
        {
            return Result.Failure<ResultadoMarcacion>(new Error(
                "Asistencia.MarcacionMuyCercana",
                $"Ya registraste tu entrada a las {Local(abierto.HoraEntrada, zona):hh:mm tt}. " +
                $"Para evitar marcaciones dobles, la salida se habilita a partir de las {Local(habilitadaDesde, zona):hh:mm tt}.",
                ErrorType.Conflict));
        }

        abierto.HoraSalida = ahora;
        if (!string.IsNullOrWhiteSpace(observacion))
        {
            abierto.Observacion = Anexar(abierto.Observacion, observacion);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _liquidacionCalculator.LiquidarRegistroAsync(abierto.IdRegistro, cancellationToken);

        return Result.Success(new ResultadoMarcacion(abierto.IdRegistro, TipoSalida, ahora, Local(ahora, zona).ToString("hh:mm tt"), aviso));
    }

    private async Task<Result<ResultadoMarcacion>> RegistrarEntradaAsync(
        long idEmpleado,
        RegistroAsistencia? abierto,
        DateTimeOffset ahora,
        TimeZoneInfo zona,
        string codFuenteMarcacion,
        string? observacion,
        string? aviso,
        CancellationToken cancellationToken)
    {
        if (abierto is not null)
        {
            return Result.Failure<ResultadoMarcacion>(new Error(
                "Asistencia.RegistroEnCurso",
                $"Ya tienes una entrada en curso desde las {Local(abierto.HoraEntrada, zona):hh:mm tt}. Marca salida primero.",
                ErrorType.Conflict));
        }

        var fechaHoy = DateOnly.FromDateTime(ahora.UtcDateTime);

        // Turno que corresponde: ventana [inicio - 60 min, fin) y sin registro de asistencia.
        var candidatos = await _context.Asignaciones
            .AsNoTracking()
            .Where(a => a.IdEmpleado == idEmpleado &&
                        a.CodEstadoAsignacion != EstadosAsignacion.Anulada &&
                        a.Fecha >= fechaHoy.AddDays(-1) &&
                        a.Fecha <= fechaHoy.AddDays(1) &&
                        !_context.RegistrosAsistencia.Any(r => r.IdAsignacion == a.IdAsignacion))
            .ToListAsync(cancellationToken);

        var turno = candidatos
            .Where(a => ahora >= a.Periodo.Inicio.AddMinutes(-60) && ahora < a.Periodo.Fin)
            .OrderBy(a => a.Periodo.Inicio)
            .FirstOrDefault();

        if (turno is null && !_configuration.GetValue("AppSettings:PermitirAsistenciaSinTurno", false))
        {
            return Result.Failure<ResultadoMarcacion>(new Error(
                "Asistencia.SinTurnoProgramado",
                aviso is null
                    ? "No tienes un turno programado vigente para registrar entrada en este horario."
                    : $"{aviso} No tienes un turno programado vigente para registrar entrada en este horario.",
                ErrorType.Conflict));
        }

        var registro = new RegistroAsistencia
        {
            IdEmpleado = idEmpleado,
            IdAsignacion = turno?.IdAsignacion,
            HoraEntrada = ahora,
            CodFuenteMarcacion = codFuenteMarcacion.Trim().ToUpperInvariant(),
            Observacion = string.IsNullOrWhiteSpace(observacion) ? null : Recortar(observacion),
            CreadoEn = ahora
        };

        await _context.RegistrosAsistencia.AddAsync(registro, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ResultadoMarcacion(registro.IdRegistro, TipoEntrada, ahora, Local(ahora, zona).ToString("hh:mm tt"), aviso));
    }

    private bool JornadaVencida(RegistroAsistencia registro, DateTimeOffset ahora)
    {
        var limite = registro.Asignacion is not null
            ? registro.Asignacion.Periodo.Fin.AddHours(HorasMargenCierreTurno)
            : registro.HoraEntrada.AddHours(HorasMaximasJornadaSinTurno);

        return ahora > limite;
    }

    private static DateTimeOffset Local(DateTimeOffset instante, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTime(instante, zona);

    private static string Anexar(string? actual, string nuevo) =>
        Recortar(string.IsNullOrWhiteSpace(actual) ? nuevo : $"{actual} | {nuevo}");

    private static string Recortar(string texto) =>
        texto.Length <= LongitudMaximaObservacion ? texto : texto[..LongitudMaximaObservacion];
}
