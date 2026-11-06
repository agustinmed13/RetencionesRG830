namespace RetencionesRG830.Infrastructure.Servicios;

/// <summary>
/// El archivo de SICORE no se pudo generar porque algún valor no entra en su campo.
/// Es un tipo propio para que la pantalla lo distinga de un error inesperado: éste se
/// le muestra al operador, porque dice qué operación corregir. Trae la lista entera,
/// para no tener que ir descubriendo los problemas de a uno.
/// </summary>
public class ErrorArchivoSicore : InvalidOperationException
{
    public IReadOnlyList<string> Problemas { get; }

    public ErrorArchivoSicore(IReadOnlyList<string> problemas)
        : base(string.Join(Environment.NewLine, problemas))
    {
        Problemas = problemas;
    }
}
