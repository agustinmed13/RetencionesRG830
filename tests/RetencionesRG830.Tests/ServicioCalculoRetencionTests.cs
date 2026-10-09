using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Domain.Enums;

namespace RetencionesRG830.Tests;

/// <summary>
/// Estos son "tests dorados": los datos NO son inventados. Salen de la hoja
/// "Base" de la planilla real del estudio, con la retención que efectivamente
/// se le practicó a cada proveedor. Si estos tests pasan, el sistema calcula
/// igual que la planilla que hoy usan en producción.
/// </summary>
public class ServicioCalculoRetencionTests
{
    /// <summary>
    /// El régimen 78 (enajenación de bienes muebles) con los valores exactos
    /// de la hoja "Tablas" de la planilla.
    /// </summary>
    private static Regimen Regimen78() => new Regimen
    {
        Codigo = 78,
        Descripcion = "Enajenación de bienes muebles y bienes de cambio.",
        TipoCalculo = TipoCalculoRegimen.TasaFija,
        TasaInscripto = 0.02m,
        TasaNoInscriptoHumana = 0.10m,
        TasaNoInscriptoResto = 0.10m,
        MontoNoSujetoARetencion = 224000m,
        MinimoRetencion = 240m,
        VigenciaDesde = new DateOnly(2026, 1, 1)
    };

