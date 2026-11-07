namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// Se pidió anular una operación que ya estaba anulada. No es un error del sistema:
/// pasa con dos pestañas abiertas o con dos personas a la vez. Es un tipo propio para
/// que la pantalla lo distinga de un error inesperado y le explique al operador qué
/// pasó, en lugar de mostrarle la página genérica.
/// </summary>
public class OperacionYaAnuladaException : InvalidOperationException
{
    public OperacionYaAnuladaException()
        : base("La operación ya estaba anulada.")
    {
    }
}
