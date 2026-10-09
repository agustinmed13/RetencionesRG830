using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Web.Models;
using RetencionesRG830.Infrastructure.Servicios;
using RetencionesRG830.Web.Infraestructura;

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

        var modelo = new OperacionViewModel
        {
            ClienteId = cliente.Id,
            ClienteRazonSocial = cliente.RazonSocial,
            FechaComprobante = hoy,
            FechaRetencion = hoy
        };
        await PrepararFormularioAsync(modelo);

        return View(modelo);
    }

    /// <summary>
    /// Cálculo en vivo para el panel lateral de "Nueva operación". El JavaScript de la
    /// pantalla manda el formulario cada vez que cambia un dato, y esta acción
    /// devuelve el panel ya armado (HTML), listo para insertar.
    ///
    /// La cuenta la hace SimularAsync, el mismo motor que usan la confirmación y el
    /// registro, cubierto por los tests. El navegador no calcula nada: sólo pide el
    /// panel y lo muestra. Así hay un solo motor y no pueden desincronizarse.
    ///
    /// No guarda nada. Es POST y no GET porque manda el formulario completo, y por
    /// eso también exige el token antifalsificación, como cualquier POST.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Simular(OperacionViewModel modelo)
    {
        // La ayuda del régimen y el aviso de comprobante repetido se actualizan con
        // el panel: viajan dentro de la misma respuesta.
        await CompletarAvisosAsync(modelo);

        // Mientras falten los datos que necesita el cálculo, el panel pide que se
        // completen. Los errores de formato de otros campos (un número de
        // comprobante a medio escribir) no importan acá: los valida Revisar.
        var faltanDatos = modelo.ProveedorId <= 0
            || modelo.RegimenId <= 0
            || modelo.ImporteGravado <= 0
            || modelo.FechaRetencion == default;

        // El proveedor tiene que ser de este cliente: si no, el acumulado del mes
        // se buscaría sobre operaciones de otro agente de retención.
        if (!faltanDatos)
        {
            var proveedorDelCliente = await _registro.ProveedorPerteneceAlClienteAsync(modelo.ProveedorId, modelo.ClienteId);
            var regimenExiste = await _db.Regimenes.AnyAsync(r => r.Id == modelo.RegimenId);
            faltanDatos = !proveedorDelCliente || !regimenExiste;
        }

        if (!faltanDatos)
        {
            modelo.Resultado = await _registro.SimularAsync(ArmarOperacion(modelo));
        }

        return PartialView("_PanelCalculo", modelo);
    }

    /// <summary>
    /// Primer paso: valida los datos y calcula la retención SIN guardar nada.
    /// Lleva a la pantalla de confirmación.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Revisar(OperacionViewModel modelo)
    {
        if (!await ValidarAsync(modelo))
        {
            await PrepararFormularioAsync(modelo);
            return View("Create", modelo);
        }

        var operacion = ArmarOperacion(modelo);

        modelo.Resultado = await _registro.SimularAsync(operacion);

        var proveedor = (await _db.Proveedores.FindAsync(modelo.ProveedorId))!;
        var regimen = (await _db.Regimenes.FindAsync(modelo.RegimenId))!;
        modelo.ProveedorNombre = proveedor.RazonSocial;
        modelo.ProveedorCuit = proveedor.Cuit;
        modelo.ProveedorCondicion = FormatoProveedor.Condicion(proveedor);
        modelo.RegimenNombre = regimen.Descripcion;
        modelo.RegimenCodigo = regimen.Codigo;
        modelo.ProximoNumeroCertificado = await _registro.SiguienteNumeroCertificadoAsync(modelo.ClienteId);
        await CompletarAvisosAsync(modelo);

        return View("Confirmar", modelo);
    }

    /// <summary>
    /// Vuelve al formulario conservando todo lo que ya se había cargado,
    /// para corregir un dato sin tener que tipear de nuevo.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Corregir(OperacionViewModel modelo)
    {
        await PrepararFormularioAsync(modelo);
        return View("Create", modelo);
    }

    /// <summary>
    /// Segundo paso: ahora sí guarda la operación y emite el certificado.
    /// Esta es la única acción que escribe en la base.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Confirmar(OperacionViewModel modelo)
    {
        if (!await ValidarAsync(modelo))
        {
            await PrepararFormularioAsync(modelo);
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
    private async Task<bool> ValidarAsync(OperacionViewModel modelo)
    {
        if (modelo.ProveedorId <= 0)
            ModelState.AddModelError(nameof(modelo.ProveedorId), "Tenés que elegir un proveedor.");
        // Mismo control que el cálculo en vivo. El formulario sólo ofrece proveedores
        // del cliente, pero los datos se pueden alterar antes de enviarlos; y una
        // operación con el proveedor de otro cliente ensuciaría el acumulado de las
        // dos empresas. El servicio lo vuelve a controlar al simular y al registrar.
        else if (!await _registro.ProveedorPerteneceAlClienteAsync(modelo.ProveedorId, modelo.ClienteId))
            ModelState.AddModelError(nameof(modelo.ProveedorId), "El proveedor elegido no pertenece a este cliente.");

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

    /// <summary>
    /// Todo lo que necesita la vista del formulario además del modelo: las listas
    /// de los desplegables y los avisos que dependen de lo ya elegido.
    /// </summary>
    private async Task PrepararFormularioAsync(OperacionViewModel modelo)
    {
        await CargarListas(modelo.ClienteId);
        await CompletarAvisosAsync(modelo);
    }

    /// <summary>
    /// La ayuda del régimen (qué alícuota corresponde y por qué) y las operaciones
    /// que ya tienen el mismo comprobante. Lo usan el formulario, el cálculo en vivo
    /// y la confirmación, para que digan siempre lo mismo.
    /// </summary>
    private async Task CompletarAvisosAsync(OperacionViewModel modelo)
    {
        // Sólo se consideran proveedores de este cliente: con uno ajeno, la ayuda
        // se muestra como si no hubiera proveedor elegido.
        var proveedor = modelo.ProveedorId > 0
            ? await _db.Proveedores.FirstOrDefaultAsync(p => p.Id == modelo.ProveedorId && p.ClienteId == modelo.ClienteId)
            : null;
        var regimen = modelo.RegimenId > 0 ? await _db.Regimenes.FindAsync(modelo.RegimenId) : null;

        modelo.AyudaRegimen = regimen is null ? string.Empty : FormatoRegimen.Ayuda(regimen, proveedor);

        modelo.ComprobantesRepetidos = proveedor is not null && modelo.PuntoVenta > 0 && modelo.NumeroComprobante > 0
            ? await _registro.OperacionesConMismoComprobanteAsync(ArmarOperacion(modelo))
            : new List<Operacion>();
    }

    private async Task CargarListas(int clienteId)
    {
        // La lista de proveedores lleva, además del texto de cada opción, una línea de
        // ayuda que la pantalla muestra debajo al elegir: CUIT y condición. La ayuda
        // del régimen no va acá porque depende del proveedor elegido: la arma
        // CompletarAvisosAsync y llega con el panel de cálculo.
        var proveedores = await _db.Proveedores
            .Where(p => p.ClienteId == clienteId && p.Activo)
            .OrderBy(p => p.RazonSocial)
            .ToListAsync();

        ViewBag.Proveedores = proveedores
            .Select(p => new OpcionConAyuda(p.Id, p.RazonSocial,
                $"{p.Cuit} · {FormatoProveedor.Condicion(p)}"))
            .ToList();

        var regimenes = await _db.Regimenes.OrderBy(r => r.Codigo).ToListAsync();

        ViewBag.Regimenes = regimenes
            .Select(r => new OpcionConAyuda(r.Id, $"{r.Codigo} — {r.Descripcion}", string.Empty))
            .ToList();

        var cliente = await _db.Clientes.FindAsync(clienteId);
        ViewBag.ClienteCuit = cliente?.Cuit;
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