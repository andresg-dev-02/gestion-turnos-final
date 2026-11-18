using System.Globalization;

namespace GestionTurnos.Api.Application.Notificaciones;

/// <summary>
/// Nombres de las plantillas embebidas en Infrastructure/Templates/Email/{nombre}.html.
/// </summary>
public static class PlantillasCorreo
{
    public const string CodigoVerificacion = "codigo_verificacion";
    public const string AvisoSeguridad = "aviso_seguridad";
    public const string RegistroAprobado = "registro_aprobado";
    public const string RegistroRechazado = "registro_rechazado";
    public const string TurnosPublicados = "turnos_publicados";
    public const string TurnoModificado = "turno_modificado";
    public const string TurnoAnulado = "turno_anulado";
    public const string SolicitudResuelta = "solicitud_resuelta";
    public const string ReporteSemanal = "reporte_semanal";
}

// ---------------------------------------------------------------------
// Modelos de plantilla. Solo texto ya formateado: las plantillas no hacen
// cálculos ni dependen de la cultura del servidor.
// ---------------------------------------------------------------------

public sealed record CodigoVerificacionModelo(string Nombre, string Titulo, string Mensaje, string Codigo, int MinutosVigencia);

public sealed record AvisoSeguridadModelo(string Nombre, string Titulo, string Mensaje);

public sealed record RegistroAprobadoModelo(string Nombre, string Correo, string Area, string Cargo, string FechaIngreso);

public sealed record RegistroRechazadoModelo(string Nombre, string Motivo);

public sealed record TurnoCorreoItem(string Fecha, string Turno, string Horario);

public sealed record TurnosPublicadosModelo(string Nombre, string Desde, string Hasta, IReadOnlyList<TurnoCorreoItem> Turnos);

public sealed record TurnoModificadoModelo(string Nombre, TurnoCorreoItem Anterior, TurnoCorreoItem Nuevo);

public sealed record TurnoAnuladoModelo(string Nombre, TurnoCorreoItem Turno, string Motivo);

/// <summary>Par etiqueta/valor mostrado como ficha de datos en el correo.</summary>
public sealed record DetalleCorreoItem(string Etiqueta, string Valor);

/// <summary>
/// Resultado de una solicitud (vacaciones, permiso, novedad, cambio de turno) o anulación posterior.
/// <paramref name="Positivo"/> solo decide el color del resultado (verde o rojo).
/// </summary>
public sealed record SolicitudResueltaModelo(
    string Nombre,
    string Mensaje,
    string Resultado,
    bool Positivo,
    IReadOnlyList<DetalleCorreoItem> Detalles,
    string? EtiquetaMotivo,
    string? Motivo,
    string? Nota);

public sealed record ReporteSemanalDiaItem(
    string Fecha,
    string Turno,
    string Horario,
    string Entrada,
    string Salida,
    string Trabajado,
    string Estado,
    bool Alerta);

public sealed record ReporteSemanalModelo(
    string Nombre,
    string Desde,
    string Hasta,
    int TurnosProgramados,
    int TurnosAsistidos,
    int Faltas,
    int FaltasJustificadas,
    int LlegadasTarde,
    string HorasProgramadas,
    string HorasTrabajadas,
    string TiempoTarde,
    IReadOnlyList<ReporteSemanalDiaItem> Dias,
    IReadOnlyList<DetalleCorreoItem> HorasPorCategoria,
    IReadOnlyList<DetalleCorreoItem> Novedades);

/// <summary>
/// Formatos de fecha y hora para correos (español de Colombia, independiente de la cultura del servidor).
/// </summary>
public static class FormatoCorreo
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static string Fecha(DateOnly fecha) => fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);

    public static string Horario(TimeOnly inicio, TimeOnly fin, bool cruzaMedianoche) =>
        $"{inicio:HH\\:mm} – {fin:HH\\:mm}{(cruzaMedianoche ? " (termina al día siguiente)" : string.Empty)}";

    public static string FechaCorta(DateOnly fecha) => fecha.ToString("ddd dd/MM/yyyy", Cultura);

    /// <summary>Fecha y hora de un instante en la zona horaria de la empresa.</summary>
    public static string FechaHora(DateTimeOffset instante, TimeZoneInfo zona) =>
        TimeZoneInfo.ConvertTime(instante, zona).ToString("dddd d 'de' MMMM 'de' yyyy, HH:mm", Cultura);

    public static string Hora(DateTimeOffset? instante, TimeZoneInfo zona) =>
        instante is null ? "—" : TimeZoneInfo.ConvertTime(instante.Value, zona).ToString("HH:mm", Cultura);

    /// <summary>Duración legible: "7 h 30 min", "45 min", "0 h".</summary>
    public static string Duracion(int minutos)
    {
        var signo = minutos < 0 ? "-" : string.Empty;
        minutos = Math.Abs(minutos);
        int h = minutos / 60, m = minutos % 60;
        return h == 0 && m > 0 ? $"{signo}{m} min" : m == 0 ? $"{signo}{h} h" : $"{signo}{h} h {m} min";
    }

    public static string Dias(decimal dias) =>
        dias == 1 ? "1 día" : $"{dias.ToString("0.##", Cultura)} días";

    public static TurnoCorreoItem Turno(DateOnly fecha, string nombreTurno, TimeOnly inicio, TimeOnly fin, bool cruzaMedianoche) =>
        new(Fecha(fecha), nombreTurno, Horario(inicio, fin, cruzaMedianoche));
}
