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
            || modelo.CodigoRegimen <= 0
            || modelo.ImporteGravado <= 0
            || modelo.FechaRetencion == default;

        // El proveedor tiene que ser de este cliente: si no, el acumulado del mes
        // se buscaría sobre operaciones de otro agente de retención. Y el régimen
        // tiene que tener una versión vigente a la fecha de retención: si no, la
        // ayuda debajo del régimen lo explica (CompletarAvisosAsync).
        Regimen? version = null;
        if (!faltanDatos)
        {
            var proveedorDelCliente = await _registro.ProveedorPerteneceAlClienteAsync(modelo.ProveedorId, modelo.ClienteId);
            version = await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion);
            faltanDatos = !proveedorDelCliente || version is null;
        }

        if (!faltanDatos)
        {
            modelo.Resultado = await _registro.SimularAsync(ArmarOperacion(modelo, version!.Id));
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

        // ValidarAsync ya comprobó que hay una versión vigente a la fecha de retención.
        var regimen = (await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion))!;
        var operacion = ArmarOperacion(modelo, regimen.Id);

        modelo.Resultado = await _registro.SimularAsync(operacion);

        var proveedor = (await _db.Proveedores.FindAsync(modelo.ProveedorId))!;
        modelo.ProveedorNombre = proveedor.RazonSocial;
        modelo.ProveedorCuit = proveedor.Cuit;
        modelo.ProveedorCondicion = FormatoProveedor.Condicion(proveedor);
        modelo.RegimenNombre = regimen.Descripcion;
        modelo.RegimenCodigo = regimen.Codigo;
        modelo.RegimenVigenciaDesde = regimen.VigenciaDesde;
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

        // Se vuelve a resolver acá, con los datos que llegan al confirmar: no se
        // confía en una versión que haya viajado desde la pantalla de revisión.
        var regimen = (await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion))!;
        var operacion = ArmarOperacion(modelo, regimen.Id);
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
        // Con una fecha vacía, ASP.NET ya dejó su propio error en inglés ("The value
        // '' is invalid."), que se mostraría primero: se reemplaza por uno en español.
        // Mismo arreglo que en el Simulador.
        if (modelo.FechaComprobante == default)
            ReemplazarError(nameof(modelo.FechaComprobante), "Indicá la fecha del comprobante.");
        if (modelo.FechaRetencion == default)
            ReemplazarError(nameof(modelo.FechaRetencion),
                "Indicá la fecha de la retención: define el mes del acumulado y con qué valores se calcula.");

        if (modelo.ProveedorId <= 0)
            ModelState.AddModelError(nameof(modelo.ProveedorId), "Tenés que elegir un proveedor.");
        // Mismo control que el cálculo en vivo. El formulario sólo ofrece proveedores
        // del cliente, pero los datos se pueden alterar antes de enviarlos; y una
        // operación con el proveedor de otro cliente ensuciaría el acumulado de las
        // dos empresas. El servicio lo vuelve a controlar al simular y al registrar.
        else if (!await _registro.ProveedorPerteneceAlClienteAsync(modelo.ProveedorId, modelo.ClienteId))
            ModelState.AddModelError(nameof(modelo.ProveedorId), "El proveedor elegido no pertenece a este cliente.");

        if (modelo.CodigoRegimen <= 0)
            ModelState.AddModelError(nameof(modelo.CodigoRegimen), "Tenés que elegir un régimen.");
        // Sin una versión vigente a la fecha de retención no hay valores con qué
        // calcular (regla 9). Con la fecha vacía el error ya lo da ese campo.
        else if (modelo.FechaRetencion != default
                 && await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion) is null)
            ModelState.AddModelError(nameof(modelo.CodigoRegimen), SinVersionVigente(modelo));

        if (modelo.ImporteGravado > modelo.ImporteComprobante)
            ModelState.AddModelError(nameof(modelo.ImporteGravado),
                "El importe gravado no puede ser mayor al total del comprobante.");

        return ModelState.IsValid;
    }

    /// <summary>Saca los errores que ya tenga un campo y deja sólo el mensaje dado.</summary>
    private void ReemplazarError(string campo, string mensaje)
    {
        ModelState.Remove(campo);
        ModelState.AddModelError(campo, mensaje);
    }

    private static string SinVersionVigente(OperacionViewModel modelo) =>
        $"El régimen {modelo.CodigoRegimen} no tiene valores vigentes al {modelo.FechaRetencion:dd/MM/yyyy}.";

    /// <summary>
    /// Arma la operación a partir del formulario, sin guardarla. El regimenId es el
    /// de la versión ya resuelta para la fecha de retención: el formulario sólo trae
    /// el código. Igual SimularAsync la vuelve a resolver por su cuenta.
    /// </summary>
    private Operacion ArmarOperacion(OperacionViewModel modelo, int regimenId) => new Operacion
    {
        ClienteId = modelo.ClienteId,
        ProveedorId = modelo.ProveedorId,
        RegimenId = regimenId,
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
        // La versión que rige a la fecha de retención (regla 9). La ayuda dice desde
        // cuándo rigen esos valores, para que se vea con cuáles se va a calcular; y
        // si no rige ninguna, lo dice en lugar de mostrar una alícuota.
        var regimen = modelo.CodigoRegimen > 0 && modelo.FechaRetencion != default
            ? await _registro.VersionVigenteAsync(modelo.CodigoRegimen, modelo.FechaRetencion)
            : null;

        modelo.AyudaRegimen = regimen is not null
            ? $"{FormatoRegimen.Ayuda(regimen, proveedor)} · valores vigentes desde {regimen.VigenciaDesde:dd/MM/yyyy}"
            : modelo.CodigoRegimen > 0 && modelo.FechaRetencion != default
                ? SinVersionVigente(modelo)
                : string.Empty;

        // El comprobante repetido no depende del régimen.
        modelo.ComprobantesRepetidos = proveedor is not null && modelo.PuntoVenta > 0 && modelo.NumeroComprobante > 0
            ? await _registro.OperacionesConMismoComprobanteAsync(ArmarOperacion(modelo, 0))
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

        // Una opción por CÓDIGO, no por versión (regla 9): el valor de cada opción es
        // el código. Qué versión se usa lo decide la fecha de retención, no el
        // operador. La descripción es la de la versión vigente hoy o, si no rige
        // ninguna, la de la más reciente.
        var regimenes = await _db.Regimenes.ToListAsync();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        ViewBag.Regimenes = regimenes
            .GroupBy(r => r.Codigo)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var paraMostrar = VigenciaRegimen.VersionVigente(g, hoy) ?? g.MaxBy(r => r.VigenciaDesde)!;
                return new OpcionConAyuda(g.Key, $"{g.Key} — {paraMostrar.Descripcion}", string.Empty);
            })
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