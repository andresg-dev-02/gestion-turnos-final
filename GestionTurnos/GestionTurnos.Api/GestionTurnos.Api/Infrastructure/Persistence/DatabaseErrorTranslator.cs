using GestionTurnos.Api.Application.Common;
using System.Data.Common;
using GestionTurnos.Api.Domain.Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GestionTurnos.Api.Infrastructure.Persistence;

/// <summary>
/// Traduce excepciones de base de datos (SQLSTATE de PostgreSQL y códigos de error de SQL Server)
/// a errores de dominio comprensibles para el usuario según la Sección 1 (RNF-01).
/// </summary>
public static class DatabaseErrorTranslator
{
    public static Error Translate(Exception exception)
    {
        // Los handlers envuelven todo en try/catch y pasan la excepción por aquí: un parámetro sin
        // configurar debe llegar al usuario con su mensaje, no como "error de base de datos".
        if (exception is ParametroNoConfiguradoException parametroEx)
        {
            return ParametroNoConfigurado(parametroEx);
        }

        if (TryTranslate(exception, out var error) && error is not null)
        {
            return error;
        }

        return new Error(
            "Database.Error",
            "Ocurrió un error al procesar la operación en la base de datos.",
            ErrorType.Failure);
    }

    private static readonly (string Fragmento, string Codigo, string Mensaje)[] NombresUnicos =
    {
        ("area_id_sucursal_nombre", "Organizacion.AreaNombreDuplicado", "Ya existe un área con ese nombre en la sucursal."),
        ("sucursal_nombre", "Organizacion.SucursalNombreDuplicado", "Ya existe una sucursal con ese nombre."),
        ("cargo_nombre", "Organizacion.CargoNombreDuplicado", "Ya existe un cargo con ese nombre."),
        ("turno_nombre", "Turnos.TurnoNombreDuplicado", "Ya existe un tipo de turno con ese nombre."),
        ("tipo_documento_nombre", "Parametrizacion.NombreDuplicado", "Ya existe un tipo de documento con ese nombre."),
        ("tipo_contrato_nombre", "Parametrizacion.NombreDuplicado", "Ya existe un tipo de contrato con ese nombre."),
        ("tipo_novedad_nombre", "Parametrizacion.NombreDuplicado", "Ya existe un tipo de novedad con ese nombre."),
        ("categoria_hora_nombre", "Parametrizacion.NombreDuplicado", "Ya existe una categoría de hora con ese nombre.")
    };

    public static Error ParametroNoConfigurado(ParametroNoConfiguradoException exception) => new(
        "Parametrizacion.ParametroNoConfigurado",
        exception.Message,
        ErrorType.Conflict);

    public static bool TryTranslate(Exception exception, out Error? translatedError)
    {
        translatedError = null;

        if (exception is ParametroNoConfiguradoException parametroEx)
        {
            translatedError = ParametroNoConfigurado(parametroEx);
            return true;
        }

        var dbException = FindDbException(exception);
        if (dbException is null)
        {
            return false;
        }

        // Extraer propiedades por reflexión o tipo concreto (soporta PostgresException y SqlException)
        string? sqlState = GetPropertyString(dbException, "SqlState") ?? dbException.SqlState;
        string? constraintName = GetPropertyString(dbException, "ConstraintName");
        string? tableName = GetPropertyString(dbException, "TableName");
        string messageText = GetPropertyString(dbException, "MessageText") ?? dbException.Message;

        // 1. Traducción por SQLSTATE estándar (PostgreSQL 14+ / ANSI SQL)
        if (!string.IsNullOrWhiteSpace(sqlState))
        {
            switch (sqlState)
            {
                case "23P01": // exclusion_violation
                    translatedError = TranslateExclusionViolation(constraintName, tableName, messageText);
                    return true;

                case "23514": // check_violation (incluye disparador fn_validar_asignacion RG-02 y RG-03)
                    translatedError = TranslateCheckViolation(constraintName, messageText);
                    return true;

                case "23505": // unique_violation
                    translatedError = TranslateUniqueViolation(constraintName, tableName, messageText);
                    return true;

                case "23503": // foreign_key_violation
                    translatedError = new Error(
                        "Database.ForeignKeyViolation",
                        "El dato seleccionado no existe o fue desactivado.",
                        ErrorType.Failure);
                    return true;
            }
        }

        // 2. Traducción para SQL Server (SqlException)
        if (dbException is SqlException sqlEx)
        {
            switch (sqlEx.Number)
            {
                case 2601:
                case 2627: // Unique constraint / index violation
                    translatedError = TranslateUniqueViolation(null, null, sqlEx.Message);
                    return true;

                case 547: // Foreign key o Check constraint violation en SQL Server
                    if (sqlEx.Message.Contains("CHECK", StringComparison.OrdinalIgnoreCase))
                    {
                        translatedError = TranslateCheckViolation(null, sqlEx.Message);
                    }
                    else
                    {
                        translatedError = new Error(
                            "Database.ForeignKeyViolation",
                            "El dato seleccionado no existe o fue desactivado.",
                            ErrorType.Failure);
                    }
                    return true;

                case 50000: // RAISERROR / THROW desde triggers de negocio (ej. fn_validar_asignacion o exclusiones)
                    translatedError = TranslateTriggerOrExclusionMessage(sqlEx.Message);
                    return true;
            }
        }

        // 3. Fallback si el mensaje contiene nombres de restricciones conocidas
        if (ContainsKnownConstraintOrTriggerMessage(messageText, out var fallbackError))
        {
            translatedError = fallbackError;
            return true;
        }

        return false;
    }

