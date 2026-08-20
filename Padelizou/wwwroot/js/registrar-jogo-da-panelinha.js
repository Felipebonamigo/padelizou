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
// ⚠️ O CONVIDADO SEM NOME (20/08/2026) É UM PSEUDO-ID NEGATIVO AQUI DENTRO, e um só (-1) no fio.
// A distinção é obrigatória: `disponiveisPara` e `alternar` trabalham por identidade numérica, e
// com um pseudo-id único escolher "Convidado" na dupla 1 o tiraria da dupla 2 — o quarteto nunca
// fecharia e "Salvar" ficaria desligado pra sempre. No banco os dois viram NULO e param de se
// distinguir, o que é a verdade: sem nome, o sistema não sabe se é o mesmo primo.
//
// `convidado` vem do SERVIDOR ({ noFio, maximo, rotulo } — ver Services/ConvidadoNoJogo). Nada
// disso é digitado aqui: o teto é conferido nos dois POSTs, e um número escrito à mão neste
// arquivo seria a segunda cópia da régua.
function pdzMontarRegistroDeJogo(jogadores, convidado) {
    var LIMITE_DE_BOTOES = 6;

    var escolhidos = { 1: [], 2: [] };
    var vencedor = null;

    // Só decresce; todos os negativos viram o mesmo `convidado.noFio` ao preencher os hidden.
    var proximoConvidado = -1;

    var campos = {
        1: [document.getElementById('pdzD1J1'), document.getElementById('pdzD1J2')],
        2: [document.getElementById('pdzD2J1'), document.getElementById('pdzD2J2')],
    };

    function ehConvidado(id) {
        return id < 0;
    }

    function totalDeConvidados() {
        return escolhidos[1].concat(escolhidos[2]).filter(ehConvidado).length;
    }

    function nomeDe(id) {
        // ⚠️ ANTES do `find`, senão o chip, os botões de "Quem ganhou?" e os rótulos do placar
        // saem como '?' — que é o fallback de ERRO, e reusá-lo aqui apagaria a diferença entre
        // "é de propósito" e "quebrou".
        if (ehConvidado(id)) return convidado.rotulo;
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

    // "+ Convidado": a vaga de quem jogou e não está no Padelizou.
    //
    // ⚠️ FICA FORA DE `jogadores` e FORA da contagem contra LIMITE_DE_BOTOES. Se ele entrasse na
    // lista, uma panelinha de 6 viraria 7 opções e CAIRIA NO DROPDOWN — matando a régua adaptativa
    // que é o motivo desta tela existir.
    function botaoDeConvidado(t) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'btn btn-sm rounded-pill btn-outline-secondary border-2 fst-italic';
        b.style.borderStyle = 'dashed';
        b.textContent = '+ ' + convidado.rotulo;
        // O que se perde ao usar a vaga, no ponto do clique — o convite ao pé da tela custa dois
        // minutos e este botão custa um toque; sem dizer isto, o histórico vira fila de anônimos.
        b.title = convidado.rotulo + ' não entra no ranking e o jogo não conta pra ele. '
                + 'Se a pessoa já usa o Padelizou, convide pra este jogo.';
        b.onclick = function () { alternar(t, proximoConvidado--); };
        return b;
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

        // ⚠️ NOS DOIS RAMOS. Panelinha grande cai no dropdown, e um "+ Convidado" que só existisse
        // no ramo dos botões sumiria justamente onde falta gente com mais frequência.
        //
        // ⚠️ O TETO AQUI É ESPELHO DA RÉGUA DO SERVIDOR, aceito de olho aberto: sem ele, o
        // terceiro convidado só seria recusado no POST — e a recusa é TempData + RedirectToAction,
        // ou seja PÁGINA INTEIRA NOVA, perdendo chips, vencedor e placar. Numa tela cujo motivo de
        // existir é "6 toques em pé ao lado da quadra", isso é pior que a duplicação. O número vem
        // do servidor, então as duas cópias não podem discordar.
        if (totalDeConvidados() < convidado.maximo) {
            caixa.appendChild(botaoDeConvidado(t));
        }
    }

    function desenhar() {
        desenharTime(1);
        desenharTime(2);

        // Os ids que o servidor vai receber. Vazio quando a dupla não está fechada — e aí o
        // botão de salvar está desligado, então nada incompleto chega ao POST.
        //
        // ⚠️ MAPEAMENTO EXPLÍCITO, e não o `|| ''` que estava aqui: pseudo-id negativo é falsy? Não
        // — mas `0` seria, e a troca protege contra o dia em que alguém mexer nos pseudo-ids. O
        // que importa mesmo é a segunda metade: TODOS os negativos viram o MESMO `noFio`, porque a
        // distinção entre convidado 1 e convidado 2 só serve pra esta tela, não pro banco.
        [1, 2].forEach(function (t) {
            [0, 1].forEach(function (k) {
                var v = escolhidos[t][k];
                campos[t][k].value = (v === undefined) ? '' : (ehConvidado(v) ? convidado.noFio : v);
            });
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
