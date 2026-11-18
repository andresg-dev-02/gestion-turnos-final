using GestionTurnos.Api.Domain.Entities.Parametrizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionTurnos.Api.Infrastructure.Persistence.Configurations.Parametrizacion;

public class TipoDocumentoConfiguration : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        builder.ToTable("tipo_documento", "parametrizacion");
        builder.HasKey(x => x.CodTipoDocumento);

        builder.Property(x => x.CodTipoDocumento)
            .HasColumnName("cod_tipo_documento")
            .HasMaxLength(10)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class TipoContratoConfiguration : IEntityTypeConfiguration<TipoContrato>
{
    public void Configure(EntityTypeBuilder<TipoContrato> builder)
    {
        builder.ToTable("tipo_contrato", "parametrizacion");
        builder.HasKey(x => x.CodTipoContrato);

        builder.Property(x => x.CodTipoContrato)
            .HasColumnName("cod_tipo_contrato")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class EstadoUsuarioConfiguration : IEntityTypeConfiguration<EstadoUsuario>
{
    public void Configure(EntityTypeBuilder<EstadoUsuario> builder)
    {
        builder.ToTable("estado_usuario", "parametrizacion");
        builder.HasKey(x => x.CodEstadoUsuario);

        builder.Property(x => x.CodEstadoUsuario)
            .HasColumnName("cod_estado_usuario")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class EstadoEmpleadoConfiguration : IEntityTypeConfiguration<EstadoEmpleado>
{
    public void Configure(EntityTypeBuilder<EstadoEmpleado> builder)
    {
        builder.ToTable("estado_empleado", "parametrizacion");
        builder.HasKey(x => x.CodEstadoEmpleado);

        builder.Property(x => x.CodEstadoEmpleado)
            .HasColumnName("cod_estado_empleado")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class EstadoSolicitudConfiguration : IEntityTypeConfiguration<EstadoSolicitud>
{
    public void Configure(EntityTypeBuilder<EstadoSolicitud> builder)
    {
        builder.ToTable("estado_solicitud", "parametrizacion");
        builder.HasKey(x => x.CodEstadoSolicitud);

        builder.Property(x => x.CodEstadoSolicitud)
            .HasColumnName("cod_estado_solicitud")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.EsFinal)
            .HasColumnName("es_final")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class EstadoAsignacionConfiguration : IEntityTypeConfiguration<EstadoAsignacion>
{
    public void Configure(EntityTypeBuilder<EstadoAsignacion> builder)
    {
        builder.ToTable("estado_asignacion", "parametrizacion");
        builder.HasKey(x => x.CodEstadoAsignacion);

        builder.Property(x => x.CodEstadoAsignacion)
            .HasColumnName("cod_estado_asignacion")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class TipoNovedadConfiguration : IEntityTypeConfiguration<TipoNovedad>
{
    public void Configure(EntityTypeBuilder<TipoNovedad> builder)
    {
        builder.ToTable("tipo_novedad", "parametrizacion");
        builder.HasKey(x => x.CodTipoNovedad);

        builder.Property(x => x.CodTipoNovedad)
            .HasColumnName("cod_tipo_novedad")
            .HasMaxLength(30)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.RemuneradoDefecto)
            .HasColumnName("remunerado_defecto")
            .IsRequired();

        builder.Property(x => x.AfectaHorasDefecto)
            .HasColumnName("afecta_horas_defecto")
            .IsRequired();

        builder.Property(x => x.RequiereAprobacion)
            .HasColumnName("requiere_aprobacion")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.RequiereSoporte)
            .HasColumnName("requiere_soporte")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class CategoriaHoraConfiguration : IEntityTypeConfiguration<CategoriaHora>
{
    public void Configure(EntityTypeBuilder<CategoriaHora> builder)
    {
        builder.ToTable("categoria_hora", "parametrizacion");
        builder.HasKey(x => x.CodCategoriaHora);

        builder.Property(x => x.CodCategoriaHora)
            .HasColumnName("cod_categoria_hora")
            .HasMaxLength(30)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.FactorPago)
            .HasColumnName("factor_pago")
            .HasPrecision(5, 2)
            .HasDefaultValue(1.00m)
            .IsRequired();

        builder.Property(x => x.Activo)
            .HasColumnName("activo")
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class FuenteMarcacionConfiguration : IEntityTypeConfiguration<FuenteMarcacion>
{
    public void Configure(EntityTypeBuilder<FuenteMarcacion> builder)
    {
        builder.ToTable("fuente_marcacion", "parametrizacion");
        builder.HasKey(x => x.CodFuenteMarcacion);

        builder.Property(x => x.CodFuenteMarcacion)
            .HasColumnName("cod_fuente_marcacion")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class TipoAccionConfiguration : IEntityTypeConfiguration<TipoAccion>
{
    public void Configure(EntityTypeBuilder<TipoAccion> builder)
    {
        builder.ToTable("tipo_accion", "parametrizacion");
        builder.HasKey(x => x.CodTipoAccion);

        builder.Property(x => x.CodTipoAccion)
            .HasColumnName("cod_tipo_accion")
            .HasMaxLength(20)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class DiaFestivoConfiguration : IEntityTypeConfiguration<DiaFestivo>
{
    public void Configure(EntityTypeBuilder<DiaFestivo> builder)
    {
        builder.ToTable("dia_festivo", "parametrizacion");
        builder.HasKey(x => x.Fecha);

        builder.Property(x => x.Fecha)
            .HasColumnName("fecha")
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();
    }
}

public class ParametroConfiguration : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> builder)
    {
        builder.ToTable("parametro", "parametrizacion");
        builder.HasKey(x => x.CodParametro);

        builder.Property(x => x.CodParametro)
            .HasColumnName("cod_parametro")
            .HasMaxLength(60)
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(x => x.Descripcion)
            .HasColumnName("descripcion");

        builder.Property(x => x.TipoDato)
            .HasColumnName("tipo_dato")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Editable)
            .HasColumnName("editable")
            .HasDefaultValue(true)
            .IsRequired();
    }
}

public class ParametroValorConfiguration : IEntityTypeConfiguration<ParametroValor>
{
    public void Configure(EntityTypeBuilder<ParametroValor> builder)
    {
        builder.ToTable("parametro_valor", "parametrizacion");
        builder.HasKey(x => x.IdParametroValor);

        builder.Property(x => x.IdParametroValor)
            .HasColumnName("id_parametro_valor")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CodParametro)
            .HasColumnName("cod_parametro")
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(x => x.Valor)
            .HasColumnName("valor")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.VigenteDesde)
            .HasColumnName("vigente_desde")
            .IsRequired();

        builder.Property(x => x.VigenteHasta)
            .HasColumnName("vigente_hasta");

        builder.HasOne(x => x.Parametro)
            .WithMany(p => p.Valores)
            .HasForeignKey(x => x.CodParametro)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

