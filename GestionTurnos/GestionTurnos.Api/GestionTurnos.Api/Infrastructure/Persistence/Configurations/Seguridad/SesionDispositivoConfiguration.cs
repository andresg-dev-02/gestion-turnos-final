using GestionTurnos.Api.Domain.Entities.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Seguridad;

/// <summary>seguridad.sesion_dispositivo (sql/migracion_pwa_movil.sql).</summary>
public class SesionDispositivoConfiguration : IEntityTypeConfiguration<SesionDispositivo>
{
    public void Configure(EntityTypeBuilder<SesionDispositivo> builder)
    {
        builder.ToTable("sesion_dispositivo", "seguridad");
        builder.HasKey(x => x.IdSesion);

        builder.Property(x => x.IdSesion).HasColumnName("id_sesion").UseIdentityAlwaysColumn();
        builder.Property(x => x.IdUsuario).HasColumnName("id_usuario").IsRequired();
        builder.Property(x => x.HashToken).HasColumnName("hash_token").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Familia).HasColumnName("familia").IsRequired();
        builder.Property(x => x.Recordar).HasColumnName("recordar").IsRequired();
        builder.Property(x => x.CreadoEn).HasColumnName("creado_en").IsRequired();
        builder.Property(x => x.ExpiraEn).HasColumnName("expira_en").IsRequired();
        builder.Property(x => x.RevocadaEn).HasColumnName("revocada_en");
        builder.Property(x => x.MotivoRevocacion).HasColumnName("motivo_revocacion").HasMaxLength(30);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(300);
        builder.Property(x => x.IpOrigen).HasColumnName("ip_origen").HasMaxLength(64);

        builder.HasIndex(x => x.HashToken).IsUnique();

        builder.HasOne(x => x.Usuario)
            .WithMany()
            .HasForeignKey(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
