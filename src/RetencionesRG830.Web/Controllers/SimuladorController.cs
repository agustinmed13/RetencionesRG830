using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;
using RetencionesRG830.Web.Models;

namespace RetencionesRG830.Web.Controllers;

/// <summary>
/// El Simulador es una calculadora: no guarda nada. Igual tiene que usar los
/// valores que rigen en la fecha (regla 9): con la versión equivocada da un
/// número falso igual que el registro, sólo que sin dejar rastro.
///
/// HU-15: también lo usa el rol Cliente, sin filtro por empresa, porque no lee datos
/// de ninguna: sólo régimen, importes y condición del proveedor, que se escriben en el
/// formulario. No mira el acumulado del mes ni operaciones registradas.
/// </summary>
[Authorize(Roles = "Estudio,Cliente")]
public class SimuladorController : Controller
{
    private readonly RetencionesRG830DbContext _db;
    private readonly ServicioRegistroOperaciones _registro;

    public SimuladorController(RetencionesRG830DbContext db, ServicioRegistroOperaciones registro)
    {
        _db = db;
        _registro = registro;
    }

    public async Task<IActionResult> Index()
    {
        await CargarRegimenes(null);
        return View(new SimuladorViewModel { FechaRetencion = DateOnly.FromDateTime(DateTime.Today) });
    }

    [HttpPost]
    public async Task<IActionResult> Index(SimuladorViewModel modelo)
    {
        if (modelo.CodigoRegimen <= 0)
            ModelState.AddModelError(nameof(modelo.CodigoRegimen), "Tenés que elegir un régimen.");

        // Con el campo vacío, ASP.NET ya dejó su propio error en inglés ("The value
        // '' is invalid."), que se mostraría primero: se reemplaza por uno en español.
        if (modelo.FechaRetencion == default)
        {
            ModelState.Remove(nameof(modelo.FechaRetencion));
            ModelState.AddModelError(nameof(modelo.FechaRetencion), "Indicá la fecha de retención: define con qué valores se calcula.");
        }

        if (ModelState.IsValid)
        {
            // La versión del régimen vigente a la fecha, con sus tramos de escala.
            // Es el mismo método que usa Nueva operación: el Simulador no puede
            // resolver la versión de otra manera que el registro.
            var regimen = await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion);

            if (regimen is null)
            {
                ModelState.AddModelError(nameof(modelo.CodigoRegimen),
                    $"El régimen {modelo.CodigoRegimen} no tiene valores vigentes al {modelo.FechaRetencion:dd/MM/yyyy}.");
            }
            else
            {
                // Los importes son decimal?, pero acá no pueden ser null: ModelState es
                // válido, así que [Required] ya los controló. .Value y no "?? 0": si algún
                // día llegara un null, tiene que fallar, no calcular con un cero inventado.
                modelo.Resultado = ServicioCalculoRetencion.Calcular(
                    regimen,
                    modelo.InscriptoEnGanancias,
                    modelo.TipoPersona,
                    modelo.NetoGravadoAcumuladoMensual!.Value,
                    modelo.RetencionesAcumuladasDelMes!.Value);
                modelo.RegimenVigenciaDesde = regimen.VigenciaDesde;
            }
        }

        await CargarRegimenes(modelo.CodigoRegimen);
        return View(modelo);
    }

    /// <summary>
    /// Una opción por CÓDIGO, no por versión: el valor de cada opción es el código.
    /// La descripción es la de la versión vigente hoy o, si no rige ninguna, la de
    /// la más reciente. Mismo criterio que el desplegable de Nueva operación.
    /// </summary>
    private async Task CargarRegimenes(int? seleccionado)
    {
        var regimenes = await _db.Regimenes.ToListAsync();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var opciones = regimenes
            .GroupBy(r => r.Codigo)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var paraMostrar = VigenciaRegimen.VersionVigente(g, hoy) ?? g.MaxBy(r => r.VigenciaDesde)!;
                return new { Codigo = g.Key, Texto = $"{g.Key} - {paraMostrar.Descripcion}" };
            });

        ViewBag.Regimenes = new SelectList(opciones, "Codigo", "Texto", seleccionado);
    }
}
