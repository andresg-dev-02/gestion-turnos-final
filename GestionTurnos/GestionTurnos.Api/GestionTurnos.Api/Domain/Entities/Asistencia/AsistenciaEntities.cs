using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Asistencia;

public class RegistroAsistencia : BaseEntity
{
    public long IdRegistro { get; set; }
    public long IdEmpleado { get; set; } 
    public long? IdAsignacion { get; set; }
    public DateTimeOffset HoraEntrada { get; set; }
    public DateTimeOffset? HoraSalida { get; set; }

    /// <summary>
    /// La jornada quedó sin salida y se cerró automáticamente al vencer (hora_salida sigue NULL).
    /// El administrador la corrige con el registro manual.
    /// </summary>
    public bool SalidaNoRegistrada { get; set; }
    public string CodFuenteMarcacion { get; set; } = FuentesMarcacionCodigos.Web;
    public string? Observacion { get; set; }
    public long? RegistradoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Empleado Empleado { get; set; } = null!;
    public Asignacion? Asignacion { get; set; }
    public FuenteMarcacion FuenteMarcacion { get; set; } = null!;
    public Usuario? Registrador { get; set; }
    public ICollection<DetalleHoras> DetallesHoras { get; set; } = new List<DetalleHoras>();
}

public class DetalleHoras : BaseEntity
{
    public long IdRegistro { get; set; }
    public string CodCategoriaHora { get; set; } = null!;
    public int Minutos { get; set; }

    public RegistroAsistencia Registro { get; set; } = null!;
    public CategoriaHora CategoriaHora { get; set; } = null!;
}

/// <summary>
/// Vista de base de datos: asistencia.v_horas_por_turno (solo lectura).
/// </summary>
public class VistaHorasPorTurno
{
    public long IdAsignacion { get; set; }
    public long IdEmpleado { get; set; }
    public DateOnly Fecha { get; set; }
    public int IdTurno { get; set; }
    public int MinutosProgramados { get; set; }
    public int MinutosTrabajados { get; set; }
    public DateTimeOffset? HoraEntrada { get; set; }
    public DateTimeOffset? HoraSalida { get; set; }
    public int? MinutosTarde { get; set; }
    public bool SinAsistencia { get; set; }
}

