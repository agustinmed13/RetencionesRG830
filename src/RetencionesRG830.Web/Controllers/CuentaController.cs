using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Web.Controllers;

public class CuentaController : Controller
{
    private readonly RetencionesRG830DbContext _context;

    public CuentaController(RetencionesRG830DbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Email == email);

        if (usuario == null || !PasswordHasher.Verificar(password, usuario.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Email o contraseña incorrectos.");
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

        return RedirectToAction("Index", "Regimenes");
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