// O SIMULADOR DE BALANCEAMENTO DO "PUNHOS DE SHAOLIN" (jogo/ferramentas/), conferido no Node.
//
//     node Padelizou.Tests/js/conferir-simulacao-do-shaolin.js
//
// `jogo/ferramentas/bot.js` é um jogador artificial; `jogo/ferramentas/simular.js` põe o bot pra
// lutar no motor puro milhares de vezes e agrega o resultado. Uma ferramenta de balanceamento só
// serve se os números dela forem de confiança, e é isso que se trava aqui:
//   - o bot fala a língua do motor (a entrada de `Motor.entradaVazia()`, com a borda `apertou`
//     calculada certo: apertar é UM quadro com a tecla e `apertou`; segurar não reaperta);
//   - o bot joga de verdade: o "bom" conclui a fase 1 no fácil em pelo menos 2 de 3 sementes;
//   - nenhuma simulação daqui trava (o teto estourado é sintoma de defeito — do motor ou do bot);
//   - a mesma semente dá EXATAMENTE o mesmo resumo — sem isso, uma trava não se reproduz;
//   - o acaso do bot é semeado: `Math.random` nunca é chamado numa simulação;
//   - o bot decide emendar uma vez por golpe, só enxerga o que está na tela e bloqueia de verdade;
//   - a CLI recusa semente que o motor apelidaria, repete o stdout byte a byte e não culpa o
//     motor por uma trava que pode ser do bot.
//
// Sem dependência nenhuma, como os outros conferidores: `require` de arquivos do repositório.
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo "Conferir as travas
// de JS", que varre `Padelizou.Tests/js/conferir-*.js`. Orçamento: abaixo de 8 s.
const path = require('path');
const fs = require('fs');
const childProcess = require('child_process');

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}
function fim() {
    console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
    process.exit(falhas.length === 0 ? 0 : 1);
}

const inicio = Date.now();
const FERRAMENTAS = path.join(__dirname, '..', '..', 'jogo', 'ferramentas');
const Motor = require(path.join(__dirname, '..', '..', 'jogo', 'js', 'motor.js'));
const DT = 1 / 60;

// 0. AS FERRAMENTAS EXISTEM e exportam o que o resto usa. Sem elas, nada abaixo tem sentido.
const temBot = fs.existsSync(path.join(FERRAMENTAS, 'bot.js'));
const temSimular = fs.existsSync(path.join(FERRAMENTAS, 'simular.js'));
confere('jogo/ferramentas/bot.js existe', temBot, 'não existe');
confere('jogo/ferramentas/simular.js existe', temSimular, 'não existe');
if (!temBot || !temSimular) fim();
const Bot = require(path.join(FERRAMENTAS, 'bot.js'));
const Simular = require(path.join(FERRAMENTAS, 'simular.js'));
confere('bot.js exporta criarBot e as três habilidades',
        typeof Bot.criarBot === 'function' && ['novato', 'medio', 'bom'].every(h => Bot.HABILIDADES && Bot.HABILIDADES[h]),
        `exporta: ${Object.keys(Bot).join(', ')}`);
confere('simular.js exporta simular e agregar',
        typeof Simular.simular === 'function' && typeof Simular.agregar === 'function',
        `exporta: ${Object.keys(Simular).join(', ')}`);
if (falhas.length) fim();

