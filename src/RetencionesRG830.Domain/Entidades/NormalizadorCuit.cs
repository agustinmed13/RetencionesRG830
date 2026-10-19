namespace RetencionesRG830.Domain.Entidades;

/// <summary>
/// La forma en que se guarda y se compara un CUIT: sólo sus dígitos, sin
/// separadores. "20-31456789-8", "20.31456789.8" y "20314567898" + tabulación
/// son el mismo CUIT y quedan iguales: "20314567898".
///
/// Lo usan los setters de Cliente.Cuit y Proveedor.Cuit, igual que Usuario
/// normaliza el correo: la regla está en la entidad para que ningún camino la
/// pueda saltear. Antes el CUIT se guardaba tal como llegaba, el índice único
/// (ClienteId, Cuit) comparaba ese texto, y el mismo proveedor podía quedar
/// cargado dos veces en el mismo cliente (regla 8), con dos acumulados mensuales
/// separados: el monto no sujeto se descontaba dos veces y se retenía de menos.
///
/// Normalizar no es validar: un texto con 10 dígitos queda con 10 dígitos. Que
/// sean exactamente 11 y que el verificador coincida lo controla ValidadorCuit
/// (Application) al guardar, en RetencionesRG830DbContext. Acá no se puede: el
/// Domain no depende de nada, y un setter que lanza una excepción no es un error
/// de validación sino un formulario que no se puede leer.
/// </summary>
public static class NormalizadorCuit
{
    /// <summary>
    /// Deja sólo los dígitos del 0 al 9. No usa char.IsDigit, que también acepta
    /// dígitos de otros alfabetos (por ejemplo, los arábigos orientales).
    /// </summary>
    public static string Normalizar(string? cuit) =>
        new((cuit ?? string.Empty).Where(c => c is >= '0' and <= '9').ToArray());
}
