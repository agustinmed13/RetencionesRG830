using System.ComponentModel.DataAnnotations;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Web.Models;

public class OperacionViewModel
{
    public int ClienteId { get; set; }
    public string ClienteRazonSocial { get; set; } = string.Empty;

    [Display(Name = "Proveedor")]
    public int ProveedorId { get; set; }

    [Display(Name = "Régimen")]
    public int RegimenId { get; set; }

    [Display(Name = "Tipo de comprobante")]
    public int TipoComprobante { get; set; } = 1;

    [Display(Name = "Punto de venta")]
    public int PuntoVenta { get; set; }

    [Display(Name = "Número de comprobante")]
    public long NumeroComprobante { get; set; }

    [Display(Name = "Fecha del comprobante")]
    [DataType(DataType.Date)]
    public DateOnly FechaComprobante { get; set; }

    [Display(Name = "Fecha de la retención")]
    [DataType(DataType.Date)]
    public DateOnly FechaRetencion { get; set; }

    [Display(Name = "Importe total del comprobante")]
    [Range(0, double.MaxValue, ErrorMessage = "El importe no puede ser negativo.")]
    public decimal ImporteComprobante { get; set; }

    [Display(Name = "Importe gravado")]
    [Range(0, double.MaxValue, ErrorMessage = "El importe no puede ser negativo.")]
    public decimal ImporteGravado { get; set; }
        // --- Datos que sólo se usan en la pantalla de confirmación ---

    /// <summary>El resultado del cálculo, para mostrarlo antes de guardar.</summary>
    public ResultadoCalculoRetencion? Resultado { get; set; }

    /// <summary>Nombre del proveedor elegido, para mostrarlo en la revisión.</summary>
    public string ProveedorNombre { get; set; } = string.Empty;

    /// <summary>Descripción del régimen elegido, para mostrarla en la revisión.</summary>
    public string RegimenNombre { get; set; } = string.Empty;
}