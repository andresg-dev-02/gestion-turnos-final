using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Entities.Novedades;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Entities.Turnos;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence.Context;

/// <summary>
/// DbContext principal de Gestión de Turnos y Asistencia.
/// Mapea los 7 esquemas: parametrizacion, seguridad, organizacion, turnos, asistencia, novedades y auditoria.
/// </summary>
public class GestionTurnosContext : DbContext
{
    public GestionTurnosContext(DbContextOptions<GestionTurnosContext> options) : base(options)
    {
    }

    // 1. Esquema parametrizacion
    public DbSet<TipoDocumento> TiposDocumento => Set<TipoDocumento>();
    public DbSet<TipoContrato> TiposContrato => Set<TipoContrato>();
    public DbSet<EstadoUsuario> EstadosUsuario => Set<EstadoUsuario>();
    public DbSet<EstadoEmpleado> EstadosEmpleado => Set<EstadoEmpleado>();
    public DbSet<EstadoSolicitud> EstadosSolicitud => Set<EstadoSolicitud>();
    public DbSet<EstadoAsignacion> EstadosAsignacion => Set<EstadoAsignacion>();
    public DbSet<TipoNovedad> TiposNovedad => Set<TipoNovedad>();
    public DbSet<CategoriaHora> CategoriasHora => Set<CategoriaHora>();
    public DbSet<FuenteMarcacion> FuentesMarcacion => Set<FuenteMarcacion>();
    public DbSet<TipoAccion> TiposAccion => Set<TipoAccion>();
    public DbSet<DiaFestivo> DiasFestivos => Set<DiaFestivo>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<ParametroValor> ParametroValores => Set<ParametroValor>();

    // 2. Esquema seguridad
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolesPermisos => Set<RolPermiso>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<SolicitudRegistro> SolicitudesRegistro => Set<SolicitudRegistro>();

    // 3. Esquema organizacion
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Cargo> Cargos => Set<Cargo>();
    public DbSet<Empleado> Empleados => Set<Empleado>();

    // 4. Esquema turnos
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Asignacion> Asignaciones => Set<Asignacion>();
    public DbSet<DiaDescanso> DiasDescanso => Set<DiaDescanso>();
    public DbSet<SolicitudCambioTurno> SolicitudesCambioTurno => Set<SolicitudCambioTurno>();
    public DbSet<VistaCalendario> VistaCalendario => Set<VistaCalendario>();

    // 5. Esquema asistencia
    public DbSet<RegistroAsistencia> RegistrosAsistencia => Set<RegistroAsistencia>();
    public DbSet<DetalleHoras> DetallesHoras => Set<DetalleHoras>();
    public DbSet<VistaHorasPorTurno> VistaHorasPorTurno => Set<VistaHorasPorTurno>();

    // 6. Esquema novedades
    public DbSet<PeriodoVacacional> PeriodosVacacionales => Set<PeriodoVacacional>();
    public DbSet<SolicitudVacaciones> SolicitudesVacaciones => Set<SolicitudVacaciones>();
    public DbSet<Novedad> Novedades => Set<Novedad>();
    public DbSet<VistaSaldoVacaciones> VistaSaldosVacaciones => Set<VistaSaldoVacaciones>();

    // 7. Esquema auditoria
    public DbSet<BitacoraNegocio> BitacoraNegocio => Set<BitacoraNegocio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GestionTurnosContext).Assembly);

        // Tipos nativos de PostgreSQL (tstzrange, jsonb, inet) y timestamptz en UTC.
        if (Database.IsNpgsql())
        {
            PostgreSqlModelConfiguration.Aplicar(modelBuilder);
        }

        base.OnModelCreating(modelBuilder);
    }
}
