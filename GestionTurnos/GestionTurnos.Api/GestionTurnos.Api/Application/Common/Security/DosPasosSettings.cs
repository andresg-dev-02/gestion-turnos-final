namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Configuración de la autenticación en dos pasos por correo (sección <c>DosPasos</c> de appsettings).
/// La funcionalidad es doblemente opcional: debe estar <see cref="Disponible"/> en el sistema y, además,
/// cada usuario decide activarla desde su perfil.
/// </summary>
public sealed class DosPasosSettings
{
    public const string SectionName = "DosPasos";

    /// <summary>
    /// Interruptor global. En <c>false</c> el inicio de sesión funciona exactamente como antes, aunque haya
    /// usuarios con la opción activada. Requiere ejecutar sql/migracion_correo_dos_pasos.sql antes de activarlo.
    /// </summary>
    public bool Disponible { get; set; }

    /// <summary>Dígitos del código (6 a 8).</summary>
    public int LongitudCodigo { get; set; } = 6;

    /// <summary>Minutos de vigencia de cada código.</summary>
    public int MinutosVigencia { get; set; } = 5;

    /// <summary>Intentos fallidos permitidos antes de invalidar el código.</summary>
    public int MaximoIntentos { get; set; } = 5;

    /// <summary>Espera mínima entre reenvíos del mismo código.</summary>
    public int SegundosEsperaReenvio { get; set; } = 60;

    /// <summary>Reenvíos permitidos por desafío.</summary>
    public int MaximoReenvios { get; set; } = 3;
}
