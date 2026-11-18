using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GestionTurnos.Api.Application.Common.Security;
using GestionTurnos.Api.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace GestionTurnos.Api.Infrastructure.Security;

/// <summary>
/// Implementación de <see cref="ICodigoVerificacionStore"/> sobre <see cref="IDistributedCache"/>.
///
/// - El código se genera con <see cref="RandomNumberGenerator"/> (CSPRNG) y solo se guarda su HMAC-SHA256 con sal aleatoria.
/// - El registro completo (incluida la carga, que puede contener un JWT) se cifra con Data Protection.
/// - La clave de caché es un hash: ni correos ni ids quedan legibles en Redis/SQL si se usa un caché distribuido.
/// - La comparación es de tiempo constante (<see cref="CryptographicOperations.FixedTimeEquals"/>).
/// - Lectura-modificación-escritura serializada por clave con un conjunto de semáforos (atómica dentro de la instancia).
///   En despliegues con varias instancias, el límite de intentos es aproximado (el límite de tasa global lo complementa).
/// </summary>
public sealed class CodigoVerificacionStore : ICodigoVerificacionStore
{
    private const string PrefijoCache = "gt:otp:";
    private static readonly SemaphoreSlim[] Cerrojos = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private readonly IDistributedCache _cache;
    private readonly IDataProtector _protector;
    private readonly IOptionsMonitor<DosPasosSettings> _settings;
    private readonly TimeProvider _tiempo;

    public CodigoVerificacionStore(
        IDistributedCache cache,
        IDataProtectionProvider dataProtection,
        IOptionsMonitor<DosPasosSettings> settings,
        TimeProvider tiempo)
    {
        _cache = cache;
        _protector = dataProtection.CreateProtector("GestionTurnos.CodigoVerificacion.v1");
        _settings = settings;
        _tiempo = tiempo;
    }

    public async Task<CodigoEmitido> EmitirAsync(string proposito, string clave, string? carga, CancellationToken cancellationToken = default)
    {
        var claveCache = ClaveCache(proposito, clave);
        var cerrojo = Cerrojo(claveCache);
        await cerrojo.WaitAsync(cancellationToken);
        try
        {
            var ahora = _tiempo.GetUtcNow();
            var (codigo, registro) = NuevoRegistro(carga, ahora, reenvios: 0);
            await GuardarAsync(claveCache, registro, cancellationToken);
            return new CodigoEmitido(codigo, registro.ExpiraEn);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task<CodigoReemitido?> ReemitirAsync(string proposito, string clave, CancellationToken cancellationToken = default)
    {
        var claveCache = ClaveCache(proposito, clave);
        var cerrojo = Cerrojo(claveCache);
        await cerrojo.WaitAsync(cancellationToken);
        try
        {
            var actual = await LeerAsync(claveCache, cancellationToken);
            var settings = _settings.CurrentValue;
            var ahora = _tiempo.GetUtcNow();

            if (actual is null ||
                actual.Reenvios >= settings.MaximoReenvios ||
                ahora - actual.EmitidoEn < TimeSpan.FromSeconds(settings.SegundosEsperaReenvio))
            {
                return null;
            }

            var (codigo, registro) = NuevoRegistro(actual.Carga, ahora, actual.Reenvios + 1);
            await GuardarAsync(claveCache, registro, cancellationToken);
            return new CodigoReemitido(codigo, registro.ExpiraEn, registro.Carga);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task<VerificacionCodigo> VerificarAsync(string proposito, string clave, string codigo, CancellationToken cancellationToken = default)
    {
        var claveCache = ClaveCache(proposito, clave);
        var cerrojo = Cerrojo(claveCache);
        await cerrojo.WaitAsync(cancellationToken);
        try
        {
            var registro = await LeerAsync(claveCache, cancellationToken);
            if (registro is null || registro.ExpiraEn <= _tiempo.GetUtcNow())
            {
                return new VerificacionCodigo(EstadoVerificacionCodigo.NoExisteOExpirado, null, 0);
            }

            var esperado = Convert.FromBase64String(registro.HashCodigo);
            var recibido = CalcularHash(codigo.Trim(), Convert.FromBase64String(registro.Sal));

            if (CryptographicOperations.FixedTimeEquals(esperado, recibido))
            {
                await _cache.RemoveAsync(claveCache, CancellationToken.None); // un solo uso
                return new VerificacionCodigo(EstadoVerificacionCodigo.Valido, registro.Carga, 0);
            }

            var intentos = registro.Intentos + 1;
            var restantes = _settings.CurrentValue.MaximoIntentos - intentos;
            if (restantes <= 0)
            {
                await _cache.RemoveAsync(claveCache, CancellationToken.None);
                return new VerificacionCodigo(EstadoVerificacionCodigo.IntentosAgotados, null, 0);
            }

            await GuardarAsync(claveCache, registro with { Intentos = intentos }, cancellationToken);
            return new VerificacionCodigo(EstadoVerificacionCodigo.Invalido, null, restantes);
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public Task DescartarAsync(string proposito, string clave, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(ClaveCache(proposito, clave), cancellationToken);

    // ------------------------------------------------------------------

    private (string Codigo, RegistroCodigo Registro) NuevoRegistro(string? carga, DateTimeOffset ahora, int reenvios)
    {
        var settings = _settings.CurrentValue;
        var longitud = Math.Clamp(settings.LongitudCodigo, 6, 8);
        var codigo = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, longitud)).ToString($"D{longitud}");
        var sal = RandomNumberGenerator.GetBytes(16);

        var registro = new RegistroCodigo(
            HashCodigo: Convert.ToBase64String(CalcularHash(codigo, sal)),
            Sal: Convert.ToBase64String(sal),
            Intentos: 0,
            Reenvios: reenvios,
            EmitidoEn: ahora,
            ExpiraEn: ahora.AddMinutes(Math.Max(settings.MinutosVigencia, 1)),
            Carga: carga);

        return (codigo, registro);
    }

    private async Task GuardarAsync(string claveCache, RegistroCodigo registro, CancellationToken cancellationToken)
    {
        var protegido = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(registro));
        await _cache.SetAsync(
            claveCache,
            protegido,
            new DistributedCacheEntryOptions { AbsoluteExpiration = registro.ExpiraEn },
            cancellationToken);
    }

    private async Task<RegistroCodigo?> LeerAsync(string claveCache, CancellationToken cancellationToken)
    {
        var protegido = await _cache.GetAsync(claveCache, cancellationToken);
        if (protegido is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RegistroCodigo>(_protector.Unprotect(protegido));
        }
        catch (CryptographicException)
        {
            // Llave rotada o dato manipulado: se trata como inexistente.
            await _cache.RemoveAsync(claveCache, CancellationToken.None);
            return null;
        }
    }

    private static byte[] CalcularHash(string codigo, byte[] sal) =>
        HMACSHA256.HashData(sal, Encoding.UTF8.GetBytes(codigo));

    private static string ClaveCache(string proposito, string clave) =>
        PrefijoCache + proposito + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(clave)));

    private static SemaphoreSlim Cerrojo(string claveCache) =>
        Cerrojos[(int)((uint)StringComparer.Ordinal.GetHashCode(claveCache) % (uint)Cerrojos.Length)];

    private sealed record RegistroCodigo(
        string HashCodigo,
        string Sal,
        int Intentos,
        int Reenvios,
        DateTimeOffset EmitidoEn,
        DateTimeOffset ExpiraEn,
        string? Carga);
}
