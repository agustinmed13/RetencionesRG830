using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RetencionesRG830.Domain.Entidades;
using RetencionesRG830.Infrastructure.Persistencia;
using RetencionesRG830.Infrastructure.Servicios;
using RetencionesRG830.Web.Models;

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
        return View(new SicoreViewModel
        {
            Desde = new DateOnly(hoy.Year, hoy.Month, 1),
            Hasta = hoy
        });
    }

    /// <summary>
    /// La línea que dice qué va a contener el archivo. La pide wwwroot/js/sicore.js
    /// cada vez que cambia el cliente o el período, igual que el panel de cálculo de
    /// Nueva operación: el navegador no cuenta nada, sólo muestra lo que devuelve acá.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Resumen(SicoreViewModel modelo)
    {
        if (await ValidarAsync(modelo) is not null)
            modelo.Resumen = await _registro.ResumenPeriodoSicoreAsync(
                modelo.ClienteId!.Value, modelo.Desde!.Value, modelo.Hasta!.Value);

        return PartialView("_ResumenSicore", modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Descargar(SicoreViewModel modelo)
    {
        var cliente = await ValidarAsync(modelo);
        if (cliente is null)
            return await VolverAlFormulario(modelo);

        // Desde acá, el mismo camino que antes de la migración de la pantalla: qué se
        // informa lo decide el servicio, que tiene test, y el archivo lo arma el
        // generador, que no se tocó. La pantalla sólo cambió cómo llegan los datos.
        var desde = modelo.Desde!.Value;
        var hasta = modelo.Hasta!.Value;
        var operaciones = await _registro.OperacionesParaSicoreAsync(cliente.Id, desde, hasta);

        if (operaciones.Count == 0)
        {
            // Antes redirigía y el formulario volvía con los valores por defecto. Ahora
            // vuelve con lo que eligió el operador y con el resumen, que dice si hubo
            // operaciones anuladas o sin certificado en el período.
            ModelState.AddModelError(string.Empty, "No hay retenciones para informar en ese período para ese cliente.");
            modelo.Resumen = await _registro.ResumenPeriodoSicoreAsync(cliente.Id, desde, hasta);
            return await VolverAlFormulario(modelo);
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

    /// <summary>
    /// Valida en el servidor el cliente y el período. Antes sólo los controlaba el
    /// navegador (atributo required), que no impide nada: un pedido armado a mano, o
    /// un navegador que no lo respete, llegaba con una fecha vacía convertida en
    /// 01/01/0001 o con el período al revés, y el archivo salía vacío o no salía, sin
    /// decir por qué. Devuelve el cliente si todo es válido, o null.
    /// </summary>
    private async Task<Cliente?> ValidarAsync(SicoreViewModel modelo)
    {
        Cliente? cliente = null;

        // Con un valor que no se puede leer (una fecha mal escrita, un cliente que no
        // es un número), ASP.NET ya dejó su propio error en inglés y la propiedad quedó
        // en null. Error() lo reemplaza por el de acá, en español.
        if (modelo.ClienteId is null or <= 0)
        {
            Error(nameof(modelo.ClienteId), "Elegí un cliente.");
        }
        else
        {
            cliente = await _db.Clientes.FindAsync(modelo.ClienteId.Value);
            if (cliente is null)
                Error(nameof(modelo.ClienteId), "El cliente elegido no existe.");
        }

        if (modelo.Desde is null)
            Error(nameof(modelo.Desde), "Indicá la fecha desde.");

        if (modelo.Hasta is null)
            Error(nameof(modelo.Hasta), "Indicá la fecha hasta.");

        // Con una de las dos en null, la comparación da false: el caso ya lo
        // avisaron los dos controles de arriba.
        if (modelo.Desde > modelo.Hasta)
            Error(nameof(modelo.Desde), "La fecha desde no puede ser posterior a la fecha hasta.");

        return ModelState.IsValid ? cliente : null;
    }

    /// <summary>Deja un único mensaje en el campo, en lugar del que hubiera.</summary>
    private void Error(string campo, string mensaje)
    {
        ModelState.Remove(campo);
        ModelState.AddModelError(campo, mensaje);
    }

    private async Task<IActionResult> VolverAlFormulario(SicoreViewModel modelo)
    {
        await CargarClientes();
        return View(nameof(Index), modelo);
    }

    /// <summary>
    /// Todos los clientes, también los dados de baja (regla 11: el mes anterior a la
    /// baja se presenta después de la baja). Cuáles entran lo decide el servicio, que
    /// tiene test; acá sólo se arma el texto. El de baja lo dice en la opción, para que
    /// no se lo elija creyendo que está activo.
    /// </summary>
    private async Task CargarClientes()
    {
        var clientes = (await _registro.ClientesParaSicoreAsync())
            .Select(c => new
            {
                c.Id,
                Texto = c.Activo ? c.RazonSocial : $"{c.RazonSocial} (dado de baja)"
            });

        ViewBag.Clientes = new SelectList(clientes, "Id", "Texto");
    }
}
