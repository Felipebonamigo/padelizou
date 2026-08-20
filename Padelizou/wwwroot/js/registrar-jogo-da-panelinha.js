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
function pdzMontarRegistroDeJogo(jogadores, convidado, maximoDeJogos) {
    var LIMITE_DE_BOTOES = 6;

    var lista = document.getElementById('pdzJogos');
    var modelo = document.getElementById('pdzModeloJogo');
    var botaoMais = document.getElementById('pdzMaisUmJogo');
    var avisoTeto = document.getElementById('pdzTetoDeJogos');
    var salvar = document.getElementById('pdzSalvar');
    var blocos = [];

    // OS NOMES QUE VÃO NO FIO. O jogo 1 usa os campos soltos de sempre; do 2 em diante, a
    // lista `maisResultados`. A assimetria é do servidor (ver GruposController) e morre aqui:
    // pra todo o resto deste arquivo, jogo 1 e jogo 7 são a mesma coisa.
    //
    // ⚠️ ÍNDICE CONTÍGUO A PARTIR DE 0. A ligação de coleção do ASP.NET para no primeiro
    // buraco: remover o jogo 2 e deixar `maisResultados[1]` sem o `[0]` faria o jogo 3 sumir
    // sem erro nenhum. Por isso todo bloco é renumerado a cada inclusão e a cada remoção.
    var LEGADO = {
        d1j1: 'dupla1Jogador1Id', d1j2: 'dupla1Jogador2Id',
        d2j1: 'dupla2Jogador1Id', d2j2: 'dupla2Jogador2Id',
        venc: 'vencedorLado', g1: 'gamesDupla1', g2: 'gamesDupla2',
    };
    var NA_LISTA = {
        d1j1: 'Dupla1Jogador1Id', d1j2: 'Dupla1Jogador2Id',
        d2j1: 'Dupla2Jogador1Id', d2j2: 'Dupla2Jogador2Id',
        venc: 'VencedorLado', g1: 'GamesDupla1', g2: 'GamesDupla2',
    };

    function nomeDoCampo(indice, chave) {
        return indice === 0
            ? LEGADO[chave]
            : 'maisResultados[' + (indice - 1) + '].' + NA_LISTA[chave];
    }

    // ── UM JOGO ─────────────────────────────────────────────────────────────────────────
    //
    // Toda a escolha de dupla, vencedor e placar vive aqui dentro, presa a `raiz`. Antes isto
    // era um punhado de getElementById no documento — o que funcionava enquanto a tela tinha
    // um jogo só, e viraria um bloco pintando os chips do outro no dia em que tivesse dois.
    function criarBloco(raiz) {
        var escolhidos = { 1: [], 2: [] };
        var vencedor = null;

        // Só decresce; todos os negativos viram o mesmo `convidado.noFio` ao preencher os hidden.
        var proximoConvidado = -1;

        var campos = {
            1: [raiz.querySelector('.pdz-c-d1j1'), raiz.querySelector('.pdz-c-d1j2')],
            2: [raiz.querySelector('.pdz-c-d2j1'), raiz.querySelector('.pdz-c-d2j2')],
        };
        var campoVencedor = raiz.querySelector('.pdz-c-venc');

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

        // Quem PODE aparecer neste time: todo mundo que o outro time DESTE JOGO não levou.
        //
        // ⚠️ "Deste jogo" é a palavra que importa desde que a tela passou a aceitar vários. A
        // mesma pessoa joga a noite inteira: excluí-la dos outros blocos porque já está num
        // deles impediria justamente o caso que fez este pedido existir — as mesmas quatro
        // trocando de par entre um jogo e outro.
        function disponiveisPara(t) {
            var doOutro = escolhidos[t === 1 ? 2 : 1];
            return jogadores.filter(function (j) { return doOutro.indexOf(j.id) === -1; });
        }

        function alternar(t, id) {
            var lst = escolhidos[t];
            var i = lst.indexOf(id);
            if (i >= 0) {
                lst.splice(i, 1);
            } else if (lst.length < 2) {
                lst.push(id);
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
            var caixa = raiz.querySelector('.pdz-times' + t);
            caixa.innerHTML = '';

            escolhidos[t].forEach(function (id) { caixa.appendChild(chip(t, id)); });

            var dica = raiz.querySelector('.pdz-dica' + t);
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

        function completo() {
            return escolhidos[1].length === 2 && escolhidos[2].length === 2 && vencedor !== null;
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

            var quarteto = escolhidos[1].length === 2 && escolhidos[2].length === 2;

            // ⚠️ Mexer nas duplas ZERA o vencedor. Sem isso, trocar um jogador depois de já ter
            // marcado quem ganhou deixaria o botão "🏆" numa dupla que não existe mais igual — e
            // o ranking seguiria esse vencedor.
            if (!quarteto && vencedor !== null) {
                vencedor = null;
                campoVencedor.value = '';
            }

            raiz.querySelector('.pdz-card-vencedor').classList.toggle('d-none', !quarteto);
            raiz.querySelector('.pdz-card-placar').classList.toggle('d-none', vencedor === null);

            if (quarteto) {
                raiz.querySelector('.pdz-btn-venc1').textContent = nomeDaDupla(1);
                raiz.querySelector('.pdz-btn-venc2').textContent = nomeDaDupla(2);
                raiz.querySelector('.pdz-rotulo-games1').textContent = nomeDaDupla(1);
                raiz.querySelector('.pdz-rotulo-games2').textContent = nomeDaDupla(2);
            }

            [1, 2].forEach(function (lado) {
                var b = raiz.querySelector('.pdz-btn-venc' + lado);
                var ganhou = vencedor === lado;
                b.classList.toggle('btn-success', ganhou);
                b.classList.toggle('btn-outline-success', !ganhou);
                if (ganhou) b.innerHTML = '🏆 ' + nomeDaDupla(lado);
            });

            atualizarSalvar();
        }

        Array.prototype.forEach.call(raiz.querySelectorAll('.pdz-btn-vencedor'), function (b) {
            b.onclick = function () {
                vencedor = parseInt(b.dataset.lado, 10);
                campoVencedor.value = vencedor;
                desenhar();
            };
        });

        var abrirPlacar = raiz.querySelector('.pdz-abrir-placar');
        abrirPlacar.onclick = function () {
            raiz.querySelector('.pdz-placar').classList.remove('d-none');
            abrirPlacar.classList.add('d-none');
        };

        raiz.querySelector('.pdz-jogo-remover').onclick = function () { removerBloco(bloco); };

        var bloco = {
            raiz: raiz,
            completo: completo,
            // Numerar é dar nome aos campos e escrever "Jogo N" no cabeçalho.
            numerar: function (indice, total) {
                ['d1j1', 'd1j2', 'd2j1', 'd2j2', 'venc', 'g1', 'g2'].forEach(function (chave) {
                    var alvo = raiz.querySelector('.pdz-c-' + chave);
                    if (alvo) alvo.name = nomeDoCampo(indice, chave);
                });
                var cabecalho = raiz.querySelector('.pdz-jogo-cabecalho');
                cabecalho.classList.toggle('d-none', total < 2);
                raiz.querySelector('.pdz-jogo-titulo').textContent = 'Jogo ' + (indice + 1);
            },
        };

        desenhar();
        return bloco;
    }

    function atualizarSalvar() {
        var todosProntos = blocos.length > 0 && blocos.every(function (b) { return b.completo(); });
        salvar.disabled = !todosProntos;
        salvar.textContent = blocos.length > 1
            ? 'Salvar ' + blocos.length + ' jogos'
            : 'Salvar resultado';

        // O "+ Adicionar outro" só faz sentido com o jogo atual fechado: um bloco em branco
        // atrás de outro em branco é só formulário travado com o salvar desligado.
        var noTeto = blocos.length >= maximoDeJogos;
        botaoMais.disabled = !todosProntos || noTeto;
        avisoTeto.classList.toggle('d-none', !noTeto);
        if (noTeto) {
            avisoTeto.textContent = 'Dá pra lançar até ' + maximoDeJogos
                + ' jogos de uma vez. Salve estes e comece outro lançamento.';
        }
    }

    function numerarTodos() {
        blocos.forEach(function (b, i) { b.numerar(i, blocos.length); });
        atualizarSalvar();
    }

    function adicionarBloco() {
        var raiz = modelo.content.firstElementChild.cloneNode(true);
        lista.appendChild(raiz);
        blocos.push(criarBloco(raiz));
        numerarTodos();
        return raiz;
    }

    function removerBloco(bloco) {
        // O primeiro não sai: sem nenhum jogo a tela não tem o que salvar, e um "remover" que
        // esvazia a página inteira é um botão que só decepciona.
        if (blocos.length < 2) return;
        blocos = blocos.filter(function (b) { return b !== bloco; });
        bloco.raiz.remove();
        numerarTodos();
    }

    botaoMais.onclick = function () {
        var novo = adicionarBloco();
        novo.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    adicionarBloco();
}

// ── A DATA DO JOGO: UMA SÓ NA TELA ──────────────────────────────────────────────────────
//
// 20/08/2026, print de usuário: "aqui está confuso, para registrar o jogo, lá em cima tem uma
// data e abaixo outra". E estava mesmo. A linha de resumo ("26 ago. · Paladino") e o aviso
// amarelo ("o jogo marcado pra 26/08, que ainda não chegou") saíam prontos do servidor e
// NUNCA mais mudavam. Quem obedecia o aviso e corrigia a data no campo — pra 19/08, o dia em
// que de fato jogou — ficava olhando pra três coisas: resumo em 26, aviso em 26, campo em 19.
//
// ⚠️ Não é só feio: a data é o que fatia o ranking DA SEMANA. Diante da contradição, a leitura
// natural é "o de cima é o que vale" — e aí a pessoa desiste da correção e grava o jogo na
// semana errada, que é exatamente o estrago que o aviso existe pra evitar.
//
// A regra agora é uma frase: O CAMPO É A DATA. O resumo é a cara FECHADA do painel (os dois
// nunca dividem a tela) e o aviso é recalculado a cada mudança do campo. Nenhum texto de data
// vem do servidor — se viesse, seria a segunda data, e a segunda é sempre a que fica pra trás.
function pdzMontarDataDoJogo() {
    var campo = document.getElementById('pdzDataJogo');
    // Com menos de 4 pessoas a tela mostra só o recado e nenhum formulário — não há campo
    // nenhum pra ler, e desenhar resumo de uma data que não existe é como quebrar aqui.
    if (!campo) return;

    var clubes = document.getElementById('selectClube');
    var resumo = document.getElementById('pdzResumoDataLocal');
    var resumoData = document.getElementById('pdzResumoData');
    var resumoLocal = document.getElementById('pdzResumoLocal');
    var painel = document.getElementById('pdzOndeEQuando');
    var aviso = document.getElementById('pdzAvisoDataFutura');
    var avisoTexto = document.getElementById('pdzAvisoDataFuturaTexto');

    function doisDigitos(n) { return (n < 10 ? '0' : '') + n; }

    // ⚠️ COMPARAÇÃO DE TEXTO 'aaaa-mm-dd', e hoje montado peça por peça no fuso do celular.
    // `new Date('2026-08-26')` é lido como UTC: no Brasil vira 25 às 21h, e o "amanhã" da
    // pessoa passaria por hoje — o aviso apareceria (ou sumiria) um dia fora do lugar.
    function hoje() {
        var d = new Date();
        return d.getFullYear() + '-' + doisDigitos(d.getMonth() + 1) + '-' + doisDigitos(d.getDate());
    }

    function noFuturo() { return campo.value !== '' && campo.value > hoje(); }
    function aberto() { return !painel.classList.contains('d-none'); }
    function pedacos() { return campo.value.split('-'); }

    function diaEMes() {
        var p = pedacos();
        return p[2] + '/' + p[1];
    }

    function porExtenso() {
        var p = pedacos();
        return new Date(+p[0], +p[1] - 1, +p[2]).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
    }

    function desenhar() {
        var editando = aberto();

        resumo.classList.toggle('d-none', editando || !campo.value);
        if (!editando && campo.value) {
            resumoData.textContent = porExtenso();
            var op = clubes && clubes.selectedIndex >= 0 ? clubes.options[clubes.selectedIndex] : null;
            resumoLocal.textContent = op && op.value ? ' \u00b7 ' + op.text : '';
        }

        aviso.classList.toggle('d-none', !noFuturo());
        if (noFuturo()) {
            // O jeito de corrigir muda com o estado: mandar "toque em Alterar" com o campo já
            // aberto na frente da pessoa é mandá-la procurar um botão que não está mais lá.
            avisoTexto.innerHTML = editando
                ? 'A data escolhida, <strong>' + diaEMes() + '</strong>, ainda não chegou. '
                  + 'Se o placar é de um jogo que já aconteceu, corrija a data abaixo.'
                : 'Esse é o jogo marcado pra <strong>' + diaEMes() + '</strong>, que ainda não chegou. '
                  + 'Se o placar é de um jogo que já aconteceu, toque em <strong>Alterar</strong> e corrija a data.';
        }
    }

    document.getElementById('pdzBtnAlterar').onclick = function () {
        painel.classList.remove('d-none');
        desenhar();
    };
    document.getElementById('pdzFecharOndeEQuando').onclick = function () {
        painel.classList.add('d-none');
        desenhar();
    };

    // `change` é o que o seletor nativo do celular dispara ao confirmar; `input` cobre quem
    // digita a data no teclado, pra o aviso não ficar um passo atrás do que está escrito.
    campo.onchange = desenhar;
    campo.oninput = desenhar;
    if (clubes) clubes.onchange = desenhar;

    // DATA SUGERIDA NO FUTURO É DATA QUE PEDE CORREÇÃO: o placar de um jogo que ainda não
    // aconteceu não existe, então quem está aqui jogou noutro dia. Abrir o painel de saída põe
    // o campo debaixo do aviso — a correção vira um toque em vez de dois — e tira o resumo da
    // tela, que é o que fazia parecer haver duas datas concorrendo.
    if (noFuturo()) painel.classList.remove('d-none');
    desenhar();
}
