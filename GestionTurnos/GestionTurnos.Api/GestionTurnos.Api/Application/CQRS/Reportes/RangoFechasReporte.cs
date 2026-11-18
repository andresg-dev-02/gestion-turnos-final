using FluentValidation;
using GestionTurnos.Api.Application.Common;
using Microsoft.Extensions.Configuration;

namespace GestionTurnos.Api.Application.CQRS.Reportes;

/// <summary>
/// Contrato común de los reportes (sección 14): todo reporte exige un rango de fechas.
/// </summary>
public interface IReporteConRangoFechas
{
    DateOnly FechaDesde { get; }
    DateOnly FechaHasta { get; }
}

/// <summary>
/// Reglas comunes de los reportes (sección 14):
/// el rango de fechas es obligatorio y no puede superar un año.
/// </summary>
public static class RangoFechasReporte
{
    public const int PageSizeMaximo = 50;

    public static void AplicarReglasRangoReporte<T>(this AbstractValidator<T> validator)
        where T : IReporteConRangoFechas
    {
        validator.RuleFor(x => x.FechaDesde)
            .NotEmpty().WithMessage("La fecha inicial del reporte es obligatoria.");

        validator.RuleFor(x => x.FechaHasta)
            .NotEmpty().WithMessage("La fecha final del reporte es obligatoria.")
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha final debe ser mayor o igual a la fecha inicial.")
            .LessThan(x => x.FechaDesde.AddYears(1))
            .WithMessage("El rango de fechas de un reporte no puede superar un año.");
    }

    /// <summary>
    /// Convierte el rango de fechas a instantes en la zona horaria de la empresa:
    /// [FechaDesde 00:00, FechaHasta + 1 día 00:00).
    /// </summary>
    public static (DateTimeOffset Desde, DateTimeOffset HastaExclusivo) AInstantes(
        IConfiguration configuration,
        DateOnly fechaDesde,
        DateOnly fechaHasta)
    {
        var fin = fechaHasta.AddDays(1);
        var desde = new DateTimeOffset(
            fechaDesde.ToDateTime(TimeOnly.MinValue), TimeZoneHelper.GetOffsetForDate(configuration, fechaDesde));
        var hasta = new DateTimeOffset(
            fin.ToDateTime(TimeOnly.MinValue), TimeZoneHelper.GetOffsetForDate(configuration, fin));
        return (desde, hasta);
    }

    public static string? NormalizarCodigo(string? codigo)
        => string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
}
