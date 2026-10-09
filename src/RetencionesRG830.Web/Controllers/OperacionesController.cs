using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Web.Models;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class OperacionesController : Controller
{
    private readonly RetencionesRG830DbContext _db;
    private readonly ServicioRegistroOperaciones _registro;
    private readonly GeneradorCertificadoPdf _pdf;

    public OperacionesController(
        RetencionesRG830DbContext db,
        ServicioRegistroOperaciones registro,
        GeneradorCertificadoPdf pdf)
    {
        _db = db;
        _registro = registro;
        _pdf = pdf;
    }
    public async Task<IActionResult> Index(int? clienteId)
    {
        var query = _db.Operaciones
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .AsQueryable();

        if (clienteId is not null)
            query = query.Where(o => o.ClienteId == clienteId);

        ViewBag.ClienteId = clienteId;

        // El nombre del cliente filtrado se busca aparte y no se toma de la primera
        // operación: un cliente sin operaciones todavía también tiene que mostrarlo.
        if (clienteId is not null)
            ViewBag.ClienteRazonSocial = (await _db.Clientes.FindAsync(clienteId))?.RazonSocial;

        var operaciones = await query
            .OrderByDescending(o => o.FechaRetencion)
            .ThenByDescending(o => o.NumeroCertificado)
            .ToListAsync();

        return View(operaciones);
    }

    public async Task<IActionResult> Create(int clienteId)
    {
        var cliente = await _db.Clientes.FindAsync(clienteId);
        if (cliente is null) return NotFound();

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        await CargarListas(clienteId);

        return View(new OperacionViewModel
        {
            ClienteId = cliente.Id,
            ClienteRazonSocial = cliente.RazonSocial,
            FechaComprobante = hoy,
            FechaRetencion = hoy
        });
    }

    [HttpPost]
      /// <summary>
    /// Primer paso: valida los datos y calcula la retención SIN guardar nada.
    /// Lleva a la pantalla de confirmación.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Revisar(OperacionViewModel modelo)
    {
        if (!Validar(modelo))
        {
            await CargarListas(modelo.ClienteId);
            return View("Create", modelo);
        }

        var operacion = ArmarOperacion(modelo);

        modelo.Resultado = await _registro.SimularAsync(operacion);
        modelo.ProveedorNombre = (await _db.Proveedores.FindAsync(modelo.ProveedorId))!.RazonSocial;
        modelo.RegimenNombre = (await _db.Regimenes.FindAsync(modelo.RegimenId))!.Descripcion;

        return View("Confirmar", modelo);
    }

    /// <summary>
    /// Vuelve al formulario conservando todo lo que ya se había cargado,
    /// para corregir un dato sin tener que tipear de nuevo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Corregir(OperacionViewModel modelo)
    {
        await CargarListas(modelo.ClienteId);
        return View("Create", modelo);
    }

    /// <summary>
    /// Segundo paso: ahora sí guarda la operación y emite el certificado.
    /// Esta es la única acción que escribe en la base.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Confirmar(OperacionViewModel modelo)
    {
        if (!Validar(modelo))
        {
            await CargarListas(modelo.ClienteId);
            return View("Create", modelo);
        }

        var operacion = ArmarOperacion(modelo);
        await _registro.RegistrarAsync(operacion);

        return RedirectToAction(nameof(Detalle), new { id = operacion.Id });
    }
        /// <summary>Muestra el formulario para anular una operación.</summary>
    [HttpGet]
    public async Task<IActionResult> Anular(int id)
    {
        var operacion = await _db.Operaciones
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (operacion is null) return NotFound();

        if (operacion.Anulada)
        {
            TempData["Mensaje"] = "Esa operación ya estaba anulada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View(operacion);
    }

    /// <summary>
    /// Anula la operación. El motivo es obligatorio: una anulación sin
    /// explicación no sirve como respaldo frente a una inspección.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Anular(int id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 5)
        {
            TempData["Mensaje"] = "Tenés que explicar el motivo de la anulación.";
            return RedirectToAction(nameof(Anular), new { id });
        }

        var usuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        await _registro.AnularAsync(id, motivo.Trim(), usuarioId);

        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Validaciones propias, además de las del formulario.</summary>
    private bool Validar(OperacionViewModel modelo)
    {
        if (modelo.ProveedorId <= 0)
            ModelState.AddModelError(nameof(modelo.ProveedorId), "Tenés que elegir un proveedor.");

        if (modelo.RegimenId <= 0)
            ModelState.AddModelError(nameof(modelo.RegimenId), "Tenés que elegir un régimen.");

        if (modelo.ImporteGravado > modelo.ImporteComprobante)
            ModelState.AddModelError(nameof(modelo.ImporteGravado),
                "El importe gravado no puede ser mayor al total del comprobante.");

        return ModelState.IsValid;
    }

    /// <summary>Arma la operación a partir del formulario, sin guardarla.</summary>
    private Operacion ArmarOperacion(OperacionViewModel modelo) => new Operacion
    {
        ClienteId = modelo.ClienteId,
        ProveedorId = modelo.ProveedorId,
        RegimenId = modelo.RegimenId,
        TipoComprobante = modelo.TipoComprobante,
        PuntoVenta = modelo.PuntoVenta,
        NumeroComprobante = modelo.NumeroComprobante,
        FechaComprobante = modelo.FechaComprobante,
        FechaRetencion = modelo.FechaRetencion,
        ImporteComprobante = modelo.ImporteComprobante,
        ImporteGravado = modelo.ImporteGravado,
        CreadoPorUsuarioId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value)
    };

    public async Task<IActionResult> Detalle(int id)
    {
        var operacion = await _db.Operaciones
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (operacion is null) return NotFound();

        return View(operacion);
    }

    /// <summary>
    /// Genera el certificado en PDF y lo devuelve como archivo descargable.
    /// El Include es imprescindible: el generador necesita los datos del
    /// cliente, el proveedor y el régimen, no sólo sus identificadores.
    /// </summary>
    public async Task<IActionResult> Certificado(int id)
    {
        var operacion = await _db.Operaciones
            .Include(o => o.Cliente)
            .Include(o => o.Proveedor)
            .Include(o => o.Regimen)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (operacion is null) return NotFound();
        if (operacion.Anulada)
        {
            TempData["Mensaje"] = "No se puede emitir el certificado de una operación anulada.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var pdf = _pdf.Generar(operacion);

        // El nombre incluye proveedor y período para que el operador pueda
        // encontrarlo rápido en la carpeta de descargas al adjuntarlo al mail.
        var proveedor = LimpiarParaNombreDeArchivo(operacion.Proveedor!.RazonSocial);
        var nombre = $"Certificado-{operacion.NumeroCertificado:D8}-{proveedor}-{operacion.FechaRetencion:yyyy-MM}.pdf";

        return File(pdf, "application/pdf", nombre);
    }

    private async Task CargarListas(int clienteId)
    {
        var proveedores = await _db.Proveedores
            .Where(p => p.ClienteId == clienteId && p.Activo)
            .OrderBy(p => p.RazonSocial)
            .ToListAsync();

        ViewBag.Proveedores = new SelectList(
            proveedores.Select(p => new { p.Id, Texto = $"{p.RazonSocial} ({p.Cuit})" }),
            "Id", "Texto");

        var regimenes = await _db.Regimenes.OrderBy(r => r.Codigo).ToListAsync();

        ViewBag.Regimenes = new SelectList(
            regimenes.Select(r => new { r.Id, Texto = $"{r.Codigo} - {r.Descripcion}" }),
            "Id", "Texto");
    }
        /// <summary>
    /// Deja la razón social utilizable como nombre de archivo: saca los
    /// caracteres que Windows no permite, las comas, y cambia los espacios
    /// por guiones.
    /// </summary>
    private static string LimpiarParaNombreDeArchivo(string texto)
    {
        var invalidos = System.IO.Path.GetInvalidFileNameChars();

        var limpio = new string(texto.Where(c => !invalidos.Contains(c)).ToArray())
            .Replace(",", "")
            .Trim();

        while (limpio.Contains("  "))
        {
            limpio = limpio.Replace("  ", " ");
        }

        return limpio.Replace(' ', '-');
    }
}