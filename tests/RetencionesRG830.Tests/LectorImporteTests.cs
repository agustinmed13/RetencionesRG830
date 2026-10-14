using System.Globalization;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Cómo se lee un importe escrito en un formulario: la mitad del problema del
/// separador decimal que el sistema vino a resolver (la otra mitad es el archivo de
/// SICORE, en GeneradorArchivoSicoreTests).
///
/// Estos casos fijan el comportamiento que tenía el DecimalModelBinder ANTES de
/// sacar la regla a LectorImporte: se midieron en vivo, mandando cada texto como
/// importe gravado a la pantalla de revisión (11/10/2026). Si uno de estos tests
/// se rompe, cambió cómo se lee un importe.
///
/// Un cambio deliberado desde entonces (octubre de 2026): un importe con más de dos
/// decimales es inválido. "1.234" y "1,234" se leían como 1,234; ahora se rechazan.
/// Ver el porqué en LectorImporte.
///
/// Cada caso corre dos veces, con la computadora configurada en es-AR y en en-US:
/// el resultado no puede depender de la configuración regional.
/// </summary>
public class LectorImporteTests
{
    private static readonly string[] Culturas = { "es-AR", "en-US" };

    private static readonly (string texto, string valor)[] Numeros =
    {
        ("87190.09", "87190.09"),   // como lo manda <input type="number">: con punto
        ("87190,09", "87190.09"),   // como lo escribe una persona en es-AR: con coma
        ("50000,75", "50000.75"),   // el ejemplo del mensaje de error
        // Importes de seis, siete y ocho cifras enteras, con y sin decimales. En el
        // archivo real del estudio hay un comprobante de $ 15.769.531,50.
        ("500000,75", "500000.75"),
        ("999999,99", "999999.99"),
        ("1000000,99", "1000000.99"),
        ("1000000,00", "1000000.00"),
        ("1000000", "1000000"),
        ("15769531,50", "15769531.50"),
        ("15769531.50", "15769531.50"),
        ("15769531", "15769531"),
        ("1.5", "1.5"),             // un decimal
        ("1.50", "1.50"),           // dos decimales, aunque el segundo sea cero
        ("+5", "5"),
        ("-5", "-5"),               // se lee; lo rechaza después [Range] del campo
        (".5", "0.5"),
        (",5", "0.5"),
        ("1.", "1"),
        ("0", "0"),                 // cero es un número, no un vacío
        // Espacios en los extremos: se sacan antes de leer. Hasta octubre de 2026 " 12"
        // y "12 " eran inválidos, por una suposición que dejó de ser cierta (ver
        // LectorImporte). Son los que trae un importe pegado desde Excel o desde un
        // mensaje: espacio común, tabulación y espacio duro (NBSP, U+00A0).
        (" 12", "12"),
        ("12 ", "12"),
        ("1000000,99 ", "1000000.99"),
        ("1000000,99\t", "1000000.99"),
        ("\t1000000,99", "1000000.99"),
        ("1000000,99 ", "1000000.99"),
        (" 1000000,99", "1000000.99"),
        ("  15769531,50 \t", "15769531.50"),
    };

    private static readonly string[] Invalidos =
    {
        "1.234,56",    // miles y decimales mezclados: sin separador de miles, es inválido
        "1,234.56",
        "5.000.000",
        // Más de dos decimales: la plata tiene dos. Hasta octubre de 2026 "1.234" se
        // leía como 1,234; casi siempre el operador quiso decir mil doscientos
        // treinta y cuatro.
        "1.234",
        "1,234",
        "50.000",      // el caso que motivó la regla: cincuenta mil, no cincuenta
        "50,000",
        "1.2345",
        "0.001",
        "1.500",       // tres decimales aunque terminen en cero
        // Un espacio EN EL MEDIO sigue siendo inválido: puede ser un separador de miles
        // ("1 000 000"), y la regla es no adivinar separadores de miles.
        "1 000 000",
        "1 000 000",
        "1000000, 99",
        "$ 1000000,99", // el símbolo de pesos tampoco: el campo es sólo el número
        "1e3",
        "abc",
    };

    private static readonly string?[] Vacios = { null, "", "   " };

    public static IEnumerable<object[]> CasosNumero() =>
        from c in Culturas from n in Numeros select new object[] { c, n.texto, n.valor };

    public static IEnumerable<object[]> CasosInvalido() =>
        from c in Culturas from t in Invalidos select new object[] { c, t };

    public static IEnumerable<object?[]> CasosVacio() =>
        from c in Culturas from t in Vacios select new object?[] { c, t };

    /// <summary>Corre la lectura con la computadora configurada en esa cultura.</summary>
    private static LecturaImporte LeerEn(string cultura, string? texto)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultura);
            return LectorImporte.Interpretar(texto);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [MemberData(nameof(CasosNumero))]
    public void Lee_el_numero_con_punto_o_coma_decimal(string cultura, string texto, string esperado)
    {
        var lectura = LeerEn(cultura, texto);

        Assert.Equal(ResultadoLectura.Numero, lectura.Resultado);
        Assert.Equal(decimal.Parse(esperado, CultureInfo.InvariantCulture), lectura.Valor);
    }

    [Theory]
    [MemberData(nameof(CasosInvalido))]
    public void Lo_que_no_es_un_importe_es_invalido(string cultura, string texto)
    {
        Assert.Equal(ResultadoLectura.Invalido, LeerEn(cultura, texto).Resultado);
    }

    [Theory]
    [MemberData(nameof(CasosVacio))]
    public void Un_campo_vacio_es_vacio_y_no_cero(string cultura, string? texto)
    {
        Assert.Equal(ResultadoLectura.Vacio, LeerEn(cultura, texto).Resultado);
    }

    [Fact]
    public void El_caso_que_origino_el_sistema_no_se_multiplica_por_cien_en_es_AR()
    {
        // Bajo es-AR, "87190.09" leído con la configuración regional da 8.719.009:
        // el defecto que el sistema vino a resolver.
        var lectura = LeerEn("es-AR", "87190.09");

        Assert.Equal(87190.09m, lectura.Valor);
        Assert.NotEqual(8719009m, lectura.Valor);
    }
}
