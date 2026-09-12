// A MESA DE CONTROLE OFFLINE, conferida contra um DOM falso no Node.
//
//     node Padelizou.Tests/js/conferir-mesa-offline.js
//
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo que varre
// `Padelizou.Tests/js/conferir-*.js`, e ele reprova o build. Rode à mão antes de commitar.
//
// Sem dependência nenhuma, mesmo motivo do conferir-palpitometro.js: este repositório não tem
// npm (ver SUPPLY-CHAIN.md) e não vai ganhar uma árvore de terceiros por 100 linhas.
//
// O QUE ELE GUARDA (Felipe, 12/09/2026, sobre dois marcadores trabalhando juntos):
//
//   · A FILA MANDA SÓ O LADO TOCADO. Ela guarda o placar INTEIRO por partida — é o que torna a
//     reentrega segura, porque placar absoluto reenviado dá sempre no mesmo lugar —, mas
//     mandar os dois lados a cada toque faz o aparelho do vizinho reescrever o lado que
//     ninguém tocou com o número da tela DELE, de minutos atrás se ele esteve sem sinal.
//
//   · A IDADE DO TOQUE VIAJA, e não o relógio do aparelho. Diferença entre dois instantes do
//     MESMO aparelho é confiável mesmo com a hora errada; o epoch absoluto não é. Era o epoch
//     que ordenava dois placares, e um celular adiantado travava o outro por completo.
//
//   · RECUSA APARECE. "O servidor já tem placar mais novo" esvaziava a fila com a tarja VERDE
//     de "Placar sincronizado" — o toque sumia sem nada na tela dizer por quê.
const fs = require('fs');

const fonte = fs.readFileSync(process.argv[2] || 'Padelizou/wwwroot/js/mesa-offline.js', 'utf8');

// ── O DOM FALSO ───────────────────────────────────────────────────────────────────────────
function mesa(opcoes) {
    opcoes = opcoes || {};
    const elementos = {};
    const daTela = (id) => (elementos[id] = elementos[id] || { innerText: '0', hidden: false, className: '', textContent: '', setAttribute() { }, classList: { replace() { }, add() { } }, remove() { } });

    // Os spans do placar de uma partida, como a view os desenha.
    for (const chave of ['gamesA_1', 'gamesB_1', 'setsA_1', 'setsB_1', 'pontosA_1', 'pontosB_1']) {
        daTela(chave).innerText = String((opcoes.tela || {})[chave] || 0);
    }
    daTela('mesaStatus');

    const guardado = {};
    const armazem = {
        getItem: (k) => (k in guardado ? guardado[k] : null),
        setItem: (k, v) => { guardado[k] = String(v); },
        removeItem: (k) => { delete guardado[k]; },
    };

    const chamadas = [];
    let resposta = () => ({ aplicado: true, games1: 0, games2: 0, sets1: 0, sets2: 0, pontos1: 0, pontos2: 0 });

    // Os dois gatilhos de reenvio da Mesa ficam guardados: é por eles que se exercita a
    // entrega ATRASADA, que é o caso inteiro da idade do toque.
    const ouvintes = {};
    const janela = { addEventListener: (nome, fn) => { ouvintes[nome] = fn; } };
    const doc = { getElementById: (id) => elementos[id] || null };

    // Temporizador controlável: sem ele o debounce de meio segundo não junta a rajada de
    // toques, e cada toque viraria um POST — que não é como o arquivo se comporta no navegador.
    let relogioDeTimers = [];
    let seq = 0;
    const marcar = (fn) => { relogioDeTimers.push({ id: ++seq, fn }); return seq; };
    const desmarcar = (id) => { relogioDeTimers = relogioDeTimers.filter((t) => t.id !== id); };
    const correrTimers = () => {
        const pendentes = relogioDeTimers;
        relogioDeTimers = [];
        for (const t of pendentes) t.fn();
    };

    // O relógio do aparelho, controlável: é dele que sai a IDADE do toque.
    let agora = 1_000_000;
    const relogioFalso = { now: () => agora };

    let semRede = false;
    const fetchFalso = (url, req) => {
        chamadas.push({ url, corpo: req.body });
        if (semRede) return Promise.reject(new Error('sem rede'));
        return Promise.resolve({ ok: true, json: () => Promise.resolve(resposta()) });
    };

    const MesaOffline = new Function(
        'window', 'document', 'localStorage', 'fetch', 'setInterval', 'setTimeout', 'clearTimeout',
        'Date', 'cabecalhoAntifalsificacao',
        fonte + '\nreturn MesaOffline;'
    )(janela, doc, armazem, fetchFalso, (fn) => { ouvintes.tique = fn; return 0; },
        marcar, desmarcar, relogioFalso, (o) => o);

    return {
        MesaOffline,
        chamadas,
        elementos,
        tarja: () => (elementos.mesaStatus.textContent || ''),
        responder(fn) { resposta = fn; },
        avancarRelogio(ms) { agora += ms; },
        correrTimers,
        derrubarARede(v) { semRede = v; },
        aRedeVoltou() { return ouvintes.online(); },
    };
}

