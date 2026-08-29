using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Un Régimen es un código de la RG 830 (el Anexo II que vimos en la hoja
/// "Tablas" del Excel): por ejemplo el 78 (venta de bienes muebles), el 94
/// (locaciones de servicios) o el 116 (honorarios de directores).
///
/// Esta es la pieza más importante del diseño: acá viven las reglas de
/// cálculo (tasas y piso no sujeto a retención) como DATOS, con una fecha de
/// vigencia, en vez de estar escritas adentro de una fórmula de Excel. Así,
/// cuando AFIP actualiza un valor, se agrega una fila nueva de Régimen con
/// el mismo Codigo y la fecha desde la que rige, sin tocar ni una línea de
/// código ni las filas anteriores (que siguen sirviendo para recalcular
/// operaciones viejas con el valor que estaba vigente en su momento).
/// </summary>
public class Regimen
{
    public int Id { get; set; }

    /// <summary>Código de régimen de la RG 830 (78, 94, 116, etc).</summary>
    public int Codigo { get; set; }
    public string Descripcion { get; set; } = string.Empty;

    public TipoCalculoRegimen TipoCalculo { get; set; }

    // Estos tres campos sólo se usan cuando TipoCalculo == TasaFija.
    // Son, literalmente, las columnas "INSCRIPTOS / NO INSCRIP. PERS HUM /
    // NO INSCRIP. RESTO" de la hoja Tablas del Excel, expresadas como
    // fracción (0.02 = 2%) en vez de porcentaje.
    public decimal TasaInscripto { get; set; }
    public decimal TasaNoInscriptoHumana { get; set; }
    public decimal TasaNoInscriptoResto { get; set; }

    /// <summary>
    /// Piso exento: monto hasta el cual no corresponde retener nada en el
    /// acumulado del mes para ese proveedor y régimen (columna "MONTOS NO
    /// SUJETOS A RETENCION" de la hoja Tablas).
    /// </summary>
    public decimal MontoNoSujetoARetencion { get; set; }

        /// <summary>
    /// Retención mínima para proveedores INSCRIPTOS: si la retención
    /// calculada da menos que esto, no se retiene nada. En la RG 830 son
    /// $240 para todos los conceptos.
    /// </summary>
    public decimal MinimoRetencion { get; set; }

    /// <summary>
    /// Retención mínima para proveedores NO inscriptos. Es $240 en general,
    /// pero $1.020 en alquileres de inmuebles urbanos (régimen 31).
    ///
    /// En la planilla Excel esta excepción está escrita a mano adentro de la
    /// fórmula, preguntando literalmente si el régimen es el 31. Acá se
    /// modela como un dato más del régimen: si el día de mañana AFIP suma
    /// otro concepto con mínimo diferenciado, se carga el valor y listo, sin
    /// tocar el código.
    /// </summary>
    public decimal MinimoRetencionNoInscripto { get; set; } = 240m;

    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }

    public ICollection<TramoEscala> Tramos { get; set; } = new List<TramoEscala>();
    public ICollection<Operacion> Operaciones { get; set; } = new List<Operacion>();
}
