namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Servicio para consultar los valores vigentes de parametrizacion.parametro_valor según una fecha dada (Sección 6.2).
/// Convierte el valor según <c>parametro.tipo_dato</c>. No acepta valores por defecto: ningún número de regla
/// de negocio queda escrito en el código. Si el parámetro no tiene valor vigente para la fecha, o su tipo no
/// coincide con el pedido, lanza <see cref="Common.ParametroNoConfiguradoException"/>.
/// </summary>
public interface IParametroService
{
    Task<int> ObtenerEnteroAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<decimal> ObtenerDecimalAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<bool> ObtenerBooleanoAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<TimeOnly> ObtenerHoraAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<string> ObtenerTextoAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default);
}
