// A TELA NÃO SAI DEBAIXO DE QUEM ESTÁ OLHANDO, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-abas-que-ficam.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo dos outros conferidores: este repositório não tem npm.
//
// ── O QUE ELE GUARDA ──────────────────────────────────────────────────────────────────────
//
// 🗣️ Felipe, 12/09/2026, três vezes no mesmo dia: *"as vezes to olhando as finalizadas e ele
// automaticamente volta para tela do ao vivo"* · *"ao mudar algum filtro, as vezes sai da tela
// que esta"* · *"estava mexendo na aba palpiteiros e sozinho foi para o aovivo, isso nao pode
// acontecer, ele tem q se manter na tela q esta, a menos q o usuario clique em algo"*.
//
// 🕳️ DOIS DEFEITOS SOMADOS, e o primeiro é medido: no HTML entregue da página do torneio o
// `jogos-abas.js` sai na linha 3941 e o `bootstrap.bundle.js` na 4736 — o script da MEMÓRIA DE
// ABA roda 795 linhas antes de o Bootstrap existir, cai no `if (!window.bootstrap) return` e
// nunca registra o ouvinte. Conferido no navegador: depois de clicar em "Finalizadas", o
// `sessionStorage` continua VAZIO e o `#jogosTabs` tem ZERO ouvintes. A memória de aba, escrita
// em 08/08/2026, nunca funcionou nessa tela.
//
// 🕳️ O segundo é quem PUXA o gatilho: o atualizador de 20 em 20 segundos recarrega a página
// inteira quando a lista de jogos em quadra muda (jogo entrou, jogo acabou) — e num sábado isso
// é o tempo todo. Some com a pessoa de onde ela estava: da aba Palpiteiros, das Finalizadas, de
// qualquer lugar. Sem a memória de aba pra devolver o lugar, o estrago é completo.
//
// ⚠️ A ORDEM DOS SCRIPTS É PARTE DO TESTE. Este arquivo roda o `jogos-abas.js` **sem**
// `window.bootstrap` definido e só depois define — igual à página real. Um teste que definisse
// o Bootstrap antes passaria com o defeito de pé, que é o que aconteceu por um mês.
const fs = require('fs');

let falhas = 0;
function ok(condicao, texto) {
    console.log((condicao ? '  ok  ' : ' FALHA') + ' · ' + texto);
    if (!condicao) falhas++;
}

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function classList(inicial) {
    const set = new Set(inicial || []);
    return { add: (c) => set.add(c), remove: (c) => set.delete(c), contains: (c) => set.has(c) };
}

function elemento(atributos, classes) {
    const el = {
        _atributos: atributos || {},
        classList: classList(classes),
        _ouvintes: {},
        getAttribute: (n) => (n in el._atributos ? el._atributos[n] : null),
        setAttribute: (n, v) => { el._atributos[n] = v; },
        addEventListener: (tipo, fn) => { (el._ouvintes[tipo] = el._ouvintes[tipo] || []).push(fn); },
        disparar: (tipo, evento) => (el._ouvintes[tipo] || []).forEach((fn) => fn(evento)),
        querySelector: () => null,
        querySelectorAll: () => [],
    };
    return el;
}

// Uma barra de abas com botões; `data-bs-target` é o que o script guarda.
function barra(id, alvos, ativo, extras) {
    const botoes = alvos.map((alvo) =>
        elemento(Object.assign({ 'data-bs-target': alvo }, extras || {}), alvo === ativo ? ['nav-link', 'active'] : ['nav-link']));
    const ul = elemento({ 'data-torneio-id': '901' });
    ul.querySelector = (sel) => {
        const m = /\[data-bs-target="([^"]+)"\]/.exec(sel);
        return m ? botoes.find((b) => b.getAttribute('data-bs-target') === m[1]) || null : null;
    };
    ul.querySelectorAll = () => botoes;
    ul.id = id;
    ul._botoes = botoes;
    return ul;
}

function montarJanela(barras, comBootstrap) {
    const guardado = {};
    const mostradas = [];
    const doc = {
        readyState: 'loading',
        _ouvintes: {},
        getElementById: (id) => barras[id] || null,
        querySelector: () => null,
        addEventListener: (tipo, fn) => { (doc._ouvintes[tipo] = doc._ouvintes[tipo] || []).push(fn); },
        disparar: (tipo) => (doc._ouvintes[tipo] || []).forEach((fn) => fn({})),
    };
    const win = {
        document: doc,
        sessionStorage: {
            getItem: (k) => (k in guardado ? guardado[k] : null),
            setItem: (k, v) => { guardado[k] = String(v); },
            removeItem: (k) => { delete guardado[k]; },
        },
        _guardado: guardado,
        _mostradas: mostradas,
        addEventListener: (tipo, fn) => doc.addEventListener(tipo, fn),
    };
    if (comBootstrap) win.bootstrap = bootstrapFalso(mostradas);
    return win;
}

