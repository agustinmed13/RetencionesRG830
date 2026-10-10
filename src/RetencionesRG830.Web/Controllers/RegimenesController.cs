using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authorization;
using RetencionesRG830.Web.Models;

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
    // Agrupa las versiones por código y separa la que rige hoy de las demás.
    // Cuál rige no lo decide acá: lo decide VigenciaRegimen.
    public async Task<IActionResult> Index()
    {
        var regimenes = await _context.Regimenes
            .Include(r => r.Tramos)   // trae también los tramos de escala de cada régimen.
            .OrderBy(r => r.Codigo)
            .ToListAsync();

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var modelo = new RegimenesViewModel { Fecha = hoy };

        foreach (var versiones in regimenes.GroupBy(r => r.Codigo))
        {
            var vigente = VigenciaRegimen.VersionVigente(versiones, hoy);
            var masReciente = versiones.MaxBy(r => r.VigenciaDesde)!;

            modelo.Vigentes.Add(new RegimenVigente
            {
                Codigo = versiones.Key,
                Version = vigente,
                Descripcion = (vigente ?? masReciente).Descripcion
            });

            modelo.OtrasVersiones.AddRange(versiones
                .Where(r => r != vigente)
                .OrderByDescending(r => r.VigenciaDesde)
                .Select(r => new OtraVersion
                {
                    Version = r,
                    Estado = VigenciaRegimen.Estado(r, versiones, hoy)
                }));
        }

        return View(modelo);
    }
}
