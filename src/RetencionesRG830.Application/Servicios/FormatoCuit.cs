namespace RetencionesRG830.Application.Servicios;

/// <summary>
/// Cómo se MUESTRA un CUIT: "30700000008" → "30-70000000-8". Es sólo
/// presentación: el CUIT se guarda y se compara con sus 11 dígitos, sin guiones
/// (NormalizadorCuit, en la entidad). Lo usan las pantallas y el certificado PDF;
/// el archivo de SICORE no, porque lleva los dígitos solos.
///
/// Está en Application y no en la Web porque el certificado lo arma
/// Infrastructure, que no ve la Web.
/// </summary>
public static class FormatoCuit
{
    /// <summary>
    /// Con guiones si son exactamente 11 dígitos. Cualquier otra cosa se devuelve
    /// tal cual: mostrar mal un dato raro es mejor que una pantalla que no abre, y
    /// así el dato raro se ve como es.
    /// </summary>
    public static string ParaMostrar(string? cuit)
    {
        if (cuit is not { Length: 11 } || !cuit.All(c => c is >= '0' and <= '9'))
            return cuit ?? string.Empty;

        return $"{cuit[..2]}-{cuit[2..10]}-{cuit[10]}";
    }
}
