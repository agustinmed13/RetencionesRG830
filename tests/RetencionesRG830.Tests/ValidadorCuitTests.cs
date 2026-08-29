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
}