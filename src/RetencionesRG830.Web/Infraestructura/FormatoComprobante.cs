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
}
