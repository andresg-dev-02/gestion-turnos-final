namespace GestionTurnos.Api.Application.Common;

/// <summary>
/// Se lanza cuando una regla de negocio necesita un parámetro que no tiene valor vigente para la fecha,
/// o cuyo valor no corresponde a su <c>tipo_dato</c>. Es un error de configuración: los valores de las
/// reglas viven en <c>parametrizacion.parametro_valor</c> y nunca tienen un respaldo escrito en el código.
/// </summary>
public sealed class ParametroNoConfiguradoException : Exception
{
    public string CodParametro { get; }
    public DateOnly Fecha { get; }

    public ParametroNoConfiguradoException(string codParametro, DateOnly fecha, string detalle)
        : base($"El parámetro '{codParametro}' {detalle} para la fecha {fecha:yyyy-MM-dd}. Configúrelo en Parametrización.")
    {
        CodParametro = codParametro;
        Fecha = fecha;
    }
}
