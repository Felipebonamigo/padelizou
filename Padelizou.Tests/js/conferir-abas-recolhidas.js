// O CINTO DAS ABAS RECOLHIDAS, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-abas-recolhidas.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo do conferir-palpitrometro.js: este repositório não tem
// npm (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 100 linhas.
//
// O QUE ELE GUARDA. As abas "Pagamentos e impedimentos" e "Planejamento de quadras" saem da
// barra depois que a chave é publicada (`.pdz-aba-recolhida { display: none }`) e VOLTAM quando
// são a aba ativa — senão os treze redirects de `#pagamentos` abrem o painel certo com a barra
// sem nada marcado. Quem faz o "voltam" é `:has(> .nav-link.active)`, e aí está o risco: o app
// do Padelizou **é o próprio site** dentro de uma casca TWA (ANDROID.md:3), ou seja, roda no
// WebView do aparelho. Onde o `:has()` não existe, a primeira regra vale e a segunda é
// descartada — a aba ativa fica invisível, que é exatamente o estrago que ela previne.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/abas-recolhidas.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function classList(inicial) {
    const set = new Set(inicial);
    return {
        add: (c) => set.add(c),
        remove: (c) => set.delete(c),
        contains: (c) => set.has(c),
        _tem: () => [...set],
    };
}

function item(recolhida) {
    const link = { dataset: {} };
    const li = {
        classList: classList(recolhida ? ['nav-item', 'pdz-aba-recolhida'] : ['nav-item']),
        dataset: {},
        querySelector: (sel) => (sel === '.nav-link' ? link : null),
    };
    link.li = li;
    return li;
}

function barra(itens) {
    const ouvintes = {};
    return {
        _itens: itens,
        _ouvintes: ouvintes,
        addEventListener: (nome, fn) => { (ouvintes[nome] = ouvintes[nome] || []).push(fn); },
        querySelectorAll: () => itens,
        disparar(nome, evento) { (ouvintes[nome] || []).forEach((fn) => fn(evento)); },
    };
}

function rodar({ temHas, itens }) {
    const b = barra(itens);
    const janela = {
        CSS: temHas === null ? undefined : { supports: (s) => temHas && s === 'selector(:has(*))' },
    };
    const doc = {
        readyState: 'complete',
        addEventListener() { },
        querySelector: (sel) => (sel === '#torneioTabs' ? b : null),
    };
    // O arquivo é uma IIFE que fala com `window` e `document` globais.
    new Function('window', 'document', 'self', fonte)(janela, doc, janela);
    return b;
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

// 1. SEM `:has()`: o cinto assume o trabalho.
{
    const pagamentos = item(true);
    const planejamento = item(true);
    const jogos = item(false);
    const b = rodar({ temHas: false, itens: [jogos, pagamentos, planejamento] });

    conferir('sem :has(), o cinto se registra na barra', (b._ouvintes['shown.bs.tab'] || []).length === 1);

    b.disparar('shown.bs.tab', { target: pagamentos.querySelector('.nav-link') });

    conferir('a aba ATIVA volta a aparecer', !pagamentos.classList.contains('pdz-aba-recolhida'));
    conferir('a outra recolhida continua fora da barra', planejamento.classList.contains('pdz-aba-recolhida'));
    conferir('a aba que nunca recolhe fica intocada', !jogos.classList.contains('pdz-aba-recolhida'));

    // Trocar de aba tem que DEVOLVER a anterior pro esconderijo — senão a barra vai juntando
    // aba solta a cada clique, que é a poluição que a mudança veio tirar.
    b.disparar('shown.bs.tab', { target: planejamento.querySelector('.nav-link') });

    conferir('trocando de aba, a anterior volta a se recolher', pagamentos.classList.contains('pdz-aba-recolhida'));
    conferir('e a nova aparece', !planejamento.classList.contains('pdz-aba-recolhida'));

    // E uma aba que NUNCA foi recolhida não pode virar recolhida por causa do cinto.
    b.disparar('shown.bs.tab', { target: jogos.querySelector('.nav-link') });
    conferir('aba comum nunca ganha a classe', !jogos.classList.contains('pdz-aba-recolhida'));
    conferir('as duas de gestão voltam recolhidas', pagamentos.classList.contains('pdz-aba-recolhida')
        && planejamento.classList.contains('pdz-aba-recolhida'));
}

// 2. COM `:has()`: o CSS já resolve, e ANTES da primeira pintura. O cinto fica quieto — dois
//    mecanismos ligados ao mesmo tempo é a segunda fonte da verdade que sempre diverge.
{
    const pagamentos = item(true);
    const b = rodar({ temHas: true, itens: [pagamentos] });
    conferir('com :has(), o cinto NÃO se registra', (b._ouvintes['shown.bs.tab'] || []).length === 0);
    conferir('com :has(), a classe fica como o servidor mandou', pagamentos.classList.contains('pdz-aba-recolhida'));
}

// 3. NAVEGADOR SEM `CSS.supports` (WebView antigo de verdade): o cinto tem que ligar, não
//    estourar. `window.CSS` indefinido é o caso mais provável de todos.
{
    const pagamentos = item(true);
    const b = rodar({ temHas: null, itens: [pagamentos] });
    conferir('sem window.CSS, o cinto liga em vez de estourar', (b._ouvintes['shown.bs.tab'] || []).length === 1);
}

console.log('');
console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
process.exit(falhas === 0 ? 0 : 1);
