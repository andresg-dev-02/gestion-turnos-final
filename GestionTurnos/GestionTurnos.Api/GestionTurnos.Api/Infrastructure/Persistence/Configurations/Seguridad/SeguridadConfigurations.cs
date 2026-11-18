using GestionTurnos.Api.Domain.Entities.Seguridad;
using GestionTurnos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Seguridad;

public class RolConfiguration : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> builder)
    {
        builder.ToTable("rol", "seguridad");
        builder.HasKey(x => x.IdRol);

        builder.Property(x => x.IdRol)
            .HasColumnName("id_rol")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200);

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class PermisoConfiguration : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> builder)
    {
        builder.ToTable("permiso", "seguridad");
        builder.HasKey(x => x.IdPermiso);

        builder.Property(x => x.IdPermiso)
            .HasColumnName("id_permiso")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Codigo)
            .HasColumnName("codigo")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Descripcion)
            .HasColumnName("descripcion")
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.Codigo).IsUnique();
    }
}

public class RolPermisoConfiguration : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> builder)
    {
        builder.ToTable("rol_permiso", "seguridad");
        builder.HasKey(x => new { x.IdRol, x.IdPermiso });

        builder.Property(x => x.IdRol)
            .HasColumnName("id_rol");

        builder.Property(x => x.IdPermiso)
            .HasColumnName("id_permiso");

        builder.HasOne(x => x.Rol)
            .WithMany(r => r.RolPermisos)
            .HasForeignKey(x => x.IdRol)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Permiso)
            .WithMany(p => p.RolPermisos)
            .HasForeignKey(x => x.IdPermiso)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", "seguridad");
        builder.HasKey(x => x.IdUsuario);

        builder.Property(x => x.IdUsuario)
            .HasColumnName("id_usuario")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Correo)
            .HasColumnName("correo")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.HashContrasena)
            .HasColumnName("hash_contrasena")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IdRol)
            .HasColumnName("id_rol")
            .IsRequired();

        builder.Property(x => x.CodEstadoUsuario)
            .HasColumnName("cod_estado_usuario")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosUsuario.Activo)
            .IsRequired();

        builder.Property(x => x.UltimoAcceso)
            .HasColumnName("ultimo_acceso");

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.HasIndex(x => x.Correo)
            .HasDatabaseName("uq_usuario_correo")
            .IsUnique();

        builder.HasOne(x => x.Rol)
            .WithMany(r => r.Usuarios)
            .HasForeignKey(x => x.IdRol)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoUsuario)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SolicitudRegistroConfiguration : IEntityTypeConfiguration<SolicitudRegistro>
{
    public void Configure(EntityTypeBuilder<SolicitudRegistro> builder)
    {
        builder.ToTable("solicitud_registro", "seguridad");
        builder.HasKey(x => x.IdSolicitudRegistro);

        builder.Property(x => x.IdSolicitudRegistro)
            .HasColumnName("id_solicitud_registro")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CodTipoDocumento)
            .HasColumnName("cod_tipo_documento")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.NumeroDocumento)
            .HasColumnName("numero_documento")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Nombres)
            .HasColumnName("nombres")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Apellidos)
            .HasColumnName("apellidos")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Correo)
            .HasColumnName("correo")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(30);

        builder.Property(x => x.HashContrasena)
            .HasColumnName("hash_contrasena")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.CodEstadoSolicitud)
            .HasColumnName("cod_estado_solicitud")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosSolicitud.Pendiente)
            .IsRequired();

        builder.Property(x => x.SolicitadoEn)
            .HasColumnName("solicitado_en")
            .IsRequired();

        builder.Property(x => x.RevisadoPor)
            .HasColumnName("revisado_por");

        builder.Property(x => x.RevisadoEn)
            .HasColumnName("revisado_en");

        builder.Property(x => x.MotivoRechazo)
            .HasColumnName("motivo_rechazo")
            .HasMaxLength(300);

        builder.Property(x => x.IdUsuarioCreado)
            .HasColumnName("id_usuario_creado");

        builder.HasOne(x => x.TipoDocumento)
            .WithMany()
            .HasForeignKey(x => x.CodTipoDocumento)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoSolicitud)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoSolicitud)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Revisor)
            .WithMany()
            .HasForeignKey(x => x.RevisadoPor)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsuarioCreado)
            .WithMany()
            .HasForeignKey(x => x.IdUsuarioCreado)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

