using System.Globalization;

namespace GestionTurnos.Api.Domain.ValueObjects;

/// <summary>
/// Representa un rango de tiempo con zona horaria equivalente a tstzrange '[inicio, fin)' de la base de datos.
/// </summary>
public readonly record struct PeriodoRango : IEquatable<PeriodoRango>
{
    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fin { get; }

    public PeriodoRango(DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (fin <= inicio)
        {
            throw new ArgumentException("La fecha/hora de fin debe ser posterior a la de inicio.", nameof(fin));
        }

        Inicio = inicio;
        Fin = fin;
    }

    public int DuracionMinutos => (int)Math.Round((Fin - Inicio).TotalMinutes);

    public bool SeCruzaCon(PeriodoRango otro) => Inicio < otro.Fin && otro.Inicio < Fin;

    public bool SeCruzaCon(DateTimeOffset otroInicio, DateTimeOffset otroFin) =>
        Inicio < otroFin && otroInicio < Fin;

    /// <summary>
    /// Construye el PeriodoRango a partir de una fecha, hora de inicio, hora de fin, indicador de cruce de medianoche y desplazamiento horario.
    /// Equivalente a la expresión SQL de la sección 6.1 (RG-05).
    /// </summary>
    public static PeriodoRango DesdeTurno(
        DateOnly fecha,
        TimeOnly horaInicio,
        TimeOnly horaFin,
        bool cruzaMedianoche,
        TimeSpan offsetZonaHoraria)
    {
        var inicioLocal = fecha.ToDateTime(horaInicio, DateTimeKind.Unspecified);
        var fechaFin = cruzaMedianoche || horaFin <= horaInicio ? fecha.AddDays(1) : fecha;
        var finLocal = fechaFin.ToDateTime(horaFin, DateTimeKind.Unspecified);

        return new PeriodoRango(
            new DateTimeOffset(inicioLocal, offsetZonaHoraria),
            new DateTimeOffset(finLocal, offsetZonaHoraria));
    }

    /// <summary>
    /// Formato literal de rango '[inicio,fin)' compatible con tstzrange.
    /// </summary>
    public override string ToString() =>
        $"[\"{Inicio.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}\",\"{Fin.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}\")";

    public static PeriodoRango Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new FormatException("El valor de periodo tstzrange está vacío.");
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('[') || trimmed.StartsWith('('))
        {
            trimmed = trimmed[1..];
        }
        if (trimmed.EndsWith(')') || trimmed.EndsWith(']'))
        {
            trimmed = trimmed[..^1];
        }

        var parts = trimmed.Split(',', 2);
        if (parts.Length != 2)
        {
            throw new FormatException($"Formato de periodo inválido: '{raw}'.");
        }

        var inicioStr = parts[0].Trim().Trim('"');
        var finStr = parts[1].Trim().Trim('"');

        var inicio = DateTimeOffset.Parse(inicioStr, CultureInfo.InvariantCulture);
        var fin = DateTimeOffset.Parse(finStr, CultureInfo.InvariantCulture);

        return new PeriodoRango(inicio, fin);
    }
}

