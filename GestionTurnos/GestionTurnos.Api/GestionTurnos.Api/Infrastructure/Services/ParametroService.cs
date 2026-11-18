using GestionTurnos.Api.Application.Common;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IParametroService"/> que consulta el valor vigente en parametrizacion.parametro_valor (Sección 6.2):
/// SELECT p.tipo_dato, v.valor FROM parametrizacion.parametro p JOIN parametrizacion.parametro_valor v USING (cod_parametro)
/// WHERE p.cod_parametro = $1 AND v.vigente_desde &lt;= $fecha AND (v.vigente_hasta IS NULL OR v.vigente_hasta &gt;= $fecha);
/// El valor se convierte según tipo_dato. Sin valor vigente o con tipo distinto lanza <see cref="ParametroNoConfiguradoException"/>.
/// </summary>
public sealed class ParametroService : IParametroService
{
    private readonly GestionTurnosContext _context;

    public ParametroService(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<int> ObtenerEnteroAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (codigo, valor) = await ObtenerVigenteAsync(codParametro, fecha, TiposDatoParametro.Entero, cancellationToken);
        return ConversorParametros.TryEntero(valor, out var resultado)
            ? resultado
            : throw ValorInvalido(codigo, fecha, valor);
    }

    public async Task<decimal> ObtenerDecimalAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (codigo, valor) = await ObtenerVigenteAsync(codParametro, fecha, TiposDatoParametro.Decimal, cancellationToken);
        return ConversorParametros.TryDecimal(valor, out var resultado)
            ? resultado
            : throw ValorInvalido(codigo, fecha, valor);
    }

    public async Task<bool> ObtenerBooleanoAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (codigo, valor) = await ObtenerVigenteAsync(codParametro, fecha, TiposDatoParametro.Booleano, cancellationToken);
        return ConversorParametros.TryBooleano(valor, out var resultado)
            ? resultado
            : throw ValorInvalido(codigo, fecha, valor);
    }

    public async Task<TimeOnly> ObtenerHoraAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (codigo, valor) = await ObtenerVigenteAsync(codParametro, fecha, TiposDatoParametro.Hora, cancellationToken);
        return ConversorParametros.TryHora(valor, out var resultado)
            ? resultado
            : throw ValorInvalido(codigo, fecha, valor);
    }

    public async Task<string> ObtenerTextoAsync(string codParametro, DateOnly fecha, CancellationToken cancellationToken = default)
    {
        var (_, valor) = await ObtenerVigenteAsync(codParametro, fecha, TiposDatoParametro.Texto, cancellationToken);
        return valor;
    }

    private async Task<(string Codigo, string Valor)> ObtenerVigenteAsync(
        string codParametro,
        DateOnly fecha,
        string tipoEsperado,
        CancellationToken cancellationToken)
    {
        var codigo = codParametro.Trim().ToUpperInvariant();

        var vigente = await _context.ParametroValores
            .AsNoTracking()
            .Where(v => v.CodParametro == codigo &&
                        v.VigenteDesde <= fecha &&
                        (v.VigenteHasta == null || v.VigenteHasta >= fecha))
            .OrderByDescending(v => v.VigenteDesde)
            .Select(v => new { v.Valor, v.Parametro.TipoDato })
            .FirstOrDefaultAsync(cancellationToken);

        if (vigente is null)
        {
            throw new ParametroNoConfiguradoException(codigo, fecha, "no tiene un valor vigente");
        }

        if (!string.Equals(vigente.TipoDato, tipoEsperado, StringComparison.OrdinalIgnoreCase))
        {
            throw new ParametroNoConfiguradoException(
                codigo, fecha, $"es de tipo {vigente.TipoDato} y se esperaba {tipoEsperado}");
        }

        return (codigo, vigente.Valor);
    }

    private static ParametroNoConfiguradoException ValorInvalido(string codigo, DateOnly fecha, string valor) =>
        new(codigo, fecha, $"tiene un valor vigente ('{valor}') que no corresponde a su tipo de dato");
}
