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

    public const int CantidadDeDigitos = 11;

    public const string MensajeDigitoVerificador =
        "El CUIT ingresado no es válido (el dígito verificador no coincide).";

    /// <summary>
    /// Devuelve true si el CUIT es matemáticamente válido. Acepta el CUIT
    /// con o sin guiones (por ejemplo "20-12345678-6" o "20123456786").
    /// </summary>
    public static bool EsValido(string? cuit) => Motivo(cuit) is null;

    /// <summary>
    /// Por qué el CUIT no es válido, en un mensaje para el operador, o null si es
    /// válido. Distingue las dos causas, porque se corrigen distinto: si faltan o
    /// sobran dígitos, hay que revisar cuántos se escribieron; si son 11 y el
    /// verificador no cierra, hay uno mal tipeado. Antes las dos decían "el dígito
    /// verificador no coincide", y con 10 dígitos el operador buscaba un error que
    /// no era ese.
    ///
    /// Cuenta sólo los dígitos del 0 al 9, igual que NormalizadorCuit, que es lo
    /// que se guarda. Antes usaba char.IsDigit, que también acepta dígitos de otros
    /// alfabetos (los arábigos orientales, por ejemplo) y hacía la cuenta del
    /// verificador con valores que no son de 0 a 9.
    /// </summary>
    public static string? Motivo(string? cuit)
    {
        var soloDigitos = new string((cuit ?? string.Empty).Where(c => c is >= '0' and <= '9').ToArray());

        if (soloDigitos.Length != CantidadDeDigitos)
            return $"El CUIT tiene {soloDigitos.Length} {(soloDigitos.Length == 1 ? "dígito" : "dígitos")}; " +
                   $"tienen que ser {CantidadDeDigitos}.";

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            var digito = soloDigitos[i] - '0';
            suma += digito * Multiplicadores[i];
        }

        var resto = suma % 11;
        var verificadorEsperado = 11 - resto;
        if (verificadorEsperado == 11) verificadorEsperado = 0;
        if (verificadorEsperado == 10) return MensajeDigitoVerificador; // no existe ningún CUIT válido con este resultado

        var digitoVerificadorReal = soloDigitos[10] - '0';
        return verificadorEsperado == digitoVerificadorReal ? null : MensajeDigitoVerificador;
    }
}
