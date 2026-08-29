using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Web.Models;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class SimuladorController : Controller
{
    private readonly RetencionesRG830DbContext _db;

    public SimuladorController(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        await CargarRegimenes(null);
        return View(new SimuladorViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Index(SimuladorViewModel modelo)
    {
        if (modelo.RegimenId <= 0)
        {
            ModelState.AddModelError(nameof(modelo.RegimenId), "Tenés que elegir un régimen.");
        }

        if (ModelState.IsValid)
        {
            // Include(r => r.Tramos) es imprescindible: sin eso, EF Core trae
            // el Régimen pero con la lista de tramos vacía, y el cálculo por
            // escala fallaría. Es un error clásico al usar un ORM.
            var regimen = await _db.Regimenes
                .Include(r => r.Tramos)
                .FirstOrDefaultAsync(r => r.Id == modelo.RegimenId);

            if (regimen is null) return NotFound();

            modelo.Resultado = ServicioCalculoRetencion.Calcular(
                regimen,
                modelo.InscriptoEnGanancias,
                modelo.TipoPersona,
                modelo.NetoGravadoAcumuladoMensual,
                modelo.RetencionesAcumuladasDelMes);
        }

        await CargarRegimenes(modelo.RegimenId);
        return View(modelo);
    }

    private async Task CargarRegimenes(int? seleccionado)
    {
        var regimenes = await _db.Regimenes.OrderBy(r => r.Codigo).ToListAsync();

        ViewBag.Regimenes = new SelectList(
            regimenes.Select(r => new { r.Id, Texto = $"{r.Codigo} - {r.Descripcion}" }),
            "Id", "Texto", seleccionado);
    }
}