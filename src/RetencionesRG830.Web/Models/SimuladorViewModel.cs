using System.ComponentModel.DataAnnotations;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Web.Models;

/// <summary>
/// Un "ViewModel": una clase que existe sólo para una pantalla puntual.
/// No es una entidad de la base de datos -no se guarda en ningún lado-,
/// sino el molde de lo que esta pantalla necesita recibir y mostrar.
/// </summary>
public class SimuladorViewModel
{
    [Display(Name = "Régimen")]
    public int RegimenId { get; set; }

    [Display(Name = "Proveedor inscripto en Ganancias")]
    public bool InscriptoEnGanancias { get; set; } = true;

    [Display(Name = "Tipo de persona")]
    public TipoPersona TipoPersona { get; set; } = TipoPersona.HumanaYSucesionIndivisa;

    [Display(Name = "Neto gravado acumulado del mes")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
    public decimal NetoGravadoAcumuladoMensual { get; set; }

    [Display(Name = "Retenciones ya practicadas en el mes")]
    [Range(0, double.MaxValue, ErrorMessage = "El monto no puede ser negativo.")]
    public decimal RetencionesAcumuladasDelMes { get; set; }

    /// <summary>Queda en null hasta que se aprieta "Calcular".</summary>
    public ResultadoCalculoRetencion? Resultado { get; set; }
}