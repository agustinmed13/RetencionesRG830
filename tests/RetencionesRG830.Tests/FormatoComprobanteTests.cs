using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Cómo se muestran el tipo de comprobante y el número de certificado. Es sólo
/// presentación, pero el certificado es un documento fiscal: el contador lee un
/// número en el papel y lo busca en el sistema, así que los dos tienen que decir lo
/// mismo. El código numérico que va al SICORE no pasa por acá: lo fija
/// GeneradorArchivoSicoreTests.
/// </summary>
public class FormatoComprobanteTests
{
    [Theory]
    [InlineData(1, "Factura", "FC")]
    [InlineData(2, "Nota de débito", "ND")]
    [InlineData(3, "Nota de crédito", "NC")]
    [InlineData(4, "Recibo", "RE")]
    public void Cada_codigo_tiene_su_nombre_y_su_abreviatura(int codigo, string nombre, string abreviatura)
    {
        Assert.Equal(nombre, FormatoComprobante.NombreTipo(codigo));
        Assert.Equal(abreviatura, FormatoComprobante.Abreviatura(codigo));
    }

    /// <summary>Un código que no está en la tabla se muestra, no rompe la pantalla.</summary>
    [Fact]
    public void Un_codigo_desconocido_se_nombra_con_su_numero()
    {
        Assert.Equal("Comprobante tipo 99", FormatoComprobante.NombreTipo(99));
        Assert.Equal("Tipo 99", FormatoComprobante.Abreviatura(99));
    }

    [Theory]
    [InlineData(1, "00000001")]
    [InlineData(41, "00000041")]
    [InlineData(12345678, "12345678")]
    public void El_numero_de_certificado_sale_con_ocho_digitos(int numero, string esperado)
    {
        Assert.Equal(esperado, FormatoComprobante.NumeroCertificado(numero));
    }

    /// <summary>
    /// El PDF decía "Certificado N° 1" y el listado 00000001. Este test mira el texto
    /// que imprime el generador, no sólo el formato: si el generador volviera a
    /// escribir el número suelto, falla acá.
    /// </summary>
    [Fact]
    public void El_certificado_PDF_imprime_el_numero_con_ocho_digitos()
    {
        Assert.Equal("Certificado N° 00000001", GeneradorCertificadoPdf.TextoNumeroCertificado(1));
    }
}
