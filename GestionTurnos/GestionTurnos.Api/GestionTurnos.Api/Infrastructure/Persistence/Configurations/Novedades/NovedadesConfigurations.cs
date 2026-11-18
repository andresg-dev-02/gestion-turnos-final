using GestionTurnos.Api.Domain.Entities.Novedades;
using GestionTurnos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Novedades;

public class PeriodoVacacionalConfiguration : IEntityTypeConfiguration<PeriodoVacacional>
{
    public void Configure(EntityTypeBuilder<PeriodoVacacional> builder)
    {
        builder.ToTable("periodo_vacacional", "novedades");
        builder.HasKey(x => x.IdPeriodoVacacional);

        builder.Property(x => x.IdPeriodoVacacional)
            .HasColumnName("id_periodo_vacacional")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.FechaInicio)
            .HasColumnName("fecha_inicio")
            .IsRequired();

        builder.Property(x => x.FechaFin)
            .HasColumnName("fecha_fin")
            .IsRequired();

        builder.Property(x => x.DiasOtorgados)
            .HasColumnName("dias_otorgados")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.HasIndex(x => new { x.IdEmpleado, x.FechaInicio }).IsUnique();

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SolicitudVacacionesConfiguration : IEntityTypeConfiguration<SolicitudVacaciones>
{
    public void Configure(EntityTypeBuilder<SolicitudVacaciones> builder)
    {
        builder.ToTable("solicitud_vacaciones", "novedades");
        builder.HasKey(x => x.IdSolicitudVacaciones);

        builder.Property(x => x.IdSolicitudVacaciones)
            .HasColumnName("id_solicitud_vacaciones")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.FechaInicio)
            .HasColumnName("fecha_inicio")
            .IsRequired();

        builder.Property(x => x.FechaFin)
            .HasColumnName("fecha_fin")
            .IsRequired();

        builder.Property(x => x.DiasSolicitados)
            .HasColumnName("dias_solicitados")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.CodEstadoSolicitud)
            .HasColumnName("cod_estado_solicitud")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosSolicitud.Pendiente)
            .IsRequired();

        builder.Property(x => x.Observacion)
            .HasColumnName("observacion")
            .HasMaxLength(300);

        builder.Property(x => x.SolicitadoPor)
            .HasColumnName("solicitado_por")
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

        builder.HasIndex(x => new { x.IdEmpleado, x.FechaInicio }).HasDatabaseName("ix_vacaciones_empleado");

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoSolicitud)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoSolicitud)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Solicitante)
            .WithMany()
            .HasForeignKey(x => x.SolicitadoPor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Resolutor)
            .WithMany()
            .HasForeignKey(x => x.ResueltoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NovedadConfiguration : IEntityTypeConfiguration<Novedad>
{
    public void Configure(EntityTypeBuilder<Novedad> builder)
    {
        builder.ToTable("novedad", "novedades");
        builder.HasKey(x => x.IdNovedad);

        builder.Property(x => x.IdNovedad)
            .HasColumnName("id_novedad")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.CodTipoNovedad)
            .HasColumnName("cod_tipo_novedad")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Inicio)
            .HasColumnName("inicio")
            .IsRequired();

        builder.Property(x => x.Fin)
            .HasColumnName("fin")
            .IsRequired();

        builder.Property(x => x.Remunerado)
            .HasColumnName("remunerado")
            .IsRequired();

        builder.Property(x => x.AfectaHoras)
            .HasColumnName("afecta_horas")
            .IsRequired();

        builder.Property(x => x.CodEstadoSolicitud)
            .HasColumnName("cod_estado_solicitud")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosSolicitud.Pendiente)
            .IsRequired();

        builder.Property(x => x.Observaciones)
            .HasColumnName("observaciones");

        builder.Property(x => x.UrlSoporte)
            .HasColumnName("url_soporte")
            .HasMaxLength(500);

        builder.Property(x => x.RegistradoPor)
            .HasColumnName("registrado_por")
            .IsRequired();

        builder.Property(x => x.RegistradoEn)
            .HasColumnName("registrado_en")
            .IsRequired();

        builder.Property(x => x.ResueltoPor)
            .HasColumnName("resuelto_por");

        builder.Property(x => x.ResueltoEn)
            .HasColumnName("resuelto_en");

        builder.Property(x => x.MotivoRechazo)
            .HasColumnName("motivo_rechazo")
            .HasMaxLength(300);

        builder.HasIndex(x => new { x.IdEmpleado, x.Inicio }).HasDatabaseName("ix_novedad_empleado");

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoNovedad)
            .WithMany()
            .HasForeignKey(x => x.CodTipoNovedad)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoSolicitud)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoSolicitud)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Registrador)
            .WithMany()
            .HasForeignKey(x => x.RegistradoPor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Resolutor)
            .WithMany()
            .HasForeignKey(x => x.ResueltoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VistaSaldoVacacionesConfiguration : IEntityTypeConfiguration<VistaSaldoVacaciones>
{
    public void Configure(EntityTypeBuilder<VistaSaldoVacaciones> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_saldo_vacaciones", "novedades");

        builder.Property(x => x.IdEmpleado).HasColumnName("id_empleado");
        builder.Property(x => x.DiasOtorgados).HasColumnName("dias_otorgados").HasPrecision(8, 2);
        builder.Property(x => x.DiasUtilizados).HasColumnName("dias_utilizados").HasPrecision(8, 2);
        builder.Property(x => x.DiasEnSolicitud).HasColumnName("dias_en_solicitud").HasPrecision(8, 2);
        builder.Property(x => x.DiasDisponibles).HasColumnName("dias_disponibles").HasPrecision(8, 2);
    }
}