function bootstrapFalso(mostradas) {
    return {
        Tab: {
            getOrCreateInstance: (botao) => ({
                show: () => {
                    mostradas.push(botao.getAttribute('data-bs-target'));
                    botao.classList.add('active');
                    // O Bootstrap de verdade avisa a barra depois de mostrar.
                    if (botao._barra) botao._barra.disparar('shown.bs.tab', { target: botao });
                },
            }),
        },
    };
}

function rodar(fonte, win) {
    // `window`, `document` e `bootstrap` como globais, que é como o script os vê no navegador.
    const f = new Function('window', 'document', 'sessionStorage', 'bootstrap', fonte);
    f(win, win.document, win.sessionStorage, win.bootstrap);
}

const FONTE_ABAS = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/jogos-abas.js', 'utf8');

console.log('\n── A ABA ESCOLHIDA FICA GUARDADA ────────────────────────────────────────────');

(function aOrdemDeProducao() {
    // A ORDEM REAL DA PÁGINA: o script roda ANTES do bootstrap.bundle.js (medido: linha 3941
    // contra 4736) e antes do DOM terminar. Se ele desistir aqui, a memória nunca existe.
    const jogos = barra('jogosTabs', ['#aovivo', '#agendadas', '#finalizadas'], '#aovivo');
    const win = montarJanela({ jogosTabs: jogos }, false);

    rodar(FONTE_ABAS, win);

    // Só DEPOIS o Bootstrap chega e a página fica pronta — exatamente como no navegador.
    win.bootstrap = bootstrapFalso(win._mostradas);
    win.document.readyState = 'complete';
    win.document.disparar('DOMContentLoaded');

    // A pessoa clica em "Finalizadas".
    jogos.disparar('shown.bs.tab', { target: jogos._botoes[2] });

    ok(win._guardado['pdz-aba-jogos:901'] === '#finalizadas',
        'clicar em Finalizadas guarda a escolha mesmo com o script rodando antes do Bootstrap');
})();

(function devolveOLugarNaVoltaDaPagina() {
    const jogos = barra('jogosTabs', ['#aovivo', '#agendadas', '#finalizadas'], '#aovivo');
    const win = montarJanela({ jogosTabs: jogos }, false);
    jogos._botoes.forEach((b) => { b._barra = jogos; });
    win.sessionStorage.setItem('pdz-aba-jogos:901', '#finalizadas');

    rodar(FONTE_ABAS, win);
    win.bootstrap = bootstrapFalso(win._mostradas);
    win.document.readyState = 'complete';
    win.document.disparar('DOMContentLoaded');

    ok(win._mostradas.indexOf('#finalizadas') >= 0,
        'a página que renasce no Ao Vivo volta pra Finalizadas, que é onde a pessoa estava');
})();

(function aAbaDeCimaTambem() {
    // 🗣️ *"estava mexendo na aba palpiteiros e sozinho foi para o aovivo"*. A memória cobria só
    // as sub-abas; a barra de cima (Inscritos, Gerenciar, Jogos, Chaves, Times, Palpiteiros)
    // não era lembrada por ninguém, então todo recarregamento devolvia a pessoa pra Jogos.
    const torneio = barra('torneioTabs', ['#jogosDoTorneio', '#palpiteiros'], '#jogosDoTorneio');
    const jogos = barra('jogosTabs', ['#aovivo', '#agendadas'], '#aovivo');
    torneio._botoes.forEach((b) => { b._barra = torneio; });
    const win = montarJanela({ torneioTabs: torneio, jogosTabs: jogos }, false);

    rodar(FONTE_ABAS, win);
    win.bootstrap = bootstrapFalso(win._mostradas);
    win.document.readyState = 'complete';
    win.document.disparar('DOMContentLoaded');

    torneio.disparar('shown.bs.tab', { target: torneio._botoes[1] });
    ok(win._guardado['pdz-aba-torneio:901'] === '#palpiteiros',
        'a aba de cima (Palpiteiros) também é guardada');

    // Nova visita: a página nasce em Jogos e tem que voltar pra Palpiteiros.
    const torneio2 = barra('torneioTabs', ['#jogosDoTorneio', '#palpiteiros'], '#jogosDoTorneio');
    const jogos2 = barra('jogosTabs', ['#aovivo', '#agendadas'], '#aovivo');
    const win2 = montarJanela({ torneioTabs: torneio2, jogosTabs: jogos2 }, false);
    win2.sessionStorage.setItem('pdz-aba-torneio:901', '#palpiteiros');

    rodar(FONTE_ABAS, win2);
    win2.bootstrap = bootstrapFalso(win2._mostradas);
    win2.document.readyState = 'complete';
    win2.document.disparar('DOMContentLoaded');

    ok(win2._mostradas.indexOf('#palpiteiros') >= 0,
        'quem estava em Palpiteiros volta pra Palpiteiros, não pro Ao Vivo');
})();

