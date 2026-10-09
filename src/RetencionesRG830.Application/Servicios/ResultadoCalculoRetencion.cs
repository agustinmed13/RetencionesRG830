namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// El resultado completo de un cálculo de retención. A propósito, cada
/// propiedad se corresponde con una fila de la hoja "Cálculo" de la planilla
/// Excel original, para poder comparar los dos resultados lado a lado y
/// mostrar en la defensa que el sistema hace exactamente lo mismo.
/// </summary>
public class ResultadoCalculoRetencion
{
    /// <summary>
    /// El neto gravado acumulado del mes con el que se hizo el cálculo, incluida la
    /// operación actual. Es un dato de entrada: el motor lo devuelve para que el
    /// desglose que ve el operador salga completo del resultado, sin que la
    /// pantalla tenga que reconstruir ningún renglón por su cuenta.
    /// </summary>
    public decimal BaseAcumuladaDelMes { get; set; }

    /// <summary>
    /// Lo ya retenido antes al mismo proveedor, en el mismo régimen y el mismo
    /// mes, que se descuenta de la retención determinada. También es un dato de
    /// entrada que se devuelve por la misma razón.
    /// </summary>
    public decimal RetencionesAnterioresDelMes { get; set; }

    /// <summary>
    /// true si la retención se determinó por la escala progresiva (régimen por
    /// escala y proveedor inscripto). En ese caso TasaAplicada no dice nada y la
    /// pantalla muestra "Según escala" en lugar de un porcentaje.
    /// </summary>
    public bool SeAplicoEscala { get; set; }

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