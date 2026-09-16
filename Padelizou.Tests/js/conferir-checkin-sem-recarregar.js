// O CHECK-IN QUE NÃO RECARREGA A PÁGINA, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-checkin-sem-recarregar.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo dos outros conferidores: este repositório não tem npm.
//
// ── O QUE ELE GUARDA ──────────────────────────────────────────────────────────────────────
//
// 🗣️ Felipe, 15/09/2026: *"no checkin, ao clicar para marcar, nao deveria atualizar a pagina
// inteira, como estava acontecendo, isso foi alterado ?"* e, logo em seguida, *"sim, faça. o
// jogo tem q subir na hora"*.
//
// 🕳️ NÃO TINHA SIDO. Cada bolinha era um POST → 302 → GET da página inteira: mais de 1MB no
// torneio de 97 jogos, a cada um dos quatro cliques de um jogo. O que existia era o
// `data-manter-posicao`, que devolve a rolagem pra mesma altura DEPOIS da recarga — disfarce
// do sintoma, não a cura: a página some e renasce, o <iframe> da transmissão reinicia junto,
// e na internet do clube isso é uma espera por clique.
//
// ✅ Agora o clique não navega: o POST vai por `fetch`, a bolinha pinta na hora, e a LISTA
// NOVA é pedida uma vez só, no fim da rajada de cliques — é ela que faz o jogo completo subir
// pro topo do horário (Services/OrdemNoHorario). A ordem continua sendo a do servidor.
//
// ⚠️ AS TRÊS ARMADILHAS QUE ESTE ARQUIVO EXISTE PRA PEGAR:
//
//   1. **`resposta.ok` NÃO é prova de que gravou.** Sessão vencida responde 302 pra tela de
//      login, o `fetch` segue o desvio e entrega 200 com o HTML do login. Um check pintado de
//      verde sem linha no banco é pior que o recarregamento. Quem prova é o **204**.
//   2. **A lista não pode ser pedida no meio da rajada.** Quem marca os quatro jogadores de um
//      jogo dispara quatro POSTs em dois segundos. Uma lista pedida entre eles volta sem os
//      cliques que ainda estão no ar, e a bolinha que a pessoa acabou de pintar PISCA de volta
//      pra cinza. Por isso a bandeira `window.pdzMarcandoCheckIn` e o pedido único no fim.
//   3. **Falha tem que APARECER.** Rede caída = a bolinha volta ao que era, em vermelho e com
//      o aviso — nunca um check que a tela mostra e o banco não tem.
const fs = require('fs');

