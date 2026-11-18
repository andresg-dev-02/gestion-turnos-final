using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.Enums;
using GestionTurnos.Api.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Turnos;

public class TurnoConfiguration : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("turno", "turnos");
        builder.HasKey(x => x.IdTurno);

        builder.Property(x => x.IdTurno)
            .HasColumnName("id_turno")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.HoraInicio)
            .HasColumnName("hora_inicio")
            .IsRequired();

        builder.Property(x => x.HoraFin)
            .HasColumnName("hora_fin")
            .IsRequired();

        builder.Property(x => x.MinutosDescanso)
            .HasColumnName("minutos_descanso")
            .HasDefaultValue(0)
            .IsRequired();

        // Regla Sección 1: No escribas turnos.turno.cruza_medianoche ni duracion_minutos
        var cruzaProp = builder.Property(x => x.CruzaMedianoche)
            .HasColumnName("cruza_medianoche")
            .ValueGeneratedOnAddOrUpdate();
        cruzaProp.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        cruzaProp.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        var duracionProp = builder.Property(x => x.DuracionMinutos)
            .HasColumnName("duracion_minutos")
            .ValueGeneratedOnAddOrUpdate();
        duracionProp.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        duracionProp.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder.Property(x => x.Color)
            .HasColumnName("color")
            .HasMaxLength(7);

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class AsignacionConfiguration : IEntityTypeConfiguration<Asignacion>
{
    public void Configure(EntityTypeBuilder<Asignacion> builder)
    {
        builder.ToTable("asignacion", "turnos");
        builder.HasKey(x => x.IdAsignacion);

        builder.Property(x => x.IdAsignacion)
            .HasColumnName("id_asignacion")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.IdTurno)
            .HasColumnName("id_turno")
            .IsRequired();

        builder.Property(x => x.Fecha)
            .HasColumnName("fecha")
            .IsRequired();

        builder.Property(x => x.Periodo)
            .HasColumnName("periodo")
            .HasConversion(
                v => v.ToString(),
                v => PeriodoRango.Parse(v))
            .IsRequired();

        builder.Property(x => x.CodEstadoAsignacion)
            .HasColumnName("cod_estado_asignacion")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosAsignacion.Programada)
            .IsRequired();

        builder.Property(x => x.Observacion)
            .HasColumnName("observacion")
            .HasMaxLength(300);

        builder.Property(x => x.CreadoPor)
            .HasColumnName("creado_por")
            .IsRequired();

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.HasIndex(x => x.Fecha).HasDatabaseName("ix_asignacion_fecha");
        builder.HasIndex(x => new { x.IdEmpleado, x.Fecha }).HasDatabaseName("ix_asignacion_empleado");
        builder.HasIndex(x => new { x.IdTurno, x.Fecha }).HasDatabaseName("ix_asignacion_turno");
        builder.HasIndex(x => new { x.IdAsignacion, x.IdEmpleado }).IsUnique();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany(t => t.Asignaciones)
            .HasForeignKey(x => x.IdTurno)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoAsignacion)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoAsignacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Creador)
            .WithMany()
            .HasForeignKey(x => x.CreadoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DiaDescansoConfiguration : IEntityTypeConfiguration<DiaDescanso>
{
    public void Configure(EntityTypeBuilder<DiaDescanso> builder)
    {
        builder.ToTable("dia_descanso", "turnos");
        builder.HasKey(x => x.IdDiaDescanso);

        builder.Property(x => x.IdDiaDescanso)
            .HasColumnName("id_dia_descanso")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.Fecha)
            .HasColumnName("fecha")
            .IsRequired();

        builder.Property(x => x.CreadoPor)
            .HasColumnName("creado_por")
            .IsRequired();

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.HasIndex(x => new { x.IdEmpleado, x.Fecha }).IsUnique();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Creador)
            .WithMany()
            .HasForeignKey(x => x.CreadoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SolicitudCambioTurnoConfiguration : IEntityTypeConfiguration<SolicitudCambioTurno>
{
    public void Configure(EntityTypeBuilder<SolicitudCambioTurno> builder)
    {
        builder.ToTable("solicitud_cambio_turno", "turnos");
        builder.HasKey(x => x.IdSolicitudCambio);

        builder.Property(x => x.IdSolicitudCambio)
            .HasColumnName("id_solicitud_cambio")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdAsignacionSolicitante)
            .HasColumnName("id_asignacion_solicitante")
            .IsRequired();

        builder.Property(x => x.IdAsignacionCompanero)
            .HasColumnName("id_asignacion_companero")
            .IsRequired();

        builder.Property(x => x.Motivo)
            .HasColumnName("motivo")
            .HasMaxLength(300);

        builder.Property(x => x.CodEstadoSolicitud)
            .HasColumnName("cod_estado_solicitud")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosSolicitud.Pendiente)
            .IsRequired();

        builder.Property(x => x.SolicitadoEn)
            .HasColumnName("solicitado_en")
            .IsRequired();

        builder.Property(x => x.ResueltoPor)
            .HasColumnName("resuelto_por");

        builder.Property(x => x.ResueltoEn)
            .HasColumnName("resuelto_en");

        builder.Property(x => x.MotivoRechazo)
            .HasColumnName("motivo_rechazo")
            .HasMaxLength(300);

        builder.HasOne(x => x.AsignacionSolicitante)
            .WithMany()
            .HasForeignKey(x => x.IdAsignacionSolicitante)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AsignacionCompanero)
            .WithMany()
            .HasForeignKey(x => x.IdAsignacionCompanero)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoSolicitud)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoSolicitud)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Resolutor)
            .WithMany()
            .HasForeignKey(x => x.ResueltoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VistaCalendarioConfiguration : IEntityTypeConfiguration<VistaCalendario>
{
    public void Configure(EntityTypeBuilder<VistaCalendario> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_calendario", "turnos");

        builder.Property(x => x.IdEmpleado).HasColumnName("id_empleado");
        builder.Property(x => x.Tipo).HasColumnName("tipo");
        builder.Property(x => x.Detalle).HasColumnName("detalle");
        builder.Property(x => x.Inicio).HasColumnName("inicio");
        builder.Property(x => x.Fin).HasColumnName("fin");
    }
}

