using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RetencionesRG830.Web.Infraestructura;

/// <summary>
/// Interpreta los importes que llegan de los formularios aceptando tanto
/// punto como coma decimal.
///
/// Hace falta porque los campos &lt;input type="number"&gt; del navegador
/// SIEMPRE envían el número con punto ("87190.09"), sin importar el idioma
/// de la computadora, mientras que la aplicación está configurada en es-AR,
/// donde el punto significa separador de miles. Sin esto, "87190.09" se
/// interpretaba como 8.719.009: cien veces más grande, y sin ningún aviso.
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

        var texto = valor.FirstValue;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return Task.CompletedTask;
        }

        // Sin AllowThousands a propósito: así el punto nunca se puede
        // confundir con un separador de miles.
        const NumberStyles estilos = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (decimal.TryParse(texto, estilos, CultureInfo.InvariantCulture, out var numero)
            || decimal.TryParse(texto, estilos, new CultureInfo("es-AR"), out numero))
        {
            bindingContext.Result = ModelBindingResult.Success(numero);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName, "Ingresá un importe válido.");
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