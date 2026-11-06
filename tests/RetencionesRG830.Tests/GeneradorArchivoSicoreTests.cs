using System.Globalization;
using RetencionesRG830.Application.Servicios;
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

    /// <summary>Una operación corriente, a la que cada caso le cambia un valor.</summary>
    private static Operacion OperacionComun() => CrearOperacion(
        "04/08/2026", 1, 2514, 501000.02m, 414049.59m,
        "12/08/2026", 3800.99m, "20-31456789-8", 4);

    /// <summary>
    /// Cada campo variable con el valor más largo que entra. Las líneas reales entran
    /// holgadas, así que no prueban el borde. Además de medir 145, cada caso mira que
    /// el valor esté en su posición: un campo corrido podría dar 145 igual.
    /// </summary>
    public static TheoryData<Action<Operacion>, int, string> ValoresEnElLimite => new()
    {
        { o => o.TipoComprobante = 99, 1, "99" },
        // 13-28: el punto de venta y el número llenan los 16 caracteres sin espacios.
        { o => { o.PuntoVenta = 99999; o.NumeroComprobante = 99999999999; }, 13, "9999999999999999" },
        // El formato habitual, 5 + 8, deja tres espacios.
        { o => { o.PuntoVenta = 99999; o.NumeroComprobante = 99999999; }, 13, "9999999999999   " },
        { o => o.ImporteComprobante = 9999999999999.99m, 29, "9999999999999,99" },
        { o => o.Regimen!.Codigo = 999, 49, "999" },
        { o => o.BaseCalculoAcumulada = 99999999999.99m, 53, "99999999999,99" },
        { o => o.MontoRetenido = 99999999999.99m, 80, "99999999999,99" },
        // El monto retenido se guarda sin redondear: el redondeo no llega a desbordar.
        { o => o.MontoRetenido = 99999999999.99499999m, 80, "99999999999,99" },
        { o => o.Proveedor!.Cuit = "27-33678912-0", 112, "00000000027336789120" },
        // El número de certificado es un int: nunca llega a pasarse de los 14.
        { o => o.NumeroCertificado = int.MaxValue, 132, "00002147483647" }
    };

    [Theory]
    [MemberData(nameof(ValoresEnElLimite))]
    public void Un_valor_en_el_limite_de_su_campo_mide_145_y_queda_en_su_lugar(
        Action<Operacion> cambiar, int posicion, string esperado)
    {
        var operacion = OperacionComun();
        cambiar(operacion);

        var linea = new GeneradorArchivoSicore().GenerarLinea(operacion);

        Assert.Equal(145, linea.Length);
        Assert.Equal(esperado, linea.Substring(posicion - 1, esperado.Length));
    }

    /// <summary>Todos los campos en su límite a la vez, en un solo renglón.</summary>
    [Fact]
    public void Todos_los_campos_en_su_limite_a_la_vez_miden_145()
    {
        var operacion = OperacionComun();
        foreach (var caso in ValoresEnElLimite)
            ((Action<Operacion>)caso[0])(operacion);
        // El último caso de 13-28 dejó el número de 8: se vuelve al más largo.
        operacion.NumeroComprobante = 99999999999;

        var linea = new GeneradorArchivoSicore().GenerarLinea(operacion);

        Assert.Equal(145, linea.Length);
    }

    /// <summary>
    /// Cada campo variable con un valor que no entra. El de 12 dígitos es el caso del
    /// diagnóstico: con punto de venta de 5, daba un renglón de 146 sin ningún error.
    /// El número de certificado no está porque un int no puede desbordar sus 14.
    /// </summary>
    public static TheoryData<Action<Operacion>, string, string> ValoresQueNoEntran => new()
    {
        { o => o.TipoComprobante = 100, "Código de comprobante", "100" },
        { o => { o.PuntoVenta = 12345; o.NumeroComprobante = 202600001234; },
            "Número del comprobante", "12345202600001234" },
        { o => { o.PuntoVenta = 100000; o.NumeroComprobante = 99999999999; },
            "Número del comprobante", "10000099999999999" },
        { o => o.ImporteComprobante = 10000000000000m, "Importe del comprobante", "10000000000000,00" },
        { o => o.Regimen!.Codigo = 1000, "Código de régimen", "1000" },
        { o => o.BaseCalculoAcumulada = 100000000000m, "Base de cálculo", "100000000000,00" },
        { o => o.MontoRetenido = 100000000000m, "Importe de la retención", "100000000000,00" },
        // Entra sin redondear, pero al redondear a dos decimales suma un dígito. Por eso
        // se mide el texto final y no el número.
        { o => o.MontoRetenido = 99999999999.995m, "Importe de la retención", "100000000000,00" },
        { o => o.Proveedor!.Cuit = "123456789012345678901",
            "Número de documento del retenido", "123456789012345678901" }
    };

    [Theory]
    [MemberData(nameof(ValoresQueNoEntran))]
    public void Un_valor_que_no_entra_falla_nombrando_la_operacion_el_campo_y_el_valor(
        Action<Operacion> cambiar, string campo, string valor)
    {
        var operacion = OperacionComun();
        operacion.Id = 77;
        cambiar(operacion);

        var error = Assert.Throws<ErrorArchivoSicore>(() => new GeneradorArchivoSicore().GenerarLinea(operacion));

        var problema = Assert.Single(error.Problemas);
        Assert.Contains("Operación 77", problema);
        Assert.Contains("certificado N°", problema);
        Assert.Contains($"\"{campo}\"", problema);
        Assert.Contains($"\"{valor}\"", problema);
    }

    /// <summary>
    /// Una operación mala no deja salir el archivo, ni siquiera con las buenas: uno
    /// incompleto parece bueno y se presentaría sin esa retención. El error las nombra
    /// a todas juntas, para no tener que ir descubriéndolas de a una.
    /// </summary>
    [Fact]
    public void Con_una_operacion_que_no_entra_no_sale_el_archivo_y_se_nombran_todas()
    {
        var buena = OperacionComun();
        var mala = OperacionComun();
        mala.Id = 11;
        mala.TipoComprobante = 100;
        var peor = OperacionComun();
        peor.Id = 12;
        peor.TipoComprobante = 100;
        peor.BaseCalculoAcumulada = 100000000000m;

        var error = Assert.Throws<ErrorArchivoSicore>(
            () => new GeneradorArchivoSicore().Generar(new[] { buena, mala, peor }));

        Assert.Equal(3, error.Problemas.Count);
        Assert.Single(error.Problemas, p => p.Contains("Operación 11"));
        Assert.Equal(2, error.Problemas.Count(p => p.Contains("Operación 12")));
    }

    /// <summary>
    /// La premisa del control por campo: que cada campo mida su ancho da 145 sólo si
    /// las posiciones encajan. Mira la tabla, sin generar ninguna línea, y es el único
    /// que ve un campo corrido: si se mueven Desde y Hasta juntos, el ancho no cambia,
    /// el valor sigue entrando y la línea sigue midiendo 145.
    /// </summary>
    [Fact]
    public void La_tabla_de_campos_cubre_las_145_posiciones_sin_huecos_ni_superposiciones()
    {
        var campos = GeneradorArchivoSicore.Campos;

        Assert.Equal(1, campos[0].Desde);
        Assert.All(campos, campo => Assert.True(campo.Ancho > 0, $"\"{campo.Nombre}\" no tiene ancho."));
        foreach (var (anterior, siguiente) in campos.Zip(campos.Skip(1)))
        {
            Assert.True(siguiente.Desde == anterior.Hasta + 1,
                $"\"{siguiente.Nombre}\" empieza en {siguiente.Desde}, "
                + $"pero \"{anterior.Nombre}\" termina en {anterior.Hasta}.");
        }
        Assert.Equal(145, campos[^1].Hasta);
        Assert.Equal(145, campos.Sum(campo => campo.Ancho));
    }

    /// <summary>
    /// El control final, con tablas defectuosas en las que cada campo pasa su control
    /// individual: una con un campo de más y otra con uno de menos. Con la tabla
    /// correcta nunca salta, así que es la única forma de saber que funciona.
    /// </summary>
    [Fact]
    public void Una_linea_armada_que_no_mide_145_falla_nombrando_la_operacion_y_el_largo()
    {
        var operacion = OperacionComun();
        operacion.Id = 77;
        var conUnoDeMas = GeneradorArchivoSicore.Campos
            .Append(new GeneradorArchivoSicore.Campo("Campo de más", 146, 146, _ => "X"))
            .ToList();
        var conUnoDeMenos = GeneradorArchivoSicore.Campos.SkipLast(1).ToList();
        var generador = new GeneradorArchivoSicore();

        var deMas = Assert.Throws<ErrorArchivoSicore>(() => generador.GenerarLinea(operacion, conUnoDeMas));
        var deMenos = Assert.Throws<ErrorArchivoSicore>(() => generador.GenerarLinea(operacion, conUnoDeMenos));

        var problema = Assert.Single(deMas.Problemas);
        Assert.Contains("Operación 77", problema);
        Assert.Contains("mide 146 caracteres", problema);
        Assert.Contains("mide 131 caracteres", Assert.Single(deMenos.Problemas));
    }

    /// <summary>
    /// Las líneas reales de arriba son todas facturas: los demás códigos no estaban
    /// cubiertos, y por eso los recibos salieron con 04 sin que nada avisara. Busca
    /// el tipo por su nombre, como lo elige el operador, para probar el camino entero
    /// desde la tabla hasta las posiciones 1-2 del archivo.
    /// </summary>
    [Theory]
    [InlineData("Factura", "01")]
    [InlineData("Recibo", "02")]
    [InlineData("Nota de crédito", "03")]
    [InlineData("Nota de débito", "04")]
    public void Cada_tipo_sale_con_su_codigo_oficial_en_las_posiciones_1_y_2(string nombre, string codigo)
    {
        var operacion = CrearOperacion(
            "04/08/2026", 1, 2514, 501000.02m, 414049.59m,
            "12/08/2026", 3800.99m, "20-31456789-8", 4);
        operacion.TipoComprobante = FormatoComprobante.Tipos.Single(t => t.Nombre == nombre).Codigo;

        var linea = new GeneradorArchivoSicore().GenerarLinea(operacion);

        Assert.Equal(145, linea.Length);
        Assert.Equal(codigo, linea[..2]);
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

    /// <summary>
    /// Una operación que no retuvo no emitió certificado y no se informa (regla 10).
    /// La selección ya la deja afuera (OperacionesParaSicoreAsync); si igual llegara
    /// al generador, falla en lugar de escribir un certificado en ceros, que AFIP
    /// recibiría como una retención más.
    /// </summary>
    [Fact]
    public void Una_operacion_sin_certificado_no_se_escribe_en_el_archivo()
    {
        var operacion = CrearOperacion(
            "04/09/2026", 1, 3001, 100000m, 100000m,
            "04/09/2026", 0m, "20-31456789-8", 1);
        operacion.NumeroCertificado = null;

        Assert.Throws<InvalidOperationException>(() => new GeneradorArchivoSicore().GenerarLinea(operacion));
    }
}
