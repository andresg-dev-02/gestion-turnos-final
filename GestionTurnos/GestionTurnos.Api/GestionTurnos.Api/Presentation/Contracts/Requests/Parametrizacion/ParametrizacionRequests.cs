namespace GestionTurnos.Api.Presentation.Contracts.Requests.Parametrizacion;

public sealed record GuardarCatalogoSimpleRequest(
    string Codigo,
    string Nombre,
    bool Activo = true);

public sealed record GuardarTipoNovedadRequest(
    string CodTipoNovedad,
    string Nombre,
    bool RemuneradoDefecto,
    bool AfectaHorasDefecto,
    bool RequiereAprobacion = true,
    bool RequiereSoporte = false,
    bool Activo = true);

public sealed record GuardarCategoriaHoraRequest(
    string CodCategoriaHora,
    string Nombre,
    decimal FactorPago = 1.00m,
    bool Activo = true);

public sealed record GuardarDiaFestivoRequest(
    DateOnly Fecha,
    string Nombre);

public sealed record CambiarValorParametroRequest(
    string Valor,
    DateOnly VigenteDesde);

