using GestionTurnos.Api.Application.Behaviors.Notificaciones;
using GestionTurnos.Api.Application.Common.Notificaciones;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.CQRS.Novedades;
using GestionTurnos.Api.Application.CQRS.Seguridad.Auth;
using GestionTurnos.Api.Application.CQRS.Seguridad.Registro;
using GestionTurnos.Api.Application.CQRS.Turnos;
using GestionTurnos.Api.Application.DTOs.Seguridad;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Application.Services.DosPasos;
using GestionTurnos.Api.Application.Services.ReporteSemanal;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Infrastructure.Integrations.Email;
using GestionTurnos.Api.Infrastructure.Security;
using GestionTurnos.Shared.EMAIL;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Extensions;

/// <summary>
/// Registro del servicio de correo: SMTP (Shared), cola + despachador en segundo plano, plantillas,
/// verificación en dos pasos, notificaciones (registro, turnos, vacaciones, novedades, cambios de turno)
/// y reporte semanal al trabajador.
/// </summary>
public static class NotificacionesCorreoServiceCollectionExtensions
{
    /// <summary>
    /// IMPORTANTE: invocar DESPUÉS de <c>AddApplication()</c>. Los behaviors cerrados registrados aquí deben quedar
    /// después de Logging/Authorization/Validation para ejecutarse solo con peticiones ya autorizadas y válidas
    /// (MediatR anida los behaviors en el orden de registro).
    /// </summary>
    public static IServiceCollection AddNotificacionesCorreo(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Transporte SMTP reutilizable (GestionTurnos.Shared)
        services.AddSharedEmail(configuration);

        // 2. Configuración tipada, validada al arrancar
        services.AddOptions<NotificacionesSettings>()
            .Bind(configuration.GetSection(NotificacionesSettings.SectionName))
            .Validate(s => s.CapacidadCola > 0 && s.MaximoReintentos >= 0 && s.GradoParalelismo > 0,
                "Notificaciones: CapacidadCola y GradoParalelismo deben ser > 0 y MaximoReintentos >= 0.")
            .Validate(s => string.IsNullOrWhiteSpace(s.UrlAplicacion) || Uri.TryCreate(s.UrlAplicacion, UriKind.Absolute, out _),
                "Notificaciones:UrlAplicacion debe ser una URL absoluta.")
            .ValidateOnStart();

        services.AddOptions<DosPasosSettings>()
            .Bind(configuration.GetSection(DosPasosSettings.SectionName))
            .Validate(s => s.LongitudCodigo is >= 6 and <= 8, "DosPasos:LongitudCodigo debe estar entre 6 y 8.")
            .Validate(s => s.MinutosVigencia is >= 1 and <= 15, "DosPasos:MinutosVigencia debe estar entre 1 y 15.")
            .Validate(s => s.MaximoIntentos is >= 1 and <= 10, "DosPasos:MaximoIntentos debe estar entre 1 y 10.")
            .Validate(s => s.SegundosEsperaReenvio >= 0 && s.MaximoReenvios >= 0, "DosPasos: valores de reenvío no válidos.")
            .ValidateOnStart();

        // 3. Cola en memoria + despachador en segundo plano
        services.AddSingleton<ColaCorreos>();
        services.AddHostedService<DespachadorCorreosService>();
        services.AddScoped<INotificadorCorreo, NotificadorCorreo>();

        // 4. Códigos de verificación. AddDistributedMemoryCache usa TryAdd: si antes se registra Redis
        //    (AddStackExchangeRedisCache) para varias instancias, se respeta ese registro.
        services.AddDistributedMemoryCache();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICodigoVerificacionStore, CodigoVerificacionStore>();
        services.AddScoped<IServicioCodigosDosPasos, ServicioCodigosDosPasos>();

        // 5. Behaviors que agregan el correo a comandos existentes sin modificar sus handlers
        services.AddTransient<IPipelineBehavior<IniciarSesionCommand, Result<LoginResponseDto>>, DosPasosInicioSesionBehavior>();
        services.AddTransient<IPipelineBehavior<AprobarSolicitudRegistroCommand, Result<AprobacionRegistroResultDto>>, NotificarAprobacionRegistroBehavior>();
        services.AddTransient<IPipelineBehavior<RechazarSolicitudRegistroCommand, Result>, NotificarRechazoRegistroBehavior>();
        services.AddTransient<IPipelineBehavior<PublicarAsignacionesTurnoCommand, Result<int>>, NotificarPublicacionTurnosBehavior>();
        services.AddTransient<IPipelineBehavior<ModificarAsignacionTurnoCommand, Result>, NotificarModificacionTurnoBehavior>();
        services.AddTransient<IPipelineBehavior<AnularAsignacionTurnoCommand, Result>, NotificarAnulacionTurnoBehavior>();
        services.AddTransient<IPipelineBehavior<ResolverSolicitudVacacionesCommand, Result>, NotificarResolucionVacacionesBehavior>();
        services.AddTransient<IPipelineBehavior<AnularVacacionesAprobadasCommand, Result>, NotificarAnulacionVacacionesBehavior>();
        services.AddTransient<IPipelineBehavior<ResolverNovedadCommand, Result>, NotificarResolucionNovedadBehavior>();
        services.AddTransient<IPipelineBehavior<ResolverCambioTurnoCommand, Result>, NotificarResolucionCambioTurnoBehavior>();

        // 6. Reporte semanal al trabajador (programado en segundo plano)
        services.AddScoped<IServicioReporteSemanal, ServicioReporteSemanal>();
        services.AddHostedService<ReporteSemanalBackgroundService>();

        return services;
    }
}
