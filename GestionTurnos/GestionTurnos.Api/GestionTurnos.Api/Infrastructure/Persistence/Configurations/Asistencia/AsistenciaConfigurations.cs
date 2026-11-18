using GestionTurnos.Api.Domain.Entities.Asistencia;
using GestionTurnos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Asistencia;

public class RegistroAsistenciaConfiguration : IEntityTypeConfiguration<RegistroAsistencia>
{
    public void Configure(EntityTypeBuilder<RegistroAsistencia> builder)
    {
        builder.ToTable("registro", "asistencia");
        builder.HasKey(x => x.IdRegistro);

        builder.Property(x => x.IdRegistro)
            .HasColumnName("id_registro")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .IsRequired();

        builder.Property(x => x.IdAsignacion)
            .HasColumnName("id_asignacion");

        builder.Property(x => x.HoraEntrada)
            .HasColumnName("hora_entrada")
            .IsRequired();

        builder.Property(x => x.HoraSalida)
            .HasColumnName("hora_salida");

        builder.Property(x => x.SalidaNoRegistrada)
            .HasColumnName("salida_no_registrada")
            .IsRequired();

        builder.Property(x => x.CodFuenteMarcacion)
            .HasColumnName("cod_fuente_marcacion")
            .HasMaxLength(20)
            .HasDefaultValue(FuentesMarcacionCodigos.Web)
            .IsRequired();

        builder.Property(x => x.Observacion)
            .HasColumnName("observacion")
            .HasMaxLength(300);

        builder.Property(x => x.RegistradoPor)
            .HasColumnName("registrado_por");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.HasIndex(x => x.IdAsignacion).IsUnique();
        builder.HasIndex(x => new { x.IdEmpleado, x.HoraEntrada }).HasDatabaseName("ix_registro_empleado");

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Asignacion)
            .WithMany()
            .HasForeignKey(x => x.IdAsignacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FuenteMarcacion)
            .WithMany()
            .HasForeignKey(x => x.CodFuenteMarcacion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Registrador)
            .WithMany()
            .HasForeignKey(x => x.RegistradoPor)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DetalleHorasConfiguration : IEntityTypeConfiguration<DetalleHoras>
{
    public void Configure(EntityTypeBuilder<DetalleHoras> builder)
    {
        builder.ToTable("detalle_horas", "asistencia");
        builder.HasKey(x => new { x.IdRegistro, x.CodCategoriaHora });

        builder.Property(x => x.IdRegistro)
            .HasColumnName("id_registro");

        builder.Property(x => x.CodCategoriaHora)
            .HasColumnName("cod_categoria_hora")
            .HasMaxLength(30);

        builder.Property(x => x.Minutos)
            .HasColumnName("minutos")
            .IsRequired();

        builder.HasOne(x => x.Registro)
            .WithMany(r => r.DetallesHoras)
            .HasForeignKey(x => x.IdRegistro)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CategoriaHora)
            .WithMany()
            .HasForeignKey(x => x.CodCategoriaHora)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class VistaHorasPorTurnoConfiguration : IEntityTypeConfiguration<VistaHorasPorTurno>
{
    public void Configure(EntityTypeBuilder<VistaHorasPorTurno> builder)
    {
        builder.HasNoKey();
        builder.ToView("v_horas_por_turno", "asistencia");

        builder.Property(x => x.IdAsignacion).HasColumnName("id_asignacion");
        builder.Property(x => x.IdEmpleado).HasColumnName("id_empleado");
        builder.Property(x => x.Fecha).HasColumnName("fecha");
        builder.Property(x => x.IdTurno).HasColumnName("id_turno");
        builder.Property(x => x.MinutosProgramados).HasColumnName("minutos_programados");
        builder.Property(x => x.MinutosTrabajados).HasColumnName("minutos_trabajados");
        builder.Property(x => x.HoraEntrada).HasColumnName("hora_entrada");
        builder.Property(x => x.HoraSalida).HasColumnName("hora_salida");
        builder.Property(x => x.MinutosTarde).HasColumnName("minutos_tarde");
        builder.Property(x => x.SinAsistencia).HasColumnName("sin_asistencia");
    }
}

