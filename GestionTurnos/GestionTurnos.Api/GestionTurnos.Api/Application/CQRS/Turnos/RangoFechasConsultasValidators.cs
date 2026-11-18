using FluentValidation;

namespace GestionTurnos.Api.Application.CQRS.Turnos;

// =====================================================================
// Validación de rangos de fechas en consultas de turnos.
// Sin estas reglas, una consulta sin fechas (DateOnly = 0001-01-01) o con el
// rango invertido respondía 200 con una lista vacía, ocultando el error del cliente.
// =====================================================================

public sealed class ListarAsignacionesTurnoQueryValidator : AbstractValidator<ListarAsignacionesTurnoQuery>
{
    public ListarAsignacionesTurnoQueryValidator()
    {
        RuleFor(x => x.FechaDesde).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.FechaHasta)
            .NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.FechaDesde).WithMessage("La fecha final debe ser igual o posterior a la inicial.")
            .LessThanOrEqualTo(x => x.FechaDesde.AddDays(366)).WithMessage("El rango no puede superar un año.");
    }
}

public sealed class ListarDiasDescansoQueryValidator : AbstractValidator<ListarDiasDescansoQuery>
{
    public ListarDiasDescansoQueryValidator()
    {
        RuleFor(x => x.FechaDesde).NotEmpty().WithMessage("La fecha inicial es obligatoria.");
        RuleFor(x => x.FechaHasta)
            .NotEmpty().WithMessage("La fecha final es obligatoria.")
            .GreaterThanOrEqualTo(x => x.FechaDesde).WithMessage("La fecha final debe ser igual o posterior a la inicial.")
            .LessThanOrEqualTo(x => x.FechaDesde.AddDays(366)).WithMessage("El rango no puede superar un año.");
    }
}

public sealed class ObtenerMiCalendarioQueryValidator : AbstractValidator<ObtenerMiCalendarioQuery>
{
    public ObtenerMiCalendarioQueryValidator()
    {
        RuleFor(x => x.Desde).NotEmpty().WithMessage("El inicio del rango es obligatorio.");
        RuleFor(x => x.Hasta)
            .NotEmpty().WithMessage("El fin del rango es obligatorio.")
            .GreaterThan(x => x.Desde).WithMessage("El fin del rango debe ser posterior al inicio.")
            .LessThanOrEqualTo(x => x.Desde.AddDays(366)).WithMessage("El rango no puede superar un año.");
    }
}
