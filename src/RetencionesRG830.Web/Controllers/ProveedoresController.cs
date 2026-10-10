using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class ProveedoresController : Controller
{
    private readonly RetencionesRG830DbContext _db;
    private readonly ServicioProveedores _servicio;

    public ProveedoresController(RetencionesRG830DbContext db, ServicioProveedores servicio)
    {
        _db = db;
        _servicio = servicio;
    }

    public async Task<IActionResult> Index(int? clienteId)
    {
        var query = _db.Proveedores.Include(p => p.Cliente).AsQueryable();

        if (clienteId is not null)
            query = query.Where(p => p.ClienteId == clienteId);

        var cliente = clienteId is null ? null : await _db.Clientes.FindAsync(clienteId);
        ViewBag.ClienteId = clienteId;
        ViewBag.ClienteNombre = cliente?.RazonSocial;
        ViewBag.ClienteActivo = cliente?.Activo ?? false;

        var proveedores = await query.OrderBy(p => p.RazonSocial).ToListAsync();
        return View(proveedores);
    }

    public async Task<IActionResult> Create(int? clienteId)
    {
        await CargarListaClientes(clienteId);
        return View(new Proveedor { ClienteId = clienteId ?? 0 });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Proveedor proveedor)
    {
        // En el alta sí se elige el cliente: es la única vez.
        if (proveedor.ClienteId <= 0)
            ModelState.AddModelError(nameof(Proveedor.ClienteId), "Tenés que elegir un cliente.");
        ValidarCuit(proveedor);

        if (!ModelState.IsValid)
        {
            await CargarListaClientes(proveedor.ClienteId);
            return View(proveedor);
        }

        try
        {
            _db.Proveedores.Add(proveedor);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Proveedor.Cuit), "Ese cliente ya tiene un proveedor cargado con ese CUIT.");
            await CargarListaClientes(proveedor.ClienteId);
            return View(proveedor);
        }

        return RedirectToAction(nameof(Index), new { clienteId = proveedor.ClienteId });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var proveedor = await _db.Proveedores
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proveedor is null) return NotFound();
        return View(proveedor);
    }

    /// <summary>
    /// El cliente del proveedor no se edita (regla 8): la vista lo muestra como
    /// texto y ServicioProveedores ignora el ClienteId que llegue en el POST,
    /// aunque alguien lo agregue a mano. Ver ServicioProveedoresTests.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Proveedor proveedor)
    {
        if (id != proveedor.Id) return NotFound();

        ValidarCuit(proveedor);
        if (!ModelState.IsValid)
            return await VolverAEditar(proveedor);

        Proveedor? guardado;
        try
        {
            guardado = await _servicio.ActualizarAsync(proveedor);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Proveedor.Cuit), "Ese cliente ya tiene un proveedor cargado con ese CUIT.");
            return await VolverAEditar(proveedor);
        }

        if (guardado is null) return NotFound();
        return RedirectToAction(nameof(Index), new { clienteId = guardado.ClienteId });
    }

    /// <summary>
    /// Pantalla de confirmación de la baja o la reactivación. No cambia nada:
    /// muestra con nombre y CUIT qué proveedor se va a modificar, igual que en
    /// Clientes.
    /// </summary>
    public async Task<IActionResult> CambiarActivo(int id)
    {
        var proveedor = await _db.Proveedores
            .Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proveedor is null) return NotFound();
        return View(proveedor);
    }

    /// <summary>
    /// Recibe el estado confirmado en lugar de invertir el actual: si el
    /// formulario se envía dos veces, el proveedor queda como se confirmó.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CambiarActivo(int id, bool activo)
    {
        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor is null) return NotFound();

        proveedor.Activo = activo;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { clienteId = proveedor.ClienteId });
    }

    /// <summary>
    /// Vuelve a mostrar la edición con los errores. El cliente se lee de la base,
    /// no del formulario: es el que la vista muestra y adonde vuelve "Cancelar".
    /// </summary>
    private async Task<IActionResult> VolverAEditar(Proveedor proveedor)
    {
        var clienteId = await _db.Proveedores
            .Where(p => p.Id == proveedor.Id)
            .Select(p => p.ClienteId)
            .FirstOrDefaultAsync();

        proveedor.ClienteId = clienteId;
        proveedor.Cliente = await _db.Clientes.FindAsync(clienteId);
        return View(nameof(Edit), proveedor);
    }

    private async Task CargarListaClientes(int? clienteIdSeleccionado)
    {
        var clientes = await _db.Clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .ToListAsync();

        ViewBag.Clientes = new SelectList(clientes, nameof(Cliente.Id), nameof(Cliente.RazonSocial), clienteIdSeleccionado);
    }

    private void ValidarCuit(Proveedor proveedor)
    {
        if (!ValidadorCuit.EsValido(proveedor.Cuit))
        {
            ModelState.AddModelError(nameof(Proveedor.Cuit), "El CUIT ingresado no es válido (el dígito verificador no coincide).");
        }
    }
}
