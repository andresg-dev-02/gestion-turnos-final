namespace GestionTurnos.Api.Domain.Enums;

public static class EstadosUsuario
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";
    public const string Bloqueado = "BLOQUEADO";
}

public static class EstadosEmpleado
{
    public const string Activo = "ACTIVO";
    public const string Inactivo = "INACTIVO";
    public const string Suspendido = "SUSPENDIDO";
}

public static class EstadosSolicitud
{
    public const string Pendiente = "PENDIENTE";
    public const string Aprobada = "APROBADA";
    public const string Rechazada = "RECHAZADA";
    public const string Cancelada = "CANCELADA";
}

public static class EstadosAsignacion
{
    public const string Programada = "PROGRAMADA";
    public const string Confirmada = "CONFIRMADA";
    public const string Anulada = "ANULADA";
}

public static class TiposNovedadCodigos
{
    public const string Incapacidad = "INCAPACIDAD";
    public const string Calamidad = "CALAMIDAD";
    public const string PermisoPersonal = "PERMISO_PERSONAL";
    public const string AusenciaJustificada = "AUSENCIA_JUST";
    public const string AusenciaInjustificada = "AUSENCIA_INJUST";

    public static readonly IReadOnlySet<string> PermitidosParaEmpleado = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Incapacidad,
        Calamidad,
        PermisoPersonal
    };
}

public static class CategoriasHoraCodigos
{
    public const string Ordinaria = "ORDINARIA";
    public const string RecargoNocturno = "RECARGO_NOCTURNO";
    public const string ExtraDiurna = "EXTRA_DIURNA";
    public const string ExtraNocturna = "EXTRA_NOCTURNA";
    public const string DominicalFestivo = "DOMINICAL_FESTIVO";
}

public static class FuentesMarcacionCodigos
{
    public const string Web = "WEB";
    public const string Movil = "MOVIL";
    public const string Admin = "ADMIN";
    public const string Biometrico = "BIOMETRICO";
}

public static class TiposAccionAuditoria
{
    public const string Crear = "CREAR";
    public const string Modificar = "MODIFICAR";
    public const string Eliminar = "ELIMINAR";
    public const string Aprobar = "APROBAR";
    public const string Rechazar = "RECHAZAR";
    public const string InicioSesion = "INICIO_SESION";
}

public static class ParametrosCodigos
{
    public const string JornadaMaxHorasDia = "JORNADA_MAX_HORAS_DIA";
    public const string JornadaMaxHorasSemana = "JORNADA_MAX_HORAS_SEMANA";
    public const string DescansoMinEntreTurnos = "DESCANSO_MIN_ENTRE_TURNOS";
    public const string HoraInicioNocturna = "HORA_INICIO_NOCTURNA";
    public const string HoraFinNocturna = "HORA_FIN_NOCTURNA";
    public const string ToleranciaEntradaMin = "TOLERANCIA_ENTRADA_MIN";
    public const string VacacionesDiasPorAnio = "VACACIONES_DIAS_POR_ANIO";
    public const string ConflictoBloquea = "CONFLICTO_BLOQUEA";
}

public static class TiposDatoParametro
{
    public const string Entero = "ENTERO";
    public const string Decimal = "DECIMAL";
    public const string Texto = "TEXTO";
    public const string Booleano = "BOOLEANO";
    public const string Hora = "HORA";
}
