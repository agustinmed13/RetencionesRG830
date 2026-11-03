using System.ComponentModel.DataAnnotations;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Web.Models;

/// <summary>
/// La pantalla de SICORE: cliente y período del archivo que se va a descargar.
///
/// Los tres datos admiten null: vacío no es cero, ni "hoy", ni el primer cliente.
/// Que falten lo controla SicoreController.Validar, con un mensaje en español debajo
/// de cada campo. No se usa [Required] a propósito: con una fecha mal escrita ASP.NET
/// deja su propio error en inglés, y el controlador lo tiene que reemplazar igual.
/// Cuidado al compararlos: null &lt;= 0 da false, así que "falta o no es válido" se
/// escribe "is null or &lt;= 0".
/// </summary>
public class SicoreViewModel
{
    [Display(Name = "Cliente")]
    public int? ClienteId { get; set; }

    /// <summary>Fecha de retención desde, inclusive.</summary>
    [Display(Name = "Desde")]
    [DataType(DataType.Date)]
    public DateOnly? Desde { get; set; }

    /// <summary>Fecha de retención hasta, inclusive.</summary>
    [Display(Name = "Hasta")]
    [DataType(DataType.Date)]
    public DateOnly? Hasta { get; set; }

    /// <summary>
    /// Qué va a contener el archivo. Null mientras falte algún dato o haya un error:
    /// no se muestra un número calculado con datos que no valen.
    /// </summary>
    public ResumenPeriodoSicore? Resumen { get; set; }
}
