// O DIA DA SEMANA POR EXTENSO, embaixo do campo de data.
//
// 🗣️ É o pedido do Felipe de 25/08/2026, e é o MOTIVO de a tela ter calendário em vez da
// rodinha do Android: "o motivo de marcar aula assim é para que o professor consiga saber que
// dia da semana que é". O calendário mostra o dia enquanto está aberto — depois de fechar
// sobra "15/09/2026", que é justamente a pergunta que ele está respondendo.
//
// COMO USAR: no <input type="date">, `data-dia-da-semana="id-do-elemento-que-recebe-o-texto"`.
//
//   <input type="date" name="data" data-dia-da-semana="dia-da-semana-42" />
//   <div class="form-text fw-bold" id="dia-da-semana-42"></div>
//
// ⚠️ MORA AQUI, e não copiado dentro de cada tela, porque são TRÊS telas marcando aula
// (Adicionar, Editar e o encaixe da Minha Agenda) e a do encaixe repete o par uma vez por
// aula na fila. Régua copiada é como duas telas passam a discordar — e aqui a discordância
// seria justamente no detalhe abaixo, que não dá erro nenhum quando está errado.
(function () {
    var DIAS = ['domingo', 'segunda-feira', 'terça-feira', 'quarta-feira',
                'quinta-feira', 'sexta-feira', 'sábado'];

    function escrever(campo) {
        var alvo = document.getElementById(campo.getAttribute('data-dia-da-semana'));
        if (!alvo) return;

        var partes = (campo.value || '').split('-');
        if (partes.length !== 3) { alvo.textContent = ''; return; }

        // ⚠️ MONTADO POR PARTES, e NUNCA `new Date(campo.value)`: a string só-data é lida como
        // UTC pelo JavaScript e volta o DIA ANTERIOR em qualquer fuso negativo — ou seja, no
        // Brasil inteiro. Uma aula de terça apareceria como segunda, e o erro é mudo.
        var quando = new Date(+partes[0], +partes[1] - 1, +partes[2]);
        alvo.textContent = isNaN(quando) ? '' : DIAS[quando.getDay()];
    }

    function ligar() {
        var campos = document.querySelectorAll('input[type="date"][data-dia-da-semana]');
        for (var i = 0; i < campos.length; i++) {
            var campo = campos[i];
            campo.addEventListener('change', function () { escrever(this); });
            escrever(campo);   // o campo pode já nascer preenchido (sugestão, ou aula existente)
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', ligar);
    } else {
        ligar();
    }
})();
