using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Organizacion;

public class Sucursal : BaseEntity
{
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Area> Areas { get; set; } = new List<Area>();
}

public class Area : BaseEntity
{
    public int IdArea { get; set; }
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    public Sucursal Sucursal { get; set; } = null!;
    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}

public class Cargo : BaseEntity
{
    public int IdCargo { get; set; }
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;

    public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
}

public class Empleado : BaseEntity
{
    public long IdEmpleado { get; set; }
    public long? IdUsuario { get; set; }
    public string CodTipoDocumento { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string? CorreoContacto { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public int IdArea { get; set; }
    public int IdCargo { get; set; }
    public string? CodTipoContrato { get; set; }
    public DateOnly FechaIngreso { get; set; }
    public DateOnly? FechaRetiro { get; set; }
    public string CodEstadoEmpleado { get; set; } = EstadosEmpleado.Activo;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Usuario? Usuario { get; set; }
    public TipoDocumento TipoDocumento { get; set; } = null!;
    public Area Area { get; set; } = null!;
    public Cargo Cargo { get; set; } = null!;
    public TipoContrato? TipoContrato { get; set; }
    public EstadoEmpleado EstadoEmpleado { get; set; } = null!;
}

