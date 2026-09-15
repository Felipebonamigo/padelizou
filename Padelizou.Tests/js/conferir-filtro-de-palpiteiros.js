// O FILTRO "JOGANDO / DE FORA" DA TABELA DE PALPITEIROS, conferido contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-filtro-de-palpiteiros.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo dos irmãos: este repositório não tem npm
// (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 40 linhas.
//
// O QUE ELE GUARDA (12/09/2026 — 🗣️ Felipe: *"coloque um filtro, para ver se a pessoa esta
// jogando o torneio ou nao"*):
//   · os três recortes escondem e devolvem a linha certa, e "Todos" devolve TUDO;
//   · a POSIÇÃO não é reescrita — o filtro esconde linha, não renumera a tabela;
//   · o escopo é respeitado: duas tabelas na mesma página (o hub tem abas) não se filtram
//     uma à outra;
//   · e o botão ativo é UM só, com o `aria-pressed` acompanhando — a classe `active` do
//     Bootstrap é tinta, e quem usa leitor de tela precisa do estado.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/filtro-de-palpiteiros.js', 'utf8');

// ⚠️ UM `document` FALSO QUE DEVOLVE AS LINHAS DAS DUAS TABELAS, de propósito: é ele que dá à
// trava do vizinho como discriminar. Sem esse caminho global, "a tabela do vizinho fica
// intacta" passaria com QUALQUER implementação — não existiria jeito de o arquivo errar.
const todasAsLinhas = [];
const documentoFalso = {
    querySelectorAll: (sel) => (sel === 'tbody tr[data-joga]' ? todasAsLinhas : []),
};

// O arquivo declara uma função global (é ela que o `onclick` do Razor chama).
const filtrarPalpiteiros =
    new Function('document', fonte + '; return filtrarPalpiteiros;')(documentoFalso);

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function classList() {
    const set = new Set();
    return {
        toggle: (c, ligado) => (ligado ? set.add(c) : set.delete(c)),
        contains: (c) => set.has(c),
    };
}

function linha(joga, posicao) {
    return {
        dataset: { joga: joga ? 'true' : 'false' },
        hidden: false,
        _posicao: posicao,
    };
}

function botao(filtro) {
    return {
        dataset: { filtro },
        classList: classList(),
        atributos: {},
        setAttribute(nome, valor) { this.atributos[nome] = valor; },
    };
}

// Um escopo `.pdz-palpiteiros` com os três botões e as linhas dele.
function escopo(linhas) {
    const botoes = [botao('todos'), botao('jogando'), botao('fora')];
    const alvo = {
        querySelectorAll: (sel) => {
            if (sel === '[data-filtro]') return botoes;
            if (sel === 'tbody tr[data-joga]') return linhas;
            return [];
        },
    };
    botoes.forEach((b) => { b.closest = (sel) => (sel === '.pdz-palpiteiros' ? alvo : null); });
    linhas.forEach((l) => todasAsLinhas.push(l));
    return { botoes, linhas, alvo };
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

const visiveis = (linhas) => linhas.filter((l) => !l.hidden).map((l) => l._posicao);

// 1. OS TRÊS RECORTES.
{
    // Uma tabela como a do Er: jogador, torcedor, jogador, torcedor.
    const linhas = [linha(true, 1), linha(false, 2), linha(true, 3), linha(false, 4)];
    const t = escopo(linhas);

    filtrarPalpiteiros(t.botoes[1]); // Jogando
    conferir('"Jogando" deixa só quem disputa', JSON.stringify(visiveis(linhas)) === '[1,3]');

    filtrarPalpiteiros(t.botoes[2]); // De fora
    conferir('"De fora" deixa só quem não disputa', JSON.stringify(visiveis(linhas)) === '[2,4]');

    filtrarPalpiteiros(t.botoes[0]); // Todos
    conferir('"Todos" devolve a tabela inteira', JSON.stringify(visiveis(linhas)) === '[1,2,3,4]');

    // ⚠️ A POSIÇÃO É A DO TORNEIO INTEIRO. Renumerar dentro do recorte inventaria uma
    // classificação que não existe — quem filtra quer saber quem está na frente dele na
    // tabela de verdade.
    filtrarPalpiteiros(t.botoes[2]);
    conferir('o filtro NÃO renumera: a 2ª e a 4ª seguem 2 e 4',
        JSON.stringify(visiveis(linhas)) === '[2,4]');
}

// 2. O BOTÃO ATIVO É UM SÓ, e o leitor de tela sabe qual.
{
    const t = escopo([linha(true, 1), linha(false, 2)]);

    filtrarPalpiteiros(t.botoes[1]);
    conferir('o botão clicado fica ativo', t.botoes[1].classList.contains('active'));
    conferir('os outros dois perdem o ativo',
        !t.botoes[0].classList.contains('active') && !t.botoes[2].classList.contains('active'));
    conferir('o aria-pressed acompanha',
        t.botoes[1].atributos['aria-pressed'] === 'true'
        && t.botoes[0].atributos['aria-pressed'] === 'false');

    // Trocar de recorte não pode deixar dois botões acesos.
    filtrarPalpiteiros(t.botoes[0]);
    conferir('trocando de recorte, o anterior apaga',
        t.botoes[0].classList.contains('active') && !t.botoes[1].classList.contains('active'));
}

// 3. DUAS TABELAS NA MESMA PÁGINA (o hub do Ranking tem abas): uma não filtra a outra.
{
    const minhas = [linha(true, 1), linha(false, 2)];
    const doVizinho = [linha(true, 1), linha(false, 2)];
    const t = escopo(minhas);
    escopo(doVizinho); // existe, e ninguém a clicou

    filtrarPalpiteiros(t.botoes[1]);
    conferir('a tabela do vizinho fica intacta', doVizinho.every((l) => !l.hidden));
}

// 4. BOTÃO FORA DE UM ESCOPO: cala, em vez de estourar. `closest` devolvendo null é o caso de
//    quem move o botão de lugar na view e esquece o container.
{
    const solto = botao('jogando');
    solto.closest = () => null;
    let estourou = false;
    try { filtrarPalpiteiros(solto); } catch { estourou = true; }
    conferir('botão sem escopo não estoura', !estourou);
}

console.log('');
console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
process.exit(falhas === 0 ? 0 : 1);
