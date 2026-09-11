// A TRAVA DE CLIQUE DO PALPITRÔMETRO, conferida contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-palpitrometro.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo "Conferir a trava
// de clique do palpitrômetro (JS)" do `ci.yml`, e ele reprova o build. Rode à mão antes de
// commitar: descobrir pelo PR vermelho custa um ciclo.
//
// Sem dependência nenhuma de propósito: `require('fs')` e mais nada — nem no CI, que usa o
// `node` que já vem no runner. Um `npm install` aqui traria package.json, lockfile e supply
// chain pra um repositório que hoje não tem nada disso (ver SUPPLY-CHAIN.md), por uma
// conferência de 150 linhas.
//
// O defeito que ele guarda: 10/09/2026, `DbUpdateException em POST /Partidas/Votar` em
// produção. Dois POSTs do mesmo dedo. A trava do servidor está em `PalpiteService` e tem teste
// em `PalpiteEmDobroTests`; esta aqui é a metade da tela.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/palpitrometro.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function elemento() {
    return {
        textContent: '', innerHTML: '', innerText: '',
        style: {}, classList: { toggle() { }, remove() { }, contains: () => false },
        dataset: {},
    };
}

function palpitrometro(partidaId, dupla1Id, meuVoto) {
    const filhos = {};
    for (const sel of ['[data-pct="1"]', '[data-pct="2"]', '[data-bar="1"]', '[data-bar="2"]',
                       '.pdz-total-votos', '.pdz-palpite-placar', '.pdz-palpite-consenso',
                       '.pdz-consenso-placar', '.pdz-consenso-votos',
                       // 11/09/2026: o resumo que substitui a fileira de fichas depois da escolha.
                       '.pdz-palpite-placar-resumo', '.pdz-palpite-fichas', '.pdz-resumo-placar']) {
        filhos[sel] = elemento();
    }
    filhos['.pdz-palpite-placar-resumo'].querySelector = sel => filhos[sel] || null;
    // As fichas de placar de verdade: é NELAS que dá pra ver com que palpite a tela TERMINOU
    // pintada — a marca "btn-success" é a ficha escolhida.
    const fichas = [[6, 4], [6, 3]].map(([v, p]) => ({
        dataset: { vencedor: String(v), perdedor: String(p) },
        marcada: false,
        classList: { toggle(classe, ligada) { if (classe === 'btn-success') this.dono.marcada = ligada; } },
    }));
    fichas.forEach(f => { f.classList.dono = f; });
    filhos['.pdz-palpite-placar'].querySelectorAll = () => fichas;
    filhos['.pdz-palpite-placar'].querySelector = sel => filhos[sel] || null;
    filhos['.pdz-palpite-consenso'].querySelector = sel => filhos[sel] || null;

    // O botão "retirar": é NELE que se vê se a tela escondeu o que já foi desfeito.
    const botaoRetirar = { hidden: false };

    const container = {
        fichas,
        botaoRetirar,
        dataset: { partidaId: String(partidaId), dupla1Id: String(dupla1Id), meuVoto: meuVoto || '' },
        classList: { contains: () => false, remove() { }, toggle() { } },
        querySelector: sel => filhos[sel] || null,
        querySelectorAll: sel => (sel === '.pdz-retirar-palpite' ? [botaoRetirar] : []),
    };

    const toque = duplaId => ({ dataset: { duplaId: String(duplaId), votavel: 'true' }, closest: () => container });
    const ficha = (v, p) => ({ dataset: { vencedor: String(v), perdedor: String(p) }, closest: () => container });
    const retirar = () => ({ closest: () => container });
    // O "trocar" procura o BLOCO do placar, não o cartão inteiro.
    const trocar = () => ({ closest: () => filhos['.pdz-palpite-placar'] });
    return { container, filhos, toque, ficha, retirar, trocar };
}

// ── O SERVIDOR FALSO ──────────────────────────────────────────────────────────────────────
// Registra cada POST com o instante em que ENTROU e em que RESPONDEU — é assim que se vê se
// dois pedidos se cruzaram.
function servidor(atrasos = {}) {
    const posts = [];
    let emVoo = 0, cruzaram = false;

    async function fetchFalso(url, opcoes) {
        const i = posts.length;
        const corpo = opcoes.body;
        posts.push({ corpo, respondido: false });

        emVoo++;
        if (emVoo > 1) cruzaram = true;
        await new Promise(r => setTimeout(r, atrasos[i] ?? 1));
        emVoo--;
        posts[i].respondido = true;

        // O resumo que o servidor devolveria pra ESTE palpite — é ele que a tela pinta.
        const p = new URLSearchParams(corpo);
        return {
            ok: true,
            json: async () => ({
                votosDupla1: 1, votosDupla2: 0, totalVotos: 1,
                percentualDupla1: 100, percentualDupla2: 0,
                // Sem `duplaId` o pedido é o RETIRAR: a resposta volta sem voto meu.
                meuVotoDuplaId: p.has('duplaId') ? Number(p.get('duplaId')) : null,
                meuPlacarLado1: p.has('placar1') ? Number(p.get('placar1')) : null,
                meuPlacarLado2: p.has('placar2') ? Number(p.get('placar2')) : null,
                placarEmSets: false, placarMaisPalpitadoLado1: null, placarMaisPalpitadoLado2: null,
                placarMaisPalpitadoVotos: 0, palpitesComPlacar: 0,
            }),
        };
    }

    return { posts, fetchFalso, cruzou: () => cruzaram };
}

