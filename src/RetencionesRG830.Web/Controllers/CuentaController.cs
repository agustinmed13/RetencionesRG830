using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RetencionesRG830.Infrastructure.Servicios;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Web.Controllers;

public class CuentaController : Controller
{
    private readonly ServicioUsuarios _usuarios;

    public CuentaController(ServicioUsuarios usuarios)
    {
        _usuarios = usuarios;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string? email, string? password, string? returnUrl)
    {
        // Los dos parámetros son string? porque eso es lo que llega de verdad: ASP.NET
        // convierte un campo vacío en null. Antes eran string, y la contraseña vacía
        // llegaba igual como null a PasswordHasher.Verificar, que la rechazaba con una
        // excepción: error 500.
        //
        // Si falta alguno, se responde sin consultar la base. Así el mensaje no puede
        // revelar si el correo existe: es el mismo exista o no, porque ni siquiera se
        // busca. Por la misma razón no dice cuál de los dos falta.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError(string.Empty, "Ingresá tu correo y tu contraseña.");
            return View();
        }

        // Sin distinguir mayúsculas ni espacios, en cualquier motor: el correo se
        // normaliza al guardarlo y al buscarlo (ver ServicioUsuariosTests).
        var usuario = await _usuarios.BuscarPorEmailAsync(email);

        if (usuario == null || !PasswordHasher.Verificar(password, usuario.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Correo o contraseña incorrectos.");
            return View();
        }

        // Los "claims" son los datos sobre el usuario que van a quedar
        // disponibles, adentro de la cookie, en cada pedido futuro.
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Email),
            new Claim(ClaimTypes.Role, usuario.Rol.ToString())
        };
        
        if (usuario.ClienteId.HasValue)
        {
            claims.Add(new Claim("ClienteId", usuario.ClienteId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        // ReturnUrl es la página que el usuario intentó abrir sin sesión: el middleware
        // de autenticación lo agrega al mandarlo al login, y el formulario lo conserva
        // porque se envía a la misma URL. Sólo se usa si es una dirección de este mismo
        // sitio. Sin esa comprobación habría una redirección abierta: un enlace a
        // /Cuenta/Login?ReturnUrl=https://sitio-falso.com dejaría a quien ingresa con sus
        // credenciales reales en una página del atacante. IsLocalUrl también rechaza
        // las formas disfrazadas, como "//sitio-falso.com" o "/\sitio-falso.com".
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        // Destino por defecto: Inicio. Es la única página, además del login, que no exige
        // rol, así que sirve para Estudio y para Cliente. Antes iba a Regímenes, que exige
        // rol Estudio, y un usuario Cliente caía en "Acceso denegado" al entrar.
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccesoDenegado()
    {
        return View();
    }
}