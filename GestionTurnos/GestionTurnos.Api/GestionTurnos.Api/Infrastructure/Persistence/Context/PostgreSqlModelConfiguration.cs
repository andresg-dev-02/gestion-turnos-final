using System.Net;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Entities.Turnos;
using GestionTurnos.Api.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NpgsqlTypes;

namespace GestionTurnos.Api.Infrastructure.Persistence.Context;

/// <summary>
/// Ajustes del modelo que solo aplican con PostgreSQL (sql/gestion_turnos.sql). Las configuraciones
/// de cada entidad siguen siendo las mismas para ambos proveedores; aquí solo se corrigen los tipos
/// nativos de PostgreSQL que SQL Server guarda como texto:
/// <list type="bullet">
/// <item><c>turnos.asignacion.periodo</c> es <c>tstzrange</c> (RG-01 depende de él).</item>
/// <item><c>auditoria.bitacora_negocio.valor_anterior/valor_nuevo</c> son <c>jsonb</c> e <c>ip_origen</c> es <c>inet</c>.</item>
/// <item>Npgsql solo escribe <c>timestamptz</c> con offset 0: todo <see cref="DateTimeOffset"/> se envía en UTC
/// (mismo instante) y se lee en UTC.</item>
/// </list>
/// </summary>
internal static class PostgreSqlModelConfiguration
{
    public static void Aplicar(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asignacion>()
            .Property(x => x.Periodo)
            .HasColumnType("tstzrange")
            .HasConversion(new ValueConverter<PeriodoRango, NpgsqlRange<DateTime>>(
                v => ARango(v),
                v => APeriodo(v)));

        modelBuilder.Entity<BitacoraNegocio>(builder =>
        {
            builder.Property(x => x.ValorAnterior).HasColumnType("jsonb");
            builder.Property(x => x.ValorNuevo).HasColumnType("jsonb");
            builder.Property(x => x.IpOrigen)
                .HasColumnType("inet")
                .HasConversion(new ValueConverter<string?, IPAddress>(
                    v => IPAddress.Parse(v!),
                    v => v.ToString()));
        });

        var aUtc = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => v);

        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propiedad in entidad.GetProperties()
                         .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
            {
                propiedad.SetValueConverter(aUtc);
            }
        }
    }

    private static NpgsqlRange<DateTime> ARango(PeriodoRango periodo) =>
        new(periodo.Inicio.UtcDateTime, true, periodo.Fin.UtcDateTime, false);

    private static PeriodoRango APeriodo(NpgsqlRange<DateTime> rango) =>
        new(
            new DateTimeOffset(DateTime.SpecifyKind(rango.LowerBound, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(rango.UpperBound, DateTimeKind.Utc)));
}
