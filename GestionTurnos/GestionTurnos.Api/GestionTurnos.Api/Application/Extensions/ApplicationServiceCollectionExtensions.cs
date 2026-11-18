using System.Reflection;
using FluentValidation;
using GestionTurnos.Api.Application.Behaviors;
using GestionTurnos.Api.Application.Interfaces;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Api.Application.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());

            // Pipeline Behaviors (orden de ejecución)
            // 1. Logging y métricas de tiempo
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            // 2. Autorización fail-fast
            cfg.AddOpenBehavior(typeof(AuthorizationBehavior<,>));
            // 3. Validación automática FluentValidation
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        // Escaneo de validadores FluentValidation
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        // Calculadores aislados de reglas de negocio (Secciones 3.7 y 6.3)
        services.AddScoped<ILiquidacionHorasCalculator, Calculators.LiquidacionHorasCalculator>();
        services.AddScoped<Services.Asistencia.MarcacionEmpleadoService>();
        services.AddScoped<Services.Sesiones.SesionesService>();
        services.AddScoped<IDiasVacacionesCalculator, Calculators.DiasVacacionesCalculator>();

        // Revisión de conflictos de turnos (sección 9.1): consulta previa, asignación y modificación
        services.AddScoped<Services.IRevisionConflictosTurno, Services.RevisionConflictosTurno>();

        return services;
    }
}

