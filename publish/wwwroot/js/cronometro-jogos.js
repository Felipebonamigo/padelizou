document.addEventListener("DOMContentLoaded", function () {
    function atualizarCronometros() {
        const cronometros = document.querySelectorAll('.cronometro');

        cronometros.forEach(function (elemento) {
            const inicioStr = elemento.getAttribute('data-inicio');

            if (inicioStr) {
                const dataInicio = new Date(inicioStr);
                const agora = new Date();
                const diferencaMs = agora - dataInicio;

                if (diferencaMs > 0) {
                    const minutos = Math.floor(diferencaMs / 60000);
                    const segundos = Math.floor((diferencaMs % 60000) / 1000);
                    const segundosFormatados = segundos < 10 ? '0' + segundos : segundos;

                    elemento.innerText = minutos + ':' + segundosFormatados + ' min';
                }
            }
        });
    }

    atualizarCronometros();
    setInterval(atualizarCronometros, 1000);
});