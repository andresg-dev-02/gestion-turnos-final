using GestionTurnos.Api.Application.Interfaces;

namespace GestionTurnos.Api.Infrastructure.Security;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        try
        {
            if (BCrypt.Net.BCrypt.Verify(password, passwordHash))
            {
                return true;
            }
        }
        catch
        {
            // Ignorar si fue generado con EnhancedHashPassword
        }

        try
        {
            return BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash);
        }
        catch
        {
            return false;
        }
    }
}
