using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Organizacion;

public class SucursalConfiguration : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> builder)
    {
        builder.ToTable("sucursal", "organizacion");
        builder.HasKey(x => x.IdSucursal);

        builder.Property(x => x.IdSucursal)
            .HasColumnName("id_sucursal")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Direccion)
            .HasColumnName("direccion")
            .HasMaxLength(200);

        builder.Property(x => x.Ciudad)
            .HasColumnName("ciudad")
            .HasMaxLength(80);

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("area", "organizacion");
        builder.HasKey(x => x.IdArea);

        builder.Property(x => x.IdArea)
            .HasColumnName("id_area")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdSucursal)
            .HasColumnName("id_sucursal")
            .IsRequired();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => new { x.IdSucursal, x.Nombre }).IsUnique();

        builder.HasOne(x => x.Sucursal)
            .WithMany(s => s.Areas)
            .HasForeignKey(x => x.IdSucursal)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CargoConfiguration : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("cargo", "organizacion");
        builder.HasKey(x => x.IdCargo);

        builder.Property(x => x.IdCargo)
            .HasColumnName("id_cargo")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class EmpleadoConfiguration : IEntityTypeConfiguration<Empleado>
{
    public void Configure(EntityTypeBuilder<Empleado> builder)
    {
        builder.ToTable("empleado", "organizacion");
        builder.HasKey(x => x.IdEmpleado);

        builder.Property(x => x.IdEmpleado)
            .HasColumnName("id_empleado")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.IdUsuario)
            .HasColumnName("id_usuario");

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

        builder.Property(x => x.CorreoContacto)
            .HasColumnName("correo_contacto")
            .HasMaxLength(150);

        builder.Property(x => x.Telefono)
            .HasColumnName("telefono")
            .HasMaxLength(30);

        builder.Property(x => x.Direccion)
            .HasColumnName("direccion")
            .HasMaxLength(200);

        builder.Property(x => x.IdArea)
            .HasColumnName("id_area")
            .IsRequired();

        builder.Property(x => x.IdCargo)
            .HasColumnName("id_cargo")
            .IsRequired();

        builder.Property(x => x.CodTipoContrato)
            .HasColumnName("cod_tipo_contrato")
            .HasMaxLength(20);

        builder.Property(x => x.FechaIngreso)
            .HasColumnName("fecha_ingreso")
            .IsRequired();

        builder.Property(x => x.FechaRetiro)
            .HasColumnName("fecha_retiro");

        builder.Property(x => x.CodEstadoEmpleado)
            .HasColumnName("cod_estado_empleado")
            .HasMaxLength(20)
            .HasDefaultValue(EstadosEmpleado.Activo)
            .IsRequired();

        builder.Property(x => x.CreadoEn)
            .HasColumnName("creado_en")
            .IsRequired();

        builder.HasIndex(x => x.IdUsuario).IsUnique();
        builder.HasIndex(x => new { x.CodTipoDocumento, x.NumeroDocumento }).IsUnique();
        builder.HasIndex(x => x.IdArea).HasDatabaseName("ix_empleado_area");
        builder.HasIndex(x => x.IdCargo).HasDatabaseName("ix_empleado_cargo");

        builder.HasOne(x => x.Usuario)
            .WithOne(u => u.Empleado)
            .HasForeignKey<Empleado>(x => x.IdUsuario)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoDocumento)
            .WithMany()
            .HasForeignKey(x => x.CodTipoDocumento)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Area)
            .WithMany(a => a.Empleados)
            .HasForeignKey(x => x.IdArea)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cargo)
            .WithMany(c => c.Empleados)
            .HasForeignKey(x => x.IdCargo)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoContrato)
            .WithMany()
            .HasForeignKey(x => x.CodTipoContrato)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.EstadoEmpleado)
            .WithMany()
            .HasForeignKey(x => x.CodEstadoEmpleado)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

