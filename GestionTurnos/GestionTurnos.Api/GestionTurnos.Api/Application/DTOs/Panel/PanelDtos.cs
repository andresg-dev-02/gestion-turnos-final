namespace GestionTurnos.Api.Application.DTOs.Panel;

/// <summary>
/// Contadores del panel inicial del administrador (FLUJO_BD_ADMIN sección 3).
/// Propiedades con setter para que EF Core la materialice desde SQL nativo.
/// </summary>
public sealed record PanelInicialDto
{
    public int RegistrosPendientes { get; init; }
    public int VacacionesPendientes { get; init; }
    public int NovedadesPendientes { get; init; }
    public int EmpleadosActivos { get; init; }
    public int TurnosHoy { get; init; }
    public int SinAsistenciaHoy { get; init; }
}