// 1. A ENTRADA DO BOT tem o formato de Motor.entradaVazia() e a borda `apertou` é a de um dedo:
//    apertou[b] só no quadro em que a tecla passa de solta pra apertada. Confere quadro a quadro
//    numa luta de verdade (fase 1, 40 s de jogo), com o mesmo laço que o jogo usa.
{
    const mundo = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 5, dificuldade: 'normal' });
    const bot = Bot.criarBot({ habilidade: 'medio', semente: 5 });
    const molde = Motor.entradaVazia();
    const chaves = Object.keys(molde).filter(k => k !== 'apertou').sort().join(',');
    let formatoErrado = null, bordaErrada = null, apertos = 0, andouPraDireita = false;
    let anterior = Motor.entradaVazia();
    for (let q = 0; q < 60 * 40 && !mundo.concluida && !mundo.fimDeJogo; q++) {
        const e = bot.decidir(mundo);
        if (!formatoErrado) {
            const ks = Object.keys(e).filter(k => k !== 'apertou').sort().join(',');
            const tiposOk = Motor.BOTOES.every(b => typeof e[b] === 'boolean' && typeof e.apertou[b] === 'boolean');
            if (ks !== chaves || !e.apertou || !tiposOk) formatoErrado = `quadro ${q}: chaves=${ks}`;
        }
        for (const b of Motor.BOTOES) {
            if (!bordaErrada && e.apertou[b] !== (e[b] && !anterior[b])) bordaErrada = `quadro ${q}, ${b}: tecla=${e[b]} antes=${anterior[b]} apertou=${e.apertou[b]}`;
            if (e.apertou[b]) apertos++;
        }
        if (e.direita) andouPraDireita = true;
        anterior = { apertou: {} };
        for (const b of Motor.BOTOES) anterior[b] = e[b];
        Motor.passo(mundo, DT, [e]);
    }
    confere('a entrada do bot tem o formato de Motor.entradaVazia()', !formatoErrado, formatoErrado);
    confere('apertou = tecla apertada agora e solta no quadro anterior', !bordaErrada, bordaErrada);
    confere('o bot aperta botões e anda pra direita', apertos > 20 && andouPraDireita, `apertos=${apertos} direita=${andouPraDireita}`);
    confere('o bot sai do lugar (passou a primeira onda em 40 s)', mundo.onda >= 1 || mundo.jogadores[0].x > 500, `onda=${mundo.onda} x=${Math.round(mundo.jogadores[0].x)}`);
}

// 2. O ACASO DO BOT É SEMEADO: com Math.random envenenado, a simulação roda igual. O motor já
//    promete isso dele; o bot tem que prometer o dele.
const mathRandom = Math.random;
let chamouMathRandom = 0;
Math.random = () => { chamouMathRandom++; return mathRandom(); };
const resumos = [1, 2, 3].map(semente => Simular.simular({ fase: 1, dificuldade: 'facil', habilidade: 'bom', semente }));
Math.random = mathRandom;
confere('nenhuma simulação chama Math.random', chamouMathRandom === 0, `chamou ${chamouMathRandom} vez(es)`);

// 3. O BOM CONCLUI A FASE 1 NO FÁCIL em pelo menos 2 de 3 sementes. Se isso cair, ou o bot
//    desaprendeu, ou a fase 1 ficou difícil demais — as duas coisas o Felipe quer saber.
{
    const concluidas = resumos.filter(r => r.resultado === 'concluiu').length;
    confere('o bot bom conclui a fase 1 no fácil em 2 de 3 sementes', concluidas >= 2,
            resumos.map(r => `semente ${r.semente}: ${r.resultado} em ${r.tempo}s, ${r.vidasPerdidas} vida(s) perdida(s)`).join(' · '));
    confere('o resumo traz o que o README promete',
            resumos.every(r => ['concluiu', 'tempo', 'vidasPerdidas', 'mortesPorOnda', 'danoRecebido', 'pontos', 'finalizacoes', 'maiorCombo'].every(k => k in r)),
            `chaves: ${Object.keys(resumos[0]).join(', ')}`);
    confere('quem concluiu marcou pontos e bateu em alguém', resumos.filter(r => r.concluiu).every(r => r.pontos > 0 && r.maiorCombo >= 3),
            resumos.map(r => `pontos=${r.pontos} combo=${r.maiorCombo}`).join(' · '));
}

// 4. MESMA SEMENTE, MESMO RESUMO — duas vezes, byte a byte. Trava que não se reproduz não se corrige.
{
    const de_novo = Simular.simular({ fase: 1, dificuldade: 'facil', habilidade: 'bom', semente: 2 });
    confere('a mesma semente dá exatamente o mesmo resumo', JSON.stringify(de_novo) === JSON.stringify(resumos[1]),
            `\n    1ª: ${JSON.stringify(resumos[1])}\n    2ª: ${JSON.stringify(de_novo)}`);
    const outra = Simular.simular({ fase: 1, dificuldade: 'facil', habilidade: 'bom', semente: 4 });
    // Sem o campo `semente`: senão dois resumos iguais passariam por diferentes só pelo rótulo.
    const semRotulo = r => JSON.stringify(Object.assign({}, r, { semente: 0 }));
    confere('semente diferente, luta diferente', semRotulo(outra) !== semRotulo(resumos[1]), `resumos iguais: ${semRotulo(outra)}`);
    resumos.push(de_novo, outra);
}

