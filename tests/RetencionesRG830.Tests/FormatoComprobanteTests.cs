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
    /// <summary>
    /// Códigos de comprobante de SICORE. Fuente: relevamiento del autor, noviembre de
    /// 2026, en tres fuentes coincidentes, una de ellas un instructivo de AFIP (ver
    /// CLAUDE.md, "Códigos de comprobante"). Está escrita acá a propósito, y no leída
    /// de FormatoComprobante.Tipos: si la tabla del sistema cambia, este test falla.
    /// Si falla, el error casi seguro no está acá.
    /// </summary>
    private static readonly (int Codigo, string Nombre)[] TablaOficialSicore =
    {
        (1, "Factura"),
        (2, "Recibo"),
        (3, "Nota de crédito"),
        (4, "Nota de débito"),
        (5, "Otro comprobante"),
        (6, "Orden de pago")
    };

    /// <summary>Existen en SICORE, pero el sistema todavía no los ofrece.</summary>
    private static readonly int[] CodigosSinImplementar = { 5, 6 };

    /// <summary>
    /// Recibo y Nota de débito estuvieron invertidos desde la primera versión, y
    /// ningún test lo vio porque el que había copiaba la tabla equivocada.
    /// </summary>
    [Fact]
    public void Los_tipos_ofrecidos_son_los_de_la_tabla_oficial_de_SICORE()
    {
        var esperados = TablaOficialSicore.Where(t => !CodigosSinImplementar.Contains(t.Codigo));

        Assert.Equal(esperados, FormatoComprobante.Tipos.Select(t => (t.Codigo, t.Nombre)));
    }

    /// <summary>
    /// Agregar 05 o 06 no es sumar un renglón: antes tiene que estar el control de los
    /// 145 caracteres del SICORE, y la orden de pago trae sus propias preguntas de
    /// numeración (regla 12). Cuando eso esté, este test se actualiza a conciencia.
    /// </summary>
    [Fact]
    public void Otro_comprobante_y_orden_de_pago_todavia_no_se_ofrecen()
    {
        Assert.All(CodigosSinImplementar, codigo =>
            Assert.DoesNotContain(FormatoComprobante.Tipos, t => t.Codigo == codigo));
    }

    [Theory]
    [InlineData(1, "Factura", "FC")]
    [InlineData(2, "Recibo", "RE")]
    [InlineData(3, "Nota de crédito", "NC")]
    [InlineData(4, "Nota de débito", "ND")]
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
