using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Tests;

public class ValidadorCuitTests
{
    [Theory]
    [InlineData("20-12345678-6")]   // CUIT de persona humana, válido
    [InlineData("30-71234561-2")]   // CUIT de sociedad, válido
    [InlineData("20123456786")]     // el mismo de arriba, sin guiones
    public void Cuits_validos_devuelven_true(string cuit)
    {
        Assert.True(ValidadorCuit.EsValido(cuit));
    }

    [Theory]
    [InlineData("20-12345678-4")]   // dígito verificador incorrecto
    [InlineData("20-12345678")]     // le falta el dígito verificador
    [InlineData("")]
    [InlineData(null)]
    public void Cuits_invalidos_devuelven_false(string? cuit)
    {
        Assert.False(ValidadorCuit.EsValido(cuit));
    }

    // --- El motivo ----------------------------------------------------------
    // Antes todo CUIT inválido decía "el dígito verificador no coincide", también
    // con 10 dígitos (2045332916, probado en el formulario).

    [Theory]
    [InlineData("2045332916", "El CUIT tiene 10 dígitos; tienen que ser 11.")]
    [InlineData("20-4533291-6", "El CUIT tiene 10 dígitos; tienen que ser 11.")]
    [InlineData("204533291612", "El CUIT tiene 12 dígitos; tienen que ser 11.")]
    [InlineData("2", "El CUIT tiene 1 dígito; tienen que ser 11.")]
    [InlineData("", "El CUIT tiene 0 dígitos; tienen que ser 11.")]
    public void Sin_11_digitos_el_motivo_dice_cuantos_tiene(string cuit, string mensaje)
    {
        Assert.Equal(mensaje, ValidadorCuit.Motivo(cuit));
    }

    [Theory]
    [InlineData("20-12345678-4")]
    [InlineData("20123456784")]
    public void Con_11_digitos_y_el_verificador_mal_el_motivo_es_el_verificador(string cuit)
    {
        Assert.Equal("El CUIT ingresado no es válido (el dígito verificador no coincide).", ValidadorCuit.Motivo(cuit));
    }

    [Fact]
    public void Un_CUIT_valido_no_tiene_motivo()
    {
        Assert.Null(ValidadorCuit.Motivo("20-12345678-6"));
    }
}