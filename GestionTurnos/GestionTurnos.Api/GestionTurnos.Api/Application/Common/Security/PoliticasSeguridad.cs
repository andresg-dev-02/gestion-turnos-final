namespace GestionTurnos.Api.Application.Common.Security;

/// <summary>
/// Constantes tipadas para políticas y códigos de permisos del sistema de Gestión de Turnos y Asistencia (Sección 2).
/// Sincronizado con <see cref="PermisosSistema"/> y <c>sql/seed_permisos_administrador.sql</c>.
/// </summary>
public static class PoliticasSeguridad
{
    public const string Autenticado = "PoliticaAutenticado";

    // Códigos de los 13 permisos finos (RF-01, RG-08)
    public const string UsuariosGestionar = PermisosSistema.UsuariosGestionar;
    public const string RegistrosAprobar = PermisosSistema.RegistrosAprobar;
    public const string EmpleadosGestionar = PermisosSistema.EmpleadosGestionar;
    public const string OrganizacionGestionar = PermisosSistema.OrganizacionGestionar;
    public const string TurnosConfigurar = PermisosSistema.TurnosConfigurar;
    public const string TurnosProgramar = PermisosSistema.TurnosProgramar;
    public const string AsistenciaConsultar = PermisosSistema.AsistenciaConsultar;
    public const string AsistenciaRegistrarManual = PermisosSistema.AsistenciaRegistrarManual;
    public const string VacacionesAprobar = PermisosSistema.VacacionesAprobar;
    public const string NovedadesAprobar = PermisosSistema.NovedadesAprobar;
    public const string ParametrosEditar = PermisosSistema.ParametrosEditar;
    public const string ReportesConsultar = PermisosSistema.ReportesConsultar;
    public const string AuditoriaConsultar = PermisosSistema.AuditoriaConsultar;
}
