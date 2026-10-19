using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Tests;

/// <summary>
/// Cómo se muestra un CUIT. Es sólo presentación: lo guardado son los 11 dígitos.
/// </summary>
public class FormatoCuitTests
{
    [Theory]
    [InlineData("30700000008", "30-70000000-8")]
    [InlineData("20314567898", "20-31456789-8")]
    [InlineData("27325678947", "27-32567894-7")]
    public void Once_digitos_se_muestran_con_guiones(string guardado, string esperado)
    {
        Assert.Equal(esperado, FormatoCuit.ParaMostrar(guardado));
    }

    /// <summary>
    /// Lo que no son 11 dígitos se muestra tal cual, sin romper la pantalla. No
    /// debería llegar nunca (el contexto no deja guardarlo), pero si llega, se ve.
    /// </summary>
    [Theory]
    [InlineData("3070000000")]      // diez dígitos
    [InlineData("307000000088")]    // doce dígitos
    [InlineData("30-70000000-8")]   // ya con guiones
    [InlineData("3070000000A")]     // once caracteres, uno no es dígito
    [InlineData("")]
    public void Lo_que_no_son_11_digitos_se_muestra_tal_cual(string guardado)
    {
        Assert.Equal(guardado, FormatoCuit.ParaMostrar(guardado));
    }

    [Fact]
    public void Un_CUIT_nulo_se_muestra_vacio()
    {
        Assert.Equal(string.Empty, FormatoCuit.ParaMostrar(null));
    }
}