// ── O MODAL "QUEM VOTOU" ──────────────────────────────────────────────────────────────────
// Ele não vive dentro do cartão: fala com `document.getElementById` e com o `bootstrap`. Por
// isso um DOM falso separado, e não mais um pedaço do `palpitrometro()` de cima.
function telaDoModal() {
    const els = {};
    for (const id of ['modalVerVotos', 'modalVerVotosNome1', 'modalVerVotosNome2',
                      'modalVerVotosLista1', 'modalVerVotosLista2']) {
        els[id] = elemento();
    }
    return {
        els,
        document: { getElementById: id => els[id] || null },
        bootstrap: { Modal: { getOrCreateInstance: () => ({ show() { } }) } },
    };
}

function carregar(fetchFalso, tela) {
    const montar = new Function('fetch', 'alert', 'cabecalhoAntifalsificacao', 'document', 'bootstrap',
        fonte + '\n; return { votarPalpite, palpitarPlacar, retirarPalpite, trocarPlacar, verVotos };');
    return montar(fetchFalso, () => { }, h => h, tela && tela.document, tela && tela.bootstrap);
}

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}

(async () => {
    // 1. TOQUE DUPLO no mesmo nome: o segundo toque não diz nada de novo → UM POST só.
    {
        const s = servidor({ 0: 30 });
        const js = carregar(s.fetchFalso);
        const { toque } = palpitrometro(7, 10);
        const el = toque(10);
        const a = js.votarPalpite(el);
        const b = js.votarPalpite(el);   // o dedo bateu duas vezes
        await Promise.all([a, b]);
        confere('toque duplo no mesmo nome manda UM POST', s.posts.length === 1,
                `mandou ${s.posts.length}`);
    }

    // 2. VOTO + FICHA (dois palpites DIFERENTES, o segundo no meio do primeiro): os dois vão,
    //    em fila — e a tela termina com o ÚLTIMO, não com o que respondeu por último.
    {
        const s = servidor({ 0: 40, 1: 1 });   // o primeiro demora; sem fila ele pinta por último
        const js = carregar(s.fetchFalso);
        const { container, toque, ficha } = palpitrometro(7, 10, '10');
        const a = js.votarPalpite(toque(10));
        const b = js.palpitarPlacar(ficha(6, 4));
        await Promise.all([a, b]);

        confere('voto e ficha no meio: os dois chegam ao servidor', s.posts.length === 2,
                `mandou ${s.posts.length}`);
        confere('um de cada vez — os dois POSTs não se cruzam', !s.cruzou(), 'cruzaram');
        // ⚠️ ESTA é a que importa: sem fila, a resposta do primeiro POST (que demorou) volta
        // DEPOIS da do segundo e repinta a tela com o palpite VELHO — a ficha 6x4 apaga
        // sozinha na frente da pessoa, e só o F5 conserta.
        const marcada = container.fichas.find(f => f.marcada);
        confere('a tela termina pintada com o ÚLTIMO palpite (6x4)',
                marcada && marcada.dataset.vencedor === '6' && marcada.dataset.perdedor === '4',
                marcada ? `ficha ${marcada.dataset.vencedor}x${marcada.dataset.perdedor}` : 'nenhuma ficha marcada');
    }

    // 3. A trava é POR CARTÃO: dois jogos na mesma tela não esperam um pelo outro.
    {
        const s = servidor({ 0: 30, 1: 30 });
        const js = carregar(s.fetchFalso);
        const um = palpitrometro(7, 10);
        const outro = palpitrometro(8, 20);
        await Promise.all([js.votarPalpite(um.toque(10)), js.votarPalpite(outro.toque(20))]);
        confere('dois jogos diferentes falam ao mesmo tempo', s.posts.length === 2 && s.cruzou(),
                `${s.posts.length} POSTs, cruzaram=${s.cruzou()}`);
    }

    // 4. RETIRAR entra na MESMA fila do voto — e é a rota certa que vai.
    //    ⚠️ Voto e retirada mexem na MESMA linha do banco. Soltas, as duas se cruzam e a tela
    //    termina pintada pela resposta que chegou por último: o palpite REAPARECENDO depois de
    //    retirado, e só o F5 consertando.
    {
        const s = servidor({ 0: 40, 1: 1 });   // o voto demora; sem fila ele pinta por último
        const js = carregar(s.fetchFalso);
        const { container, toque, retirar } = palpitrometro(7, 10, '10');

        const a = js.votarPalpite(toque(10));
        const b = js.retirarPalpite(retirar());
        await Promise.all([a, b]);

        confere('retirar e votar não se cruzam', s.posts.length === 2 && !s.cruzou(),
                `${s.posts.length} POSTs, cruzaram=${s.cruzou()}`);
        confere('a tela termina SEM palpite (o retirar foi o último)',
                container.dataset.meuVoto === '', `meuVoto=${container.dataset.meuVoto}`);
        confere('o botão de retirar se esconde sozinho depois da retirada',
                container.botaoRetirar.hidden === true, 'continuou visível');
    }

    // 5. Retirar duas vezes seguidas é UM POST só — o toque duplo não vira dois pedidos.
    {
        const s = servidor({ 0: 30 });
        const js = carregar(s.fetchFalso);
        const { retirar } = palpitrometro(7, 10, '10');
        const el = retirar();
        await Promise.all([js.retirarPalpite(el), js.retirarPalpite(el)]);
        confere('toque duplo no retirar manda UM POST', s.posts.length === 1, `mandou ${s.posts.length}`);
    }

    // 6. ESCOLHER A FICHA RECOLHE A FILEIRA — e o "trocar" a traz de volta, sem POST.
    {
        const s = servidor();
        const js = carregar(s.fetchFalso);
        const { filhos, ficha, trocar } = palpitrometro(7, 10, '10');

        await js.palpitarPlacar(ficha(6, 4));

        confere('depois de escolher, as fichas se recolhem',
                filhos['.pdz-palpite-fichas'].style.display === 'none'
                && filhos['.pdz-palpite-placar-resumo'].style.display === 'flex',
                `fichas=${filhos['.pdz-palpite-fichas'].style.display}, resumo=${filhos['.pdz-palpite-placar-resumo'].style.display}`);
        confere('o resumo diz o placar escolhido',
                filhos['.pdz-resumo-placar'].innerText === '6 x 4',
                `disse "${filhos['.pdz-resumo-placar'].innerText}"`);

        const postsAntes = s.posts.length;
        js.trocarPlacar(trocar());
        confere('o "trocar" reabre a fileira',
                filhos['.pdz-palpite-fichas'].style.display === 'block'
                && filhos['.pdz-palpite-placar-resumo'].style.display === 'none',
                `fichas=${filhos['.pdz-palpite-fichas'].style.display}`);
        // ⚠️ Trocar de ideia não é palpite até a ficha ser tocada: um POST aqui gravaria uma
        // intenção que a pessoa ainda não teve.
        confere('o "trocar" NÃO fala com o servidor', s.posts.length === postsAntes,
                `mandou ${s.posts.length - postsAntes} POST(s)`);
    }

    // 7. O JOGO QUE SUMIU ENQUANTO A PÁGINA ESTAVA ABERTA — 11/09/2026, três
    //    `InvalidOperationException` em `GET /Partidas/VerVotos` no mesmo minuto, em produção.
    //
    //    ⚠️ O servidor agora responde 404 (jogo apagado não é defeito), mas SEM esta metade o
    //    conserto seria pela metade: o `response.json()` sem olhar o `ok` estoura no HTML da
    //    página de erro, o modal fica em "Carregando..." pra sempre e o dedo bate de novo — é
    //    exatamente por isso que foram TRÊS erros no mesmo minuto, e não um.
    {
        const tela = telaDoModal();
        const js = carregar(async () => ({
            ok: false, status: 404,
            json: async () => { throw new Error('a página de erro é HTML, não JSON'); },
        }), tela);

        let estourou = null;
        try {
            await js.verVotos('9999', 'Dupla A', 'Dupla B');
        } catch (e) {
            estourou = e;
        }

        const mostrado = tela.els['modalVerVotosLista1'].innerHTML;
        confere('jogo apagado: o modal AVISA, em vez de ficar em "Carregando..."',
                !estourou && !mostrado.includes('Carregando') && mostrado.includes('atualize a página'),
                estourou ? `estourou: ${estourou.message}` : `mostrou "${mostrado}"`);
    }

    // 8. E o aviso não pode ter comido o caminho feliz: com 200 na mão, o modal continua
    //    listando quem votou dos dois lados.
    {
        const tela = telaDoModal();
        const js = carregar(async () => ({
            ok: true,
            json: async () => ({
                votantesDupla1: [{ nome: 'Ana Pasinato', placarVencedor: 6, placarPerdedor: 4 }],
                votantesDupla2: [],
            }),
        }), tela);

        await js.verVotos('7', 'Dupla A', 'Dupla B');

        confere('com 200 o modal lista quem votou',
                tela.els['modalVerVotosLista1'].innerHTML.includes('Ana Pasinato')
                && tela.els['modalVerVotosNome1'].innerText === 'Dupla A · 1',
                `lista="${tela.els['modalVerVotosLista1'].innerHTML}", titulo="${tela.els['modalVerVotosNome1'].innerText}"`);
    }

    console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
    process.exit(falhas.length === 0 ? 0 : 1);
})();
