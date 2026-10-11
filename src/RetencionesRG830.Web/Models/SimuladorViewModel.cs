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
    /// <summary>
    /// El código de régimen (78, 94, 116...), no una versión: la versión la resuelve
    /// el servidor con FechaRetencion, igual que en Nueva operación (regla 9).
    /// </summary>
    [Display(Name = "Régimen")]
    public int CodigoRegimen { get; set; }

    /// <summary>
    /// Define con qué valores se calcula: los vigentes a esta fecha. La pantalla la
    /// propone con la fecha de hoy, y se puede cambiar para simular una operación
    /// atrasada. Sin valor inicial acá a propósito: si un envío llega sin fecha, es
    /// un error de validación, no "hoy" en silencio.
    /// </summary>
    [Display(Name = "Fecha de retención")]
    [DataType(DataType.Date)]
    public DateOnly FechaRetencion { get; set; }

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

    /// <summary>Desde cuándo rigen los valores con que se calculó el resultado.</summary>
    public DateOnly? RegimenVigenciaDesde { get; set; }
}