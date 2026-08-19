// REGISTRAR UM JOGO DA PANELINHA EM ~6 TOQUES.
//
// Veio de uma sugestão de usuário (18/08/2026): a tela era um formulário com quatro <select>
// de jogador mais dois campos de placar, e o registro é coisa que se faz no celular, em pé,
// ao lado da quadra, com o jogo recém-acabado. Quem chega aqui veio do jogo da semana — o
// sistema já sabe grupo, data, clube e quem joga.
//
// O fluxo passou a ser: 2 toques (dupla 1) + 2 toques (dupla 2) + 1 (vencedor) + 1 (salvar).
//
// ⚠️ REGRA ADAPTATIVA, que é o miolo disto: até 6 opções, os nomes viram BOTÕES; acima disso,
// um seletor. E a régua é reavaliada a cada escolha — panelinha de 8 começa no seletor, e
// depois que a dupla 1 leva dois, sobram 6 e a dupla 2 já aparece como botão. Dropdown é o
// caso excepcional, não o padrão.
function pdzMontarRegistroDeJogo(jogadores) {
    var LIMITE_DE_BOTOES = 6;

    var escolhidos = { 1: [], 2: [] };
    var vencedor = null;

    var campos = {
        1: [document.getElementById('pdzD1J1'), document.getElementById('pdzD1J2')],
        2: [document.getElementById('pdzD2J1'), document.getElementById('pdzD2J2')],
    };

    function nomeDe(id) {
        var j = jogadores.find(function (x) { return x.id === id; });
        return j ? j.nome : '?';
    }

    function nomeDaDupla(t) {
        return escolhidos[t].map(nomeDe).join(' + ');
    }

    // Quem PODE aparecer neste time: todo mundo que o outro time não levou.
    function disponiveisPara(t) {
        var doOutro = escolhidos[t === 1 ? 2 : 1];
        return jogadores.filter(function (j) { return doOutro.indexOf(j.id) === -1; });
    }

    function alternar(t, id) {
        var lista = escolhidos[t];
        var i = lista.indexOf(id);
        if (i >= 0) {
            lista.splice(i, 1);
        } else if (lista.length < 2) {
            lista.push(id);
        }
        // Escolher aqui muda o que sobra pro outro time — os dois se redesenham.
        desenhar();
    }

    function chip(t, id) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm btn-success rounded-pill';
        b.innerHTML = nomeDe(id) + ' <span aria-hidden="true">&times;</span>';
        b.setAttribute('aria-label', 'Tirar ' + nomeDe(id) + ' da dupla');
        b.onclick = function () { alternar(t, id); };
        return b;
    }

    function botaoDeNome(t, j) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm rounded-pill ' + (j.confirmado ? 'btn-outline-success' : 'btn-outline-secondary');
        b.textContent = j.nome;
        // Quem confirmou presença ganha um ponto verde. É só destaque — a lista mostra todo
        // mundo, porque quem jogou e esqueceu de confirmar tem que poder ser lançado.
        if (j.confirmado) b.innerHTML = '<span class="pdz-ponto-confirmado"></span>' + j.nome;
        if (j.convidado) b.title = 'Convidado';
        b.onclick = function () { alternar(t, j.id); };
        return b;
    }

    function seletorDeNome(t, restantes) {
        var s = document.createElement('select');
        s.className = 'form-select form-select-sm';
        s.innerHTML = '<option value="">Escolher jogador...</option>';
        restantes.forEach(function (j) {
            var o = document.createElement('option');
            o.value = j.id;
            o.textContent = j.nome + (j.confirmado ? ' ✓' : '');
            s.appendChild(o);
        });
        s.onchange = function () { if (s.value) alternar(t, parseInt(s.value, 10)); };
        return s;
    }

    function desenharTime(t) {
        var caixa = document.getElementById('pdzTimes' + t);
        caixa.innerHTML = '';

        escolhidos[t].forEach(function (id) { caixa.appendChild(chip(t, id)); });

        var dica = document.getElementById('pdzDica' + t);
        if (escolhidos[t].length === 2) {
            dica.textContent = 'pronta';
            return;
        }
        dica.textContent = escolhidos[t].length === 1 ? 'falta 1' : 'escolha 2 jogadores';

        var restantes = disponiveisPara(t).filter(function (j) {
            return escolhidos[t].indexOf(j.id) === -1;
        });

        if (restantes.length <= LIMITE_DE_BOTOES) {
            restantes.forEach(function (j) { caixa.appendChild(botaoDeNome(t, j)); });
        } else {
            var envelope = document.createElement('div');
            envelope.style.minWidth = '14rem';
            envelope.appendChild(seletorDeNome(t, restantes));
            caixa.appendChild(envelope);
        }
    }

    function desenhar() {
        desenharTime(1);
        desenharTime(2);

        // Os ids que o servidor vai receber. Vazio quando a dupla não está fechada — e aí o
        // botão de salvar está desligado, então nada incompleto chega ao POST.
        [1, 2].forEach(function (t) {
            campos[t][0].value = escolhidos[t][0] || '';
            campos[t][1].value = escolhidos[t][1] || '';
        });

        var completo = escolhidos[1].length === 2 && escolhidos[2].length === 2;

        // ⚠️ Mexer nas duplas ZERA o vencedor. Sem isso, trocar um jogador depois de já ter
        // marcado quem ganhou deixaria o botão "🏆" numa dupla que não existe mais igual — e
        // o ranking seguiria esse vencedor.
        if (!completo && vencedor !== null) {
            vencedor = null;
            document.getElementById('pdzVencedor').value = '';
        }

        document.getElementById('pdzCardVencedor').classList.toggle('d-none', !completo);
        document.getElementById('pdzCardPlacar').classList.toggle('d-none', vencedor === null);
        document.getElementById('pdzSalvar').disabled = !(completo && vencedor !== null);

        if (completo) {
            document.getElementById('pdzBtnVenc1').textContent = nomeDaDupla(1);
            document.getElementById('pdzBtnVenc2').textContent = nomeDaDupla(2);
            document.getElementById('pdzRotuloGames1').textContent = nomeDaDupla(1);
            document.getElementById('pdzRotuloGames2').textContent = nomeDaDupla(2);
        }

        [1, 2].forEach(function (lado) {
            var b = document.getElementById('pdzBtnVenc' + lado);
            var ganhou = vencedor === lado;
            b.classList.toggle('btn-success', ganhou);
            b.classList.toggle('btn-outline-success', !ganhou);
            if (ganhou) b.innerHTML = '🏆 ' + nomeDaDupla(lado);
        });
    }

    Array.prototype.forEach.call(document.querySelectorAll('.pdz-btn-vencedor'), function (b) {
        b.onclick = function () {
            vencedor = parseInt(b.dataset.lado, 10);
            document.getElementById('pdzVencedor').value = vencedor;
            desenhar();
        };
    });

    desenhar();
}
