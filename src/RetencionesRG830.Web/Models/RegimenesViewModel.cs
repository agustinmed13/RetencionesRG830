using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Web.Models;

/// <summary>
/// La pantalla de Regímenes separa la versión que rige hoy de las demás, para
/// que nadie lea un valor viejo creyendo que es el actual. Cuál rige lo decide
/// VigenciaRegimen, la misma regla que va a usar el cálculo.
/// </summary>
public class RegimenesViewModel
{
    public DateOnly Fecha { get; set; }

    /// <summary>Una fila por código de régimen, con su versión vigente.</summary>
    public List<RegimenVigente> Vigentes { get; set; } = new();

    /// <summary>Las versiones que no rigen hoy: históricas y próximas.</summary>
    public List<OtraVersion> OtrasVersiones { get; set; } = new();
}

public class RegimenVigente
{
    public int Codigo { get; set; }

    /// <summary>
    /// La versión que rige hoy, o null si el código no tiene ninguna (todas
    /// vencieron o todavía no empezaron). La fila aparece igual, para que el
    /// régimen no desaparezca de la pantalla sin explicación.
    /// </summary>
    public Regimen? Version { get; set; }

    /// <summary>Descripción de la vigente o, si no hay, de la versión más reciente.</summary>
    public string Descripcion { get; set; } = string.Empty;
}

public class OtraVersion
{
    public Regimen Version { get; set; } = null!;
    public EstadoVigencia Estado { get; set; }
}