    private static Error TranslateExclusionViolation(string? constraintName, string? tableName, string messageText)
    {
        var combined = $"{constraintName} {tableName} {messageText}";

        if (combined.Contains("ex_asignacion_sin_cruce", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Turnos.CruceHorario",
                "El empleado ya tiene un turno que se cruza con ese horario.",
                ErrorType.Conflict);
        }

        if (combined.Contains("ex_vacaciones_sin_cruce", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Vacaciones.CruceFechas",
                "Ya existe una solicitud de vacaciones en esas fechas.",
                ErrorType.Conflict);
        }

        if (combined.Contains("parametro_valor", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Parametrizacion.VigenciaCruzada",
                "Ya hay un valor vigente para ese parámetro en esas fechas.",
                ErrorType.Conflict);
        }

        return new Error(
            "Database.ExclusionViolation",
            "El rango de fechas u horas se cruza con un registro existente.",
            ErrorType.Conflict);
    }

    private static Error TranslateCheckViolation(string? constraintName, string messageText)
    {
        // Si proviene del disparador fn_validar_asignacion (RG-02, RG-03), reenviar el texto del error tal cual
        if (IsTriggerValidationMessage(messageText))
        {
            return new Error(
                "Turnos.ValidacionAsignacion",
                CleanTriggerMessage(messageText),
                ErrorType.Conflict);
        }

        var combined = $"{constraintName} {messageText}";
        if (combined.Contains("ex_asignacion_sin_cruce", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Turnos.CruceHorario",
                "El empleado ya tiene un turno que se cruza con ese horario.",
                ErrorType.Conflict);
        }

        return new Error(
            "Database.CheckViolation",
            "Las fechas u horas no son válidas.",
            ErrorType.Failure);
    }

