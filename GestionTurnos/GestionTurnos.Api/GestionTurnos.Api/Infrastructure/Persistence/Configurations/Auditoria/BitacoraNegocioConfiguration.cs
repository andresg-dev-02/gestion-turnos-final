using GestionTurnos.Api.Domain.Entities.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Auditoria;

public class BitacoraNegocioConfiguration : IEntityTypeConfiguration<BitacoraNegocio>
{
    public void Configure(EntityTypeBuilder<BitacoraNegocio> builder)
    {
        builder.ToTable("bitacora_negocio", "auditoria");
        builder.HasKey(x => x.IdBitacora);

        builder.Property(x => x.IdBitacora)
            .HasColumnName("id_bitacora")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FechaHora)
            .HasColumnName("fecha_hora")
            .IsRequired();

        builder.Property(x => x.IdUsuario)
            .HasColumnName("id_usuario");

        builder.Property(x => x.CodTipoAccion)
            .HasColumnName("cod_tipo_accion")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Esquema)
            .HasColumnName("esquema")
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(x => x.Tabla)
            .HasColumnName("tabla")
            .HasMaxLength(63)
            .IsRequired();

        builder.Property(x => x.IdRegistro)
            .HasColumnName("id_registro")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado");

        builder.Property(x => x.ValorAnterior)
            .HasColumnName("valor_anterior");

        builder.Property(x => x.ValorNuevo)
            .HasColumnName("valor_nuevo");

        builder.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(300);

        builder.Property(x => x.IpOrigen)
            .HasColumnName("ip_origen");

        builder.HasIndex(x => new { x.IdEmpleado, x.FechaHora }).HasDatabaseName("ix_bitacora_empleado");
        builder.HasIndex(x => x.FechaHora).HasDatabaseName("ix_bitacora_fecha");
        builder.HasIndex(x => new { x.CodTipoAccion, x.FechaHora }).HasDatabaseName("ix_bitacora_accion");
        builder.HasIndex(x => new { x.Esquema, x.Tabla, x.IdRegistro }).HasDatabaseName("ix_bitacora_registro");

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoAccion)
            .WithMany()
            .HasForeignKey(x => x.CodTipoAccion)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Empleado)
            .WithMany()
            .HasForeignKey(x => x.IdEmpleado)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

