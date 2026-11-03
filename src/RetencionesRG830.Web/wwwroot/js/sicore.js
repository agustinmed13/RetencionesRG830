// Pantalla "Archivo para SICORE": mantiene actualizada la línea que dice cuántas
// operaciones va a contener el archivo.
//
// Este archivo NO cuenta ni filtra operaciones. Cada vez que cambia el cliente o el
// período, manda el formulario a la acción Resumen del servidor, que cuenta con el
// mismo método que arma el archivo, y devuelve la línea ya armada. Acá sólo se la
// inserta. Es el mismo criterio que nueva-operacion.js con el panel de cálculo.
document.addEventListener('DOMContentLoaded', function () {
    var form = document.getElementById('form-sicore');
    var resumen = document.getElementById('resumen-sicore');
    if (!form || !resumen) return;

    var url = form.getAttribute('data-resumen-url');
    var espera = null;        // temporizador para no pedir en cada tecla
    var pedidoEnCurso = null; // para descartar respuestas viejas

    function actualizarResumen() {
        // Una respuesta vieja que llegue después de una nueva pisaría la línea con
        // el conteo de otro período: se cancela el pedido anterior.
        if (pedidoEnCurso) pedidoEnCurso.abort();
        pedidoEnCurso = new AbortController();

        // El formulario viaja completo, incluido el token antifalsificación.
        fetch(url, {
            method: 'POST',
            body: new URLSearchParams(new FormData(form)),
            signal: pedidoEnCurso.signal
        })
            .then(function (respuesta) {
                if (!respuesta.ok) throw new Error('HTTP ' + respuesta.status);
                return respuesta.text();
            })
            .then(function (html) {
                resumen.innerHTML = html;
            })
            .catch(function (error) {
                if (error.name === 'AbortError') return;
                // Mejor ningún número que uno de otro período.
                resumen.innerHTML =
                    '<div class="nota-panel nota-panel-seccion nota-panel-alerta">' +
                    'No se pudo contar las operaciones del período. La descarga funciona igual.</div>';
            });
    }

    function programarActualizacion() {
        clearTimeout(espera);
        espera = setTimeout(actualizarResumen, 300);
    }

    form.addEventListener('input', programarActualizacion);
    form.addEventListener('change', programarActualizacion);

    // Al volver atrás, el navegador puede restaurar en el formulario valores
    // distintos de los que el servidor usó para dibujar la línea: se la pide de nuevo.
    actualizarResumen();
});
