using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Auditoria;

/// <summary>
/// Tabla auditoria.bitacora_negocio (RF-08).
/// La bitácora es inmutable (solo inserción y consulta).
/// </summary>
public class BitacoraNegocio : BaseEntity
{
    public long IdBitacora { get; set; }
    public DateTimeOffset FechaHora { get; set; } = DateTimeOffset.UtcNow;
    public long? IdUsuario { get; set; }
    public string CodTipoAccion { get; set; } = null!;
    public string Esquema { get; set; } = null!;
    public string Tabla { get; set; } = null!;
    public string IdRegistro { get; set; } = null!;
    public long? IdEmpleado { get; set; }
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string? Descripcion { get; set; }
    public string? IpOrigen { get; set; }

    public Usuario? Usuario { get; set; }
    public TipoAccion TipoAccion { get; set; } = null!;
    public Empleado? Empleado { get; set; }
}