// 5. NENHUMA SIMULAÇÃO DO CONFERIDOR TRAVA — e o caminho da trava existe: com um teto de 5 s de
//    jogo, a fase 1 não fecha, e isso sai como TRAVA com a semente, não como "fim de jogo".
{
    const travadas = resumos.filter(r => r.resultado === 'trava');
    confere('nenhuma simulação do conferidor trava', travadas.length === 0, travadas.map(r => `semente ${r.semente}`).join(', '));
    const curta = Simular.simular({ fase: 1, dificuldade: 'facil', habilidade: 'bom', semente: 9, tetoSegundos: 5 });
    confere('estourar o teto é TRAVA, com a semente e a onda', curta.resultado === 'trava' && curta.semente === 9 && curta.trava && typeof curta.trava.onda === 'number',
            JSON.stringify(curta));
}

// 6. A AGREGAÇÃO conta certo: taxa de conclusão, percentis e mortes por onda, num lote feito à mão.
{
    const lote = [
        { semente: 1, resultado: 'concluiu', concluiu: true, tempo: 100, vidasPerdidas: 0, mortesPorOnda: {}, danoRecebido: 10, pontos: 1000, finalizacoes: 1, maiorCombo: 5 },
        { semente: 2, resultado: 'concluiu', concluiu: true, tempo: 200, vidasPerdidas: 1, mortesPorOnda: { 4: 1 }, danoRecebido: 30, pontos: 2000, finalizacoes: 0, maiorCombo: 7 },
        { semente: 3, resultado: 'fim-de-jogo', concluiu: false, tempo: 300, vidasPerdidas: 3, mortesPorOnda: { 2: 1, 4: 2 }, danoRecebido: 90, pontos: 500, finalizacoes: 0, maiorCombo: 3 },
        { semente: 4, resultado: 'trava', concluiu: false, tempo: 900, vidasPerdidas: 0, mortesPorOnda: {}, danoRecebido: 5, pontos: 100, finalizacoes: 0, maiorCombo: 2, trava: { onda: 3 } },
    ];
    const a = Simular.agregar(lote);
    confere('taxa de conclusão = concluídas / total', a.taxaDeConclusao === 0.5, `taxa=${a.taxaDeConclusao}`);
    confere('travas listadas com a semente', a.travas.length === 1 && a.travas[0].semente === 4, JSON.stringify(a.travas));
    confere('mortes somadas por onda', a.mortesPorOnda[4] === 3 && a.mortesPorOnda[2] === 1, JSON.stringify(a.mortesPorOnda));
    confere('tempo de conclusão: mediana só de quem concluiu', a.tempoDeConclusao.p50 === 150 && a.tempoDeConclusao.media === 150,
            JSON.stringify(a.tempoDeConclusao));
    confere('dano recebido: média e percentis do lote todo', a.danoRecebido.media === 33.75 && a.danoRecebido.p50 === 20 && a.danoRecebido.max === 90,
            JSON.stringify(a.danoRecebido));
}

// 7. EMENDAR É DECIDIDO UMA VEZ POR GOLPE. Visto de fora: num golpe do jogador que acertou e já
//    passou da janela ativa (quando o bot decide emendar), se ele apertou soco em algum quadro, apertou
//    já no PRIMEIRO quadro elegível. Soco que só aparece depois = a decisão foi sorteada de novo
//    (o `decisoes.clear()` rodando logo depois do `set` fazia isso). Várias lutas, porque o mapa só
//    enche depois de umas dezenas de golpes.
{
    const resorteios = [];
    for (const [fase, habilidade, semente] of [[1, 'novato', 1], [1, 'medio', 6], [1, 'novato', 2], [2, 'medio', 3], [1, 'bom', 4], [2, 'novato', 5]]) {
        const mundo = Motor.criarMundo({ fase, jogadores: ['long'], semente, dificuldade: 'normal' });
        const bot = Bot.criarBot({ habilidade, semente });
        const j = mundo.jogadores[0];
        const golpes = new Map();   // chave do golpe → { primeiro: apertou no 1º quadro elegível, depois: apertou só depois }
        for (let q = 0; q < 60 * 300 && !mundo.concluida && !mundo.fimDeJogo; q++) {
            const g = j.golpe;
            const elegivel = j.estado === 'atacando' && g && g.proximo && j.acertou && j.quadro >= g.inicio + g.ativo;
            const chave = elegivel ? `${j.golpeNome}@${(mundo.tempo - j.quadro).toFixed(3)}` : null;
            const e = bot.decidir(mundo);
            if (chave) {
                if (!golpes.has(chave)) golpes.set(chave, { primeiro: e.soco });
                else if (e.soco && !golpes.get(chave).primeiro && !golpes.get(chave).avisado) {
                    golpes.get(chave).avisado = true;
                    resorteios.push(`fase ${fase} ${habilidade} semente ${semente}: ${chave}`);
                }
            }
            Motor.passo(mundo, DT, [e]);
        }
    }
    confere('o bot decide emendar UMA vez por golpe (nada de sortear de novo)', resorteios.length === 0, resorteios.join(' · '));
}

