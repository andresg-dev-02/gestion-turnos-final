using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Novedades;

public class PeriodoVacacional : BaseEntity
{
    public long IdPeriodoVacacional { get; set; }
    public long IdEmpleado { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal DiasOtorgados { get; set; }

    public Empleado Empleado { get; set; } = null!;
}

public class SolicitudVacaciones : BaseEntity
{
    public long IdSolicitudVacaciones { get; set; }
    public long IdEmpleado { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public decimal DiasSolicitados { get; set; }
    public string CodEstadoSolicitud { get; set; } = EstadosSolicitud.Pendiente;
    public string? Observacion { get; set; }
    public long SolicitadoPor { get; set; }
    public DateTimeOffset SolicitadoEn { get; set; } = DateTimeOffset.UtcNow;
    public long? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public string? MotivoRechazo { get; set; }

    public Empleado Empleado { get; set; } = null!;
    public EstadoSolicitud EstadoSolicitud { get; set; } = null!;
    public Usuario Solicitante { get; set; } = null!;
    public Usuario? Resolutor { get; set; }
}

public class Novedad : BaseEntity
{
    public long IdNovedad { get; set; }
    public long IdEmpleado { get; set; }
    public string CodTipoNovedad { get; set; } = null!;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
    public bool Remunerado { get; set; }
    public bool AfectaHoras { get; set; }
    public string CodEstadoSolicitud { get; set; } = EstadosSolicitud.Pendiente;
    public string? Observaciones { get; set; }
    public string? UrlSoporte { get; set; }
    public long RegistradoPor { get; set; }
    public DateTimeOffset RegistradoEn { get; set; } = DateTimeOffset.UtcNow;
    public long? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public string? MotivoRechazo { get; set; }

    public Empleado Empleado { get; set; } = null!;
    public TipoNovedad TipoNovedad { get; set; } = null!;
    public EstadoSolicitud EstadoSolicitud { get; set; } = null!;
    public Usuario Registrador { get; set; } = null!;
    public Usuario? Resolutor { get; set; }
}

/// <summary>
/// Vista de base de datos: novedades.v_saldo_vacaciones (solo lectura).
/// </summary>
public class VistaSaldoVacaciones
{
    public long IdEmpleado { get; set; }
    public decimal DiasOtorgados { get; set; }
    public decimal DiasUtilizados { get; set; }
    public decimal DiasEnSolicitud { get; set; }
    public decimal DiasDisponibles { get; set; }
}

