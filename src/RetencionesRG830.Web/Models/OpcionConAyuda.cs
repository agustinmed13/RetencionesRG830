namespace RetencionesRG830.Web.Models;

/// <summary>
/// Una opción de un desplegable con una línea de ayuda que la pantalla muestra
/// debajo cuando se la elige (por ejemplo, el CUIT y la condición del proveedor).
/// </summary>
public record OpcionConAyuda(int Id, string Texto, string Ayuda);
