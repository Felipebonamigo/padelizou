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
    let guardouARolagem = 0;
    const win = {
        document: doc,
        hidden: false,
        location: { href: 'http://x/Torneios/Details/901', reload: () => { recarregou++; } },
        setInterval: (fn) => { win._tique = fn; return 1; },
        getSelection: () => '',
        fetch: () => Promise.resolve({ ok: true, text: () => Promise.resolve('<html></html>') }),
        DOMParser: function () { this.parseFromString = () => respostaDoServidor; },
        pdzGuardarPosicaoNaLista: () => { guardouARolagem++; },
        _recarregou: () => recarregou,
        _guardouARolagem: () => guardouARolagem,
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

// ⚠️ ESTAS DUAS PÁGINAS NÃO TÊM A GRADE `#pdzAoVivoCartoes`: são o CAMINHO DE ESCAPE, o que
// sobrou do comportamento antigo pra quando o remendo cartão a cartão não é possível. O remendo
// em si está na terceira seção, com a grade de verdade.
(async function () {
    // 1. Quem ESTÁ olhando o Ao Vivo: a lista mudou debaixo dele e não deu pra remendar —
    //    recarregar é o certo, guardando a altura da rolagem.
    const olhando = paginaComAoVivo(true, ['10'], ['10', '11']);
    await tique(olhando);
    ok(olhando._recarregou() === 1, 'sem a grade, com a pessoa NO Ao Vivo, a mudança recarrega');
    ok(olhando._guardouARolagem() === 1,
        'o recarregamento guarda a altura da página pra devolver a pessoa onde ela estava');

    // 2. Quem está em OUTRA aba (Finalizadas, Palpiteiros): a tela dele não pode sumir.
    const emOutraAba = paginaComAoVivo(false, ['10'], ['10', '11']);
    await tique(emOutraAba);
    ok(emOutraAba._recarregou() === 0,
        'com a pessoa em outra aba, a mudança NÃO recarrega a página debaixo dela');

})();

console.log('── JOGO QUE ENTRA OU SAI DO AO VIVO NÃO RECARREGA A PÁGINA ─────────────────');

// 🗣️ Felipe: *"nao é possivel fazer com que a pagina nao precise recarregar inteira, apenas os
// placares? e quando entrar ou sair um jogo do aovivo, ele apenas adicionar na tela sem precisar
// carregar?"*  Dá — e o que segurava era o <iframe> da transmissão: MOVER ou reescrever um iframe
// é recarregá-lo. Inserir um cartão novo e remover um que saiu não move os que ficam.
//
// ⚠️ O TESTE GUARDA OS IFRAMES DOS SOBREVIVENTES: cada cartão carrega um objeto `video` com um
// contador de "quantas vezes fui recarregado". Se o remendo mover ou reescrever o cartão de quem
// continua em quadra, o contador sobe — e é isso que o Felipe viu em 08/08 ("o youtube está
// parando sozinho aqui do nada").

// ⚠️ O DOM FALSO COPIA A GRADE DE VERDADE, e não uma simplificação dela:
//     #aovivo  >  #pdzAoVivoCartoes (.row)  >  .col-lg-6  >  .pdz-live-card
// É a COLUNA que entra e sai da grade, não o cartão pelado. Um teste com o cartão solto no
// painel passaria com um remendo que na página real não acha o que remover.
function cartaoVivo(id) {
    const el = elemento({ 'data-partida-id': String(id) }, ['pdz-live-card']);
    el.video = { recarregou: 0 };
    el.hasAttribute = () => false;
    el.querySelector = (sel) => (sel === '.pdz-live-header' ? el._header || (el._header = elemento({})) : null);
    const col = elemento({}, ['col-lg-6']);
    col.cartao = el;
    el.parentNode = col;
    return col;
}

// A grade com filhos de verdade: dá pra inserir, remover e contar.
function grade(colunas) {
    const row = elemento({}, ['row']);
    row.filhos = colunas.slice();
    colunas.forEach((c) => { c.parentNode = row; });
    row.removeChild = (no) => { row.filhos = row.filhos.filter((f) => f !== no); };
    row.insertBefore = (no, ref) => {
        const i = ref ? row.filhos.indexOf(ref) : -1;
        if (i < 0) row.filhos.push(no); else row.filhos.splice(i, 0, no);
        no.parentNode = row;
        // Quem já estava NÃO é tocado: inserir não recarrega vídeo de ninguém.
    };
    row.querySelector = (sel) => {
        const m = /\[data-partida-id="(\d+)"\]/.exec(sel);
        const achada = m && row.filhos.find((c) => c.cartao.getAttribute('data-partida-id') === m[1]);
        return achada ? achada.cartao : null;
    };
    row.querySelectorAll = (sel) => (sel === '.pdz-live-card' ? row.filhos.map((c) => c.cartao) : []);
    return row;
}

// O painel inteiro. Trocar o `innerHTML` dele REINICIA todo vídeo que estava dentro — é
// exatamente o que o Felipe viu em 08/08 ("o youtube está parando sozinho aqui do nada"), e o
// contador é o que denuncia um remendo que reescreve em vez de inserir.
function painel(row) {
    const pane = elemento({}, ['tab-pane']);
    Object.defineProperty(pane, 'innerHTML', {
        get: () => row.filhos.map((c) => c.cartao.getAttribute('data-partida-id')).join(','),
        set: () => {
            row.filhos.forEach((c) => { c.cartao.video.recarregou++; });
            row.filhos = [];
        },
    });
    return pane;
}

function documentoDeJogos(ids, aoVivoVisivel) {
    const row = grade(ids.map(cartaoVivo));
    const pane = painel(row);
    const doc = {
        hidden: false, readyState: 'complete', activeElement: null, _ouvintes: {},
        addEventListener(t, f) { (this._ouvintes[t] = this._ouvintes[t] || []).push(f); },
        getElementById: (id) => (id === 'jogosTabsContent' ? elemento({}) : null),
        importNode: (no) => no,
        querySelector: (sel) => {
            if (sel === '.pdz-live-card') return row.filhos.length ? row.filhos[0].cartao : null;
            if (sel === '.modal.show') return null;
            if (sel === '#aovivo') return pane;
            if (sel === '#aovivo.active') return aoVivoVisivel ? pane : null;
            if (sel === '#jogosDoTorneio') return null;   // /Torneios/Jogos: a lista É a página
            if (sel === '#pdzAoVivoCartoes') return row;
            if (/\.pdz-live-card\[data-partida-id="\d+"\]/.test(sel)) return row.querySelector(sel);
            return null;
        },
        querySelectorAll: (sel) => (sel === '.pdz-live-card' ? row.querySelectorAll(sel) : []),
    };
    doc._row = row;
    return doc;
}

async function tiqueComCartoes(idsAgora, idsNoServidor, aoVivoVisivel) {
    const doc = documentoDeJogos(idsAgora, aoVivoVisivel);
    const respostaDoServidor = documentoDeJogos(idsNoServidor, true);
    let recarregou = 0;
    const win = {
        document: doc, hidden: false,
        location: { href: 'http://x/Torneios/Jogos/901', reload: () => { recarregou++; } },
        setInterval: (fn) => { win._tique = fn; return 1; },
        getSelection: () => '',
        fetch: () => Promise.resolve({ ok: true, text: () => Promise.resolve('<html></html>') }),
        DOMParser: function () { this.parseFromString = () => respostaDoServidor; },
    };
    const f = new Function('window', 'document', 'DOMParser', FONTE_ATUALIZA);
    f(win, doc, win.DOMParser);
    win._tique();
    await new Promise((r) => setTimeout(r, 30));
    return {
        recarregou,
        naTela: doc._row.filhos.map((c) => c.cartao.getAttribute('data-partida-id')),
        videosRecarregados: doc._row.filhos.reduce((n, c) => n + c.cartao.video.recarregou, 0),
    };
}

(async function () {
    // 1. Um jogo ENTRA em quadra: aparece na tela, sem recarregar e sem mexer no vídeo de quem já estava.
    const entrou = await tiqueComCartoes(['10'], ['10', '11'], true);
    ok(entrou.recarregou === 0, 'jogo que ENTRA no ao vivo não recarrega a página');
    ok(entrou.naTela.join(',') === '10,11', 'o jogo que entrou aparece na tela, na ordem do servidor');
    ok(entrou.videosRecarregados === 0, 'o vídeo de quem já estava em quadra não é reiniciado');

    // 2. Um jogo entra NO MEIO da grade: a ordem é a do servidor (quadra 1, 2, 3...), e não
    //    "o novo no fim". Inserir sempre no fim passaria no caso 1 e erraria aqui.
    const noMeio = await tiqueComCartoes(['10', '30'], ['10', '20', '30'], true);
    ok(noMeio.naTela.join(',') === '10,20,30', 'o jogo que entrou no meio entra no meio');
    ok(noMeio.videosRecarregados === 0, 'inserir no meio não reinicia o vídeo dos vizinhos');

    // 3. Um jogo SAI de quadra (acabou): some da tela, sem recarregar.
    const saiu = await tiqueComCartoes(['10', '11'], ['11'], true);
    ok(saiu.recarregou === 0, 'jogo que SAI do ao vivo não recarrega a página');
    ok(saiu.naTela.join(',') === '11', 'o jogo que acabou some da tela');
    ok(saiu.videosRecarregados === 0, 'o vídeo de quem continua em quadra não é reiniciado');

    // 4. Troca completa (acabaram os dois, entraram outros dois): ninguém sobrevive, mas
    //    continua sem recarregamento.
    const trocouTudo = await tiqueComCartoes(['10', '11'], ['12', '13'], true);
    ok(trocouTudo.recarregou === 0, 'trocar os jogos todos de uma vez não recarrega');
    ok(trocouTudo.naTela.join(',') === '12,13', 'a grade fica com os jogos novos, na ordem');

    // 5. O ÚLTIMO jogo sai: aí não há vídeo a preservar e o painel inteiro pode ser trocado
    //    (é o que traz o "Nenhum jogo rolando no momento").
    const esvaziou = await tiqueComCartoes(['10'], [], true);
    ok(esvaziou.recarregou === 0, 'o último jogo sair também não recarrega a página');
    ok(esvaziou.naTela.length === 0, 'a grade fica vazia quando acaba o último jogo');

    // 6. Quem está em OUTRA aba também ganha os cartões novos — de graça, sem a tela sumir.
    //    Antes o remendo nem era tentado pra ele: os cartões ficavam velhos até ele voltar.
    const emOutraAba = await tiqueComCartoes(['10'], ['10', '11'], false);
    ok(emOutraAba.recarregou === 0, 'em outra aba, nada recarrega');
    ok(emOutraAba.naTela.join(',') === '10,11', 'em outra aba, o Ao Vivo é remendado em silêncio');

    await aAlturaDaListaVolta();

    console.log(falhas === 0 ? '\nTUDO VERDE\n' : '\n' + falhas + ' FALHA(S)\n');
    process.exit(falhas === 0 ? 0 : 1);
})();

// ── FILTRAR NÃO JOGA A PÁGINA PRO TOPO ────────────────────────────────────────────────────
//
// 🗣️ Felipe, 12/09/2026: *"quando eu clico em meu jogos, a pagina sobe la para o inicio tambem,
// tinha q aparece na aba meus jogos ja"*.
//
// 🕳️ O `js/manter-posicao-na-lista.js` guarda a altura desde 10/09, mas **só no `submit`** — e o
// "Meus jogos" é um `<a>`, de propósito: liga/desliga de um toque. Link não dispara `submit`,
// então o clique passava batido e a página renascia no começo, com a barra de pagamento na tela
// e a lista de jogos lá embaixo. Os cinco selects do painel de filtros tinham o mesmo buraco por
// outro caminho: `form.submit()` chamado por JS **não dispara o evento `submit`** — quem dispara
// é `requestSubmit()`.
const FONTE_POSICAO = fs.readFileSync(
    process.argv[4] || 'Padelizou/wwwroot/js/manter-posicao-na-lista.js', 'utf8');

// `atributo`: null = link sem opt-in; '' = modo ALTURA; '#algo' = modo ELEMENTO.
function paginaComLista(atributo) {
    const guardado = {};
    let rolou = null;
    let trouxeProTela = null;

    const link = elemento(atributo === null ? {} : { 'data-manter-posicao': atributo }, ['btn']);
    link.tagName = 'A';
    const icone = elemento({});                       // o <i> DENTRO do link: é nele que o dedo
    icone.parentNode = link;                          // encosta no celular, não no <a>.
    const fechar = (el, sel) => (sel === 'a[data-manter-posicao]'
        ? (atributo !== null && (el === link || el.parentNode === link) ? link : null) : null);
    link.closest = (sel) => fechar(link, sel);
    icone.closest = (sel) => fechar(icone, sel);

    const barra = elemento({});
    barra.scrollIntoView = () => { trouxeProTela = '#filtroJogos'; };

    const doc = {
        _ouvintes: {},
        addEventListener(t, f) { (this._ouvintes[t] = this._ouvintes[t] || []).push(f); },
        disparar(t, ev) { (this._ouvintes[t] || []).forEach((f) => f(ev)); },
        querySelector: (sel) => (sel === '#filtroJogos' ? barra : null),
    };
    const win = {
        document: doc,
        location: { pathname: '/Torneios/Details/26' },
        scrollY: 1240,
        sessionStorage: {
            getItem: (k) => (k in guardado ? guardado[k] : null),
            setItem: (k, v) => { guardado[k] = String(v); },
            removeItem: (k) => { delete guardado[k]; },
        },
        requestAnimationFrame: (f) => f(),
        scrollTo: (x, y) => { rolou = y; },
        addEventListener(t, f) { doc.addEventListener(t, f); },
        _guardado: guardado,
        _icone: icone,
        _rolou: () => rolou,
        _trouxeProTela: () => trouxeProTela,
    };
    const f = new Function('window', 'document', FONTE_POSICAO);
    f(win, doc);
    return win;
}

const CLIQUE = { button: 0, defaultPrevented: false, metaKey: false, ctrlKey: false, shiftKey: false, altKey: false };

async function aAlturaDaListaVolta() {
    console.log('── FILTRAR NÃO JOGA A PÁGINA PRO TOPO ──────────────────────────────────────');

    const CHAVE = 'pdz-posicao-na-lista:/Torneios/Details/26';

    // 1. O "Meus jogos" pede o modo ELEMENTO: traz a BARRA DE FILTROS de volta pra tela, e não
    //    uma altura em pixels.
    //
    //    ⚠️ POR QUE NÃO A ALTURA — foi medido no navegador, num celular de 390px: com "Meus
    //    jogos" ligado a lista cai de 97 jogos pra 3, o documento encolhe, e a rolagem guardada
    //    não existe mais na página nova. A barra estava a 27px do topo da tela antes do clique e
    //    voltava a 315px — o começo da página, que é exatamente a queixa dele.
    const porElemento = paginaComLista('#filtroJogos');
    porElemento.document.disparar('click', Object.assign({ target: porElemento._icone }, CLIQUE));
    ok(porElemento._guardado[CHAVE] === '#filtroJogos',
        'clicar no "Meus jogos" guarda a BARRA a trazer de volta (guardou: ' + porElemento._guardado[CHAVE] + ')');

    porElemento.document.disparar('load', {});
    ok(porElemento._trouxeProTela() === '#filtroJogos', 'na volta, a barra de filtros é trazida pra tela');
    ok(porElemento._rolou() === null, 'e nenhuma altura em pixels é aplicada por cima');
    ok(porElemento._guardado[CHAVE] === undefined, 'a memória é lida UMA vez e apagada');

    // 2. O modo ALTURA continua valendo pra quem não muda o tamanho da lista (o check-in, a
    //    troca de horário): atributo sem valor.
    const porAltura = paginaComLista('');
    porAltura.document.disparar('click', Object.assign({ target: porAltura._icone }, CLIQUE));
    ok(porAltura._guardado[CHAVE] === '1240',
        'sem valor no atributo, o que se guarda é a altura (guardou: ' + porAltura._guardado[CHAVE] + ')');
    porAltura.document.disparar('load', {});
    ok(porAltura._rolou() === 1240, 'a página volta na altura em que estava (' + porAltura._rolou() + ')');

    // 3. Link SEM o atributo continua não guardando nada: é opt-in, e uma ação que leva pra
    //    outra tela não quer voltar pra uma posição que já não quer dizer nada.
    const semAtributo = paginaComLista(null);
    semAtributo.document.disparar('click', Object.assign({ target: semAtributo._icone }, CLIQUE));
    ok(Object.keys(semAtributo._guardado).length === 0, 'link sem o atributo não guarda nada');

    // 4. Abrir em nova aba (ctrl/cmd, ou o botão do meio) NÃO é sair da tela: guardar aqui
    //    deixaria uma memória órfã pra atropelar a próxima visita a esta página.
    const novaAba = paginaComLista('#filtroJogos');
    novaAba.document.disparar('click', Object.assign({}, CLIQUE, { target: novaAba._icone, ctrlKey: true }));
    novaAba.document.disparar('click', Object.assign({}, CLIQUE, { target: novaAba._icone, button: 1 }));
    ok(Object.keys(novaAba._guardado).length === 0, 'abrir em nova aba não deixa memória órfã guardada');

    // 5. Seletor que não é `#id` simples não vira `querySelector`: o valor é lido de volta do
    //    sessionStorage, que é da origem inteira, e não só do que este arquivo escreveu.
    const forjado = paginaComLista('#filtroJogos');
    forjado.sessionStorage.setItem(CHAVE, 'a[href],*');
    forjado.document.disparar('load', {});
    ok(forjado._trouxeProTela() === null, 'valor guardado que não é #id simples é ignorado');

    // 5. O select do painel de filtros aplica por JS, e `form.submit()` NÃO dispara `submit`.
    //    Isto aqui é leitura de fonte porque o defeito mora no Razor, não neste arquivo.
    const razor = fs.readFileSync('Padelizou/Views/Torneios/_JogosDoTorneio.cshtml', 'utf8');
    ok(!/onchange="this\.form\.submit\(\)"/.test(razor),
        'nenhum select aplica com form.submit() (que não dispara o evento submit)');
    ok((razor.match(/requestSubmit\(\)/g) || []).length >= 5,
        'os cinco filtros do painel aplicam com requestSubmit(), que dispara o submit');
}
