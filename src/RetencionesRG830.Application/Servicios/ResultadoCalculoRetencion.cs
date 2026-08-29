namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// El resultado completo de un cálculo de retención. A propósito, cada
/// propiedad se corresponde con una fila de la hoja "Cálculo" de la planilla
/// Excel original, para poder comparar los dos resultados lado a lado y
/// mostrar en la defensa que el sistema hace exactamente lo mismo.
/// </summary>
public class ResultadoCalculoRetencion
{
    /// <summary>Fila "Mínimo no sujeto a retención" (celda C21 del Excel).</summary>
    public decimal MinimoNoSujetoARetencion { get; set; }

    /// <summary>Fila "Tasa aplicable" (C22). En regímenes por escala queda en 0.</summary>
    public decimal TasaAplicada { get; set; }

    /// <summary>Fila "Neto sujeto a retención" (C24).</summary>
    public decimal NetoSujetoARetencion { get; set; }

    /// <summary>Fila "Retención acumulada determinada" (C25).</summary>
    public decimal RetencionAcumuladaDeterminada { get; set; }

    /// <summary>Fila "Retención a practicar" (C26): el monto final a retener.</summary>
    public decimal RetencionAPracticar { get; set; }

    /// <summary>Aclaración cuando no corresponde retener (celda E26).</summary>
    public string? Observacion { get; set; }
}