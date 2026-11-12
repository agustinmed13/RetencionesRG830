using System.Security.Claims;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Traduce el usuario autenticado a su AlcanceEmpresa (HU-15): qué empresas puede ver.
/// Es lo único que el controlador decide sobre la empresa: después se la pasa al
/// servicio, que es el que filtra.
///
/// Sale de la cookie que arma CuentaController al iniciar sesión: el rol, y para el rol
/// Cliente, el claim "ClienteId". Ante cualquier dato que no cierre (un Cliente sin
/// empresa, un rol desconocido) devuelve Ninguna: si hay dudas, no ve nada.
///
/// No tiene test automático: el proyecto de tests no referencia la Web. Por eso es
/// corto, y lo que importa (qué hace el servicio con cada alcance) está probado en
/// AislamientoEmpresasTests.
/// </summary>
public static class AlcanceDelUsuario
{
    public static AlcanceEmpresa Alcance(this ClaimsPrincipal usuario)
    {
        if (usuario.IsInRole("Estudio"))
            return AlcanceEmpresa.Estudio;

        if (usuario.IsInRole("Cliente")
            && int.TryParse(usuario.FindFirst("ClienteId")?.Value, out var clienteId))
            return AlcanceEmpresa.DelCliente(clienteId);

        return AlcanceEmpresa.Ninguna;
    }
}
