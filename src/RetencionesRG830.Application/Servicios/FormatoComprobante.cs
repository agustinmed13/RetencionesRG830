using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Cómo se MUESTRAN los datos de una operación: tipo de comprobante, número de
/// certificado y los avisos que los nombran. Es sólo presentación: no decide nada
/// del cálculo. El archivo de SICORE no la usa, porque lleva el código numérico del
/// comprobante y el certificado con 14 dígitos, como pide la especificación.
///
/// Está en Application y no en la Web por el mismo motivo que FormatoCuit: el
/// certificado lo arma Infrastructure, que no ve la Web. Antes el PDF tenía su propia
/// copia de los tipos y escribía "Certificado N° 1" mientras las pantallas decían
/// 00000001: el contador leía un número en el papel y buscaba otro en el sistema.
/// </summary>
public static class FormatoComprobante
{
    /// <summary>
    /// Los tipos de comprobante que el sistema ofrece. Es la única tabla: de acá salen
    /// el formulario de carga, los nombres, las abreviaturas y el certificado PDF. El
    /// código es el que se guarda en la base y el que va a las posiciones 1-2 del
    /// archivo de SICORE, así que no es un número interno: lo fija AFIP.
    ///
    /// Códigos de comprobante de SICORE:
    ///   01 Factura
    ///   02 Recibo
    ///   03 Nota de crédito
    ///   04 Nota de débito
    ///   05 Otro comprobante   (existe, el sistema todavía no lo ofrece)
    ///   06 Orden de pago      (existe, el sistema todavía no lo ofrece: regla 12)
    ///
    /// Fuente: relevamiento del autor, noviembre de 2026, en tres fuentes coincidentes,
    /// una de ellas un instructivo de AFIP. El detalle está en CLAUDE.md, "Códigos de
    /// comprobante". Hasta entonces este comentario citaba la hoja Tablas del Excel,
    /// que no está en el repositorio, y la tabla tenía Recibo y Nota de débito
    /// invertidos. Cambiar un código de acá cambia el significado de lo ya guardado:
    /// necesita una migración de datos (ver CorrigeCodigosComprobante).
    /// </summary>
    public static readonly IReadOnlyList<(int Codigo, string Nombre, string Abreviatura)> Tipos = new[]
    {
        (1, "Factura", "FC"),
        (2, "Recibo", "RE"),
        (3, "Nota de crédito", "NC"),
        (4, "Nota de débito", "ND")
    };

    /// <summary>Nombre del tipo de comprobante: "Factura", "Recibo"...</summary>
    public static string NombreTipo(int tipo) =>
        Tipos.Where(t => t.Codigo == tipo).Select(t => t.Nombre).FirstOrDefault() ?? $"Comprobante tipo {tipo}";

    /// <summary>Abreviatura del tipo de comprobante, para las columnas de las tablas.</summary>
    public static string Abreviatura(int tipo) =>
        Tipos.Where(t => t.Codigo == tipo).Select(t => t.Abreviatura).FirstOrDefault() ?? $"Tipo {tipo}";

    /// <summary>
    /// Comprobante completo: "FC 00003-00012847". Punto de venta de cinco
    /// cifras y número de ocho, como en la pantalla de detalle.
    /// </summary>
    public static string Describir(Operacion operacion) =>
        $"{Abreviatura(operacion.TipoComprobante)} " +
        $"{operacion.PuntoVenta:D5}-{operacion.NumeroComprobante:D8}";

    /// <summary>Número de certificado con ocho cifras: "00000041".</summary>
    public static string NumeroCertificado(int numero) => numero.ToString("D8");

    /// <summary>
    /// Por qué una operación no emite certificado (regla 10). Lo dicen la
    /// confirmación, antes de registrar, y el detalle, después: el mismo texto en
    /// los dos lugares.
    /// </summary>
    public const string MotivoSinCertificado =
        "No se emite certificado: la retención a practicar es $ 0, porque no alcanza el mínimo. "
        + "La operación se registra igual, porque suma al acumulado del mes de este proveedor en este régimen.";

    /// <summary>
    /// Aviso de comprobante repetido. No es un error: la retención se practica al
    /// pagar, y una factura pagada en cuotas da varias retenciones con el mismo
    /// comprobante. Por eso el texto informa y deja seguir.
    /// </summary>
    public static string AvisoRepetido(IReadOnlyList<Operacion> existentes)
    {
        // Una operación que no retuvo no tiene número de certificado (regla 10): se la
        // nombra por su fecha.
        var lista = string.Join(" y ", existentes.Select(o => o.NumeroCertificado is int numero
            ? $"certificado N° {NumeroCertificado(numero)} del {o.FechaRetencion:dd/MM/yyyy}"
            : $"una sin certificado del {o.FechaRetencion:dd/MM/yyyy}"));
        var cuantas = existentes.Count == 1
            ? "Ya hay una operación vigente con este comprobante para este proveedor:"
            : $"Ya hay {existentes.Count} operaciones vigentes con este comprobante para este proveedor:";
        return $"{cuantas} {lista}. Si es otro pago de la misma factura (por ejemplo, una cuota), podés seguir; si no, revisá los datos.";
    }
}
