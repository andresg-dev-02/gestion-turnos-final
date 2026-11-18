using GestionTurnos.Api.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// seguridad.usuario_dos_pasos (sql/migracion_correo_dos_pasos.sql). Se registra sola mediante
/// ApplyConfigurationsFromAssembly; se consulta con <c>context.Set&lt;UsuarioDosPasos&gt;()</c>.
/// </summary>
public class UsuarioDosPasosConfiguration : IEntityTypeConfiguration<UsuarioDosPasos>
{
    public void Configure(EntityTypeBuilder<UsuarioDosPasos> builder)
    {
        builder.ToTable("usuario_dos_pasos", "seguridad");
        builder.HasKey(x => x.IdUsuario);

        builder.Property(x => x.IdUsuario)
            .HasColumnName("id_usuario")
            .ValueGeneratedNever();

        builder.Property(x => x.Habilitado)
            .HasColumnName("habilitado")
            .IsRequired();

        builder.Property(x => x.HabilitadoEn)
            .HasColumnName("habilitado_en");

        builder.Property(x => x.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .IsRequired();

        builder.Property(x => x.ActualizadoPor)
            .HasColumnName("actualizado_por");

        builder.HasOne(x => x.Usuario)
            .WithOne()
            .HasForeignKey<UsuarioDosPasos>(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
