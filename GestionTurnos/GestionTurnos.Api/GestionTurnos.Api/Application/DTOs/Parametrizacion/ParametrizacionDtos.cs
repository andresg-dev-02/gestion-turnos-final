namespace GestionTurnos.Api.Application.DTOs.Parametrizacion;

public sealed record CatalogoItemDto(
    string Codigo,
    string Nombre,
    bool Activo);

public sealed record TipoNovedadDto(
    string CodTipoNovedad,
    string Nombre,
    bool RemuneradoDefecto,
    bool AfectaHorasDefecto,
    bool RequiereAprobacion,
    bool RequiereSoporte,
    bool Activo);

public sealed record CategoriaHoraDto(
    string CodCategoriaHora,
    string Nombre,
    decimal FactorPago,
    bool Activo);

public sealed record DiaFestivoDto(
    DateOnly Fecha,
    string Nombre);

public sealed record ParametroDto(
    string CodParametro,
    string Nombre,
    string? Descripcion,
    string TipoDato,
    bool Editable,
    string? ValorVigente,
    DateOnly? VigenteDesde);

public sealed record ParametroValorDto(
    long IdParametroValor,
    string CodParametro,
    string Valor,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);

