using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class RegimenesController : Controller
{
    private readonly RetencionesRG830DbContext _context;

    // El Controller no crea el DbContext por su cuenta: se lo "inyecta" el
    // propio ASP.NET Core a través del constructor. Es el mismo mecanismo
    // que usamos en Program.cs con AddDbContext -por eso funciona sin que
    // tengamos que instanciarlo nosotros con "new".
    public RegimenesController(RetencionesRG830DbContext context)
    {
        _context = context;
    }

    // Este método responde cuando alguien entra a la URL /Regimenes.
    public async Task<IActionResult> Index()
    {
        var regimenes = await _context.Regimenes
            .Include(r => r.Tramos)   // trae también los tramos de escala de cada régimen.
            .OrderBy(r => r.Codigo)
            .ToListAsync();

        return View(regimenes);
    }
}