    // Cada línea es una operación real de la hoja Base:
    //   (neto gravado acumulado del mes, ya retenido antes, retención esperada)
    [Theory]
    // Gómez, comprobante 00001-00002514: primera operación del mes.
    [InlineData(414049.59, 0, 3800.9918)]
    // Gómez, comprobante 00001-00002515: SEGUNDA operación del mismo
    // mes y el mismo proveedor. El acumulado es 414049.59 + 87190.09.
    // Este es el caso que hoy le sale mal al estudio cuando se controla a ojo.
    [InlineData(501239.68, 3800.9918, 1743.8018)]
    // Servicios del Sur S.H., comprobante 0005-00010650.
    [InlineData(834623.98, 0, 12212.4796)]
    // Ramírez, Laura Beatriz, comprobante 00003-00005527.
    [InlineData(617428.58, 0, 7868.5716)]
    // Sosa, Marta Elena, comprobante 00003-00000505.
    [InlineData(510346, 0, 5726.92)]
    public void Reproduce_los_calculos_reales_de_la_planilla(
        double netoGravadoAcumulado, double retenidoPreviamente, double retencionEsperada)
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen78(),
            proveedorInscriptoEnGanancias: true,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: (decimal)netoGravadoAcumulado,
            retencionesAcumuladasDelMes: (decimal)retenidoPreviamente);

        Assert.Equal((decimal)retencionEsperada, resultado.RetencionAPracticar, 4);
    }
        /// <summary>
    /// El régimen 116 (honorarios de director) con la escala general de la
    /// hoja "Tablas". A diferencia del 78, no tiene una tasa fija para
    /// inscriptos: se calcula "por escala".
    /// </summary>
    private static Regimen Regimen116()
    {
        var regimen = new Regimen
        {
            Codigo = 116,
            Descripcion = "Honorarios de director de sociedades anónimas.",
            TipoCalculo = TipoCalculoRegimen.EscalaProgresiva,
            TasaInscripto = 0m,              // no aplica: va por escala
            TasaNoInscriptoHumana = 0.28m,
            TasaNoInscriptoResto = 0.28m,
            MontoNoSujetoARetencion = 67170m,
            MinimoRetencion = 240m,
            VigenciaDesde = new DateOnly(2026, 1, 1)
        };

        // Los ocho tramos de la escala general, tal cual la hoja "Tablas".
        var tramos = new[]
        {
            (Desde:     0m, Hasta: (decimal?)  8000m, Fijo:     0m, Pct: 0.05m),
            (Desde:  8000m, Hasta: (decimal?) 16000m, Fijo:   400m, Pct: 0.09m),
            (Desde: 16000m, Hasta: (decimal?) 24000m, Fijo:  1120m, Pct: 0.12m),
            (Desde: 24000m, Hasta: (decimal?) 32000m, Fijo:  2080m, Pct: 0.15m),
            (Desde: 32000m, Hasta: (decimal?) 48000m, Fijo:  3280m, Pct: 0.19m),
            (Desde: 48000m, Hasta: (decimal?) 64000m, Fijo:  6320m, Pct: 0.23m),
            (Desde: 64000m, Hasta: (decimal?) 96000m, Fijo: 10000m, Pct: 0.27m),
            (Desde: 96000m, Hasta: (decimal?)   null, Fijo: 18640m, Pct: 0.31m),
        };

        foreach (var t in tramos)
        {
            regimen.Tramos.Add(new TramoEscala
            {
                Desde = t.Desde,
                Hasta = t.Hasta,
                MontoFijo = t.Fijo,
                Porcentaje = t.Pct,
                VigenciaDesde = new DateOnly(2026, 1, 1)
            });
        }

        return regimen;
    }

    [Theory]
    // Acumulado 200000 - piso 67170 = 132830, cae en el último tramo:
    // 18640 + 31% sobre (132830 - 96000) = 18640 + 11417,30 = 30057,30
    [InlineData(200000, 0, 30057.30)]
    // Acumulado 100000 - 67170 = 32830, tramo 32000/48000:
    // 3280 + 19% sobre 830 = 3280 + 157,70 = 3437,70
    [InlineData(100000, 0, 3437.70)]
    // Segundo pago del mes: se descuenta lo ya retenido.
    [InlineData(200000, 3437.70, 26619.60)]
    // Acumulado por debajo del piso: no se retiene nada.
    [InlineData(50000, 0, 0)]
    public void Calcula_por_escala_progresiva_para_inscriptos(
        double netoGravadoAcumulado, double retenidoPreviamente, double retencionEsperada)
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen116(),
            proveedorInscriptoEnGanancias: true,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: (decimal)netoGravadoAcumulado,
            retencionesAcumuladasDelMes: (decimal)retenidoPreviamente);

        Assert.Equal((decimal)retencionEsperada, resultado.RetencionAPracticar, 4);
    }

    /// <summary>
    /// Caso borde importante: en la planilla, el "s/escala" figura únicamente
    /// en la columna de INSCRIPTOS. A un proveedor NO inscripto en el mismo
    /// régimen 116 se le aplica una tasa fija del 28%, y además sin piso.
    /// </summary>
    [Fact]
    public void Proveedor_no_inscripto_no_usa_la_escala_sino_la_tasa_fija()
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen116(),
            proveedorInscriptoEnGanancias: false,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 100000m,
            retencionesAcumuladasDelMes: 0m);

        // Sin piso (100000) por el 28% = 28000. NO 19880, que sería la escala.
        Assert.Equal(0m, resultado.MinimoNoSujetoARetencion);
        Assert.Equal(28000m, resultado.RetencionAPracticar, 4);
    }
        /// <summary>
    /// El régimen 31 (alquileres de inmuebles urbanos) con los valores de la
    /// hoja "Tablas". Es el único de la tabla con mínimo de retención
    /// diferenciado: $240 para inscriptos, $1.020 para no inscriptos.
    /// </summary>
    private static Regimen Regimen31() => new Regimen
    {
        Codigo = 31,
        Descripcion = "Alquileres o arrendamientos de bienes inmuebles urbanos.",
        TipoCalculo = TipoCalculoRegimen.TasaFija,
        TasaInscripto = 0.06m,
        TasaNoInscriptoHumana = 0.28m,
        TasaNoInscriptoResto = 0.25m,
        MontoNoSujetoARetencion = 11200m,
        MinimoRetencion = 240m,
        MinimoRetencionNoInscripto = 1020m,
        VigenciaDesde = new DateOnly(2026, 1, 1)
    };

    /// <summary>
    /// Proveedor NO inscripto: sin piso, 28% sobre 3.000 da 840, que no
    /// llega al mínimo de $1.020, así que no corresponde retener nada.
    /// </summary>
    [Fact]
    public void No_inscripto_en_alquileres_urbanos_usa_el_minimo_de_1020()
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen31(),
            proveedorInscriptoEnGanancias: false,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 3000m,
            retencionesAcumuladasDelMes: 0m);

        Assert.Equal(840m, resultado.RetencionAcumuladaDeterminada, 4);
        Assert.Equal(0m, resultado.RetencionAPracticar);
    }

    /// <summary>
    /// El mismo régimen, pero con proveedor inscripto: acá el mínimo es $240,
    /// así que una retención de $528 sí se practica.
    /// </summary>
    [Fact]
    public void Inscripto_en_alquileres_urbanos_usa_el_minimo_de_240()
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen31(),
            proveedorInscriptoEnGanancias: true,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 20000m,
            retencionesAcumuladasDelMes: 0m);

        Assert.Equal(528m, resultado.RetencionAPracticar, 4);
    }

    /// <summary>
    /// No inscripto pero superando el mínimo: 28% sobre 5.000 da 1.400,
    /// que sí pasa los $1.020 y se retiene.
    /// </summary>
    [Fact]
    public void No_inscripto_retiene_cuando_supera_los_1020()
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen31(),
            proveedorInscriptoEnGanancias: false,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 5000m,
            retencionesAcumuladasDelMes: 0m);

        Assert.Equal(1400m, resultado.RetencionAPracticar, 4);
    }

    /// <summary>
    /// Los siete renglones del desglose que ve el operador salen todos del
    /// resultado del motor, y tienen que cerrar entre sí como en la hoja Cálculo.
    /// Es el caso del prototipo de interfaz: segundo pago del mes a un mismo
    /// proveedor en el régimen 78 (datos ficticios).
    /// </summary>
    [Fact]
    public void El_desglose_sale_completo_del_resultado_y_cierra()
    {
        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: Regimen78(),
            proveedorInscriptoEnGanancias: true,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 2_000_000m,
            retencionesAcumuladasDelMes: 11_520m);

        Assert.Equal(2_000_000m, resultado.BaseAcumuladaDelMes);           // 1. base acumulada
        Assert.Equal(224_000m, resultado.MinimoNoSujetoARetencion);        // 2. monto no sujeto
        Assert.Equal(1_776_000m, resultado.NetoSujetoARetencion);          // 3. neto sujeto
        Assert.Equal(0.02m, resultado.TasaAplicada);                       // 4. alícuota
        Assert.False(resultado.SeAplicoEscala);
        Assert.Equal(35_520m, resultado.RetencionAcumuladaDeterminada, 4); // 5. determinada
        Assert.Equal(11_520m, resultado.RetencionesAnterioresDelMes);      // 6. anteriores
        Assert.Equal(24_000m, resultado.RetencionAPracticar, 4);           // 7. a practicar

        // Y cada renglón se deduce de los anteriores.
        Assert.Equal(resultado.BaseAcumuladaDelMes - resultado.MinimoNoSujetoARetencion,
                     resultado.NetoSujetoARetencion);
        Assert.Equal(resultado.RetencionAcumuladaDeterminada - resultado.RetencionesAnterioresDelMes,
                     resultado.RetencionAPracticar);
    }

    /// <summary>
    /// La pantalla muestra "Según escala" en lugar de la alícuota cuando el motor
    /// usó la escala. Sólo pasa con régimen por escala Y proveedor inscripto.
    /// </summary>
    [Theory]
    [InlineData(116, true, true)]   // escala e inscripto: escala
    [InlineData(116, false, false)] // escala pero no inscripto: tasa fija del 28%
    [InlineData(78, true, false)]   // régimen de tasa fija: nunca escala
    public void Informa_si_se_aplico_la_escala(int codigoRegimen, bool inscripto, bool esperado)
    {
        var regimen = codigoRegimen == 116 ? Regimen116() : Regimen78();

        var resultado = ServicioCalculoRetencion.Calcular(
            regimen: regimen,
            proveedorInscriptoEnGanancias: inscripto,
            tipoPersona: TipoPersona.HumanaYSucesionIndivisa,
            netoGravadoAcumuladoMensual: 200_000m,
            retencionesAcumuladasDelMes: 0m);

        Assert.Equal(esperado, resultado.SeAplicoEscala);
    }

    /// <summary>
    /// La alícuota según la condición del proveedor (celda C22). La pantalla de
    /// carga usa este mismo método para explicarle al operador qué tasa se aplica,
    /// así que tiene que coincidir con la que usa el cálculo. Régimen 94, el que
    /// tiene tasas distintas para persona humana y para el resto.
    /// </summary>
    [Theory]
    [InlineData(true, TipoPersona.HumanaYSucesionIndivisa, 0.02)] // inscripto
    [InlineData(false, TipoPersona.HumanaYSucesionIndivisa, 0.28)] // no inscripto, persona humana
    [InlineData(false, TipoPersona.Resto, 0.25)]                  // no inscripto, resto
    public void La_tasa_aplicable_depende_de_la_condicion_del_proveedor(
        bool inscripto, TipoPersona tipoPersona, double tasaEsperada)
    {
        var regimen94 = new Regimen
        {
            Codigo = 94,
            TipoCalculo = TipoCalculoRegimen.TasaFija,
            TasaInscripto = 0.02m,
            TasaNoInscriptoHumana = 0.28m,
            TasaNoInscriptoResto = 0.25m,
            MontoNoSujetoARetencion = 67170m,
            MinimoRetencion = 240m
        };

        var tasa = ServicioCalculoRetencion.TasaAplicable(regimen94, inscripto, tipoPersona);
        var resultado = ServicioCalculoRetencion.Calcular(regimen94, inscripto, tipoPersona, 500_000m, 0m);

        Assert.Equal((decimal)tasaEsperada, tasa);
        Assert.Equal(tasa, resultado.TasaAplicada); // la pantalla y el cálculo dicen lo mismo
    }
}