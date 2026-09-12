// O SALVAR DO PLACAR AO VIVO, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-placar-ao-vivo.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo do conferir-palpitometro.js: este repositório não tem
// npm (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 100 linhas.
//
// O QUE ELE GUARDA (Felipe, 12/09/2026: *"um dos marcadores está reclamando que ao marcar não
// está salvando na hora, pode ser internet ruim e os outros marcando junto"*):
//
//   O POST LEVA SÓ OS CARDS QUE FORAM TOCADOS. O formulário do lote reúne TODAS as quadras no
//   ar — os campos moram dentro dos cards e se ligam a ele por `form="pdzPlacaresAoVivo"` —,
//   então `new FormData(form)` junta o torneio inteiro. Com dois marcadores trabalhando, o
//   toque de um reescrevia a quadra do outro com o placar que a tela dele tinha: até 20
//   segundos velho, que é o intervalo da atualização automática. Quem perde o game é quem NÃO
//   tocou em nada, e não há erro em lugar nenhum pra denunciar.
//
//   ⚠️ O `FormData` daqui é fiel ao do navegador de propósito: recebendo o formulário, ele
//   recolhe todo campo ligado a ele, inclusive os que estão fora dele. É esse recolhimento que
//   era o defeito — um `FormData` de mentira mais simpático esconderia exatamente o que este
//   arquivo existe pra pegar.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/placar-ao-vivo.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────