// 8. A SEMENTE VÁLIDA é de 1 a 4294967295 — o motor semeia com `(semente >>> 0) || 1`, então 0 é
//    o mesmo mundo que 1, 2^32+k o mesmo que k, e acima de 2^53 `semente + k` nem anda: amostra
//    repetida contada como independente. A CLI recusa (código 2, como as outras opções) e a API
//    também, pra quem chama `lote` direto.
const SIMULAR_JS = path.join(FERRAMENTAS, 'simular.js');
const cli = (...args) => childProcess.spawnSync(process.execPath, [SIMULAR_JS, ...args], { encoding: 'utf8' });
{
    const aceitas = [];
    for (const args of [['--semente', '0'], ['--semente', '-3'], ['--semente', '4294967296'], ['--semente', '4294967295', '--n', '2'], ['--semente', '9007199254740992', '--n', '1']]) {
        const r = cli('--teto', '1', ...args);
        if (r.status !== 2) aceitas.push(`${args.join(' ')} → código ${r.status}`);
    }
    confere('a CLI recusa semente fora de 1..4294967295 (código 2)', aceitas.length === 0, aceitas.join(' · '));
    const ultima = cli('--teto', '1', '--n', '1', '--semente', '4294967295', '--json');
    confere('a CLI aceita a última semente válida', ultima.status === 0, `código ${ultima.status}: ${ultima.stderr}`);
    const recusou = f => { try { f(); return false; } catch (erro) { return /semente/.test(erro.message); } };
    confere('simular() recusa semente 0', recusou(() => Simular.simular({ fase: 1, semente: 0, tetoSegundos: 1 })), 'aceitou');
    confere('lote() recusa faixa que passa de 4294967295', recusou(() => Simular.lote({ fase: 1, semente: 4294967295, n: 2, tetoSegundos: 1 })), 'aceitou');
}

// 9. A SAÍDA PADRÃO SE REPETE byte a byte: o README manda fotografar `> antes.txt`, mudar o motor e
//    comparar. Tempo de relógio (passos/s) no stdout faz o `diff` nunca sair vazio — ele vai pro
//    stderr, e o JSON do stdout não o carrega.
{
    const diferentes = [];
    for (const args of [['--n', '2', '--teto', '20'], ['--n', '2', '--teto', '20', '--json']]) {
        const a = cli(...args), b = cli(...args);
        if (a.status !== 0 || b.status !== 0) { diferentes.push(`${args.join(' ')} → códigos ${a.status}/${b.status}: ${a.stderr}`); continue; }
        if (a.stdout !== b.stdout) {
            const la = a.stdout.split('\n'), lb = b.stdout.split('\n');
            const k = la.findIndex((l, i) => l !== lb[i]);
            diferentes.push(`${args.join(' ')} → linha ${k + 1}: "${la[k]}" / "${lb[k]}"`);
        }
    }
    confere('duas rodadas iguais dão o mesmo stdout (tempo de relógio fica no stderr)', diferentes.length === 0, diferentes.join(' · '));
}

// 10. TRAVA NÃO É SÓ DO MOTOR. O simulador só sabe que o teto estourou; um bot quebrado (que não
//     anda, ou soca parado) ou um --teto curto dão o mesmo sintoma. O rótulo não afirma causa que
//     não conhece, e o retrato da trava traz o que separa "inimigo inalcançável" de "bot parado":
//     há quanto tempo o jogador não acerta ninguém e onde está o inimigo vivo mais perto.
{
    const curta = Simular.simular({ fase: 1, dificuldade: 'facil', habilidade: 'bom', semente: 9, tetoSegundos: 30 });
    const t = curta.trava || {};
    confere('o retrato da trava traz o tempo sem acertar ninguém', typeof t.semAcertar === 'number' && t.semAcertar >= 0, JSON.stringify(t));
    confere('o retrato da trava traz o inimigo vivo mais perto (ou null)', 'maisPerto' in t && (t.maisPerto === null || typeof t.maisPerto.dx === 'number'), JSON.stringify(t));
    const texto = cli('--fase', '1', '--n', '1', '--teto', '30');
    confere('o texto da trava não põe a culpa no motor sem saber', texto.status === 0 && /TRAVAS/.test(texto.stdout) && !/defeito do motor/.test(texto.stdout) && /motor ou bot/.test(texto.stdout),
            `código ${texto.status}: ${texto.stdout.split('\n').filter(l => /TRAVA/.test(l)).join(' / ')}`);
}

