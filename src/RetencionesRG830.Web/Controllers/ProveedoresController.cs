using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class ProveedoresController : Controller
{
    private readonly RetencionesRG830DbContext _db;

    public ProveedoresController(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int? clienteId)
    {
        var query = _db.Proveedores.Include(p => p.Cliente).AsQueryable();

        if (clienteId is not null)
            query = query.Where(p => p.ClienteId == clienteId);

        ViewBag.ClienteId = clienteId;
        ViewBag.ClienteNombre = clienteId is null
            ? null
            : (await _db.Clientes.FindAsync(clienteId))?.RazonSocial;

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
        ValidarProveedor(proveedor);
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
        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor is null) return NotFound();
        await CargarListaClientes(proveedor.ClienteId);
        return View(proveedor);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Proveedor proveedor)
    {
        if (id != proveedor.Id) return NotFound();

        ValidarProveedor(proveedor);
        if (!ModelState.IsValid)
        {
            await CargarListaClientes(proveedor.ClienteId);
            return View(proveedor);
        }

        try
        {
            _db.Proveedores.Update(proveedor);
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

    [HttpPost]
    public async Task<IActionResult> CambiarActivo(int id)
    {
        var proveedor = await _db.Proveedores.FindAsync(id);
        if (proveedor is null) return NotFound();

        proveedor.Activo = !proveedor.Activo;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { clienteId = proveedor.ClienteId });
    }

    private async Task CargarListaClientes(int? clienteIdSeleccionado)
    {
        var clientes = await _db.Clientes
            .Where(c => c.Activo)
            .OrderBy(c => c.RazonSocial)
            .ToListAsync();

        ViewBag.Clientes = new SelectList(clientes, nameof(Cliente.Id), nameof(Cliente.RazonSocial), clienteIdSeleccionado);
    }

    private void ValidarProveedor(Proveedor proveedor)
    {
        if (proveedor.ClienteId <= 0)
        {
            ModelState.AddModelError(nameof(Proveedor.ClienteId), "Tenés que elegir un cliente.");
        }

        if (!ValidadorCuit.EsValido(proveedor.Cuit))
        {
            ModelState.AddModelError(nameof(Proveedor.Cuit), "El CUIT ingresado no es válido (el dígito verificador no coincide).");
        }
    }
}