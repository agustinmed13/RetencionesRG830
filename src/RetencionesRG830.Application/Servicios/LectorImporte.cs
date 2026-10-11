using System.Globalization;

namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Interpreta un importe tal como llega escrito de un formulario, aceptando tanto
/// punto como coma decimal.
///
/// Hace falta porque los campos &lt;input type="number"&gt; del navegador SIEMPRE
/// envían el número con punto ("87190.09"), sin importar el idioma de la
/// computadora, mientras que la aplicación está configurada en es-AR, donde el
/// punto significa separador de miles. Sin esto, "87190.09" se interpretaba como
/// 8.719.009: cien veces más grande, y sin ningún aviso.
///
/// Vive en Application, y no en el binder de la Web, para poder probarlo: el
/// proyecto de tests no referencia la Web. El DecimalModelBinder sólo traduce el
/// resultado a lo que espera ASP.NET.
///
/// Distingue tres casos, porque vacío no es cero: vacío es "no cargó nada", cero
/// es "dijo que vale cero". Qué hacer con un vacío lo decide quien llama.
/// </summary>
public static class LectorImporte
{
    // Sin AllowThousands a propósito: así el punto nunca se puede confundir con un
    // separador de miles. Sin AllowLeadingWhite ni AllowTrailingWhite: " 12" es
    // inválido (el navegador no manda espacios en un campo numérico).
    private const NumberStyles Estilos = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    private static readonly CultureInfo EsAr = new("es-AR");

    /// <summary>
    /// Vacío (null, "" o sólo espacios), un número, o inválido. No depende de la
    /// configuración regional de la computadora: prueba primero con punto decimal
    /// (InvariantCulture) y después con coma (es-AR), siempre las mismas dos.
    /// </summary>
    public static LecturaImporte Interpretar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new LecturaImporte(ResultadoLectura.Vacio, 0m);

        if (decimal.TryParse(texto, Estilos, CultureInfo.InvariantCulture, out var numero)
            || decimal.TryParse(texto, Estilos, EsAr, out numero))
            return new LecturaImporte(ResultadoLectura.Numero, numero);

        return new LecturaImporte(ResultadoLectura.Invalido, 0m);
    }
}

public enum ResultadoLectura
{
    /// <summary>No se cargó nada.</summary>
    Vacio,
    /// <summary>Se leyó un número: está en Valor.</summary>
    Numero,
    /// <summary>Se escribió algo que no es un importe.</summary>
    Invalido
}

/// <summary>El resultado de leer un importe. Valor sólo tiene sentido si es un Numero.</summary>
public readonly record struct LecturaImporte(ResultadoLectura Resultado, decimal Valor);
