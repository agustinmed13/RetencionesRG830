using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Cuál de las versiones de un régimen rige en una fecha (regla 9).
///
/// Un régimen puede tener varias versiones: cuando AFIP actualiza un valor se
/// agrega una fila nueva de Regimen con el mismo Codigo y otra VigenciaDesde.
/// Esta clase es la única que decide cuál vale en una fecha, para que la
/// pantalla de Regímenes y el cálculo no puedan discrepar.
///
/// Recibe la fecha como parámetro en vez de leer el reloj: así se puede probar
/// con cualquier fecha, y el cálculo podrá pedir la versión vigente a la fecha
/// de retención de la operación, no a la de hoy.
/// </summary>
public static class VigenciaRegimen
{
    /// <summary>
    /// La versión que rige en la fecha, o null si ninguna rige.
    ///
    /// Rige una versión si VigenciaDesde ≤ fecha y, si tiene VigenciaHasta,
    /// fecha ≤ VigenciaHasta (el último día está incluido). Si rigen varias, gana
    /// la de VigenciaDesde más reciente: la versión nueva reemplaza a la anterior
    /// aunque a la anterior nadie le haya cargado VigenciaHasta.
    ///
    /// Empate: si dos versiones que rigen tienen la misma VigenciaDesde, lanza
    /// InvalidOperationException en lugar de elegir una. Elegir cualquiera
    /// significaría calcular una retención con valores tomados al azar. En la base
    /// no puede pasar, porque el índice único (Codigo, VigenciaDesde) lo impide:
    /// si pasa, los datos están mal y hay que enterarse.
    /// </summary>
    /// <param name="versiones">Las versiones de un mismo régimen (mismo Codigo).</param>
    public static Regimen? VersionVigente(IEnumerable<Regimen> versiones, DateOnly fecha)
    {
        var lista = versiones.ToList();

        if (lista.Select(r => r.Codigo).Distinct().Count() > 1)
            throw new ArgumentException("Las versiones recibidas son de regímenes distintos.", nameof(versiones));

        var queRigen = lista
            .Where(r => r.VigenciaDesde <= fecha && (r.VigenciaHasta is null || fecha <= r.VigenciaHasta))
            .OrderByDescending(r => r.VigenciaDesde)
            .ToList();

        if (queRigen.Count == 0) return null;

        if (queRigen.Count > 1 && queRigen[0].VigenciaDesde == queRigen[1].VigenciaDesde)
            throw new InvalidOperationException(
                $"El régimen {queRigen[0].Codigo} tiene dos versiones vigentes desde el " +
                $"{queRigen[0].VigenciaDesde:dd/MM/yyyy}: no se puede saber cuál usar.");

        return queRigen[0];
    }

    /// <summary>
    /// Estado de una versión en la fecha, respecto de las demás versiones de su
    /// régimen: la que rige, una que ya no rige, o una que todavía no empezó.
    /// </summary>
    public static EstadoVigencia Estado(Regimen version, IEnumerable<Regimen> versiones, DateOnly fecha)
    {
        if (version.VigenciaDesde > fecha) return EstadoVigencia.Proxima;
        return VersionVigente(versiones, fecha) == version ? EstadoVigencia.Vigente : EstadoVigencia.Historica;
    }
}

public enum EstadoVigencia
{
    /// <summary>Es la versión que rige en la fecha.</summary>
    Vigente,
    /// <summary>Rigió antes: la reemplazó otra más nueva o se cumplió su VigenciaHasta.</summary>
    Historica,
    /// <summary>Está cargada pero todavía no empezó a regir.</summary>
    Proxima
}
