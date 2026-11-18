namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Códigos de permiso del sistema (Sección 2). Coinciden con <c>seguridad.permiso.codigo</c>.
/// La autorización se hace siempre por código de permiso, nunca por nombre de rol.
/// Debe mantenerse sincronizado con <c>sql/seed_permisos_administrador.sql</c>.
/// </summary>
public static class PermisosSistema
{
    public const string UsuariosGestionar = "usuarios.gestionar";
    public const string RegistrosAprobar = "registros.aprobar";
    public const string EmpleadosGestionar = "empleados.gestionar";
    public const string OrganizacionGestionar = "organizacion.gestionar";
    public const string TurnosConfigurar = "turnos.configurar";
    public const string TurnosProgramar = "turnos.programar";
    public const string AsistenciaConsultar = "asistencia.consultar";
    public const string AsistenciaRegistrarManual = "asistencia.registrar_manual";
    public const string VacacionesAprobar = "vacaciones.aprobar";
    public const string NovedadesAprobar = "novedades.aprobar";
    public const string ParametrosEditar = "parametros.editar";
    public const string ReportesConsultar = "reportes.consultar";
    public const string AuditoriaConsultar = "auditoria.consultar";

    /// <summary>
    /// Catálogo completo (código y descripción) de los 13 permisos que recibe el rol Administrador.
    /// </summary>
    public static readonly IReadOnlyList<(string Codigo, string Descripcion)> Catalogo =
    [
        (UsuariosGestionar, "Crear, activar y desactivar usuarios; administrar roles y permisos"),
        (RegistrosAprobar, "Aprobar o rechazar solicitudes de registro"),
        (EmpleadosGestionar, "Crear, actualizar, desactivar y reactivar empleados"),
        (OrganizacionGestionar, "Sucursales, áreas y cargos"),
        (TurnosConfigurar, "Tipos de turno"),
        (TurnosProgramar, "Asignar, modificar, anular y publicar turnos; descansos y cambios de turno"),
        (AsistenciaConsultar, "Ver la asistencia de todos los empleados"),
        (AsistenciaRegistrarManual, "Registrar o corregir marcaciones"),
        (VacacionesAprobar, "Registrar días causados y resolver solicitudes de vacaciones"),
        (NovedadesAprobar, "Resolver y registrar permisos y ausencias"),
        (ParametrosEditar, "Parámetros de negocio y catálogos"),
        (ReportesConsultar, "Calendario general y reportes"),
        (AuditoriaConsultar, "Consultar la bitácora de auditoría")
    ];

    /// <summary>Los 13 códigos de permiso del sistema.</summary>
    public static readonly IReadOnlyList<string> Todos = Catalogo.Select(p => p.Codigo).ToArray();
}