// Um seletor simples: tag, #id, .classe e [atributo="valor"], sem combinador — é tudo o que o
// arquivo usa.
function analisar(sel) {
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

function casa(no, regra) {
    if (regra.tag && no.tagName !== regra.tag) return false;
    if (regra.id && no.getAttribute('id') !== regra.id) return false;
    if (regra.classes.some((c) => !no.classList.contains(c))) return false;
    return regra.attrs.every(({ nome, valor }) =>
        no.hasAttribute(nome) && (valor === null || no.getAttribute(nome) === valor));
}

function elemento(tag, atributos, filhos) {
    const attrs = Object.assign({}, atributos || {});
    const no = {
        tagName: tag.toUpperCase(),
        filhos: filhos || [],
        pai: null,
        textContent: '',
        hidden: false,
        disabled: false,
        select() { },
        getAttribute: (n) => (n in attrs ? String(attrs[n]) : null),
        setAttribute: (n, v) => { attrs[n] = String(v); },
        hasAttribute: (n) => n in attrs,
        removeAttribute: (n) => { delete attrs[n]; },
    };

    // ⚠️ `input.value` é SEMPRE string no navegador, inclusive depois de `campo.value = 4`
    // (o −/+ escreve número). Um DOM falso que guardasse o número cru faria uma comparação com
    // `String(...)` falhar aqui e passar em produção — ou o contrário, que é pior.
    let valor = attrs.value === undefined ? '' : String(attrs.value);
    Object.defineProperty(no, 'value', {
        get: () => valor,
        set: (v) => { valor = String(v); },
    });

    function classes() { return (attrs.class || '').split(/\s+/).filter(Boolean); }
    function escrever(lista) { attrs.class = lista.join(' '); }

    no.classList = {
        contains: (c) => classes().includes(c),
        add: (c) => { if (!classes().includes(c)) escrever(classes().concat(c)); },
        remove: (c) => escrever(classes().filter((x) => x !== c)),
        toggle: (c, liga) => (liga ? no.classList.add(c) : no.classList.remove(c)),
    };
    Object.defineProperty(no, 'className', {
        get: () => attrs.class || '',
        set: (v) => { attrs.class = v; },
    });
    Object.defineProperty(no, 'name', { get: () => (attrs.name === undefined ? '' : attrs.name) });
    // `form.id` é o que o ouvinte de `submit` compara — atributo de HTML e propriedade de
    // elemento são a mesma coisa no navegador, e um DOM falso que só tivesse o atributo
    // deixaria o ouvinte sair calado e o teste passar sem ter exercitado nada.
    Object.defineProperty(no, 'id', { get: () => (attrs.id === undefined ? '' : attrs.id) });

    no.descendentes = function () {
        const saida = [];
        for (const filho of no.filhos) saida.push(filho, ...filho.descendentes());
        return saida;
    };
    no.querySelectorAll = (sel) => {
        const regra = analisar(sel);
        return no.descendentes().filter((d) => casa(d, regra));
    };
    no.querySelector = (sel) => no.querySelectorAll(sel)[0] || null;
    no.closest = (sel) => {
        const regra = analisar(sel);
        for (let atual = no; atual; atual = atual.pai) if (casa(atual, regra)) return atual;
        return null;
    };

    for (const filho of no.filhos) filho.pai = no;
    return no;
}

// Um card AO VIVO como a view o desenha: o `partidaId` escondido, os pontos do tie-break e os
// dois contadores de games, todos ligados ao formulário do lote pelo atributo `form`.
function card(id, games1, games2, pontos1, pontos2) {
    const noLote = { form: 'pdzPlacaresAoVivo' };
    const contador = (nome, valor, teto) => elemento('span', { class: 'pdz-live-contador' }, [
        elemento('button', Object.assign({ class: 'pdz-live-passo', 'data-passo': '-1' }, noLote)),
        elemento('input', Object.assign({
            type: 'number', class: 'pdz-live-games pdz-live-input', name: nome, value: valor, max: teto,
        }, noLote)),
        elemento('button', Object.assign({ class: 'pdz-live-passo', 'data-passo': '1' }, noLote)),
    ]);

    return elemento('div', { class: 'pdz-live-card', 'data-partida-id': String(id) }, [
        elemento('div', { class: 'pdz-live-header' }, [
            elemento('span', { class: 'pdz-live-salvo' }),
            elemento('div', { class: 'pdz-live-placar' }, [
                elemento('input', Object.assign({ type: 'hidden', name: 'partidaId', value: String(id) }, noLote)),
                contador('games1', String(games1), '9'),
            ]),
            elemento('div', { class: 'pdz-live-placar' }, [contador('games2', String(games2), '9')]),
            // O bloco do tie-break nasce no HTML de toda fase que o comporta, escondido — e
            // campo escondido é campo do mesmo jeito (ver TieBreakNaTelaTests).
            elemento('div', { class: 'pdz-live-tiebreak', 'data-tiebreak': String(id) }, [
                contador('pontos1', String(pontos1 || 0), '99'),
                contador('pontos2', String(pontos2 || 0), '99'),
            ]),
        ]),
    ]);
}

function pagina(cards) {
    const proprios = [
        elemento('input', { type: 'hidden', name: '__RequestVerificationToken', value: 'tok' }),
        elemento('input', { type: 'hidden', name: 'id', value: '7' }),
        elemento('input', { type: 'hidden', name: 'voltarPara', value: 'Jogos' }),
    ];
    const form = elemento('form', { id: 'pdzPlacaresAoVivo' }, proprios);
    form.action = '/Torneios/SalvarPlacaresAoVivo';

    const corpo = elemento('body', {}, [form].concat(cards));

    // Como o navegador monta o `FormData` de um formulário: os campos DELE mais todos os que
    // apontam pra ele pelo atributo `form`, em ordem de documento.
    form.todosOsCampos = () => corpo.descendentes().filter((no) =>
        no.tagName === 'INPUT' && (no.pai === form || no.getAttribute('form') === 'pdzPlacaresAoVivo'));

    const ouvintes = {};
    const doc = {
        activeElement: null,
        addEventListener: (nome, fn) => { (ouvintes[nome] = ouvintes[nome] || []).push(fn); },
        getElementById: (id) => (id === 'pdzPlacaresAoVivo' ? form : null),
        querySelector: (sel) => corpo.querySelector(sel),
        querySelectorAll: (sel) => corpo.querySelectorAll(sel),
        disparar(nome, evento) { (ouvintes[nome] || []).forEach((fn) => fn(evento)); },
    };

    let relogio = [];
    let seq = 0;
    const chamadas = [];
    // Nulo = servidor fiel: devolve exatamente o que recebeu. É o que o de verdade faz quando
    // o placar cabe no formato da fase — e é a resposta contra a qual o card decide se diz
    // "salvo", então o padrão aqui não pode ser uma lista vazia.
    let resposta = null;
    let falhar = false;

    function ecoDoServidor(corpo) {
        const ids = corpo.getAll('partidaId');
        const games1 = corpo.getAll('games1');
        const games2 = corpo.getAll('games2');
        return {
            salvos: ids.length,
            placares: ids.map((id, i) => ({
                partidaId: Number(id),
                games1: Number(games1[i]),
                games2: Number(games2[i]),
                aoVivo: true,
            })),
        };
    }

    const janela = {
        setTimeout(fn, ms) { relogio.push({ id: ++seq, fn, ms }); return seq; },
        clearTimeout(id) { relogio = relogio.filter((t) => t.id !== id); },
        fetch(url, opcoes) {
            chamadas.push({ url, corpo: opcoes.body });
            if (falhar) return Promise.resolve({ ok: false, status: 500, headers: { get: () => 'text/html' } });
            return Promise.resolve({
                ok: true,
                headers: { get: () => 'application/json' },
                json: () => Promise.resolve(resposta ? resposta() : ecoDoServidor(opcoes.body)),
            });
        },
    };

    class FormDataFalso {
        constructor(form) {
            this._pares = [];
            if (form && form.todosOsCampos) {
                for (const campo of form.todosOsCampos()) this.append(campo.name, campo.value);
            }
        }
        append(nome, valor) { this._pares.push([nome, String(valor)]); }
        getAll(nome) { return this._pares.filter((p) => p[0] === nome).map((p) => p[1]); }
    }

    // O arquivo é uma IIFE que fala com `window`, `document` e o `FormData` global.
    new Function('window', 'document', 'self', 'FormData', fonte)(janela, doc, janela, FormDataFalso);

    return {
        doc,
        form,
        cards,
        chamadas,
        relogio: () => relogio,
        correrTimers() {
            const pendentes = relogio.slice().sort((a, b) => a.ms - b.ms);
            relogio = [];
            for (const t of pendentes) t.fn();
        },
        responder(fn) { resposta = fn; },
        derrubarARede(v) { falhar = v; },
    };
}

// Esperar as promessas do `fetch` assentarem — o arquivo encadeia `.then` de verdade.
const assentar = () => new Promise((r) => setImmediate(r));

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

// O corpo do POST número `i` — vazio quando ele nem aconteceu, pra uma falha não derrubar o
// conferidor antes de mostrar as outras.
function corpoDe(tela, i) {
    return (tela.chamadas[i] && tela.chamadas[i].corpo) || { getAll: () => [] };
}

function clicarNoMais(tela, card, nome) {
    const campo = card.querySelector('.pdz-live-input[name="' + nome + '"]');
    const botao = campo.pai.querySelectorAll('.pdz-live-passo')[1];
    tela.doc.disparar('click', { target: botao, preventDefault() { } });
}

(async function () {
    // 1. DOIS MARCADORES, DUAS QUADRAS: o toque numa não escreve na outra.
    {
        const a = card(101, 3, 2);
        const b = card(202, 5, 5);
        const tela = pagina([a, b]);

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();
        await assentar();

        conferir('um toque = um POST', tela.chamadas.length === 1);
        const corpo = corpoDe(tela, 0);
        conferir('o POST leva o card tocado', corpo.getAll('partidaId').join() === '101');
        conferir('e NÃO leva a quadra do vizinho', corpo.getAll('partidaId').indexOf('202') === -1);
        conferir('o game tocado vai com o valor novo', corpo.getAll('games1').join() === '4');
        // ⚠️ O lado que ninguém tocou vai como "não toquei", e não com o número da tela: é o
        // que impede dois aparelhos no MESMO jogo de se atropelarem (conferência 10).
        conferir('o lado não tocado do card viaja como -1', corpo.getAll('games2').join() === '-1');
        conferir('um pontos1 por card, casado por índice', corpo.getAll('pontos1').length === 1);
        conferir('o token do antiforgery vai sempre', corpo.getAll('__RequestVerificationToken').join() === 'tok');
        conferir('o id do torneio vai sempre', corpo.getAll('id').join() === '7');
        conferir('o card do vizinho ficou intocado na tela',
            b.querySelector('.pdz-live-input[name="games1"]').value === '5');
    }

    // 2. DOIS CARDS TOCADOS: os dois vão, e os arrays casam por índice.
    {
        const a = card(101, 3, 2);
        const b = card(202, 5, 5);
        const tela = pagina([a, b]);

        clicarNoMais(tela, a, 'games1');
        clicarNoMais(tela, b, 'games2');
        tela.correrTimers();
        await assentar();

        const corpo = corpoDe(tela, 0);
        conferir('os dois cards tocados vão no mesmo POST',
            corpo.getAll('partidaId').join() === '101,202');
        conferir('games1 casa por índice', corpo.getAll('games1').join() === '4,-1');
        conferir('games2 casa por índice', corpo.getAll('games2').join() === '-1,6');
        conferir('pontos1 tem uma entrada por card', corpo.getAll('pontos1').length === 2);
    }

    // 3. A BANDEIRA DO TOQUE POR ENTREGAR: enquanto o servidor não confirma, o card fica
    //    marcado — é o que a atualização automática lê pra não trocar o cabeçalho por baixo do
    //    número recém-tocado (js/jogos-ao-vivo-atualiza.js).
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);

        clicarNoMais(tela, a, 'games1');
        conferir('o card fica marcado no TOQUE, antes do POST sair', a.hasAttribute('data-pdz-mexido'));

        tela.correrTimers();
        await assentar();
        conferir('e a marca sai quando o servidor confirma', !a.hasAttribute('data-pdz-mexido'));
    }

    // 4. REDE CAÍDA: a marca FICA (o toque não chegou) e o card avisa.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);
        tela.derrubarARede(true);

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();
        await assentar();

        conferir('placar que não chegou continua marcado', a.hasAttribute('data-pdz-mexido'));
        conferir('e o card diz que não salvou',
            a.querySelector('.pdz-live-salvo').textContent.indexOf('não salvou') === 0);

        // E o "Salvar placares" é a retentativa: o card ainda mexido vai de novo.
        tela.derrubarARede(false);
        tela.doc.disparar('submit', { target: tela.form, preventDefault() { } });
        tela.correrTimers();
        await assentar();

        conferir('o Salvar placares reenvia o que ficou pra trás', tela.chamadas.length === 2);
        conferir('e só ele', corpoDe(tela, 1).getAll('partidaId').join() === '101');
        conferir('a marca sai depois que ele chega', !a.hasAttribute('data-pdz-mexido'));
    }

    // 5. TOQUE NO MEIO DO ENVIO: o segundo toque não se perde nem sai sem o primeiro.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();          // dispara o POST (ainda sem assentar)
        clicarNoMais(tela, a, 'games1');
        await assentar();
        tela.correrTimers();
        await assentar();

        conferir('o toque que chegou no meio do envio sai depois', tela.chamadas.length === 2);
        conferir('e leva o valor mais novo', corpoDe(tela, 1).getAll('games1').join() === '5');
        conferir('com a marca limpa no fim', !a.hasAttribute('data-pdz-mexido'));
    }

    // 6. SALVAR PLACARES SEM TER MEXIDO EM NADA: não manda POST nenhum. Mandar o formulário
    //    inteiro "por garantia" é exatamente o caminho que reescrevia a quadra do vizinho.
    {
        const a = card(101, 3, 2);
        const b = card(202, 5, 5);
        const tela = pagina([a, b]);

        tela.doc.disparar('submit', { target: tela.form, preventDefault() { } });
        await assentar();

        conferir('sem card mexido, o Salvar placares não posta nada', tela.chamadas.length === 0);
        conferir('e o card avisa que não havia o que salvar',
            a.querySelector('.pdz-live-salvo').textContent === 'nada mudou');
        conferir('o vizinho recebe o mesmo aviso',
            b.querySelector('.pdz-live-salvo').textContent === 'nada mudou');

        // E o aviso some sozinho, como o "salvo": o card volta a ser só o card.
        tela.correrTimers();
        conferir('e o aviso some sozinho', a.querySelector('.pdz-live-salvo').textContent === '');
    }

    // 7. O "SALVO" TEM QUE SER VERDADE (Felipe, 12/09/2026: *"estou clicando pra marcar,
    //    aparece salvo, mas nao salva as vezes"*). O servidor CORRIGE o que recebe — o teto da
    //    fase manda, e numa soma de 5 um 6 vira 4 —, e o card dizia "salvo" sem olhar a
    //    resposta. Pior: o número corrigido não chega nem à tela quando o dedo está no campo
    //    (não se escreve por baixo de quem digita), então o card ficava com o número que a
    //    pessoa marcou, a tarja verde e um placar diferente no banco.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);
        tela.responder(() => ({
            salvos: 1,
            placares: [{ partidaId: 101, games1: 4, games2: 2, aoVivo: true }],
        }));

        // O dedo fica no campo: é o caso em que a tela NÃO é corrigida e só a tarja pode avisar.
        clicarNoMais(tela, a, 'games1');   // manda 4... e o servidor devolve 4? não: pede 4, grava 4
        tela.correrTimers();
        await assentar();
        conferir('placar que o servidor gravou igual ao que foi mandado diz só "salvo"',
            a.querySelector('.pdz-live-salvo').textContent === 'salvo');
    }

    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);
        tela.responder(() => ({
            salvos: 1,
            placares: [{ partidaId: 101, games1: 2, games2: 2, aoVivo: true }],
        }));

        clicarNoMais(tela, a, 'games1');   // manda 4, o servidor grava 2
        tela.correrTimers();
        await assentar();

        conferir('placar corrigido pelo servidor aparece na tarja',
            a.querySelector('.pdz-live-salvo').textContent === 'salvo como 2 x 2');
    }

    // 8. JOGO QUE SAIU DO AR entre a tela carregar e o toque (alguém finalizou noutro
    //    aparelho): o servidor PULA o card calado, e o "salvo" era mentira inteira.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);
        tela.responder(() => ({
            salvos: 0,
            placares: [{ partidaId: 101, games1: 3, games2: 2, aoVivo: false }],
        }));

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();
        await assentar();

        conferir('jogo fora do ar não diz "salvo"',
            a.querySelector('.pdz-live-salvo').textContent === 'este jogo saiu do ar');
        conferir('e aparece como erro',
            a.querySelector('.pdz-live-salvo').className.indexOf('pdz-live-salvo-erro') !== -1);
        conferir('sem ficar na fila (tentar de novo não adianta)', !a.hasAttribute('data-pdz-mexido'));
    }

    // 9. RESPOSTA QUE NEM CITA O CARD: 200 com JSON, mas sem prova de que aquele jogo entrou.
    //    Silêncio não é confirmação — o card continua na fila e a tarja avisa.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);
        tela.responder(() => ({ salvos: 0, placares: [] }));

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();
        await assentar();

        conferir('card sem resposta do servidor não diz "salvo"',
            a.querySelector('.pdz-live-salvo').textContent === 'não salvou — toque de novo');
        conferir('e continua na fila', a.hasAttribute('data-pdz-mexido'));
    }

    // 10. DOIS MARCADORES NO MESMO JOGO, UM DE CADA LADO (Felipe, 12/09/2026: *"quando um de
    //     um lado marcava e o outro junto as vezes, um deles nao pegava"*). O card mandava os
    //     DOIS lados em todo POST, então o segundo a chegar devolvia o lado do primeiro pro
    //     número da tela dele — de até 20 segundos atrás. Agora o lado não tocado viaja como
    //     -1 e o servidor não encosta nele.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);

        clicarNoMais(tela, a, 'games1');
        tela.correrTimers();
        await assentar();

        const corpo = corpoDe(tela, 0);
        conferir('o lado tocado vai com o número', corpo.getAll('games1').join() === '4');
        conferir('o lado NÃO tocado vai como -1', corpo.getAll('games2').join() === '-1');
        conferir('e os pontos do tie-break também', corpo.getAll('pontos1').join() === '-1');
        conferir('nos dois lados', corpo.getAll('pontos2').join() === '-1');
    }

    // 11. TOCOU NOS DOIS LADOS: os dois vão com número. O "não toquei" é por CAMPO, não por card.
    {
        const a = card(101, 3, 2);
        const tela = pagina([a]);

        clicarNoMais(tela, a, 'games1');
        clicarNoMais(tela, a, 'games2');
        tela.correrTimers();
        await assentar();

        const corpo = corpoDe(tela, 0);
        conferir('tocou nos dois, os dois vão', corpo.getAll('games1').join() === '4'
            && corpo.getAll('games2').join() === '3');
    }

    // 12. O PONTO DO TIE-BREAK segue a mesma régua — no 8x8 cada mesário conta o ponto do seu
    //     lado, e mandar o do outro junto é reescrevê-lo.
    {
        const a = card(101, 8, 8, 5, 3);
        const tela = pagina([a]);

        clicarNoMais(tela, a, 'pontos1');
        tela.correrTimers();
        await assentar();

        const corpo = corpoDe(tela, 0);
        conferir('o ponto tocado vai com o número', corpo.getAll('pontos1').join() === '6');
        conferir('o ponto do outro lado vai como -1', corpo.getAll('pontos2').join() === '-1');
        conferir('e os games, que ninguém tocou, também', corpo.getAll('games1').join() === '-1'
            && corpo.getAll('games2').join() === '-1');
    }

    // 13. UMA ENTRADA POR CARD EM CADA ARRAY, sempre — é o que casa `partidaId[]` com
    //     `games1[]` por índice. Card sem o campo na tela (HTML velho em cache) não pode
    //     simplesmente sumir do array: ele desalinharia todos os jogos depois dele.
    {
        const a = card(101, 3, 2);
        const b = card(202, 5, 5);
        const tela = pagina([a, b]);

        clicarNoMais(tela, a, 'games1');
        clicarNoMais(tela, b, 'games2');
        tela.correrTimers();
        await assentar();

        const corpo = corpoDe(tela, 0);
        conferir('dois cards, duas entradas em cada array',
            corpo.getAll('partidaId').length === 2 && corpo.getAll('games1').length === 2
            && corpo.getAll('games2').length === 2 && corpo.getAll('pontos1').length === 2);
        conferir('e cada um só com o lado que tocou',
            corpo.getAll('games1').join() === '4,-1' && corpo.getAll('games2').join() === '-1,6');
    }

    console.log('');
    console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
    process.exit(falhas === 0 ? 0 : 1);
})();
