using System.Globalization;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Tests dorados del archivo para SICORE.
///
/// El formato de estas líneas -las 145 posiciones fijas y el lugar exacto de
/// cada campo- se obtuvo analizando el archivo real que el estudio contable
/// presentó en agosto de 2026, y se validó reproduciéndolo carácter por
/// carácter contra ese archivo.
///
/// Los datos identificatorios (CUIT, importes de las partes) fueron
/// reemplazados por valores ficticios antes de publicar el código, para no
/// exponer información fiscal de terceros. Los CUIT ficticios son
/// matemáticamente válidos, y la estructura del registro es exactamente la
/// misma que se validó contra el archivo original.
/// </summary>
public class GeneradorArchivoSicoreTests
{
    private static Operacion CrearOperacion(
        string fechaComprobante,
        int puntoVenta,
        long numeroComprobante,
        decimal importeComprobante,
        decimal baseAcumulada,
        string fechaRetencion,
        decimal montoRetenido,
        string cuitProveedor,
        int numeroCertificado)
        => new Operacion
        {
            TipoComprobante = 1,
            FechaComprobante = DateOnly.ParseExact(fechaComprobante, "dd/MM/yyyy", CultureInfo.InvariantCulture),
            PuntoVenta = puntoVenta,
            NumeroComprobante = numeroComprobante,
            ImporteComprobante = importeComprobante,
            BaseCalculoAcumulada = baseAcumulada,
            FechaRetencion = DateOnly.ParseExact(fechaRetencion, "dd/MM/yyyy", CultureInfo.InvariantCulture),
            MontoRetenido = montoRetenido,
            NumeroCertificado = numeroCertificado,
            Proveedor = new Proveedor { Cuit = cuitProveedor },
            Regimen = new Regimen { Codigo = 78 }
        };

    [Theory]
    // Gómez, factura 00001-00002514: primera operación del mes.
    [InlineData("04/08/2026", 1, 2514, 501000.02, 414049.59, "12/08/2026", 3800.99, "20-31456789-8", 4,
        "0104/08/20260000100002514   0000000501000,020217078100000414049,5912/08/202601000000003800,99000,0000/00/0000800000000002031456789800000000000004")]
    // Gómez, factura 00001-00002515: la base informada es el ACUMULADO
    // del mes, no el gravado de este comprobante.
    [InlineData("04/08/2026", 1, 2515, 87190.00, 501239.59, "12/08/2026", 1743.80, "20-31456789-8", 5,
        "0104/08/20260000100002515   0000000087190,000217078100000501239,5912/08/202601000000001743,80000,0000/00/0000800000000002031456789800000000000005")]
    // Servicios del Sur S.H., factura 00005-00010650.
    [InlineData("30/07/2026", 5, 10650, 1009895.02, 834623.98, "13/08/2026", 12212.48, "30-64567891-1", 6,
        "0130/07/20260000500010650   0000001009895,020217078100000834623,9813/08/202601000000012212,48000,0000/00/0000800000000003064567891100000000000006")]
    // Ramírez, Laura Beatriz, factura 00003-00005527.
    [InlineData("31/07/2026", 3, 5527, 747088.58, 617428.58, "13/08/2026", 7868.57, "27-32567894-7", 7,
        "0131/07/20260000300005527   0000000747088,580217078100000617428,5813/08/202601000000007868,57000,0000/00/0000800000000002732567894700000000000007")]
    // Sosa, Marta Elena, factura 00003-00000505.
    [InlineData("12/08/2026", 3, 505, 617518.66, 510346.00, "13/08/2026", 5726.92, "27-33678912-0", 8,
        "0112/08/20260000300000505   0000000617518,660217078100000510346,0013/08/202601000000005726,92000,0000/00/0000800000000002733678912000000000000008")]
    public void Reproduce_las_lineas_reales_presentadas_por_el_estudio(
        string fechaComprobante,
        int puntoVenta,
        long numeroComprobante,
        double importeComprobante,
        double baseAcumulada,
        string fechaRetencion,
        double montoRetenido,
        string cuitProveedor,
        int numeroCertificado,
        string lineaEsperada)
    {
        var operacion = CrearOperacion(
            fechaComprobante, puntoVenta, numeroComprobante,
            (decimal)importeComprobante, (decimal)baseAcumulada,
            fechaRetencion, (decimal)montoRetenido,
            cuitProveedor, numeroCertificado);

        var linea = new GeneradorArchivoSicore().GenerarLinea(operacion);

        Assert.Equal(145, linea.Length);
        Assert.Equal(lineaEsperada, linea);
    }

    /// <summary>
    /// Este es el test que cubre el error que hoy le hace rechazar
    /// presentaciones al estudio: según la configuración regional de la
    /// computadora que arme el archivo, los decimales salen con punto o con
    /// coma. Acá se genera la misma línea bajo dos configuraciones
    /// regionales distintas y se comprueba que el resultado sea idéntico.
    /// </summary>
    [Fact]
    public void El_formato_de_los_numeros_no_depende_de_la_computadora()
    {
        var operacion = CrearOperacion(
            "04/08/2026", 1, 2514, 501000.02m, 414049.59m,
            "12/08/2026", 3800.99m, "20-31456789-8", 4);

        var generador = new GeneradorArchivoSicore();
        var culturaOriginal = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var enUnaComputadoraEnIngles = generador.GenerarLinea(operacion);

            CultureInfo.CurrentCulture = new CultureInfo("es-AR");
            var enUnaComputadoraEnEspanol = generador.GenerarLinea(operacion);

            Assert.Equal(enUnaComputadoraEnIngles, enUnaComputadoraEnEspanol);
            Assert.Contains("3800,99", enUnaComputadoraEnIngles);
            Assert.DoesNotContain("3800.99", enUnaComputadoraEnIngles);
        }
        finally
        {
            CultureInfo.CurrentCulture = culturaOriginal;
        }
    }

    /// <summary>
    /// El archivo completo pone una línea por operación, separadas por
    /// salto de línea, ordenadas por fecha de retención.
    /// </summary>
    [Fact]
    public void El_archivo_completo_arma_una_linea_por_operacion()
    {
        var operaciones = new[]
        {
            CrearOperacion("04/08/2026", 1, 2514, 501000.02m, 414049.59m, "12/08/2026", 3800.99m, "20-31456789-8", 4),
            CrearOperacion("04/08/2026", 1, 2515, 87190.00m, 501239.59m, "12/08/2026", 1743.80m, "20-31456789-8", 5),
        };

        var archivo = new GeneradorArchivoSicore().Generar(operaciones);
        var lineas = archivo.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lineas.Length);
        Assert.All(lineas, l => Assert.Equal(145, l.Length));
    }
}