const assentar = () => new Promise((r) => setImmediate(r));

// ── AS CONFERÊNCIAS ───────────────────────────────────────────────────────────────────────
let falhas = 0;
function conferir(nome, condicao) {
    if (condicao) { console.log('  ok   · ' + nome); return; }
    falhas++;
    console.log(' FALHA · ' + nome);
}

function campoDoCorpo(corpo, nome) {
    const achado = String(corpo).split('&').find((p) => p.split('=')[0] === nome);
    return achado === undefined ? null : achado.split('=')[1];
}

(async function () {
    // 1. UM TOQUE DE CADA LADO: o POST afirma só o lado tocado.
    {
        const t = mesa({ tela: { gamesA_1: 2, gamesB_1: 1 } });
        t.MesaOffline.iniciar({ torneioId: 9, limiteGames: 9, partidas: [1] });
        t.chamadas.length = 0;

        t.MesaOffline.tocar(1, 'games1', 1);
        t.correrTimers();
        await assentar();

        const corpo = t.chamadas[0] ? t.chamadas[0].corpo : '';
        conferir('o toque vira POST', t.chamadas.length === 1);
        conferir('o lado tocado vai com o número', campoDoCorpo(corpo, 'games1') === '3');
        conferir('o lado NÃO tocado vai como -1', campoDoCorpo(corpo, 'games2') === '-1');
        conferir('os sets, que ninguém tocou, também', campoDoCorpo(corpo, 'sets1') === '-1'
            && campoDoCorpo(corpo, 'sets2') === '-1');
        conferir('e os pontos do tie-break também', campoDoCorpo(corpo, 'pontosTieBreak1') === '-1');
    }

    // 2. DOIS LADOS TOCADOS antes da entrega: os dois viajam com número.
    {
        const t = mesa({ tela: { gamesA_1: 2, gamesB_1: 1 } });
        t.MesaOffline.iniciar({ torneioId: 9, limiteGames: 9, partidas: [1] });
        t.chamadas.length = 0;

        t.MesaOffline.tocar(1, 'games1', 1);
        t.MesaOffline.tocar(1, 'games2', 1);
        t.correrTimers();
        await assentar();

        const corpo = t.chamadas[t.chamadas.length - 1].corpo;
        conferir('tocou nos dois, os dois vão',
            campoDoCorpo(corpo, 'games1') === '3' && campoDoCorpo(corpo, 'games2') === '2');
    }

    // 3. A IDADE DO TOQUE, e não o relógio do aparelho.
    //
    //    ⚠️ Ela é medida NA HORA DE ENTREGAR, e por isso cresce com a espera: é isso que faz o
    //    toque preso numa fila offline cair, do lado do servidor, no instante em que ELE
    //    ACONTECEU — e não no instante em que a rede voltou. Mandar o epoch do aparelho fazia a
    //    mesma conta com um relógio que erra: um celular adiantado carimbava a partida no
    //    futuro e travava o aparelho do vizinho por completo.
    {
        const t = mesa({});
        t.MesaOffline.iniciar({ torneioId: 9, limiteGames: 9, partidas: [1] });
        t.chamadas.length = 0;

        // O toque acontece com a rede caída: ele fica na fila.
        t.derrubarARede(true);
        t.MesaOffline.tocar(1, 'games1', 1);
        t.correrTimers();
        await assentar();
        conferir('a idade do toque viaja', campoDoCorpo(t.chamadas[0].corpo, 'idadeMs') === '0');

        // Oito segundos depois a rede volta e a fila é entregue.
        t.avancarRelogio(8000);
        t.derrubarARede(false);
        t.aRedeVoltou();
        await assentar();

        const corpo = t.chamadas[t.chamadas.length - 1].corpo;
        conferir('e ela cresce com a espera', campoDoCorpo(corpo, 'idadeMs') === '8000');
        conferir('o lado tocado continua sendo só um', campoDoCorpo(corpo, 'games2') === '-1');
    }

    // 4. RECUSA NÃO É "SINCRONIZADO". O servidor responde 200 dizendo que já tem placar mais
    //    novo — a Mesa adota o dele, mas quem marcou precisa SABER que o toque não virou placar.
    {
        const t = mesa({});
        t.MesaOffline.iniciar({ torneioId: 9, limiteGames: 9, partidas: [1] });
        t.responder(() => ({ aplicado: false, motivo: 'já existe um placar mais novo',
                             games1: 7, games2: 2, sets1: 0, sets2: 0, pontos1: 0, pontos2: 0 }));

        t.MesaOffline.tocar(1, 'games1', 1);
        t.correrTimers();
        await assentar();

        conferir('a tela adota o placar do servidor', t.elementos.gamesA_1.innerText === 7);
        conferir('e a tarja NÃO diz sincronizado', t.tarja().indexOf('Placar sincronizado') === -1);
        conferir('ela diz que o toque não valeu', t.tarja().length > 0);
    }

    console.log('');
    console.log(falhas === 0 ? 'TUDO VERDE' : falhas + ' FALHA(S)');
    process.exit(falhas === 0 ? 0 : 1);
})();
