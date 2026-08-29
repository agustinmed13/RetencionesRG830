namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// Una Operación es un pago concreto sujeto a retención: el equivalente a
/// una fila de la hoja "Base" del Excel, o a "cargar la hoja Cálculo una vez"
/// para un comprobante puntual. Es la tabla más importante del sistema en el
/// día a día: cada vez que el estudio o un cliente registra un pago, nace
/// una fila acá.
///
/// A partir de las Operaciones ya cargadas, el sistema puede calcular solo
/// el acumulado del mes de un Proveedor para un Régimen (sumando
/// ImporteGravado de las operaciones anteriores del mismo mes), en vez de
/// que el usuario lo tenga que recordar y tipear como pasaba en el Excel.
/// </summary>
public class Operacion
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }

    public int RegimenId { get; set; }
    public Regimen? Regimen { get; set; }

    // --- Datos del comprobante que origina la retención ---
    // TipoComprobante usa los mismos códigos AFIP que vimos en la hoja
    // Tablas del Excel (1 = Factura, 2 = Recibo, etc.).
    public int TipoComprobante { get; set; }
    public int PuntoVenta { get; set; }
    public long NumeroComprobante { get; set; }
    public DateOnly FechaComprobante { get; set; }

    /// <summary>Monto total del comprobante (con IVA incluido si corresponde).</summary>
    public decimal ImporteComprobante { get; set; }

    /// <summary>Monto gravado: la base sobre la que se calcula la retención.</summary>
    public decimal ImporteGravado { get; set; }

    // --- Resultado del cálculo de retención ---
    public DateOnly FechaRetencion { get; set; }
        /// <summary>
    /// La base sobre la que se calculó la retención: el gravado acumulado
    /// del mes para ese proveedor y régimen, incluyendo esta operación.
    /// Se guarda porque es lo que informa el archivo de SICORE, y porque
    /// deja constancia de sobre qué base se calculó cada retención.
    /// </summary>
    public decimal BaseCalculoAcumulada { get; set; }
    public decimal MontoRetenido { get; set; }

    /// <summary>
    /// Numeración correlativa asignada por el sistema al confirmar la
    /// operación (nunca por el usuario a mano, para evitar los números
    /// repetidos o salteados que hoy sufre el estudio).
    /// </summary>
    public int NumeroCertificado { get; set; }

    // --- Auditoría: quién y cuándo cargó esta operación ---
    public int CreadoPorUsuarioId { get; set; }
    public DateTime FechaCarga { get; set; }
        // --- Anulación ---
    //
    // Una operación nunca se borra: se anula. El certificado ya emitido
    // salió hacia el proveedor y fue informado a AFIP, así que su número
    // no puede desaparecer -dejaría un salteo en la numeración correlativa,
    // que es justamente uno de los problemas que el estudio tiene hoy.
    //
    // Una operación anulada deja de sumar al acumulado del mes y no sale
    // en el archivo de SICORE, pero queda registrada con su motivo y su
    // responsable.

    public bool Anulada { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }
    public int? AnuladaPorUsuarioId { get; set; }
}
