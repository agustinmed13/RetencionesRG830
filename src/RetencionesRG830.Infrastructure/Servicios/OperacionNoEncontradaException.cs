namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// La operación pedida no existe, o es de una empresa que el usuario no puede ver: para
/// él es lo mismo (HU-15). Un tipo propio para que la pantalla responda "no encontrado"
/// (404) en lugar de la página de error.
///
/// Hereda de InvalidOperationException, como OperacionYaAnuladaException, pero es un
/// tipo distinto a propósito: "no existe" nunca se le puede mostrar como "ya estaba
/// anulada", que confirmaría que existe.
/// </summary>
public class OperacionNoEncontradaException : InvalidOperationException
{
    public OperacionNoEncontradaException()
        : base("La operación indicada no existe.")
    {
    }
}