(function semMemoriaNaoQuebra() {
    // Navegação privada com cookies bloqueados: `sessionStorage` ESTOURA no acesso. Falhar aqui
    // não pode derrubar a aba — sem memória, vale o padrão do servidor.
    const jogos = barra('jogosTabs', ['#aovivo', '#agendadas'], '#aovivo');
    const win = montarJanela({ jogosTabs: jogos }, false);
    win.sessionStorage = {
        getItem: () => { throw new Error('SecurityError'); },
        setItem: () => { throw new Error('SecurityError'); },
        removeItem: () => { throw new Error('SecurityError'); },
    };

    let estourou = false;
    try {
        rodar(FONTE_ABAS, win);
        win.bootstrap = bootstrapFalso(win._mostradas);
        win.document.disparar('DOMContentLoaded');
        jogos.disparar('shown.bs.tab', { target: jogos._botoes[1] });
    } catch (e) { estourou = true; }

    ok(!estourou, 'sessionStorage proibido não derruba a página');
})();

console.log('── O ATUALIZADOR NÃO RECARREGA A PÁGINA DEBAIXO DE QUEM NÃO ESTÁ OLHANDO ────');

// O atualizador de 20 em 20 segundos recarrega a página inteira quando a lista de jogos EM
// QUADRA muda. Faz sentido pra quem está olhando o Ao Vivo — o que ele lê acabou de mudar. Pra
// quem está em Palpiteiros, nas Finalizadas ou mexendo num filtro, é a tela sumindo sozinha.
const FONTE_ATUALIZA = fs.readFileSync(
    process.argv[3] || 'Padelizou/wwwroot/js/jogos-ao-vivo-atualiza.js', 'utf8');

function paginaComAoVivo(aoVivoVisivel, idsAgora, idsNoServidor) {
    const cartao = (id) => {
        const el = elemento({ 'data-partida-id': String(id) });
        el.querySelector = () => null;
        return el;
    };
    const paneAoVivo = elemento({}, aoVivoVisivel ? ['tab-pane', 'show', 'active'] : ['tab-pane']);
    const paneJogos = elemento({}, aoVivoVisivel ? ['tab-pane', 'show', 'active'] : ['tab-pane']);

    function docFalso(ids, ehServidor) {
        const cartoes = ids.map(cartao);
        return {
            hidden: false,
            readyState: 'complete',
            _ouvintes: {},
            activeElement: null,
            addEventListener(t, f) { (this._ouvintes[t] = this._ouvintes[t] || []).push(f); },
            getElementById: (id) => (id === 'jogosTabsContent' ? elemento({}) : null),
            querySelector: (sel) => {
                if (sel === '.pdz-live-card') return cartoes[0] || null;
                if (sel === '.modal.show') return null;
                if (sel.indexOf('#aovivo') === 0) return sel.indexOf('.active') > 0 ? (aoVivoVisivel ? paneAoVivo : null) : paneAoVivo;
                if (sel.indexOf('#jogosDoTorneio') === 0) return sel.indexOf('.active') > 0 ? (aoVivoVisivel ? paneJogos : null) : paneJogos;
                return null;
            },
            querySelectorAll: (sel) => (sel === '.pdz-live-card' ? cartoes : []),
        };
    }

    const doc = docFalso(idsAgora, false);
    const respostaDoServidor = docFalso(idsNoServidor, true);

    let recarregou = 0;
    const win = {
        document: doc,
        hidden: false,
        location: { href: 'http://x/Torneios/Details/901', reload: () => { recarregou++; } },
        setInterval: (fn) => { win._tique = fn; return 1; },
        getSelection: () => '',
        fetch: () => Promise.resolve({ ok: true, text: () => Promise.resolve('<html></html>') }),
        DOMParser: function () { this.parseFromString = () => respostaDoServidor; },
        _recarregou: () => recarregou,
    };
    return win;
}

async function tique(win) {
    const f = new Function('window', 'document', 'DOMParser', FONTE_ATUALIZA);
    f(win, win.document, win.DOMParser);
    if (!win._tique) throw new Error('o atualizador não se agendou');
    win._tique();
    // Deixa as promessas do fetch resolverem.
    await new Promise((r) => setTimeout(r, 30));
}

(async function () {
    // 1. Quem ESTÁ olhando o Ao Vivo: a lista mudou debaixo dele, recarregar é o certo.
    const olhando = paginaComAoVivo(true, ['10'], ['10', '11']);
    await tique(olhando);
    ok(olhando._recarregou() === 1, 'com a pessoa NO Ao Vivo, a mudança de jogos em quadra recarrega');

    // 2. Quem está em OUTRA aba (Finalizadas, Palpiteiros): a tela dele não pode sumir.
    const emOutraAba = paginaComAoVivo(false, ['10'], ['10', '11']);
    await tique(emOutraAba);
    ok(emOutraAba._recarregou() === 0,
        'com a pessoa em outra aba, a mudança NÃO recarrega a página debaixo dela');

    console.log(falhas === 0 ? '\nTUDO VERDE\n' : '\n' + falhas + ' FALHA(S)\n');
    process.exit(falhas === 0 ? 0 : 1);
})();
