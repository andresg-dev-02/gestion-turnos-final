using System.Security.Cryptography;
using System.Text;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GestionTurnos.Api.Infrastructure.Security;

/// <summary>
/// Generador y validador de tokens QR rotativos criptográficos (estilo TOTP / Authenticator).
/// Los tokens cambian automáticamente en intervalos definidos (ej. cada 20 segundos)
/// e invalidan los códigos anteriores para evitar fotografías o reutilizaciones no presenciales.
/// </summary>
public sealed class QrEstacionService : IQrEstacionService
{
    private const string Prefijo = "GTQR";
    private const string Version = "v1";
    private const int IntervaloPorDefectoSegundos = 20;
    private const string EstacionPorDefecto = "ESTACION_PRINCIPAL";
    private const string NombreEstacionPorDefecto = "Estación de Acceso Principal";

    private readonly byte[] _secretKey;
    private readonly int _intervaloSegundos;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<QrEstacionService> _logger;

    public QrEstacionService(
        IConfiguration configuration,
        IMemoryCache memoryCache,
        ILogger<QrEstacionService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;

        var configuredSecret = configuration["QrEstacion:SecretKey"]
            ?? configuration["Jwt:Key"]
            ?? "GestionTurnosTotemClaveCriptograficaSecreta2026!";
        _secretKey = Encoding.UTF8.GetBytes(configuredSecret);

        _intervaloSegundos = configuration.GetValue("QrEstacion:IntervaloSegundos", IntervaloPorDefectoSegundos);
        if (_intervaloSegundos < 10) _intervaloSegundos = 10;
        if (_intervaloSegundos > 60) _intervaloSegundos = 60;
    }

    public TokenQrEstacionDto GenerarTokenActual(string? codigoEstacion = null)
    {
        var estacion = SanitizarCodigoEstacion(codigoEstacion);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var step = nowUnix / _intervaloSegundos;
        var expiraEn = (int)(_intervaloSegundos - (nowUnix % _intervaloSegundos));

        var payload = $"{Prefijo}|{Version}|{estacion}|{step}";
        var signature = CalcularFirma(payload);
        var token = $"{payload}|{signature}";

        var nombre = estacion.Equals(EstacionPorDefecto, StringComparison.OrdinalIgnoreCase)
            ? NombreEstacionPorDefecto
            : $"Estación {estacion}";

        return new TokenQrEstacionDto(
            Token: token,
            CodigoEstacion: estacion,
            NombreEstacion: nombre,
            ExpiraEnSegundos: expiraEn,
            IntervaloSegundos: _intervaloSegundos,
            TimestampUnix: nowUnix);
    }

    public ResultadoValidacionQr ValidarToken(string token, long idEmpleado)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ResultadoValidacionQr(false, string.Empty, "El código QR presentado está vacío.");
        }

        var partes = token.Trim().Split('|');
        if (partes.Length != 5 || partes[0] != Prefijo || partes[1] != Version)
        {
            return new ResultadoValidacionQr(false, string.Empty, "El formato del código QR no corresponde a una estación de asistencia válida.");
        }

        var estacion = partes[2];
        if (!long.TryParse(partes[3], out var tokenStep))
        {
            return new ResultadoValidacionQr(false, estacion, "El sello temporal del código QR es ilegible.");
        }

        var tokenFirma = partes[4];

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currentStep = nowUnix / _intervaloSegundos;

        // Tolerancia: permitir paso actual y el inmediatamente anterior (tolerancia a latencia de escaneo/red)
        if (tokenStep < currentStep - 1)
        {
            return new ResultadoValidacionQr(false, estacion, "El código QR ha expirado. Por favor enfoca el código actualizado en la pantalla de entrada.");
        }

        if (tokenStep > currentStep + 1)
        {
            return new ResultadoValidacionQr(false, estacion, "El código QR presenta desfase temporal con la hora del servidor.");
        }

        // Verificación de firma criptográfica
        var payloadEsperado = $"{Prefijo}|{Version}|{estacion}|{tokenStep}";
        var firmaEsperada = CalcularFirma(payloadEsperado);

        var firmaEsperadaBytes = Encoding.UTF8.GetBytes(firmaEsperada);
        var tokenFirmaBytes = Encoding.UTF8.GetBytes(tokenFirma);

        if (firmaEsperadaBytes.Length != tokenFirmaBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(firmaEsperadaBytes, tokenFirmaBytes))
        {
            _logger.LogWarning("Intento de marcaje con código QR con firma inválida o adulterada. Empleado: {IdEmpleado}", idEmpleado);
            return new ResultadoValidacionQr(false, estacion, "La firma criptográfica del código QR no es válida.");
        }

        // Anti-reutilización por el mismo empleado para el mismo intervalo
        var cacheKey = $"qr_asistencia_usado:{idEmpleado}:{tokenStep}";
        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            return new ResultadoValidacionQr(false, estacion, "Ya registraste un marcaje con este código QR. Espera unos segundos al siguiente turno.");
        }

        // Marcamos como usado por este empleado durante 2 minutos
        _memoryCache.Set(cacheKey, true, TimeSpan.FromMinutes(2));

        return new ResultadoValidacionQr(true, estacion);
    }

    private string CalcularFirma(string payload)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..16]; // 16 caracteres hexadecimales para compacidad
    }

    private static string SanitizarCodigoEstacion(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return EstacionPorDefecto;
        }

        var limpio = new string(codigo.Trim().ToUpperInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());

        return string.IsNullOrEmpty(limpio) ? EstacionPorDefecto : limpio;
    }
}
