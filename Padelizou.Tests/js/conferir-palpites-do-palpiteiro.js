// O MODAL "O QUE ESTA PESSOA PALPITOU", conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-palpites-do-palpiteiro.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo do conferir-palpitometro.js: este repositório não tem
// npm (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 150 linhas.
//
// O QUE ELE GUARDA (o porquê inteiro está em PalpitesDoPalpiteiroTests):
//   · NOME DE GENTE NÃO VIRA MARCAÇÃO — a lista é montada com innerHTML, e um nome com "<"
//     quebraria o modal (ou, montado de propósito, injetaria script na página);
//   · "NÃO QUEBRA A TELA" É O PEDIDO: tudo que pode ser longo trunca com `min-width: 0`, e o
//     que não pode quebrar (ficha de placar, ponto) leva `flex-shrink-0`;
//   · quem palpitou SÓ o vencedor leva traço, nunca um "0 x 0" que ninguém disse;
//   · o jogo em aberto não anuncia resultado, e o jogo de quem estava em quadra diz por que
//     não conta;
//   · e o 404 vira frase, e não um "Carregando..." eterno (o defeito de 11/09/2026 no modal
//     irmão, que travou três vezes em produção no mesmo minuto).
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/palpites-do-palpiteiro.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function elemento() {
    return {
        innerText: '',
        innerHTML: '',
        _atributos: {},
        setAttribute(nome, valor) { this._atributos[nome] = valor; },
    };
}

function tela() {
    const porId = {
        modalPalpitesDoPalpiteiro: elemento(),
        pdzPalpitesNome: elemento(),
        pdzPalpitesResumo: elemento(),
        pdzPalpitesLista: elemento(),
        pdzPalpitesPerfil: elemento(),
    };
    return {
        porId,
        documento: { getElementById: (id) => porId[id] || null },
    };
}

// O modal do Bootstrap, reduzido ao que este arquivo usa.
function bootstrapFalso() {
    const aberturas = [];
    return {
        aberturas,
        api: { Modal: { getOrCreateInstance: () => ({ show() { aberturas.push(true); } }) } },
    };
}

// `fetch` falso: guarda a URL pedida e devolve o que o teste mandar.
function fetchFalso(resposta) {
    const chamadas = [];
    const fn = async (url) => { chamadas.push(url); return resposta; };
    fn.chamadas = chamadas;
    return fn;
}

function abrir(resposta, torneioId, jogadorId, nome) {
    const t = tela();
    const bs = bootstrapFalso();
    const buscar = fetchFalso(resposta);

    // O arquivo declara funções soltas (globais no navegador); aqui o `new Function` as deixa
    // no escopo dele, então a última linha devolve a porta de entrada.
    const ver = new Function('document', 'fetch', 'bootstrap', 'window',
        fonte + '\nreturn verPalpitesDoPalpiteiro;')(t.documento, buscar, bs.api, {});

    return ver(torneioId, jogadorId, nome).then(() => ({ tela: t, bootstrap: bs, buscar }));
}

function ok(corpo) {
    return { ok: true, status: 200, json: async () => corpo };
}

// ── OS DADOS ──────────────────────────────────────────────────────────────────────────────
// Uma resposta como a que o servidor manda (camelCase, como o ASP.NET serializa).
function resposta(extra) {
    return Object.assign({
        torneioId: 7,
        jogadorId: 42,
        jogador: 'Marcos Coelho',
        pontos: 20,
        acertos: 9,
        cravadas: 3,
        palpites: 12,
        emAberto: 27,
        linhas: [],
    }, extra);
}

