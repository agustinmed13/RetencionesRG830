namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Valida el CUIT de un Cliente o Proveedor usando el mismo algoritmo de
/// "dígito verificador" que usa AFIP -el mismo que la planilla Excel
/// calculaba con una fórmula larga de SUMAPRODUCTO. La idea: los primeros
/// 10 dígitos del CUIT, multiplicados cada uno por un número fijo y
/// sumados, tienen que dar un resultado que "encaje" matemáticamente con
/// el dígito número 11 (el "verificador"). Si no encaja, hay un error de
/// tipeo en el CUIT.
/// </summary>
public static class ValidadorCuit
{
    private static readonly int[] Multiplicadores = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

    /// <summary>
    /// Devuelve true si el CUIT es matemáticamente válido. Acepta el CUIT
    /// con o sin guiones (por ejemplo "20-12345678-6" o "20123456786").
    /// </summary>
    public static bool EsValido(string? cuit)
    {
        if (string.IsNullOrWhiteSpace(cuit))
            return false;

        var soloDigitos = new string(cuit.Where(char.IsDigit).ToArray());

        if (soloDigitos.Length != 11)
            return false;

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            var digito = soloDigitos[i] - '0';
            suma += digito * Multiplicadores[i];
        }

        var resto = suma % 11;
        var verificadorEsperado = 11 - resto;
        if (verificadorEsperado == 11) verificadorEsperado = 0;
        if (verificadorEsperado == 10) return false; // no existe ningún CUIT válido con este resultado

        var digitoVerificadorReal = soloDigitos[10] - '0';
        return verificadorEsperado == digitoVerificadorReal;
    }
}