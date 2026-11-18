using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Seguridad;

public class Rol : BaseEntity
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}

public class Permiso : BaseEntity
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;

    public ICollection<RolPermiso> RolPermisos { get; set; } = new List<RolPermiso>();
}

public class RolPermiso : BaseEntity
{
    public int IdRol { get; set; }
    public int IdPermiso { get; set; }

    public Rol Rol { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
}

public class Usuario : BaseEntity
{
    public long IdUsuario { get; set; }
    public string Correo { get; set; } = null!;
    public string HashContrasena { get; set; } = null!;
    public int IdRol { get; set; }
    public string CodEstadoUsuario { get; set; } = EstadosUsuario.Activo;
    public DateTimeOffset? UltimoAcceso { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public Rol Rol { get; set; } = null!;
    public EstadoUsuario EstadoUsuario { get; set; } = null!;
    public Empleado? Empleado { get; set; }
}

public class SolicitudRegistro : BaseEntity
{
    public long IdSolicitudRegistro { get; set; }
    public string CodTipoDocumento { get; set; } = null!;
    public string NumeroDocumento { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string Correo { get; set; } = null!;
    public string? Telefono { get; set; }
    public string HashContrasena { get; set; } = null!;
    public string CodEstadoSolicitud { get; set; } = EstadosSolicitud.Pendiente;
    public DateTimeOffset SolicitadoEn { get; set; } = DateTimeOffset.UtcNow;
    public long? RevisadoPor { get; set; }
    public DateTimeOffset? RevisadoEn { get; set; }
    public string? MotivoRechazo { get; set; }
    public long? IdUsuarioCreado { get; set; }

    public TipoDocumento TipoDocumento { get; set; } = null!;
    public EstadoSolicitud EstadoSolicitud { get; set; } = null!;
    public Usuario? Revisor { get; set; }
    public Usuario? UsuarioCreado { get; set; }
}