// 11. O BOT SÓ VÊ O QUE A TELA MOSTRA. Duas cópias do mesmo bot (mesma semente) jogam em
//     paralelo: uma vê o mundo de verdade, a outra vê o mesmo mundo com um "fantasma" FORA da tela
//     (corpo inteiro fora de [camera.x, camera.x + LARGURA]) — inimigo parado, chefe, inimigo no meio
//     de um golpe, flecha vindo, chá e vaso. Quem só lê a tela decide igual nas duas, quadro a quadro.
//     Antes, o bot mirava no gigante que só existe em `mundo.inimigos`, atirava o especial em quem
//     estava além da borda e se afastava pra "atrair" alguém que nenhuma pessoa via.
{
    const L = Motor.LARGURA, MEIA = Motor.MEIA_LARGURA;
    const apoio = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1, dificuldade: 'normal' });
    const fantasma = (tipo) => { const i = Motor.colocarInimigo(apoio, tipo, 0, 0.5); i.id = 900000 + apoio.proximoId; return i; };
    const foraDaTela = (mundo, lado, escala) => lado > 0 ? mundo.camera.x + L + MEIA * escala + 40 : mundo.camera.x - MEIA * escala - 40;
    const casos = {
        'arqueiro parado além da borda direita': (mundo, j) => {
            const i = fantasma('arqueiro'); i.x = foraDaTela(mundo, 1, i.escala); i.y = j.y; i.virado = -1;
            return { inimigos: mundo.inimigos.concat([i]) };
        },
        'gigante (chefe) além da borda direita': (mundo, j) => {
            const i = fantasma('gigante'); i.x = foraDaTela(mundo, 1, i.escala); i.y = j.y; i.virado = -1;
            return { inimigos: mundo.inimigos.concat([i]) };
        },
        'sombra além da borda esquerda': (mundo, j) => {
            const i = fantasma('sombra'); i.x = foraDaTela(mundo, -1, i.escala); i.y = j.y; i.virado = 1;
            return { inimigos: mundo.inimigos.concat([i]) };
        },
        'bruto começando um soco fora da tela': (mundo, j) => {
            const i = fantasma('bruto'); i.x = foraDaTela(mundo, 1, i.escala); i.y = j.y; i.virado = -1;
            i.estado = 'atacando'; i.golpeNome = 'soco1'; i.golpe = i.def.golpes.soco1; i.quadro = 0.02;
            return { inimigos: mundo.inimigos.concat([i]) };
        },
        'flecha vindo de fora da tela': (mundo, j) => {
            const dono = fantasma('arqueiro');
            return { projeteis: mundo.projeteis.concat([{ id: 900001, tipo: 'flecha', time: 'inimigo', dono, x: mundo.camera.x + L + 30, y: j.y, z: 40, vx: -420, vida: 2, dano: 5, virado: -1 }]) };
        },
        'chá e vaso fora da tela': (mundo, j) => ({
            itens: mundo.itens.concat([{ id: 900002, tipo: 'cha', x: mundo.camera.x + L + 40, y: j.y, z: 0, vz: 0 }]),
            objetos: mundo.objetos.concat([{ id: 900003, time: 'objeto', tipo: 'vaso', x: mundo.camera.x + L + 60, y: j.y, z: 0, item: 'cha', vida: 1, estado: 'parado', escala: 1 }]),
        }),
    };
    const vazamentos = [];
    for (const [nome, acrescentar] of Object.entries(casos)) {
        let achou = null;
        for (const [fase, habilidade, semente] of [[3, 'medio', 1], [2, 'bom', 2], [1, 'novato', 3]]) {
            if (achou) break;
            const mundo = Motor.criarMundo({ fase, jogadores: ['long'], semente, dificuldade: 'normal' });
            const real = Bot.criarBot({ habilidade, semente }), cego = Bot.criarBot({ habilidade, semente });
            const j = mundo.jogadores[0];
            // atalho: 60 s de jogo por luta (o orçamento de 8 s do conferidor); o vazamento antigo
            // aparecia antes dos 30 s em todos os casos.
            for (let q = 0; q < 60 * 60 && !mundo.concluida && !mundo.fimDeJogo; q++) {
                const e = real.decidir(mundo);
                const visto = cego.decidir(Object.assign({}, mundo, acrescentar(mundo, j)));
                const dif = Motor.BOTOES.filter(b => e[b] !== visto[b]);
                if (dif.length) { achou = `fase ${fase} ${habilidade} semente ${semente}, t=${mundo.tempo.toFixed(2)}: ${dif.join(',')}`; break; }
                Motor.passo(mundo, DT, [e]);
            }
        }
        if (achou) vazamentos.push(`${nome} → ${achou}`);
    }
    confere('o bot decide igual com ou sem coisa fora da tela', vazamentos.length === 0, '\n      ' + vazamentos.join('\n      '));
}

