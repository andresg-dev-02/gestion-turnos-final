using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Application.Calculators;

/// <summary>
/// Implementa la sección 3.7 punto 1:
/// Calcula dias_solicitados en el backend: días del rango excluyendo los de parametrizacion.dia_festivo
/// y los fines de semana (aislado en un solo componente).
/// </summary>
public sealed class DiasVacacionesCalculator : IDiasVacacionesCalculator
{
    private readonly GestionTurnosContext _context;

    public DiasVacacionesCalculator(GestionTurnosContext context)
    {
        _context = context;
    }

    public async Task<decimal> CalcularDiasSolicitadosAsync(
        DateOnly fechaInicio,
        DateOnly fechaFin,
        CancellationToken cancellationToken = default)
    {
        if (fechaFin < fechaInicio)
        {
            return 0m;
        }

        var festivosEnRango = await _context.DiasFestivos
            .AsNoTracking()
            .Where(d => d.Fecha >= fechaInicio && d.Fecha <= fechaFin)
            .Select(d => d.Fecha)
            .ToHashSetAsync(cancellationToken);

        int diasHabiles = 0;
        for (var dia = fechaInicio; dia <= fechaFin; dia = dia.AddDays(1))
        {
            if (dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            if (festivosEnRango.Contains(dia))
            {
                continue;
            }

            diasHabiles++;
        }

        return diasHabiles;
    }
}

