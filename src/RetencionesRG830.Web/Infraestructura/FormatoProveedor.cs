using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Cómo se muestran en pantalla los datos de un proveedor. Sólo presentación.
/// </summary>
public static class FormatoProveedor
{
    /// <summary>"Inscripto · Persona humana", "No inscripto · Otro sujeto".</summary>
    public static string Condicion(Proveedor proveedor) =>
        $"{(proveedor.InscriptoEnGanancias ? "Inscripto" : "No inscripto")} · {TipoPersona(proveedor.TipoPersona)}";

    /// <summary>
    /// Nombre del tipo de persona. "Resto" es el término de la hoja Tablas para
    /// sociedades y demás sujetos que no son personas humanas.
    /// </summary>
    public static string TipoPersona(TipoPersona tipo) => tipo switch
    {
        Domain.Enums.TipoPersona.HumanaYSucesionIndivisa => "Persona humana",
        _ => "Otro sujeto"
    };

    /// <summary>
    /// Nombre del tipo de persona en un desplegable, donde se elige. Es más
    /// largo que TipoPersona() a propósito: quien carga una sucesión indivisa
    /// tiene que ver que va en la primera opción, y si elige la otra se le
    /// aplica otra alícuota. En listados y ayudas alcanza con el nombre corto.
    ///
    /// Reemplaza a Html.GetEnumSelectList, que muestra el nombre del enum tal
    /// cual ("HumanaYSucesionIndivisa").
    /// </summary>
    public static string TipoPersonaParaElegir(TipoPersona tipo) => tipo switch
    {
        Domain.Enums.TipoPersona.HumanaYSucesionIndivisa => "Persona humana o sucesión indivisa",
        _ => "Otro sujeto (sociedades y demás)"
    };
}
