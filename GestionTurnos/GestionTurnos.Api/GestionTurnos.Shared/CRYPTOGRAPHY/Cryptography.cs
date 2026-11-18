using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace GestionTurnos.Shared.CRYPTOGRAPHY
{
    public class Cryptography : ICryptography
    {
        private readonly IDataProtector _protector;
        public Cryptography(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("Credentials.v1");
        }

        public string Desencriptar(string textoCifrado)
        {
            if (string.IsNullOrEmpty(textoCifrado)) return string.Empty;
            try
            {
                return _protector.Unprotect(textoCifrado);
            }
            catch (CryptographicException e)
            {
                throw new InvalidOperationException("No se pudo desencriptar el texto. La llave maestra es incorrecta o los datos están corruptos.", e);
            }

        }

        public string Encriptar(string textoPlano)
        {
            if (string.IsNullOrEmpty(textoPlano)) return string.Empty;
            return _protector.Protect(textoPlano);
        }
    }
}
