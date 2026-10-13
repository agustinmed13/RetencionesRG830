using System.ComponentModel.DataAnnotations;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Web.Models;

public class OperacionViewModel
{
    public int ClienteId { get; set; }

    /// <summary>
    /// Sólo se muestra. La completa siempre el servidor desde la base
    /// (OperacionesController.ValidarClienteAsync): no viaja en el formulario, para
    /// que un pedido alterado no pueda mostrar otro nombre en la confirmación.
    /// </summary>
    public string ClienteRazonSocial { get; set; } = string.Empty;

    [Display(Name = "Proveedor")]
    public int ProveedorId { get; set; }

    /// <summary>
    /// El código de régimen elegido (78, 94, 116...), no una versión. Qué versión
    /// corresponde la resuelve el servidor con la fecha de retención (regla 9): el
    /// operador elige el régimen, la versión es una consecuencia de la fecha.
    /// </summary>
    [Display(Name = "Régimen")]
    public int CodigoRegimen { get; set; }

    [Display(Name = "Tipo de comprobante")]
    public int TipoComprobante { get; set; } = 1;

    // Punto de venta, número e importes admiten null: vacío no es cero, la misma idea
    // que en DecimalModelBinder. Con int o decimal, un campo vacío volvía dibujado con
    // un 0, y abajo el mensaje decía que tenía que ser mayor que 0. Que sean
    // obligatorios lo controlan OperacionesController.ValidarAsync (punto de venta y
    // número) y [Required] (importes). Cuidado al compararlos: null <= 0 da false, así
    // que "falta o no es positivo" se escribe "is null or <= 0".
    [Display(Name = "Punto de venta")]
    public int? PuntoVenta { get; set; }

    [Display(Name = "Número de comprobante")]
    public long? NumeroComprobante { get; set; }

    [Display(Name = "Fecha del comprobante")]
    [DataType(DataType.Date)]
    public DateOnly FechaComprobante { get; set; }

    [Display(Name = "Fecha de la retención")]
    [DataType(DataType.Date)]
    public DateOnly FechaRetencion { get; set; }

    // Mayores que 0, no "mayores o iguales": un importe en cero no es una operación.
    // Es distinto de una operación que no alcanza el mínimo de retención, que sí se
    // registra (regla 10). Como son decimal?, DecimalModelBinder deja un campo vacío
    // en null sin error, y el que avisa es [Required], con un mensaje que nombra el
    // campo. Un texto que no es un importe ("5.000.000") lo rechaza el binder.
    [Display(Name = "Importe total del comprobante")]
    [Required(ErrorMessage = "Ingresá el importe total.")]
    [Range(0, double.MaxValue, MinimumIsExclusive = true,
        ErrorMessage = "El importe total tiene que ser mayor que 0.")]
    public decimal? ImporteComprobante { get; set; }

    [Display(Name = "Importe gravado")]
    [Required(ErrorMessage = "Ingresá el importe gravado.")]
    [Range(0, double.MaxValue, MinimumIsExclusive = true,
        ErrorMessage = "El importe gravado tiene que ser mayor que 0.")]
    public decimal? ImporteGravado { get; set; }
        // --- Datos que sólo se usan en la pantalla de confirmación ---

    /// <summary>El resultado del cálculo, para mostrarlo antes de guardar.</summary>
    public ResultadoCalculoRetencion? Resultado { get; set; }

    /// <summary>Nombre del proveedor elegido, para mostrarlo en la revisión.</summary>
    public string ProveedorNombre { get; set; } = string.Empty;

    /// <summary>Descripción del régimen elegido, para mostrarla en la revisión.</summary>
    public string RegimenNombre { get; set; } = string.Empty;

    /// <summary>Código del régimen (78, 94, 116...), para la etiqueta de la revisión.</summary>
    public int RegimenCodigo { get; set; }

    /// <summary>
    /// Desde cuándo rigen los valores con que se calculó: dice qué versión del
    /// régimen resolvió el servidor para la fecha de retención.
    /// </summary>
    public DateOnly RegimenVigenciaDesde { get; set; }

    /// <summary>CUIT del proveedor elegido.</summary>
    public string ProveedorCuit { get; set; } = string.Empty;

    /// <summary>"Inscripto · Persona humana": la condición que define tasa y piso.</summary>
    public string ProveedorCondicion { get; set; } = string.Empty;

    /// <summary>
    /// El número de certificado que se le va a asignar, para anticiparlo en la
    /// revisión. El definitivo se asigna al confirmar.
    /// </summary>
    public int ProximoNumeroCertificado { get; set; }

    /// <summary>
    /// Texto debajo del régimen: la alícuota que corresponde al proveedor elegido y
    /// por qué. Lo arma el servidor con la misma regla que usa el cálculo.
    /// </summary>
    public string AyudaRegimen { get; set; } = string.Empty;

    /// <summary>
    /// Operaciones vigentes con el mismo comprobante del mismo proveedor. No impide
    /// registrar (puede ser otra cuota de la misma factura): sólo se avisa.
    /// </summary>
    public List<Operacion> ComprobantesRepetidos { get; set; } = new();
}