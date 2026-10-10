using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RetencionesRG830.Application.Servicios;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;

namespace RetencionesRG830.Web.Controllers;

[Authorize(Roles = "Estudio")]
public class ClientesController : Controller
{
    private readonly RetencionesRG830DbContext _db;

    public ClientesController(RetencionesRG830DbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var clientes = await _db.Clientes
            .OrderBy(c => c.RazonSocial)
            .ToListAsync();
        return View(clientes);
    }

    public IActionResult Create()
    {
        return View(new Cliente());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Cliente cliente)
    {
        ValidarCuit(cliente);
        if (!ModelState.IsValid)
            return View(cliente);

        try
        {
            _db.Clientes.Add(cliente);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Cliente.Cuit), "Ya existe un cliente con ese CUIT.");
            return View(cliente);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();
        return View(cliente);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Cliente cliente)
    {
        if (id != cliente.Id) return NotFound();

        ValidarCuit(cliente);
        if (!ModelState.IsValid)
            return View(cliente);

        try
        {
            _db.Clientes.Update(cliente);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Cliente.Cuit), "Ya existe un cliente con ese CUIT.");
            return View(cliente);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Pantalla de confirmación de la baja o la reactivación. No cambia nada:
    /// muestra con nombre y CUIT qué cliente se va a modificar, para que un clic
    /// en la fila equivocada del listado no tenga efecto por sí solo.
    /// </summary>
    public async Task<IActionResult> CambiarActivo(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();
        return View(cliente);
    }

    /// <summary>
    /// Recibe el estado que se confirmó (activo o no) en lugar de invertir el
    /// actual. Si el formulario se envía dos veces, o se reenvía con "Atrás" del
    /// navegador, el cliente queda en el estado que se confirmó y no vuelve al
    /// anterior.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CambiarActivo(int id, bool activo)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente is null) return NotFound();

        cliente.Activo = activo;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private void ValidarCuit(Cliente cliente)
    {
        if (!ValidadorCuit.EsValido(cliente.Cuit))
        {
            ModelState.AddModelError(nameof(Cliente.Cuit), "El CUIT ingresado no es válido (el dígito verificador no coincide).");
        }
    }
}