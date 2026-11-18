using GestionTurnos.Api.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>
/// seguridad.usuario_foto_perfil (sql/migracion_foto_perfil.sql). Se registra sola mediante
/// ApplyConfigurationsFromAssembly; se consulta con <c>context.Set&lt;UsuarioFotoPerfil&gt;()</c>.
/// </summary>
public class UsuarioFotoPerfilConfiguration : IEntityTypeConfiguration<UsuarioFotoPerfil>
{
    public void Configure(EntityTypeBuilder<UsuarioFotoPerfil> builder)
    {
        builder.ToTable("usuario_foto_perfil", "seguridad");
        builder.HasKey(x => x.IdUsuario);

        builder.Property(x => x.IdUsuario)
            .HasColumnName("id_usuario")
            .ValueGeneratedNever();

        builder.Property(x => x.Url)
            .HasColumnName("url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.PublicId)
            .HasColumnName("public_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ActualizadoEn)
            .HasColumnName("actualizado_en")
            .IsRequired();

        builder.HasOne(x => x.Usuario)
            .WithOne()
            .HasForeignKey<UsuarioFotoPerfil>(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
