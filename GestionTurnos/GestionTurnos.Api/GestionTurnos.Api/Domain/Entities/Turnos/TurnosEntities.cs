using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;
using GestionTurnos.Api.Domain.ValueObjects;

namespace GestionTurnos.Api.Domain.Entities.Turnos;

public class Turno : BaseEntity
{
    public int IdTurno { get; set; }
    public string Nombre { get; set; } = null!;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public int MinutosDescanso { get; set; }

    /// <summary>
    /// Columna generada por la base de datos (hora_fin &lt;= hora_inicio). No se envía en INSERT ni UPDATE.
    /// </summary>
    public bool CruzaMedianoche { get; private set; }

    /// <summary>
    /// Columna generada por la base de datos. No se envía en INSERT ni UPDATE.
    /// </summary>
    public int DuracionMinutos { get; private set; }

    public string? Color { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Asignacion> Asignaciones { get; set; } = new List<Asignacion>();
}

public class Asignacion : BaseEntity
{
    public long IdAsignacion { get; set; }
    public long IdEmpleado { get; set; }
    public int IdTurno { get; set; }
    public DateOnly Fecha { get; set; }
    public PeriodoRango Periodo { get; set; }
    public string CodEstadoAsignacion { get; set; } = EstadosAsignacion.Programada;
    public string? Observacion { get; set; }
    public long CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public Turno Turno { get; set; } = null!;
    public EstadoAsignacion EstadoAsignacion { get; set; } = null!;
    public Usuario Creador { get; set; } = null!;
}

public class DiaDescanso : BaseEntity
{
    public long IdDiaDescanso { get; set; }
    public long IdEmpleado { get; set; }
    public DateOnly Fecha { get; set; }
    public long CreadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public Usuario Creador { get; set; } = null!;
}

public class SolicitudCambioTurno : BaseEntity
{
    public long IdSolicitudCambio { get; set; }
    public long IdAsignacionSolicitante { get; set; }
    public long IdAsignacionCompanero { get; set; }
    public string? Motivo { get; set; }
    public string CodEstadoSolicitud { get; set; } = EstadosSolicitud.Pendiente;
    public DateTimeOffset SolicitadoEn { get; set; } = DateTimeOffset.UtcNow;
    public long? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public string? MotivoRechazo { get; set; }

    public Asignacion AsignacionSolicitante { get; set; } = null!;
    public Asignacion AsignacionCompanero { get; set; } = null!;
    public EstadoSolicitud EstadoSolicitud { get; set; } = null!;
    public Usuario? Resolutor { get; set; }
}

/// <summary>
/// Vista de base de datos: turnos.v_calendario (solo lectura).
/// </summary>
public class VistaCalendario
{
    public long IdEmpleado { get; set; }
    public string Tipo { get; set; } = null!;
    public string Detalle { get; set; } = null!;
    public DateTimeOffset Inicio { get; set; }
    public DateTimeOffset Fin { get; set; }
}

