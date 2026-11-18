namespace GestionTurnos.Api.Application.Interfaces;

/// <summary>
/// Componente aislado para la liquidación de horas trabajadas por categoría (Sección 6.3 - RF-05, RG-03).
/// </summary>
public interface ILiquidacionHorasCalculator
{
    /// <summary>
    /// Calcula los minutos trabajados descontando descanso del turno y novedades aprobadas con afecta_horas = true,
    /// y reemplaza las filas de asistencia.detalle_horas para el id_registro dado dentro de la transacción activa.
    /// </summary>
    Task LiquidarRegistroAsync(long idRegistro, CancellationToken cancellationToken = default);
}

/// <summary>
/// Componente aislado para calcular los días hábiles solicitados de vacaciones (Sección 3.7 punto 1 - CU-03).
/// </summary>
public interface IDiasVacacionesCalculator
{
    /// <summary>
    /// Calcula los días del rango [fechaInicio, fechaFin] excluyendo fines de semana y días en parametrizacion.dia_festivo.
    /// </summary>
    Task<decimal> CalcularDiasSolicitadosAsync(DateOnly fechaInicio, DateOnly fechaFin, CancellationToken cancellationToken = default);
}

