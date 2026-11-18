using GestionTurnos.Api.Domain.Shared;

namespace GestionTurnos.Api.Domain.Entities.Parametrizacion;

public class TipoDocumento : BaseEntity
{
    public string CodTipoDocumento { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;
}

public class TipoContrato : BaseEntity
{
    public string CodTipoContrato { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Activo { get; set; } = true;
}

public class EstadoUsuario : BaseEntity
{
    public string CodEstadoUsuario { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

public class EstadoEmpleado : BaseEntity
{
    public string CodEstadoEmpleado { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

public class EstadoSolicitud : BaseEntity
{
    public string CodEstadoSolicitud { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool EsFinal { get; set; }
}

public class EstadoAsignacion : BaseEntity
{
    public string CodEstadoAsignacion { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

public class TipoNovedad : BaseEntity
{
    public string CodTipoNovedad { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool RemuneradoDefecto { get; set; }
    public bool AfectaHorasDefecto { get; set; }
    public bool RequiereAprobacion { get; set; } = true;
    public bool RequiereSoporte { get; set; }
    public bool Activo { get; set; } = true;
}

public class CategoriaHora : BaseEntity
{
    public string CodCategoriaHora { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public decimal FactorPago { get; set; } = 1.00m;
    public bool Activo { get; set; } = true;
}

public class FuenteMarcacion : BaseEntity
{
    public string CodFuenteMarcacion { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

public class TipoAccion : BaseEntity
{
    public string CodTipoAccion { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}

public class DiaFestivo : BaseEntity
{
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = null!;
}

public class Parametro : BaseEntity
{
    public string CodParametro { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string TipoDato { get; set; } = null!;
    public bool Editable { get; set; } = true;

    public ICollection<ParametroValor> Valores { get; set; } = new List<ParametroValor>();
}

public class ParametroValor : BaseEntity
{
    public long IdParametroValor { get; set; }
    public string CodParametro { get; set; } = null!;
    public string Valor { get; set; } = null!;
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }

    public Parametro Parametro { get; set; } = null!;
}