// 12. O BOT DEFENDE DE VERDADE. A reação do novato (0,32 s+) e do medio (0,18 s+) é mais lenta que
//     o arranque dos golpes comuns (garra 0,16 s, sombra 0,18 s, mestre 0,12 s): só reagindo, eles
//     bloqueavam 0,5% e 7% dos golpes na fase 2 — bots que não defendem, rotulados "25% | 55%".
//     Quem joga levanta a guarda ANTES, com inimigo perto e de frente. Medido por fora, sem confiar
//     no simulador: golpe bloqueado = quadro em que o dano subiu (a defesa deixa passar 20%) e
//     `golpesLevados` não (o motor só conta golpe que entrou).
function medirDefesa(habilidade, sementes) {
    let bloqueados = 0, levados = 0;
    for (const semente of sementes) {
        const mundo = Motor.criarMundo({ fase: 2, jogadores: ['long'], semente, dificuldade: 'normal' });
        const bot = Bot.criarBot({ habilidade, semente });
        const j = mundo.jogadores[0];
        for (let q = 0; q < 60 * 240 && !mundo.concluida && !mundo.fimDeJogo; q++) {
            const dano = j.danoLevado, golpes = j.golpesLevados;
            Motor.passo(mundo, DT, [bot.decidir(mundo)]);
            if (j.golpesLevados > golpes) levados += j.golpesLevados - golpes;
            else if (j.danoLevado > dano) bloqueados++;
        }
    }
    return { bloqueados, levados, taxa: bloqueados / Math.max(1, bloqueados + levados) };
}
{
    const sementes = [1, 2, 3, 4, 5, 6];
    const piso = { novato: 0.10, medio: 0.25, bom: 0.40 };
    const medida = {};
    for (const h of Object.keys(piso)) medida[h] = medirDefesa(h, sementes);
    const texto = Object.entries(medida).map(([h, m]) => `${h} ${Math.round(m.taxa * 100)}% (${m.bloqueados}/${m.bloqueados + m.levados})`).join(' · ');
    confere('cada bot bloqueia pelo menos o piso dele (novato 10%, medio 25%, bom 40%) na fase 2 normal',
            Object.keys(piso).every(h => medida[h].taxa >= piso[h]), texto);
    confere('quem joga melhor defende mais: novato < medio < bom', medida.novato.taxa < medida.medio.taxa && medida.medio.taxa < medida.bom.taxa, texto);
    // O resumo publica a taxa efetiva — é ela que o README mostra, não o parâmetro do bot.
    const r = Simular.simular({ fase: 2, dificuldade: 'normal', habilidade: 'medio', semente: 1, tetoSegundos: 240 });
    const m1 = medirDefesa('medio', [1]);
    confere('o resumo traz golpes bloqueados e levados, iguais à medida de fora',
            r.golpesBloqueados === m1.bloqueados && r.golpesLevados === m1.levados,
            `resumo ${r.golpesBloqueados}/${r.golpesLevados} · medido ${m1.bloqueados}/${m1.levados}`);
    const a = Simular.agregar([r]);
    confere('o agregado traz a taxa de bloqueio', a.taxaDeBloqueio === +(m1.bloqueados / Math.max(1, m1.bloqueados + m1.levados)).toFixed(4),
            `taxaDeBloqueio=${a.taxaDeBloqueio}`);
}

const segundos = (Date.now() - inicio) / 1000;
confere('o conferidor roda abaixo de 8 s', segundos < 8, `${segundos.toFixed(1)} s`);
fim();