    private static Error TranslateUniqueViolation(string? constraintName, string? tableName, string messageText)
    {
        var combined = $"{constraintName} {tableName} {messageText}";

        if (combined.Contains("uq_solicitud_registro_pendiente", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("solicitud_registro", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Seguridad.SolicitudPendienteDuplicada",
                "Ya existe una solicitud de registro pendiente con ese correo electrónico.",
                ErrorType.Conflict);
        }

        if (combined.Contains("uq_usuario_correo", StringComparison.OrdinalIgnoreCase) ||
            (combined.Contains("usuario", StringComparison.OrdinalIgnoreCase) && combined.Contains("correo", StringComparison.OrdinalIgnoreCase)))
        {
            return new Error(
                "Seguridad.CorreoDuplicado",
                "Ya existe un usuario registrado con ese correo electrónico.",
                ErrorType.Conflict);
        }

        if (combined.Contains("numero_documento", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("empleado_cod_tipo_documento_numero_documento", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Organizacion.DocumentoDuplicado",
                "Ya existe un empleado registrado con ese tipo y número de documento.",
                ErrorType.Conflict);
        }

        if (combined.Contains("id_asignacion", StringComparison.OrdinalIgnoreCase) &&
            combined.Contains("registro", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Asistencia.EntradaYaRegistrada",
                "Ya se registró la entrada de asistencia para este turno.",
                ErrorType.Conflict);
        }

        if (combined.Contains("dia_descanso", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Turnos.DescansoDuplicado",
                "El empleado ya tiene un día de descanso programado en esa fecha.",
                ErrorType.Conflict);
        }

        if (combined.Contains("periodo_vacacional", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Vacaciones.PeriodoDuplicado",
                "Ya existe un período vacacional con esa fecha de inicio para el empleado.",
                ErrorType.Conflict);
        }

        if (combined.Contains("rol_permiso", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Seguridad.PermisoYaAsignado",
                "El rol ya tiene asignado ese permiso.",
                ErrorType.Conflict);
        }

        if (combined.Contains("rol_nombre", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("seguridad.rol'", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Seguridad.RolNombreDuplicado",
                "Ya existe un rol con ese nombre.",
                ErrorType.Conflict);
        }

        // Nombres únicos de organización y catálogos (Fase 3). El fragmento coincide con el nombre de la
        // restricción en PostgreSQL (<tabla>_nombre_key) y del índice en SQL Server (IX_<tabla>_nombre).
        foreach (var (fragmento, codigo, mensaje) in NombresUnicos)
        {
            if (combined.Contains(fragmento, StringComparison.OrdinalIgnoreCase))
            {
                return new Error(codigo, mensaje, ErrorType.Conflict);
            }
        }

        if (combined.Contains("dia_festivo", StringComparison.OrdinalIgnoreCase))
        {
            return new Error(
                "Parametrizacion.FestivoDuplicado",
                "Ya existe un día festivo registrado en esa fecha.",
                ErrorType.Conflict);
        }

        return new Error(
            "Database.UniqueViolation",
            "Ya existe un registro con los mismos datos únicos.",
            ErrorType.Conflict);
    }

    private static Error TranslateTriggerOrExclusionMessage(string messageText)
    {
        if (ContainsKnownConstraintOrTriggerMessage(messageText, out var knownError) && knownError is not null)
        {
            return knownError;
        }

        return new Error("Database.RuleViolation", CleanTriggerMessage(messageText), ErrorType.Conflict);
    }

    private static bool ContainsKnownConstraintOrTriggerMessage(string messageText, out Error? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        if (IsTriggerValidationMessage(messageText))
        {
            error = new Error("Turnos.ValidacionAsignacion", CleanTriggerMessage(messageText), ErrorType.Conflict);
            return true;
        }

        if (messageText.Contains("ex_asignacion_sin_cruce", StringComparison.OrdinalIgnoreCase))
        {
            error = new Error("Turnos.CruceHorario", "El empleado ya tiene un turno que se cruza con ese horario.", ErrorType.Conflict);
            return true;
        }

        if (messageText.Contains("ex_vacaciones_sin_cruce", StringComparison.OrdinalIgnoreCase))
        {
            error = new Error("Vacaciones.CruceFechas", "Ya existe una solicitud de vacaciones en esas fechas.", ErrorType.Conflict);
            return true;
        }

        if (messageText.Contains("uq_solicitud_registro_pendiente", StringComparison.OrdinalIgnoreCase))
        {
            error = new Error("Seguridad.SolicitudPendienteDuplicada", "Ya existe una solicitud de registro pendiente con ese correo electrónico.", ErrorType.Conflict);
            return true;
        }

        if (messageText.Contains("uq_usuario_correo", StringComparison.OrdinalIgnoreCase))
        {
            error = new Error("Seguridad.CorreoDuplicado", "Ya existe un usuario registrado con ese correo electrónico.", ErrorType.Conflict);
            return true;
        }

        return false;
    }

    private static bool IsTriggerValidationMessage(string messageText)
    {
        return messageText.Contains("El empleado tiene vacaciones aprobadas", StringComparison.OrdinalIgnoreCase) ||
               messageText.Contains("El empleado tiene un permiso o ausencia aprobada", StringComparison.OrdinalIgnoreCase) ||
               messageText.Contains("El empleado tiene día de descanso programado", StringComparison.OrdinalIgnoreCase) ||
               messageText.Contains("La bitácora de auditoría no admite modificaciones", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanTriggerMessage(string messageText)
    {
        // En PostgreSQL el mensaje a veces viene precedido por "ERROR: " o "23514: "
        var cleaned = messageText.Trim();
        var prefixes = new[] { "ERROR:", "23514:" };
        foreach (var prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
            }
        }

        var firstLine = cleaned.Split('\n', '\r')[0].Trim();
        return string.IsNullOrWhiteSpace(firstLine) ? messageText : firstLine;
    }

    private static DbException? FindDbException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is DbException dbEx)
            {
                return dbEx;
            }
            ex = ex.InnerException;
        }
        return null;
    }

    private static string? GetPropertyString(object target, string propertyName)
    {
        var prop = target.GetType().GetProperty(propertyName);
        return prop?.GetValue(target) as string;
    }
}

