using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestionTurnos.Api.Application.Interfaces;
using GestionTurnos.Api.Domain.Entities.Auditoria;
using GestionTurnos.Api.Domain.Entities.Organizacion;
using GestionTurnos.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GestionTurnos.Api.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Interceptor de SaveChanges de EF Core que genera de forma automática registros en auditoria.bitacora_negocio (RF-08).
/// Es completamente agnóstico al motor de base de datos (PostgreSQL, SQL Server, InMemory) y garantiza
/// que cualquier cambio en entidades de negocio quede auditado sin depender de triggers externos.
/// </summary>
public sealed class AuditoriaSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly List<AuditEntry> _pendingAuditEntries = new();
    private bool _isAuditing;

    private static readonly HashSet<string> PropiedadesExcluidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "HashContrasena",
        "Contrasena",
        "Password",
        "Token",
        "RefreshToken",
        "CodigoVerificacion",
        "CodigoDosPasos",
        "HashCodigo",
        "HashToken"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public AuditoriaSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PrepararAuditoria(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PrepararAuditoria(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (_isAuditing || _pendingAuditEntries.Count == 0 || eventData.Context is null)
        {
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        try
        {
            _isAuditing = true;
            var context = eventData.Context;
            var bitacoraDbSet = context.Set<BitacoraNegocio>();

            foreach (var auditEntry in _pendingAuditEntries)
            {
                auditEntry.FinalizarValoresClave();
                var bitacora = auditEntry.ToBitacoraNegocio();
                bitacoraDbSet.Add(bitacora);
            }

            _pendingAuditEntries.Clear();
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _isAuditing = false;
            _pendingAuditEntries.Clear();
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        if (_isAuditing || _pendingAuditEntries.Count == 0 || eventData.Context is null)
        {
            return base.SavedChanges(eventData, result);
        }

        try
        {
            _isAuditing = true;
            var context = eventData.Context;
            var bitacoraDbSet = context.Set<BitacoraNegocio>();

            foreach (var auditEntry in _pendingAuditEntries)
            {
                auditEntry.FinalizarValoresClave();
                var bitacora = auditEntry.ToBitacoraNegocio();
                bitacoraDbSet.Add(bitacora);
            }

            _pendingAuditEntries.Clear();
            context.SaveChanges();
        }
        finally
        {
            _isAuditing = false;
            _pendingAuditEntries.Clear();
        }

        return base.SavedChanges(eventData, result);
    }

    private void PrepararAuditoria(DbContext? context)
    {
        if (_isAuditing || context is null)
        {
            return;
        }

        _pendingAuditEntries.Clear();

        var userId = _currentUserService.UserId;
        var rawIp = _currentUserService.IpAddress;
        string? validIp = null;
        if (!string.IsNullOrWhiteSpace(rawIp) && IPAddress.TryParse(rawIp, out var parsedIp))
        {
            validIp = parsedIp.ToString();
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            // Las sesiones de dispositivo cambian en cada renovación del token: auditarlas solo generaría ruido.
            if (entry.Entity is BitacoraNegocio ||
                entry.Entity is GestionTurnos.Api.Domain.Entities.Seguridad.SesionDispositivo ||
                entry.Metadata.ClrType.Name.Contains("DataProtectionKey", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var auditEntry = new AuditEntry(entry, userId, validIp);
            if (auditEntry.HasChanges())
            {
                _pendingAuditEntries.Add(auditEntry);
            }
        }
    }

    private sealed class AuditEntry
    {
        public EntityEntry Entry { get; }
        public long? IdUsuario { get; }
        public string? IpOrigen { get; }
        public string Esquema { get; }
        public string Tabla { get; }
        public string CodTipoAccion { get; }
        public string? IdRegistro { get; set; }
        public long? IdEmpleado { get; set; }
        public Dictionary<string, object?> OldValues { get; } = new();
        public Dictionary<string, object?> NewValues { get; } = new();

        public AuditEntry(EntityEntry entry, long? userId, string? ipOrigen)
        {
            Entry = entry;
            IdUsuario = userId;
            IpOrigen = ipOrigen;

            Esquema = entry.Metadata.GetSchema() ?? "public";
            Tabla = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;

            CodTipoAccion = entry.State switch
            {
                EntityState.Added => TiposAccionAuditoria.Crear,
                EntityState.Deleted => TiposAccionAuditoria.Eliminar,
                _ => TiposAccionAuditoria.Modificar
            };

            CalcularCambios();
        }

        private void CalcularCambios()
        {
            switch (Entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in Entry.Properties)
                    {
                        if (PropiedadesExcluidas.Contains(prop.Metadata.Name) || prop.Metadata.IsConcurrencyToken)
                            continue;

                        if (prop.CurrentValue is not null)
                        {
                            NewValues[prop.Metadata.Name] = prop.CurrentValue;
                        }
                    }
                    break;

                case EntityState.Deleted:
                    foreach (var prop in Entry.Properties)
                    {
                        if (PropiedadesExcluidas.Contains(prop.Metadata.Name) || prop.Metadata.IsConcurrencyToken)
                            continue;

                        if (prop.OriginalValue is not null)
                        {
                            OldValues[prop.Metadata.Name] = prop.OriginalValue;
                        }
                    }
                    break;

                case EntityState.Modified:
                    foreach (var prop in Entry.Properties)
                    {
                        if (PropiedadesExcluidas.Contains(prop.Metadata.Name) || prop.Metadata.IsPrimaryKey() || prop.Metadata.IsConcurrencyToken)
                            continue;

                        if (prop.IsModified)
                        {
                            var original = prop.OriginalValue;
                            var current = prop.CurrentValue;

                            if (!Equals(original, current))
                            {
                                OldValues[prop.Metadata.Name] = original;
                                NewValues[prop.Metadata.Name] = current;
                            }
                        }
                    }
                    break;
            }
        }

        public bool HasChanges()
        {
            if (Entry.State == EntityState.Modified)
            {
                return OldValues.Count > 0 || NewValues.Count > 0;
            }
            return true;
        }

        public void FinalizarValoresClave()
        {
            var keyProps = Entry.Metadata.FindPrimaryKey()?.Properties;
            if (keyProps != null && keyProps.Count > 0)
            {
                IdRegistro = string.Join(",", keyProps.Select(p => Entry.Property(p.Name).CurrentValue?.ToString() ?? "0"));
            }
            else
            {
                IdRegistro = "0";
            }

            if (Entry.Entity is Empleado emp && emp.IdEmpleado > 0)
            {
                IdEmpleado = emp.IdEmpleado;
            }
            else
            {
                var empProp = Entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IdEmpleado");
                if (empProp?.CurrentValue is long empId && empId > 0)
                {
                    IdEmpleado = empId;
                }
                else if (empProp?.CurrentValue is int empIdInt && empIdInt > 0)
                {
                    IdEmpleado = (long)empIdInt;
                }
            }
        }

        public BitacoraNegocio ToBitacoraNegocio()
        {
            return new BitacoraNegocio
            {
                FechaHora = DateTimeOffset.UtcNow,
                IdUsuario = IdUsuario,
                CodTipoAccion = CodTipoAccion,
                Esquema = Esquema,
                Tabla = Tabla,
                IdRegistro = string.IsNullOrWhiteSpace(IdRegistro) ? "0" : IdRegistro,
                IdEmpleado = IdEmpleado,
                ValorAnterior = OldValues.Count > 0 ? JsonSerializer.Serialize(OldValues, JsonOptions) : null,
                ValorNuevo = NewValues.Count > 0 ? JsonSerializer.Serialize(NewValues, JsonOptions) : null,
                Descripcion = null,
                IpOrigen = IpOrigen
            };
        }
    }
}
