using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class SicoreController : Controller
{
    private readonly RetencionesRG830DbContext _db;
    private readonly GeneradorArchivoSicore _generador;
    private readonly ServicioRegistroOperaciones _registro;

    public SicoreController(RetencionesRG830DbContext db, GeneradorArchivoSicore generador,
        ServicioRegistroOperaciones registro)
    {
        _db = db;
        _generador = generador;
        _registro = registro;
    }

    public async Task<IActionResult> Index()
    {
        await CargarClientes();

        // Por defecto propone el mes en curso, que es lo más habitual.
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        ViewBag.Desde = new DateOnly(hoy.Year, hoy.Month, 1).ToString("yyyy-MM-dd");
        ViewBag.Hasta = hoy.ToString("yyyy-MM-dd");

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Descargar(int clienteId, DateOnly desde, DateOnly hasta)
    {
        var cliente = await _db.Clientes.FindAsync(clienteId);
        if (cliente is null) return NotFound();

        // Qué se informa (vigentes, con certificado, por fecha de retención) lo decide
        // el servicio, que tiene test: ServicioRegistroOperacionesTests.
        var operaciones = await _registro.OperacionesParaSicoreAsync(clienteId, desde, hasta);

        if (operaciones.Count == 0)
        {
            TempData["Mensaje"] = "No hay retenciones en ese rango de fechas para ese cliente.";
            return RedirectToAction(nameof(Index));
        }

        var contenido = _generador.Generar(operaciones);

        // Sin BOM: el "byte order mark" son unos caracteres invisibles que
        // algunos editores ponen al principio del archivo, y que pueden
        // hacer que AFIP rechace la importación.
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(contenido);

        // El CUIT ya se guarda sólo con sus dígitos (NormalizadorCuit).
        var nombre = $"SICORE-{cliente.Cuit}-{desde:yyyyMMdd}-{hasta:yyyyMMdd}.txt";

        return File(bytes, "text/plain", nombre);
    }

    private async Task CargarClientes()
    {
        var clientes = await _db.Clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .ToListAsync();

        ViewBag.Clientes = new SelectList(clientes, "Id", "RazonSocial");
    }
}