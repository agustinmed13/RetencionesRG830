namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Un tramo de la escala progresiva de un Régimen, igual a una fila de la
/// tabla "ESCALA RÉGIMEN 119" (o la escala general) de la hoja Tablas del
/// Excel: "más de Desde, hasta Hasta, retienen MontoFijo más el Porcentaje
/// sobre el excedente de Desde".
///
/// Sólo tiene sentido cuando el Régimen al que pertenece tiene
/// TipoCalculo == EscalaProgresiva (por ejemplo, el 116).
/// </summary>
public class TramoEscala
{
    public int Id { get; set; }

    public int RegimenId { get; set; }
    public Regimen? Regimen { get; set; }

    public decimal Desde { get; set; }

    /// <summary>Null representa "y más" (sin techo), el último tramo de la escala.</summary>
    public decimal? Hasta { get; set; }

    public decimal MontoFijo { get; set; }
    public decimal Porcentaje { get; set; }

    public DateOnly VigenciaDesde { get; set; }
}
