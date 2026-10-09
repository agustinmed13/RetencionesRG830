// Pantalla "Nueva operación": mantiene actualizado el panel de cálculo.
//
// IMPORTANTE: este archivo NO calcula la retención ni hace ninguna cuenta. Cada vez
// que cambia un dato, manda el formulario a la acción Simular del servidor, que
// calcula con el mismo motor que la confirmación y el registro (SimularAsync,
// cubierto por los tests), y devuelve el panel ya armado. Acá sólo se lo inserta.
// Si la cuenta se hiciera también en el navegador habría dos motores, que tarde o
// temprano darían resultados distintos, y uno de ellos sin tests.
document.addEventListener('DOMContentLoaded', function () {
    var form = document.getElementById('form-operacion');
    var panel = document.getElementById('panel-calculo');
    if (!form || !panel) return;

    var url = form.getAttribute('data-simular-url');
    var espera = null;       // temporizador para no pedir en cada tecla
    var pedidoEnCurso = null; // para descartar respuestas viejas

    function actualizarPanel() {
        // Si llega una respuesta de un pedido anterior después de una más nueva,
        // pisaría el panel con datos viejos: se cancela el pedido anterior.
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
                panel.innerHTML = html;
                // La ayuda del régimen depende del proveedor elegido y la arma el
                // servidor: viene dentro del panel y se copia debajo del desplegable.
                var ayuda = panel.querySelector('[data-ayuda-regimen]');
                var destino = document.getElementById('ayuda-regimen');
                if (ayuda && destino) destino.textContent = ayuda.textContent;
            })
            .catch(function (error) {
                if (error.name === 'AbortError') return;
                panel.innerHTML =
                    '<div class="nota-panel nota-panel-alerta panel-calculo-espera">' +
                    'No se pudo actualizar el cálculo. Podés seguir cargando: ' +
                    '"Revisar cálculo" lo calcula igual.</div>';
            });
    }

    function programarActualizacion() {
        clearTimeout(espera);
        espera = setTimeout(actualizarPanel, 300);
    }

    form.addEventListener('input', programarActualizacion);
    form.addEventListener('change', programarActualizacion);

    // La línea de ayuda debajo del proveedor (CUIT y condición) viene armada desde
    // el servidor en cada opción. La del régimen llega con el panel (ver arriba).
    form.querySelectorAll('select[data-ayuda]').forEach(function (select) {
        select.addEventListener('change', function () {
            var destino = document.getElementById(select.getAttribute('data-ayuda'));
            var opcion = select.options[select.selectedIndex];
            if (destino) destino.textContent = (opcion && opcion.getAttribute('data-ayuda')) || '';
        });
    });

    // Al volver de "Corregir datos" el formulario ya trae datos: se calcula de entrada.
    actualizarPanel();
});