const fonte = fs.readFileSync(
    process.argv[2] || 'Padelizou/wwwroot/js/checkin-sem-recarregar.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────

// Um seletor simples: tag, #id, .classe e [atributo="valor"], sem combinador — menos o
// DESCENDENTE, que o arquivo usa (`form[data-pdz-checkin] button`) e que é o que faz o clique
// no <i> de dentro do botão achar o formulário certo.
function analisarSimples(sel) {
    const regra = { tag: null, id: null, classes: [], attrs: [] };
    const resto = sel.trim().replace(/\[([\w-]+)(?:=["']?([^\]"']*)["']?)?\]/g, (_, nome, valor) => {
        regra.attrs.push({ nome, valor: valor === undefined ? null : valor });
        return '';
    });
    for (const parte of resto.split(/(?=[.#])/)) {
        if (!parte) continue;
        if (parte[0] === '.') regra.classes.push(parte.slice(1));
        else if (parte[0] === '#') regra.id = parte.slice(1);
        else regra.tag = parte.toUpperCase();
    }
    return regra;
}

function casaSimples(no, regra) {
    if (regra.tag && no.tagName !== regra.tag) return false;
    if (regra.id && no.getAttribute('id') !== regra.id) return false;
    if (regra.classes.some((c) => !no.classList.contains(c))) return false;
    return regra.attrs.every(({ nome, valor }) =>
        no.hasAttribute(nome) && (valor === null || no.getAttribute(nome) === valor));
}

// `a b` — o último pedaço casa no nó, os anteriores em algum ancestral, na ordem.
function casa(no, sel) {
    const partes = sel.trim().split(/\s+/).map(analisarSimples);
    if (!casaSimples(no, partes[partes.length - 1])) return false;
    let atual = no.pai;
    for (let i = partes.length - 2; i >= 0; i--) {
        while (atual && !casaSimples(atual, partes[i])) atual = atual.pai;
        if (!atual) return false;
        atual = atual.pai;
    }
    return true;
}

function elemento(tag, atributos, filhos) {
    const attrs = Object.assign({}, atributos || {});
    const no = {
        tagName: tag.toUpperCase(),
        filhos: filhos || [],
        pai: null,
        getAttribute: (n) => (n in attrs ? String(attrs[n]) : null),
        setAttribute: (n, v) => { attrs[n] = String(v); },
        hasAttribute: (n) => n in attrs,
        removeAttribute: (n) => { delete attrs[n]; },
    };

    // ⚠️ `input.value` é SEMPRE string no navegador. É por ele que passa a INTENÇÃO do clique
    // ("true"/"false"), então um DOM falso que guardasse outra coisa esconderia o defeito.
    let valor = attrs.value === undefined ? '' : String(attrs.value);
    Object.defineProperty(no, 'value', { get: () => valor, set: (v) => { valor = String(v); } });

    function classes() { return (attrs.class || '').split(/\s+/).filter(Boolean); }
    function escrever(lista) { attrs.class = lista.join(' '); }
    no.classList = {
        contains: (c) => classes().includes(c),
        add: (c) => { if (!classes().includes(c)) escrever(classes().concat(c)); },
        remove: (c) => escrever(classes().filter((x) => x !== c)),
        toggle: (c, liga) => (liga ? no.classList.add(c) : no.classList.remove(c)),
    };

    no.descendentes = function () {
        const saida = [];
        for (const filho of no.filhos) saida.push(filho, ...filho.descendentes());
        return saida;
    };
    no.querySelectorAll = (sel) => no.descendentes().filter((d) => casa(d, sel));
    no.querySelector = (sel) => no.querySelectorAll(sel)[0] || null;
    no.closest = (sel) => {
        for (let atual = no; atual; atual = atual.pai) if (casa(atual, sel)) return atual;
        return null;
    };

    for (const filho of no.filhos) filho.pai = no;
    return no;
}

// O formulário como o `_BotaoDoCheckIn.cshtml` o desenha: a bolinha de 30px, o campo escondido
// com a INTENÇÃO do próximo clique, e o rótulo do OUTRO estado guardado pra troca.
function bolinha(jogadorId, partidaId, chegou) {
    const icone = elemento('i', { class: chegou ? 'bi bi-check-circle-fill' : 'bi bi-circle' });
    const botao = elemento('button', {
        type: 'submit',
        class: chegou ? 'pdz-jl-checkin pdz-jl-checkin-chegou' : 'pdz-jl-checkin',
        'aria-label': chegou ? 'Ana chegou 14:32 — toque pra desfazer' : 'Marcar que Ana chegou',
        title: chegou ? 'Ana chegou 14:32 — toque pra desfazer' : 'Marcar que Ana chegou',
        'data-rotulo-outro': chegou ? 'Marcar que Ana chegou' : 'Ana chegou — toque pra desfazer',
    }, [icone]);

    const form = elemento('form', { 'data-pdz-checkin': '', 'data-manter-posicao': '' }, [
        elemento('input', { type: 'hidden', name: '__RequestVerificationToken', value: 'tok' }),
        elemento('input', { type: 'hidden', name: 'jogadorId', value: String(jogadorId) }),
        elemento('input', { type: 'hidden', name: 'partidaId', value: String(partidaId) }),
        // O Razor escreve aqui o CONTRÁRIO do estado de agora: é a intenção do clique.
        elemento('input', { type: 'hidden', name: 'presente', value: chegou ? 'false' : 'true' }),
        elemento('input', { type: 'hidden', name: 'voltarPara', value: 'Jogos' }),
        botao,
    ]);
    form.action = '/Torneios/MarcarCheckIn';
    form.botao = botao;
    form.icone = icone;
    return form;
}

function pagina(forms, opcoes) {
    const op = opcoes || {};
    const corpo = elemento('body', {}, forms);

    const ouvintes = {};
    const doc = {
        addEventListener: (nome, fn) => { (ouvintes[nome] = ouvintes[nome] || []).push(fn); },
        querySelector: (sel) => corpo.querySelector(sel),
        querySelectorAll: (sel) => corpo.querySelectorAll(sel),
        disparar(nome, evento) { (ouvintes[nome] || []).forEach((fn) => fn(evento)); },
    };

    const pedidos = [];           // os POSTs que saíram
    const respostas = [];         // resolvedores, pra soltar um de cada vez
    const pedidosDeLista = [];    // cada chamada ao atualizador
    let relogio = [];
    let seq = 0;
    let listaAceita = op.listaAceita === undefined ? true : op.listaAceita;

    const janela = {
        setTimeout(fn, ms) { relogio.push({ id: ++seq, fn, ms }); return seq; },
        clearTimeout(id) { relogio = relogio.filter((t) => t.id !== id); },
        fetch(url, opcoes) {
            let soltar;
            const p = new Promise((r) => { soltar = r; });
            pedidos.push({ url, corpo: opcoes.body, opcoes });
            respostas.push(soltar);
            return p;
        },
        // A porta de entrada do js/jogos-ao-vivo-atualiza.js: devolve `false` quando a tela
        // está ocupada e a busca não saiu.
        pdzAtualizarAListaDeJogos() { pedidosDeLista.push(Date.now()); return listaAceita; },
    };

    class FormDataFalso {
        constructor(form) {
            this._pares = [];
            if (form) {
                for (const campo of form.descendentes()) {
                    if (campo.tagName === 'INPUT') this.append(campo.getAttribute('name'), campo.value);
                }
            }
        }
        append(nome, valor) { this._pares.push([nome, String(valor)]); }
        getAll(nome) { return this._pares.filter((p) => p[0] === nome).map((p) => p[1]); }
    }

    new Function('window', 'document', 'FormData', fonte)(janela, doc, FormDataFalso);

    return {
        doc,
        pedidos,
        pedidosDeLista,
        janela,
        clicar(form, alvo) {
            const evento = {
                target: alvo || form.icone, button: 0, defaultPrevented: false,
                _barrou: 0, preventDefault() { this._barrou++; this.defaultPrevented = true; },
            };
            doc.disparar('click', evento);
            return evento;
        },
        // O servidor responde ao POST número `i`. 204 = gravou.
        responder(i, status) {
            if (respostas[i]) respostas[i]({ ok: status >= 200 && status < 300, status });
        },
        aceitarALista(v) { listaAceita = v; },
        correrTimers() {
            const pendentes = relogio.slice().sort((a, b) => a.ms - b.ms);
            relogio = [];
            for (const t of pendentes) t.fn();
        },
        temTimer: () => relogio.length > 0,
    };
}

// Esperar as promessas do `fetch` assentarem — o arquivo encadeia `.then` de verdade.
const assentar = () => new Promise((r) => setImmediate(r));

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function ok(nome, condicao) {
    console.log((condicao ? '  ok   · ' : ' FALHA · ') + nome);
    if (!condicao) falhas++;
}

function corpoDe(tela, i) {
    return (tela.pedidos[i] && tela.pedidos[i].corpo) || { getAll: () => [] };
}

// O POST número `i` — um esqueleto vazio quando ele nem aconteceu, pra uma falha não derrubar
// o conferidor antes de mostrar as outras.
function pedidoDe(tela, i) {
    return tela.pedidos[i] || { url: null, opcoes: { headers: {} } };
}

(async function () {
    console.log('\n── O CLIQUE NÃO RECARREGA A PÁGINA ──────────────────────────────────────────');
    {
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);

        // O dedo encosta no <i> de dentro do botão, que é o caso do celular.
        const evento = tela.clicar(form, form.icone);

        ok('o clique é barrado: nada de POST → 302 → página inteira', evento._barrou === 1);
        ok('um clique = um POST', tela.pedidos.length === 1);
        ok('o POST vai pro MarcarCheckIn', pedidoDe(tela, 0).url === '/Torneios/MarcarCheckIn');
        ok('e por POST mesmo', pedidoDe(tela, 0).opcoes.method === 'POST');
        ok('com o cabeçalho que pede a resposta curta',
            (pedidoDe(tela, 0).opcoes.headers || {})['X-Requested-With'] === 'XMLHttpRequest');
        ok('com o cookie da sessão', pedidoDe(tela, 0).opcoes.credentials === 'same-origin');

        const corpo = corpoDe(tela, 0);
        ok('o POST leva o jogador', corpo.getAll('jogadorId').join() === '7');
        ok('o POST leva o JOGO (e não o torneio)', corpo.getAll('partidaId').join() === '33');
        ok('o POST leva a intenção do clique', corpo.getAll('presente').join() === 'true');
        ok('o token do antiforgery vai junto', corpo.getAll('__RequestVerificationToken').join() === 'tok');
        ok('e o recorte da tela também', corpo.getAll('voltarPara').join() === 'Jogos');
    }

    console.log('\n── A BOLINHA PINTA NA HORA, ANTES DA RESPOSTA ───────────────────────────────');
    {
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);
        tela.clicar(form);

        ok('a bolinha fica verde na hora do dedo', form.botao.classList.contains('pdz-jl-checkin-chegou'));
        ok('e o ícone vira o check cheio', form.icone.classList.contains('bi-check-circle-fill'));
        ok('sem sobrar o círculo vazio por baixo', !form.icone.classList.contains('bi-circle'));
        ok('o rótulo passa a ser o de desfazer',
            form.botao.getAttribute('aria-label') === 'Ana chegou — toque pra desfazer');
        ok('e o rótulo do outro estado fica guardado pro próximo clique',
            form.botao.getAttribute('data-rotulo-outro') === 'Marcar que Ana chegou');
        ok('o campo escondido já carrega a intenção do PRÓXIMO clique',
            form.querySelector('input[name="presente"]').value === 'false');

        // E o clique seguinte desmarca.
        tela.responder(0, 204);
        await assentar();
        tela.clicar(form);
        ok('o segundo clique manda desmarcar', corpoDe(tela, 1).getAll('presente').join() === 'false');
        ok('e a bolinha volta a cinza na hora', !form.botao.classList.contains('pdz-jl-checkin-chegou'));
        ok('com o círculo vazio de volta', form.icone.classList.contains('bi-circle'));
    }

    console.log('\n── 204 É A PROVA DE QUE GRAVOU ──────────────────────────────────────────────');
    {
        // Sessão vencida: 302 pra tela de login, o fetch segue e entrega 200 com HTML. Um
        // `resposta.ok` daria isso como salvo — e a bolinha ficaria verde sem linha no banco.
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);
        tela.clicar(form);
        tela.responder(0, 200);
        await assentar();

        ok('200 com HTML (sessão vencida) NÃO conta como salvo',
            !form.botao.classList.contains('pdz-jl-checkin-chegou'));
        ok('a bolinha volta ao que era', form.icone.classList.contains('bi-circle'));
        ok('em vermelho, pra falha não passar calada',
            form.botao.classList.contains('pdz-jl-checkin-erro'));
        ok('com o aviso escrito no lugar do rótulo',
            /não salvou/.test(form.botao.getAttribute('aria-label') || ''));
        tela.correrTimers();
        ok('e a lista NÃO é pedida por um clique que não gravou', tela.pedidosDeLista.length === 0);
        ok('o campo escondido volta a pedir "marcar"',
            form.querySelector('input[name="presente"]').value === 'true');
    }

    {
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);
        tela.clicar(form);
        tela.responder(0, 403);
        await assentar();
        ok('403 (não é organizador) também desfaz a pintura',
            !form.botao.classList.contains('pdz-jl-checkin-chegou'));
        ok('e avisa', form.botao.classList.contains('pdz-jl-checkin-erro'));
    }

    {
        // E o clique seguinte limpa o vermelho: o aviso é de UM clique, não uma marca eterna.
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);
        tela.clicar(form);
        tela.responder(0, 500);
        await assentar();
        tela.clicar(form);
        ok('tocar de novo tira o vermelho e tenta de novo',
            !form.botao.classList.contains('pdz-jl-checkin-erro') && tela.pedidos.length === 2);
    }

    console.log('\n── O JOGO SOBE NA HORA, E SÓ NO FIM DA RAJADA ───────────────────────────────');
    {
        // 🗣️ *"o jogo tem q subir na hora"*. Quem faz o jogo completo subir pro topo do horário
        // é a lista nova do servidor (Services/OrdemNoHorario) — pedida UMA vez, logo depois do
        // último POST da rajada.
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);
        tela.clicar(form);
        ok('enquanto o POST está no ar, a lista não é pedida', tela.pedidosDeLista.length === 0);
        ok('e a bandeira avisa o atualizador pra não trocar a tela agora',
            tela.janela.pdzMarcandoCheckIn === true);

        tela.responder(0, 204);
        await assentar();
        ok('e a bandeira baixa assim que o POST volta', !tela.janela.pdzMarcandoCheckIn);
        ok('a lista fica AGENDADA, não disparada no mesmo instante', tela.temTimer());

        tela.correrTimers();
        ok('gravou: a lista nova vem sem esperar o ciclo de 20s', tela.pedidosDeLista.length === 1);
    }

    {
        // ⚠️ O SEGUNDO CLIQUE REARMA A ESPERA. Medido no navegador antes desta trava: quatro
        // bolinhas marcadas com 120ms entre elas custaram TRÊS buscas da lista inteira (368kB
        // cada) — uma por clique, porque num servidor local o POST volta antes do toque
        // seguinte. Agrupando, a rajada inteira custa UMA.
        const a = bolinha(1, 33, false);
        const b = bolinha(2, 33, false);
        const tela = pagina([a, b]);

        tela.clicar(a);
        tela.responder(0, 204);
        await assentar();

        tela.clicar(b);                 // chega DENTRO da espera
        ok('o clique novo cancela a lista que estava agendada', !tela.temTimer());
        tela.responder(1, 204);
        await assentar();

        tela.correrTimers();
        ok('e os dois cliques juntos pedem a lista UMA vez só', tela.pedidosDeLista.length === 1);
    }

    {
        // OS QUATRO JOGADORES DE UM JOGO, em rajada. Uma lista pedida no meio volta sem os
        // cliques que ainda estão no ar — e a bolinha recém-pintada PISCA de volta pra cinza.
        const forms = [bolinha(1, 33, false), bolinha(2, 33, false),
                       bolinha(3, 33, false), bolinha(4, 33, false)];
        const tela = pagina(forms);
        forms.forEach((f) => tela.clicar(f));

        ok('quatro cliques = quatro POSTs', tela.pedidos.length === 4);

        // As respostas voltam fora de ordem, que é o normal com quatro no ar.
        tela.responder(2, 204);
        await assentar();
        tela.responder(0, 204);
        await assentar();
        tela.responder(3, 204);
        await assentar();
        ok('com POST ainda no ar, nenhuma lista é pedida', tela.pedidosDeLista.length === 0);
        ok('e a bandeira segue de pé', tela.janela.pdzMarcandoCheckIn === true);

        tela.responder(1, 204);
        await assentar();
        tela.correrTimers();
        ok('a rajada inteira pede a lista UMA vez só, no fim', tela.pedidosDeLista.length === 1);
        ok('e as quatro bolinhas ficaram verdes',
            forms.every((f) => f.botao.classList.contains('pdz-jl-checkin-chegou')));
    }

    {
        // A tela estava ocupada (placar indo pro servidor, modal aberto): o atualizador recusa.
        // Insistir é o certo — desistir deixaria o jogo sem subir até o tique de 20 segundos.
        const form = bolinha(7, 33, false);
        const tela = pagina([form], { listaAceita: false });
        tela.clicar(form);
        tela.responder(0, 204);
        await assentar();
        tela.correrTimers();

        ok('atualizador ocupado: o pedido foi feito e recusado', tela.pedidosDeLista.length === 1);
        ok('e fica um relógio pra insistir', tela.temTimer());

        tela.aceitarALista(true);
        tela.correrTimers();
        ok('na segunda tentativa a lista sai', tela.pedidosDeLista.length === 2);
        ok('e aí para de insistir', !tela.temTimer());
    }

    console.log('\n── O QUE NÃO PODE SER INTERCEPTADO ─────────────────────────────────────────');
    {
        const form = bolinha(7, 33, false);
        const tela = pagina([form]);

        const doMeio = { target: form.botao, button: 1, defaultPrevented: false, _barrou: 0, preventDefault() { this._barrou++; } };
        tela.doc.disparar('click', doMeio);
        ok('botão do meio não vira check-in', doMeio._barrou === 0 && tela.pedidos.length === 0);

        const jaBarrado = { target: form.botao, button: 0, defaultPrevented: true, _barrou: 0, preventDefault() { this._barrou++; } };
        tela.doc.disparar('click', jaBarrado);
        ok('clique que outro já tratou é deixado em paz', tela.pedidos.length === 0);

        const fora = elemento('button', { class: 'btn' });
        elemento('div', {}, [fora]);
        const solto = { target: fora, button: 0, defaultPrevented: false, _barrou: 0, preventDefault() { this._barrou++; } };
        tela.doc.disparar('click', solto);
        ok('botão que não é de check-in passa reto', solto._barrou === 0 && tela.pedidos.length === 0);
    }

    {
        // Navegador sem `fetch` (ou o arquivo carregado num WebView velho): o formulário de
        // sempre resolve, com o `data-manter-posicao` devolvendo a rolagem. Barrar o clique
        // sem ter como enviar seria o pior dos mundos — o check simplesmente não aconteceria.
        const form = bolinha(7, 33, false);
        const corpo = elemento('body', {}, [form]);
        const ouvintes = {};
        const doc = {
            addEventListener: (n, f) => { (ouvintes[n] = ouvintes[n] || []).push(f); },
            querySelector: (s) => corpo.querySelector(s),
            querySelectorAll: (s) => corpo.querySelectorAll(s),
        };
        new Function('window', 'document', 'FormData', fonte)({ setTimeout() { } }, doc, function () { });

        const evento = { target: form.botao, button: 0, defaultPrevented: false, _barrou: 0, preventDefault() { this._barrou++; } };
        (ouvintes.click || []).forEach((fn) => fn(evento));
        ok('sem fetch, o clique segue pro POST de sempre', evento._barrou === 0);
    }

    console.log('');
    if (falhas > 0) {
        console.log('✗ ' + falhas + ' conferência(s) falharam.');
        process.exit(1);
    }
    console.log('✓ tudo certo.');
})();
