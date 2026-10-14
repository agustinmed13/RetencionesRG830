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
    // separador de miles. Los espacios de los extremos se sacan antes (ver Interpretar);
    // uno en el medio ("1 000 000") queda inválido, porque podría ser un separador de
    // miles, y la regla es no adivinarlos.
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

        // Los espacios de los extremos se sacan antes de leer (octubre de 2026).
        //
        // Antes " 12" era inválido, con este motivo: "el navegador no manda espacios
        // en un campo numérico". Era cierto mientras los importes eran
        // <input type="number">, porque el navegador limpia ese valor antes de
        // enviarlo. Dejó de serlo cuando los importes de Nueva operación pasaron a
        // type="text" (para que llegara el mensaje que enseña cómo se escribe): un
        // campo de texto manda lo que tenga, y "1000000,99" copiado con un carácter
        // invisible al final se rechazaba con un mensaje sobre separadores y
        // decimales que no tenía nada que ver.
        //
        // El motivo de fondo: el operador real va a pegar importes desde Excel o desde
        // un mensaje, y de ahí vienen espacios, tabulaciones y espacios duros (NBSP,
        // U+00A0). No son parte del número ni lo vuelven ambiguo: sacarlos no adivina
        // nada.
        //
        // Se usa Trim() y no NumberStyles.AllowLeadingWhite/AllowTrailingWhite porque
        // esos sólo reconocen el espacio común, la tabulación y los saltos de línea, NO
        // el espacio duro, que es justamente el que trae Excel. Trim() saca cualquier
        // carácter que .NET considere espacio (char.IsWhiteSpace), NBSP incluido.
        var limpio = texto.Trim();

        if (decimal.TryParse(limpio, Estilos, CultureInfo.InvariantCulture, out var numero)
            || decimal.TryParse(limpio, Estilos, EsAr, out numero))
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
