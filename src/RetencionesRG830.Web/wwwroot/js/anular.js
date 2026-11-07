// Pantalla de Anular: que no se pueda anular por accidente.
//
// El botón queda deshabilitado hasta que haya motivo y la casilla esté marcada, y un
// doble clic no manda dos pedidos: el segundo encontraría la operación ya anulada y
// el operador vería una página de error, aunque la anulación hubiera salido bien.
//
// Es una ayuda, no el control: el motivo lo controla el servidor, y sin JavaScript el
// formulario sigue exigiendo los dos datos con "required".
document.addEventListener('DOMContentLoaded', function () {
    var form = document.getElementById('form-anular');
    if (!form) return;

    var motivo = document.getElementById('motivo');
    var casilla = document.getElementById('confirma-anulacion');
    var boton = document.getElementById('boton-anular');
    var enviado = false;

    // trim, igual que el servidor: minlength del navegador cuenta los espacios, y un
    // motivo de cinco espacios volvería rechazado.
    function completo() {
        return motivo.value.trim().length >= motivo.minLength && casilla.checked;
    }

    function actualizar() {
        boton.disabled = enviado || !completo();
    }

    motivo.addEventListener('input', actualizar);
    casilla.addEventListener('change', actualizar);

    form.addEventListener('submit', function (e) {
        if (enviado || !completo()) {
            e.preventDefault();
            return;
        }
        enviado = true;
        actualizar();
    });

    // El navegador puede restaurar lo escrito al volver a la página.
    actualizar();
});
