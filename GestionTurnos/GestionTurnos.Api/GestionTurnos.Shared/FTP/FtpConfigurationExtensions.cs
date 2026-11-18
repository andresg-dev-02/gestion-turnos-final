using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Shared.FTP
{
    /// <summary>
    /// Métodos de extensión para el registro del servicio FTP en el contenedor de inyección de dependencias de ASP.NET Core.
    /// </summary>
    public static class FtpConfigurationExtensions
    {
        /// <summary>
        /// Registra la infraestructura del servicio FTP de la Gobernación del Cauca a partir de la sección de configuración especificada.
        /// </summary>
        /// <param name="services">Colección de servicios de la aplicación.</param>
        /// <param name="configuration">Instancia de configuración (appsettings.json, variables de entorno, etc.).</param>
        /// <param name="sectionName">Nombre de la sección en la configuración (por defecto: "FtpSettings").</param>
        /// <returns>La colección de servicios para encadenamiento.</returns>
        public static IServiceCollection AddSharedFtp(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName = FtpSettings.SectionName)
        {
            var ftpSection = configuration.GetSection(sectionName);
            services.Configure<FtpSettings>(ftpSection);

            services.AddSingleton<IFtpService, FtpService>();

            return services;
        }

        /// <summary>
        /// Registra la infraestructura del servicio FTP configurando las opciones mediante un delegado Fluent.
        /// </summary>
        /// <param name="services">Colección de servicios de la aplicación.</param>
        /// <param name="configureOptions">Acción para personalizar los parámetros de <see cref="FtpSettings"/>.</param>
        /// <returns>La colección de servicios para encadenamiento.</returns>
        public static IServiceCollection AddSharedFtp(
            this IServiceCollection services,
            Action<FtpSettings> configureOptions)
        {
            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            services.Configure(configureOptions);
            services.AddSingleton<IFtpService, FtpService>();

            return services;
        }
    }
}
