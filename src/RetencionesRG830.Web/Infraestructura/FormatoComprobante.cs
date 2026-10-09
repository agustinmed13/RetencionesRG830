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
    /// Aviso de comprobante repetido. No es un error: la retención se practica al
    /// pagar, y una factura pagada en cuotas da varias retenciones con el mismo
    /// comprobante. Por eso el texto informa y deja seguir.
    /// </summary>
    public static string AvisoRepetido(IReadOnlyList<Operacion> existentes)
    {
        var lista = string.Join(" y ", existentes.Select(o =>
            $"N° {NumeroCertificado(o.NumeroCertificado)} del {o.FechaRetencion:dd/MM/yyyy}"));
        var cuantas = existentes.Count == 1
            ? "Ya hay una operación vigente con este comprobante para este proveedor: certificado"
            : $"Ya hay {existentes.Count} operaciones vigentes con este comprobante para este proveedor: certificados";
        return $"{cuantas} {lista}. Si es otro pago de la misma factura (por ejemplo, una cuota), podés seguir; si no, revisá los datos.";
    }
}
