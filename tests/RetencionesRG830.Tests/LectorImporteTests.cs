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
        ("1.234", "1.234"),         // el punto es decimal, NUNCA miles: no es 1234
        ("1,234", "1.234"),         // la coma también es decimal
        ("+5", "5"),
        ("-5", "-5"),               // se lee; lo rechaza después [Range] del campo
        (".5", "0.5"),
        (",5", "0.5"),
        ("1.", "1"),
        ("0", "0"),                 // cero es un número, no un vacío
    };

    private static readonly string[] Invalidos =
    {
        "1.234,56",    // miles y decimales mezclados: sin separador de miles, es inválido
        "1,234.56",
        "5.000.000",
        " 12",         // espacios: el navegador no los manda en un campo numérico
        "12 ",
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