function linha(extra) {
    return Object.assign({
        partidaId: 1,
        categoria: '2ª Categoria Masculina',
        fase: 'Final',
        escolhida: 'Marcelo / Enio',
        adversaria: 'Paulo / Andryo',
        palpitouEscolhida: 6,
        palpitouAdversaria: 4,
        emSets: false,
        apurado: true,
        estavaEmQuadra: false,
        acertou: true,
        pontos: 3,
        placarEscolhida: 6,
        placarAdversaria: 4,
    }, extra);
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

const testes = [];
function teste(fn) { testes.push(fn); }

// 1. O JOGO APURADO: o palpite, o resultado e o ponto, os três na mesma caixa.
teste(async () => {
    const r = await abrir(ok(resposta({ linhas: [linha()] })), 7, 42, 'Marcos Coelho');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    conferir('pediu a lista daquela pessoa naquele torneio',
        r.buscar.chamadas[0] === '/Torneios/PalpitesDoPalpiteiro/7?jogadorId=42');
    conferir('o modal abriu ANTES da resposta chegar', r.bootstrap.aberturas.length === 1);
    conferir('o nome de quem palpitou vai pro título', r.tela.porId.pdzPalpitesNome.innerText === 'Marcos Coelho');
    conferir('o perfil continua a um toque', r.tela.porId.pdzPalpitesPerfil._atributos.href === '/Jogadores/Perfil/42');

    conferir('a dupla escolhida aparece', html.includes('Marcelo / Enio'));
    conferir('e a adversária também', html.includes('contra Paulo / Andryo'));
    conferir('a ficha traz o placar palpitado', html.includes('>6 x 4'));
    conferir('e a linha diz o que DEU', html.includes('deu 6 x 4'));
    conferir('o ponto ganho aparece com sinal', html.includes('+3 pontos'));
    conferir('jogo apurado não fala em esperar', !html.includes('esperando resultado'));

    // O resumo é o mesmo número da linha da tabela de onde o modal foi aberto.
    const resumo = r.tela.porId.pdzPalpitesResumo.innerHTML;
    conferir('o resumo abre pelos PONTOS', resumo.includes('<strong>20 pontos</strong>'));
    conferir('e diz acertos, cravadas e o que falta',
        resumo.includes('9 de 12 acertos') && resumo.includes('3 cravadas') && resumo.includes('27 esperando resultado'));
});

// 2. NOME DE GENTE NÃO VIRA MARCAÇÃO. O caso que importa é o nome montado de propósito.
teste(async () => {
    const veneno = '<img src=x onerror=alert(1)>';
    const r = await abrir(ok(resposta({
        linhas: [linha({ escolhida: veneno, adversaria: veneno, categoria: veneno })],
    })), 7, 42, 'Quem Seja');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    // ⚠️ O que importa é que o "<" e o ">" virem TEXTO: escapados, o `onerror` continua na tela
    // como um pedaço do nome, e é só isso — o navegador não tem tag nenhuma pra pendurá-lo.
    conferir('o nome com "<" sai escapado inteiro', html.includes('&lt;img src=x onerror=alert(1)&gt;'));
    conferir('e NENHUMA tag de gente entra no documento', !html.includes('<img'));
});

// 3. QUEM PALPITOU SÓ O VENCEDOR: traço, nunca um "0 x 0" que ela não disse.
teste(async () => {
    const r = await abrir(ok(resposta({
        linhas: [linha({ palpitouEscolhida: null, palpitouAdversaria: null, pontos: 1 })],
    })), 7, 42, 'So o Vencedor');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    conferir('sem placar palpitado, o lugar dele leva um traço', html.includes('>–</span>'));
    conferir('e nenhum 0 x 0 é inventado', !html.includes('0 x 0'));
    conferir('o ponto do acerto do vencedor continua aparecendo', html.includes('+1 ponto'));
});

// 4. O JOGO QUE AINDA NÃO ACONTECEU: nada de resultado, e nada de "errou".
teste(async () => {
    const r = await abrir(ok(resposta({
        linhas: [linha({ apurado: false, acertou: false, pontos: 0, placarEscolhida: null, placarAdversaria: null })],
    })), 7, 42, 'Palpitou na Vespera');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    conferir('o jogo em aberto diz que espera resultado', html.includes('esperando resultado'));
    conferir('e não anuncia placar nenhum', !html.includes('deu '));
    conferir('nem marca erro com o X vermelho', !html.includes('bi-x-circle-fill'));
    conferir('o palpite dela continua na tela', html.includes('>6 x 4'));
});

// 5. O JOGO DE QUEM ESTAVA EM QUADRA: aparece, e diz POR QUE não conta.
teste(async () => {
    const r = await abrir(ok(resposta({
        linhas: [linha({ estavaEmQuadra: true, pontos: 0 })],
    })), 7, 42, 'Jogou e Palpitou');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    conferir('a linha aparece marcada como fora da conta', html.includes('não conta'));
    conferir('e não mostra ponto nenhum ao lado', !html.includes('pontos</span>'));
    conferir('o motivo fica no título, pra quem perguntar', html.includes('Quem está em quadra não pontua'));
});

// 6. A SEPARAÇÃO DOS DOIS BLOCOS: o que já valeu ponto em cima, o que espera embaixo — e o
//    cabeçalho só uma vez, mesmo com vários jogos em aberto.
teste(async () => {
    const r = await abrir(ok(resposta({
        linhas: [
            linha({ partidaId: 1 }),
            linha({ partidaId: 2, apurado: false, placarEscolhida: null, placarAdversaria: null }),
            linha({ partidaId: 3, apurado: false, placarEscolhida: null, placarAdversaria: null }),
        ],
    })), 7, 42, 'Palpiteiro Completo');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    const cabecalhos = html.split('>Esperando resultado<').length - 1;
    conferir('o bloco do que espera tem UM cabeçalho só', cabecalhos === 1);
    conferir('e ele vem DEPOIS do jogo já apurado',
        html.indexOf('>Esperando resultado<') > html.indexOf('deu 6 x 4'));
});

// 7. "SEM QUEBRAR A TELA": o que é longo trunca, o que é curto não encolhe. É o pedido do
//    Felipe virado em conferência — sem `min-width: 0` o `text-truncate` num flex não corta
//    nada, e quem sai empurrado pra fora da linha é a ficha do placar.
teste(async () => {
    const r = await abrir(ok(resposta({ linhas: [linha()] })), 7, 42, 'Nome');
    const html = r.tela.porId.pdzPalpitesLista.innerHTML;

    const truncados = html.split('text-truncate" style="min-width:0;"').length - 1;
    conferir('os três textos longos do card truncam (categoria, dupla, adversária)', truncados === 3);
    conferir('a ficha do placar não encolhe', html.includes('flex-shrink-0 text-nowrap'));
    conferir('o ícone também não', html.includes('flex-shrink-0"></i>'));
});

// 8. O 404 VIRA FRASE. É o defeito de 11/09/2026 no modal irmão: olhar o `ok` antes do
//    `json()`, senão o parse estoura e o modal fica em "Carregando..." pra sempre.
teste(async () => {
    const r = await abrir({
        ok: false,
        status: 404,
        json: async () => { throw new Error('a resposta de erro NÃO é JSON'); },
    }, 7, 42, 'Nunca Palpitou');

    conferir('o 404 explica em vez de travar',
        r.tela.porId.pdzPalpitesLista.innerHTML.includes('não tem palpite neste torneio'));
    conferir('e o "Carregando..." some', r.tela.porId.pdzPalpitesResumo.innerText === '');
});

// 9. A FALHA DE REDE tem a frase dela, diferente do 404 — "tente de novo" só faz sentido
//    quando tentar de novo pode dar certo.
teste(async () => {
    const r = await abrir({
        ok: false,
        status: 500,
        json: async () => { throw new Error('a resposta de erro NÃO é JSON'); },
    }, 7, 42, 'Quem Seja');

    conferir('o erro do servidor pede pra tentar de novo',
        r.tela.porId.pdzPalpitesLista.innerHTML.includes('Tente de novo'));
});

(async () => {
    for (const t of testes) await t();

    console.log('');
    console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
    process.exit(falhas === 0 ? 0 : 1);
})();
