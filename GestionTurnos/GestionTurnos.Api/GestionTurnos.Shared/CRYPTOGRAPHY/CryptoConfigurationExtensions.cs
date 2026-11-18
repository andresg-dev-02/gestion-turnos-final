using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionTurnos.Shared.CRYPTOGRAPHY
{
    public static class CryptoConfigurationExtensions 
    {
        public static IServiceCollection AddSharedCryptography(this IServiceCollection services, IConfiguration d)
        {
            var keysPath = d["DataProtection:KeysPath"];

            if (!string.IsNullOrEmpty(keysPath))
            {
                var directoryInfo = new DirectoryInfo(keysPath);
                if (!directoryInfo.Exists) directoryInfo.Create();

                var dataProtection = services.AddDataProtection()
                    .PersistKeysToFileSystem(directoryInfo)
                    .SetApplicationName("GobernacionCauca_Apps");

                if (OperatingSystem.IsWindows())
                {
                    dataProtection.ProtectKeysWithDpapi();
                }
            }
            else
            {
                services.AddDataProtection()
                    .SetApplicationName("GobernacionCauca_Apps");
            }

            services.AddSingleton<ICryptography, Cryptography>();
            return services;
        }
    }
}