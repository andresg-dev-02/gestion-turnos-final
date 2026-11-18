using System.Globalization;
using GestionTurnos.Api.Domain.Enums;

namespace GestionTurnos.Api.Application.Common;

/// <summary>
/// Conversión y validación de <c>parametrizacion.parametro_valor.valor</c> según <c>parametro.tipo_dato</c>.
/// Única fuente de verdad: la usa el servicio de lectura y el comando que cambia un valor,
/// para que lo que se guarda siempre se pueda leer después.
/// </summary>
public static class ConversorParametros
{
    private static readonly string[] FormatosHora = { "HH:mm", "HH:mm:ss", "H:mm" };

    public static bool TryEntero(string valor, out int resultado) =>
        int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out resultado);

    public static bool TryDecimal(string valor, out decimal resultado) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out resultado);

    public static bool TryBooleano(string valor, out bool resultado)
    {
        if (bool.TryParse(valor, out resultado))
        {
            return true;
        }

        switch (valor)
        {
            case "1": resultado = true; return true;
            case "0": resultado = false; return true;
            default: return false;
        }
    }

    public static bool TryHora(string valor, out TimeOnly resultado) =>
        TimeOnly.TryParseExact(valor, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out resultado);

    /// <summary>
    /// Indica si <paramref name="valor"/> es válido para <paramref name="tipoDato"/>.
    /// Un tipo desconocido se rechaza (antes se aceptaba cualquier valor).
    /// </summary>
    public static bool EsValido(string tipoDato, string valor)
    {
        return tipoDato.Trim().ToUpperInvariant() switch
        {
            TiposDatoParametro.Entero => TryEntero(valor, out _),
            TiposDatoParametro.Decimal => TryDecimal(valor, out _),
            TiposDatoParametro.Booleano => TryBooleano(valor, out _),
            TiposDatoParametro.Hora => TryHora(valor, out _),
            TiposDatoParametro.Texto => !string.IsNullOrWhiteSpace(valor),
            _ => false
        };
    }
}
