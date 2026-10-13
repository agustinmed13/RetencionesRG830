using RetencionesRG830.Domain.Entidades;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Formatos de presentación de los datos de una operación en las pantallas.
///
/// Está en Web y no en Domain porque es sólo presentación: cómo se ve un
/// comprobante en una tabla no es una regla del negocio.
/// </summary>
public static class FormatoComprobante
{
    /// <summary>
    /// Tipos de comprobante que ofrece el formulario de carga, con su código
    /// AFIP. Son los mismos que traduce GeneradorCertificadoPdf.DescribirComprobante:
    /// si se agrega uno, hay que agregarlo en los dos lugares.
    /// </summary>
    public static readonly IReadOnlyList<(int Codigo, string Nombre)> Tipos = new[]
    {
        (1, "Factura"),
        (2, "Nota de débito"),
        (3, "Nota de crédito"),
        (4, "Recibo")
    };

    /// <summary>Nombre del tipo de comprobante: "Factura", "Recibo"...</summary>
    public static string NombreTipo(int tipo) =>
        Tipos.Where(t => t.Codigo == tipo).Select(t => t.Nombre).FirstOrDefault() ?? $"Comprobante tipo {tipo}";

    /// <summary>
    /// Abreviatura del tipo de comprobante, para las columnas de las tablas.
    /// Los códigos son los de la hoja Tablas del Excel, los mismos que
    /// traduce a texto completo GeneradorCertificadoPdf.DescribirComprobante.
    /// </summary>
    public static string Abreviatura(int tipo) => tipo switch
    {
        1 => "FC",
        2 => "ND",
        3 => "NC",
        4 => "RE",
        _ => $"Tipo {tipo}"
    };

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
