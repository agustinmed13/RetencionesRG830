using Microsoft.AspNetCore.Mvc.ModelBinding;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Qué muestra el campo CUIT de los formularios de Clientes y Proveedores.
///
/// asp-for pondría el CUIT tal como está guardado, sin guiones ("30700000008"),
/// así que la vista le pasa este valor explícito (value="..."), que asp-for
/// respeta. Hay dos casos:
///
/// - El formulario se abre (alta vacía o edición): el CUIT con guiones.
/// - El formulario vuelve con un error: lo que escribió el operador, tal cual,
///   que es lo que asp-for hacía por su cuenta. Si se mostrara el CUIT del modelo,
///   el operador vería su texto ya normalizado: "30-7000000-8", con un dígito
///   menos, volvería como "3070000008", sin los guiones que ayudan a encontrar
///   el error.
/// </summary>
public static class CampoCuit
{
    public static string Valor(ModelStateDictionary estado, string? cuit) =>
        estado.TryGetValue("Cuit", out var entrada) && entrada.AttemptedValue is not null
            ? entrada.AttemptedValue
            : FormatoCuit.ParaMostrar(cuit);
}
