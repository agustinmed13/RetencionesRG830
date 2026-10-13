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
///
/// Un importe con MÁS DE DOS DECIMALES es inválido (octubre de 2026). Es una regla
/// del dominio: la plata tiene dos decimales, y un importe de factura con tres no
/// existe. Antes "1.234" se leía como 1,234, que era correcto según la regla de que
/// el punto nunca es separador de miles, pero casi siempre equivocado según la
/// intención del operador: en la Argentina la plata se escribe con puntos, y quien
/// escribe "50.000" quiere decir cincuenta mil, no cincuenta. Se registraba una
/// operación de $ 50 sin ningún aviso. Ahora esos textos son inválidos, y el
/// mensaje del formulario explica cómo se escribe.
///
/// Sirve sólo para importes en pesos. DecimalModelBinder lo aplica a TODO decimal
/// de los formularios: un campo con más de dos decimales legítimos (una alícuota,
/// por ejemplo en una pantalla para cargar regímenes) necesita otra lectura.
/// </summary>
public static class LectorImporte
{
    // Sin AllowThousands a propósito: así el punto nunca se puede confundir con un
    // separador de miles. Sin AllowLeadingWhite ni AllowTrailingWhite: " 12" es
    // inválido (el navegador no manda espacios en un campo numérico).
    private const NumberStyles Estilos = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

    private static readonly CultureInfo EsAr = new("es-AR");

    private const int DecimalesMaximos = 2;

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
        {
            // Scale es la cantidad de decimales tal como se escribieron: un decimal
            // de .NET conserva los ceros finales, así que "50.000" tiene Scale 3 y
            // "1.50" tiene Scale 2. Por eso se puede contar sobre el número leído,
            // sin volver a mirar el texto.
            if (numero.Scale > DecimalesMaximos)
                return new LecturaImporte(ResultadoLectura.Invalido, 0m);

            return new LecturaImporte(ResultadoLectura.Numero, numero);
        }

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
