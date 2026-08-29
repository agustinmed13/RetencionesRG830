// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// En los formularios de carga de datos, Enter pasa al campo siguiente en
// vez de enviar el formulario. Sólo el botón confirma.
//
// Se activa únicamente en los <form> que tengan el atributo
// "data-enter-siguiente", para no alterar formularios como el de login,
// donde Enter sí debe enviar.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('form[data-enter-siguiente]').forEach(function (form) {

        var campos = Array.prototype.slice
            .call(form.querySelectorAll('input, select'))
            .filter(function (el) {
                return el.type !== 'hidden' && !el.disabled && !el.readOnly;
            });

        form.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter') return;

            // Si el foco está en el botón, dejamos que envíe normalmente.
            var etiqueta = e.target.tagName;
            if (etiqueta === 'BUTTON' || etiqueta === 'TEXTAREA') return;
            if (e.target.type === 'submit') return;

            e.preventDefault();

            var posicion = campos.indexOf(e.target);
            if (posicion > -1 && posicion < campos.length - 1) {
                var siguiente = campos[posicion + 1];
                siguiente.focus();
                if (siguiente.select) siguiente.select();
            }
        });
    });
});