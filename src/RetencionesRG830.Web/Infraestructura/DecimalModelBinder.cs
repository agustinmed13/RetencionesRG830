using Microsoft.AspNetCore.Mvc.ModelBinding;
using RetencionesRG830.Application.Servicios;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Interpreta los importes que llegan de los formularios aceptando tanto
/// punto como coma decimal. La regla está en LectorImporte (Application), con
/// tests bajo es-AR y en-US; este binder sólo la conecta con ASP.NET.
///
/// Es el mismo problema de fondo que hace que a veces AFIP le rechace al
/// estudio el archivo de SICORE: un número que cambia de significado según
/// la configuración regional de la máquina.
/// </summary>
public class DecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valor == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);

        // Cómo se lee el texto lo decide LectorImporte (Application), que tiene tests.
        // Acá sólo se traduce el resultado a lo que espera ASP.NET.
        var lectura = LectorImporte.Interpretar(valor.FirstValue);

        switch (lectura.Resultado)
        {
            // Vacío no es cero. Antes este caso salía sin informar nada, y la propiedad
            // se quedaba en 0: así se podía registrar una operación de $ 0.
            // - En un decimal? el vacío se representa como null, y si el campo es
            //   obligatorio lo dice su [Required], con su propio mensaje.
            // - En un decimal común no hay cómo representar "vacío": es un error.
            case ResultadoLectura.Vacio:
                if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
                    bindingContext.Result = ModelBindingResult.Success(null);
                else
                    bindingContext.ModelState.TryAddModelError(
                        bindingContext.ModelName, "Ingresá un importe.");
                break;

            case ResultadoLectura.Numero:
                bindingContext.Result = ModelBindingResult.Success(lectura.Valor);
                break;

            // El mensaje enseña cómo se escribe, no sólo rechaza. Los casos típicos son
            // "5.000.000" y "50.000": así se escribe la plata en la Argentina, pero el
            // punto no se acepta como separador de miles, y un importe no puede tener
            // más de dos decimales (ver LectorImporte). Un solo mensaje cubre las dos
            // reglas, porque el operador no sabe cuál de las dos rompió.
            default:
                bindingContext.ModelState.TryAddModelError(
                    bindingContext.ModelName,
                    "Escribí el importe sin separador de miles y con dos decimales como máximo: "
                    + "por ejemplo 50000 o 50000,75.");
                break;
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Le indica a ASP.NET Core que use el intérprete de arriba para todos los
/// campos decimales de todos los formularios de la aplicación.
/// </summary>
public class DecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var tipo = Nullable.GetUnderlyingType(context.Metadata.ModelType)
                   ?? context.Metadata.ModelType;

        return tipo == typeof(decimal) ? new DecimalModelBinder() : null;
    }
}