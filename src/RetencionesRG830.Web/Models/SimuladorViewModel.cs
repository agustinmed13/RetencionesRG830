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

    // Los dos importes admiten null: vacío no es cero, igual que en OperacionViewModel.
    // Con decimal, el formulario arrancaba con un 0 dibujado, que parece un dato
    // cargado, y el campo obligatorio nunca podía quedar vacío. Como son decimal?,
    // DecimalModelBinder deja un campo vacío en null sin error, y el que avisa es
    // [Required]. [Range] no mira un null (lo da por válido): sólo controla el signo
    // de un valor que sí se escribió. Cuidado al compararlos en el código: null <= 0
    // da false, así que "falta o es negativo" se escribe "is null or < 0".
    //
    // El cero sigue siendo válido en los dos: 0 de retenciones anteriores es la
    // primera operación del mes, y simular con 0 de neto gravado no rompe nada.
    // Lo que cambia es que hay que escribirlo.
    [Display(Name = "Neto gravado acumulado del mes")]
    [Required(ErrorMessage = "Ingresá el neto gravado acumulado del mes.")]
    [Range(0, double.MaxValue, ErrorMessage = "El neto gravado no puede ser negativo.")]
    public decimal? NetoGravadoAcumuladoMensual { get; set; }

    [Display(Name = "Retenciones ya practicadas en el mes")]
    [Required(ErrorMessage = "Ingresá las retenciones ya practicadas en el mes. Si no hubo, escribí 0.")]
    [Range(0, double.MaxValue, ErrorMessage = "Las retenciones ya practicadas no pueden ser negativas.")]
    public decimal? RetencionesAcumuladasDelMes { get; set; }

    /// <summary>Queda en null hasta que se aprieta "Calcular".</summary>
    public ResultadoCalculoRetencion? Resultado { get; set; }

    /// <summary>Desde cuándo rigen los valores con que se calculó el resultado.</summary>
    public DateOnly? RegimenVigenciaDesde { get; set; }
}