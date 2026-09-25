#!/usr/bin/env node
// PUNHOS DE SHAOLIN — simulador de balanceamento. Motor puro, sem desenho: o bot de `bot.js`
// joga milhares de lutas e isto agrega o que aconteceu.
//
//     node jogo/ferramentas/simular.js --fase 1 --dificuldade normal --habilidade medio --n 100 --semente 1
//     node jogo/ferramentas/simular.js --fase 2 --personagem shen --json
//     node jogo/ferramentas/simular.js --matriz            # fases 1–4 × dificuldades × habilidades
//
// Cada simulação roda até a fase concluir, o fim de jogo, ou o TETO de tempo de jogo (15 min).
// Estourar o teto é TRAVA: a luta ficou num estado de onde não sai. O simulador não sabe de quem é
// a culpa — pode ser o motor (inimigo que não se alcança, onda que não fecha), o bot (parado, socando
// o vento) ou um --teto curto demais —, então sai no relatório com a semente que reproduz e o retrato
// que separa os casos (há quanto tempo ninguém é acertado, onde está o inimigo mais perto).
// A simulação k de um lote usa a semente `--semente + k`; a mesma semente dá o mesmo resumo.
//
// Cada fase começa do zero (3 vidas, vida e chi cheios de fábrica), não com o que sobrou da
// anterior como no jogo: assim cada fase se mede sozinha. Ver README.md desta pasta.
'use strict';

const path = require('path');
const Motor = require(path.join(__dirname, '..', 'js', 'motor.js'));
const Bot = require(path.join(__dirname, 'bot.js'));

const DT = 1 / 60;
const TETO_PADRAO = 15 * 60;            // segundos de JOGO, não de relógio
const N_LOTE = 100, N_MATRIZ = 30;      // simulações por lote / por célula da matriz, sem --n
const HABILIDADES = Object.keys(Bot.HABILIDADES);
const DIFICULDADES = Object.keys(Motor.DIFICULDADES);
const FASES_JOGAVEIS = Motor.FASES.filter(f => f.ondas.length > 0).map(f => f.numero);   // a 0 (treino) não tem onda: nunca conclui

// O motor semeia com `(semente >>> 0) || 1`: 0 é o mesmo mundo que 1, 2^32+k o mesmo que k, e acima
// de 2^53 `semente + k` nem anda. Fora desta faixa, duas lutas "diferentes" seriam a mesma.
const SEMENTE_MIN = 1, SEMENTE_MAX = 4294967295;
function conferirSementes(primeira, n) {
    const ultima = primeira + n - 1;
    if (!Number.isSafeInteger(primeira) || primeira < SEMENTE_MIN || ultima > SEMENTE_MAX) {
        throw new Error(`semente ${primeira}${n > 1 ? `–${ultima}` : ''} fora da faixa ${SEMENTE_MIN}–${SEMENTE_MAX} (o motor apelida as de fora: 0 = 1, 2^32 + k = k)`);
    }
}

function arred(v, casas) { const k = Math.pow(10, casas == null ? 2 : casas); return Math.round(v * k) / k; }

// ── UMA SIMULAÇÃO ─────────────────────────────────────────────────────────────────────────
function simular(opcoes) {
    const o = Object.assign({ fase: 1, dificuldade: 'normal', habilidade: 'medio', personagem: 'long', semente: 1, tetoSegundos: TETO_PADRAO }, opcoes || {});
    conferirSementes(o.semente, 1);
    if (!FASES_JOGAVEIS.includes(o.fase)) throw new Error(`fase ${o.fase} não é jogável (use ${FASES_JOGAVEIS.join(', ')})`);
    if (!Motor.DIFICULDADES[o.dificuldade]) throw new Error(`dificuldade desconhecida: ${o.dificuldade} (use ${DIFICULDADES.join(', ')})`);
    if (!Motor.PERSONAGENS[o.personagem]) throw new Error(`personagem desconhecido: ${o.personagem} (use ${Object.keys(Motor.PERSONAGENS).join(', ')})`);
    const mundo = Motor.criarMundo({ fase: o.fase, jogadores: [o.personagem], semente: o.semente, dificuldade: o.dificuldade });
    const bot = Bot.criarBot({ habilidade: o.habilidade, semente: o.semente });
    const j = mundo.jogadores[0];
    const teto = Math.round(o.tetoSegundos / DT);
    const mortesPorOnda = {};
    let quadros = 0, finalizacoes = 0, maiorCombo = 0, vidasPerdidas = 0, ondaDaUltimaMorte = null;
    let comboAntes = 0, ultimoAcerto = 0;   // o combo só sobe quando o jogador acerta alguém
    // Golpe bloqueado = quadro em que o dano subiu (a defesa deixa passar 20%, no mínimo 1) e
    // `golpesLevados` não (o motor só conta golpe que entrou). Dois golpes no MESMO quadro, um
    // bloqueado e um não, contam como um levado — raro, e só puxa a taxa pra baixo.
    let golpesBloqueados = 0;
    const golpesLevadosAntes = j.golpesLevados;
    while (quadros < teto && !mundo.concluida && !mundo.fimDeJogo) {
        const dano = j.danoLevado, golpes = j.golpesLevados;
        Motor.passo(mundo, DT, [bot.decidir(mundo)]);
        quadros++;
        if (j.golpesLevados === golpes && j.danoLevado > dano) golpesBloqueados++;
        for (const ev of mundo.eventos) {
            if (ev.tipo === 'finalizacao') finalizacoes++;
            else if (ev.tipo === 'morte' && ev.time === 'jogador') {
                // Onda em que morreu, contando de 1; 0 = no caminho entre ondas.
                const onda = mundo.travado ? mundo.onda + 1 : 0;
                mortesPorOnda[onda] = (mortesPorOnda[onda] || 0) + 1;
                vidasPerdidas++;
                ondaDaUltimaMorte = onda;
            }
        }
        if (j.combo > comboAntes) ultimoAcerto = quadros;
        comboAntes = j.combo;
        if (j.combo > maiorCombo) maiorCombo = j.combo;
    }
    const resultado = mundo.concluida ? 'concluiu' : mundo.fimDeJogo ? 'fim-de-jogo' : 'trava';
    const resumo = {
        semente: o.semente, fase: o.fase, dificuldade: o.dificuldade, habilidade: o.habilidade, personagem: o.personagem,
        resultado, concluiu: resultado === 'concluiu',
        tempo: arred(quadros * DT, 2),
        ondasLimpas: mundo.onda, totalDeOndas: mundo.faseDef.ondas.length,
        vidasPerdidas, mortesPorOnda, danoRecebido: j.danoLevado,
        golpesLevados: j.golpesLevados - golpesLevadosAntes, golpesBloqueados,
        pontos: mundo.pontuacao, finalizacoes, maiorCombo, quadros,
    };
    if (resultado === 'fim-de-jogo') resumo.ondaDoFimDeJogo = ondaDaUltimaMorte;
    if (resultado === 'trava') {
        // O retrato do estado preso: é o que se olha pra achar o defeito. `semAcertar` alto com
        // `maisPerto` longe = inimigo inalcançável (motor); `maisPerto` null na tela livre, ou colado
        // e ninguém apanhando = o bot não está jogando.
        let maisPerto = null;
        for (const i of mundo.inimigos) {
            if (!Motor.vivo(i)) continue;
            const dx = i.x - j.x, dy = i.y - j.y;
            if (!maisPerto || Math.abs(dx) < Math.abs(maisPerto.dx)) maisPerto = { tipo: i.tipo, dx: arred(dx, 0), dy: arred(dy, 2) };
        }
        resumo.trava = {
            semAcertar: arred((quadros - ultimoAcerto) * DT, 1), maisPerto,
            onda: mundo.onda + 1, travado: mundo.travado, telaX: arred(mundo.travado ? mundo.travaX : mundo.camera.x, 0),
            jogador: { x: arred(j.x, 0), y: arred(j.y, 2), estado: j.estado, vida: j.vida, chi: j.chi },
            inimigos: mundo.inimigos.filter(Motor.vivo).map(i => ({ tipo: i.tipo, x: arred(i.x, 0), y: arred(i.y, 2), z: arred(i.z, 0), estado: i.estado, vida: i.vida })),
        };
    }
    return resumo;
}

// ── AGREGAR ───────────────────────────────────────────────────────────────────────────────
// Percentil com interpolação linear (o "tipo 7" das planilhas): p50 de [100, 200] é 150.
function percentil(ordenados, p) {
    if (!ordenados.length) return null;
    const pos = (ordenados.length - 1) * p, baixo = Math.floor(pos), alto = Math.ceil(pos);
    return ordenados[baixo] + (ordenados[alto] - ordenados[baixo]) * (pos - baixo);
}
function estatistica(valores) {
    const v = valores.slice().sort((a, b) => a - b);
    if (!v.length) return { n: 0, media: null, p10: null, p50: null, p90: null, min: null, max: null };
    return {
        n: v.length, media: arred(v.reduce((s, x) => s + x, 0) / v.length),
        p10: arred(percentil(v, 0.1)), p50: arred(percentil(v, 0.5)), p90: arred(percentil(v, 0.9)),
        min: v[0], max: v[v.length - 1],
    };
}
function agregar(resumos) {
    const n = resumos.length;
    const concluidos = resumos.filter(r => r.resultado === 'concluiu');
    const fins = resumos.filter(r => r.resultado === 'fim-de-jogo');
    const somar = (campo) => {
        const total = {};
        for (const r of resumos) for (const [k, v] of Object.entries(r[campo] || {})) total[k] = (total[k] || 0) + v;
        return total;
    };
    const bloqueados = resumos.reduce((s, r) => s + (r.golpesBloqueados || 0), 0);
    const recebidos = bloqueados + resumos.reduce((s, r) => s + (r.golpesLevados || 0), 0);
    const fimPorOnda = {};
    for (const r of fins) if (r.ondaDoFimDeJogo != null) fimPorOnda[r.ondaDoFimDeJogo] = (fimPorOnda[r.ondaDoFimDeJogo] || 0) + 1;
    return {
        n,
        concluidas: concluidos.length, taxaDeConclusao: n ? arred(concluidos.length / n, 4) : null,
        fimDeJogo: fins.length, taxaDeFimDeJogo: n ? arred(fins.length / n, 4) : null,
        travas: resumos.filter(r => r.resultado === 'trava').map(r => Object.assign(
            { semente: r.semente, fase: r.fase, dificuldade: r.dificuldade, habilidade: r.habilidade, personagem: r.personagem }, r.trava)),
        tempoDeConclusao: estatistica(concluidos.map(r => r.tempo)),
        tempo: estatistica(resumos.map(r => r.tempo)),
        vidasPerdidas: estatistica(resumos.map(r => r.vidasPerdidas)),
        danoRecebido: estatistica(resumos.map(r => r.danoRecebido)),
        // Dos golpes que chegaram no jogador, quantos ele bloqueou — a defesa EFETIVA do bot.
        taxaDeBloqueio: recebidos ? arred(bloqueados / recebidos, 4) : null,
        pontos: estatistica(resumos.map(r => r.pontos)),
        finalizacoes: estatistica(resumos.map(r => r.finalizacoes)),
        maiorCombo: estatistica(resumos.map(r => r.maiorCombo)),
        mortesPorOnda: somar('mortesPorOnda'),
        fimDeJogoPorOnda: fimPorOnda,
    };
}

// Um lote: n simulações com sementes seguidas. Mede passos por segundo (relógio de parede, fora
// dos resumos — senão a mesma semente não daria o mesmo resumo).
function lote(opcoes) {
    const o = Object.assign({ n: 100, semente: 1 }, opcoes);
    conferirSementes(o.semente, o.n);
    const resumos = [];
    const t0 = process.hrtime.bigint();
    for (let k = 0; k < o.n; k++) resumos.push(simular(Object.assign({}, o, { semente: o.semente + k })));
    const segundos = Number(process.hrtime.bigint() - t0) / 1e9;
    const quadros = resumos.reduce((s, r) => s + r.quadros, 0);
    return { resumos, agregado: agregar(resumos), desempenho: { segundos: arred(segundos, 2), quadros, passosPorSegundo: Math.round(quadros / Math.max(segundos, 1e-9)) } };
}

// ── LINHA DE COMANDO ──────────────────────────────────────────────────────────────────────
function lerArgumentos(argv) {
    const a = { fase: 1, dificuldade: 'normal', habilidade: 'medio', personagem: 'long', n: null, semente: 1, teto: TETO_PADRAO, json: false, matriz: false };
    for (let k = 0; k < argv.length; k++) {
        const chave = argv[k];
        const valor = () => { if (k + 1 >= argv.length) throw new Error(`${chave} precisa de um valor`); return argv[++k]; };
        const inteiro = () => { const v = valor(); if (!/^-?\d+$/.test(v)) throw new Error(`${chave} espera número inteiro, veio "${v}"`); return Number(v); };
        switch (chave) {
            case '--fase': a.fase = inteiro(); break;
            case '--dificuldade': a.dificuldade = valor(); break;
            case '--habilidade': a.habilidade = valor(); break;
            case '--personagem': a.personagem = valor(); break;
            case '--n': a.n = inteiro(); break;
            case '--semente': a.semente = inteiro(); break;
            case '--teto': a.teto = inteiro(); break;
            case '--json': a.json = true; break;
            case '--matriz': a.matriz = true; break;
            case '--ajuda': case '-h': a.ajuda = true; break;
            default: throw new Error(`opção desconhecida: ${chave}`);
        }
    }
    if (!FASES_JOGAVEIS.includes(a.fase)) throw new Error(`--fase ${a.fase} não é jogável (use ${FASES_JOGAVEIS.join(', ')})`);
    if (!DIFICULDADES.includes(a.dificuldade)) throw new Error(`--dificuldade ${a.dificuldade} não existe (use ${DIFICULDADES.join(', ')})`);
    if (!HABILIDADES.includes(a.habilidade)) throw new Error(`--habilidade ${a.habilidade} não existe (use ${HABILIDADES.join(', ')})`);
    if (!Motor.PERSONAGENS[a.personagem]) throw new Error(`--personagem ${a.personagem} não existe (use ${Object.keys(Motor.PERSONAGENS).join(', ')})`);
    if (a.n != null && a.n < 1) throw new Error('--n precisa ser pelo menos 1');
    if (a.teto < 1) throw new Error('--teto precisa ser pelo menos 1 segundo');
    conferirSementes(a.semente, a.n || (a.matriz ? N_MATRIZ : N_LOTE));
    return a;
}

const pct = v => v == null ? '—' : `${Math.round(v * 100)}%`;
const num = (v, casas) => v == null ? '—' : String(arred(v, casas == null ? 1 : casas));
const col = (s, w) => String(s).padEnd(w);
const colD = (s, w) => String(s).padStart(w);
function comandoDaTrava(t, teto) {
    return `node jogo/ferramentas/simular.js --fase ${t.fase} --dificuldade ${t.dificuldade} --habilidade ${t.habilidade} --personagem ${t.personagem} --n 1 --semente ${t.semente}${teto !== TETO_PADRAO ? ` --teto ${teto}` : ''} --json`;
}
function linhaDaTrava(t, teto) {
    const inimigos = t.inimigos.map(i => `${i.tipo}(x=${i.x}, y=${i.y}, ${i.estado}, vida ${i.vida})`).join(', ') || 'nenhum vivo';
    const perto = t.maisPerto ? `${t.maisPerto.tipo} a dx=${t.maisPerto.dx}, dy=${t.maisPerto.dy}` : 'nenhum vivo';
    return `  semente ${t.semente} · fase ${t.fase} ${t.dificuldade} ${t.habilidade} ${t.personagem} · onda ${t.onda}${t.travado ? ' (tela travada)' : ' (tela livre)'}`
        + ` · jogador x=${t.jogador.x} ${t.jogador.estado} · tela ${t.telaX}–${t.telaX + Motor.LARGURA}`
        + `\n    sem acertar ninguém há ${t.semAcertar} s · mais perto: ${perto}`
        + `\n    inimigos: ${inimigos}\n    reproduzir: ${comandoDaTrava(t, teto)}`;
}
// Cabeçalho das travas: não afirma a causa, que o simulador não conhece.
function tituloDasTravas(quantas, teto) {
    const curto = teto < TETO_PADRAO ? `; teto de ${teto} s, abaixo do padrão ${TETO_PADRAO} s — pode ser só luta em andamento` : '';
    return `TRAVAS (${quantas}) — motor ou bot, investigar${curto}. Cada uma se reproduz com a semente:`;
}

function imprimirLote(a, r) {
    const g = r.agregado;
    const out = [];
    out.push(`Fase ${a.fase} (${Motor.FASES[a.fase].nome}) · ${a.dificuldade} · bot ${a.habilidade} · ${a.personagem} · n=${g.n} · sementes ${a.semente}–${a.semente + g.n - 1}`);
    out.push('');
    out.push(`  concluiu        ${pct(g.taxaDeConclusao)}  (${g.concluidas}/${g.n})`);
    out.push(`  fim de jogo     ${pct(g.taxaDeFimDeJogo)}  (${g.fimDeJogo}/${g.n})`);
    out.push(`  TRAVA           ${g.travas.length}`);
    out.push('');
    out.push(`  ${col('', 22)}${colD('média', 8)}${colD('p10', 8)}${colD('p50', 8)}${colD('p90', 8)}${colD('máx', 8)}`);
    const linha = (nome, e) => out.push(`  ${col(nome, 22)}${colD(num(e.media), 8)}${colD(num(e.p10), 8)}${colD(num(e.p50), 8)}${colD(num(e.p90), 8)}${colD(num(e.max), 8)}`);
    linha('tempo p/ concluir (s)', g.tempoDeConclusao);
    linha('vidas perdidas', g.vidasPerdidas);
    linha('dano recebido', g.danoRecebido);
    out.push(`  ${col('golpes bloqueados', 22)}${colD(pct(g.taxaDeBloqueio), 8)}  (dos que chegaram no jogador)`);
    linha('pontos', g.pontos);
    linha('finalizações', g.finalizacoes);
    linha('maior combo', g.maiorCombo);
    out.push('');
    const total = r.resumos[0] ? r.resumos[0].totalDeOndas : 0;
    const ondas = [];
    for (let k = 0; k <= total; k++) ondas.push(k);
    out.push(`  ${col('onda', 22)}${ondas.map(k => colD(k === 0 ? 'caminho' : (k === total ? `${k}·chefe` : k), 9)).join('')}`);
    out.push(`  ${col('mortes (vidas)', 22)}${ondas.map(k => colD(g.mortesPorOnda[k] || 0, 9)).join('')}`);
    out.push(`  ${col('fins de jogo', 22)}${ondas.map(k => colD(g.fimDeJogoPorOnda[k] || 0, 9)).join('')}`);
    if (g.travas.length) {
        out.push('');
        out.push(tituloDasTravas(g.travas.length, a.teto));
        for (const t of g.travas) out.push(linhaDaTrava(t, a.teto));
    }
    return out.join('\n');
}
// Tempo de relógio: vai pro STDERR, nunca pro stdout — senão `> antes.txt` e `> depois.txt` nunca
// sairiam iguais e o `diff` do README mostraria diferença mesmo sem mudança no motor.
function linhaDeDesempenho(d) {
    return `${d.quadros} passos em ${d.segundos} s · ${d.passosPorSegundo.toLocaleString('pt-BR')} passos/s`;
}

function rodarMatriz(a) {
    const n = a.n || N_MATRIZ;
    const celulas = [];
    const t0 = process.hrtime.bigint();
    let quadros = 0;
    for (const fase of FASES_JOGAVEIS) for (const dificuldade of DIFICULDADES) for (const habilidade of HABILIDADES) {
        const r = lote({ fase, dificuldade, habilidade, personagem: a.personagem, n, semente: a.semente, tetoSegundos: a.teto });
        quadros += r.desempenho.quadros;
        celulas.push({ fase, dificuldade, habilidade, agregado: r.agregado });
        if (!a.json) process.stderr.write('.');
    }
    if (!a.json) process.stderr.write('\n');
    const segundos = Number(process.hrtime.bigint() - t0) / 1e9;
    return { n, celulas, desempenho: { segundos: arred(segundos, 1), quadros, passosPorSegundo: Math.round(quadros / Math.max(segundos, 1e-9)) } };
}

function ondaQueMaisMata(mortes) {
    let melhor = null, max = 0;
    for (const [k, v] of Object.entries(mortes)) if (v > max) { max = v; melhor = k; }
    return melhor == null ? '—' : `${melhor === '0' ? 'caminho' : melhor} (${max})`;
}
function imprimirMatriz(a, m) {
    const out = [];
    out.push(`Matriz · ${a.personagem} · n=${m.n} por célula · sementes ${a.semente}–${a.semente + m.n - 1} · teto ${a.teto} s`);
    out.push('');
    const cab = [['fase', 5], ['dific.', 8], ['bot', 7], ['concl.', 7], ['fim', 6], ['trava', 6], ['t p50', 7], ['t p90', 7], ['vidas', 6], ['dano', 7], ['bloq.', 6], ['pontos', 8], ['final.', 7], ['onda que mais mata', 20]];
    out.push('| ' + cab.map(([s, w]) => col(s, w)).join(' | ') + ' |');
    out.push('|' + cab.map(([, w]) => '-'.repeat(w + 2)).join('|') + '|');
    for (const c of m.celulas) {
        const g = c.agregado;
        const valores = [c.fase, c.dificuldade, c.habilidade, pct(g.taxaDeConclusao), pct(g.taxaDeFimDeJogo), g.travas.length,
            num(g.tempoDeConclusao.p50, 0), num(g.tempoDeConclusao.p90, 0), num(g.vidasPerdidas.media, 2), num(g.danoRecebido.media, 0),
            pct(g.taxaDeBloqueio), num(g.pontos.p50, 0), num(g.finalizacoes.media, 1), ondaQueMaisMata(g.mortesPorOnda)];
        out.push('| ' + valores.map((v, k) => col(v, cab[k][1])).join(' | ') + ' |');
    }
    const travas = [].concat(...m.celulas.map(c => c.agregado.travas));
    out.push('');
    if (travas.length) {
        out.push(tituloDasTravas(travas.length, a.teto));
        for (const t of travas) out.push(linhaDaTrava(t, a.teto));
    } else out.push('Nenhuma trava.');
    return out.join('\n');
}

const AJUDA = `uso: node jogo/ferramentas/simular.js [opções]
  --fase N            ${FASES_JOGAVEIS.join(', ')} (padrão 1)
  --dificuldade D     ${DIFICULDADES.join(', ')} (padrão normal)
  --habilidade H      ${HABILIDADES.join(', ')} (padrão medio)
  --personagem P      ${Object.keys(Motor.PERSONAGENS).join(', ')} (padrão long)
  --n N               simulações (padrão 100; na matriz, 30 por célula)
  --semente S         primeira semente; a simulação k usa S + k (padrão 1; S + n - 1 até ${SEMENTE_MAX})
  --teto SEGUNDOS     tempo de jogo máximo antes de contar TRAVA (padrão ${TETO_PADRAO})
  --json              saída em JSON (resumos + agregado); os passos/s vão pro stderr
  --matriz            fases × dificuldades × habilidades, uma tabela`;

function principal(argv) {
    let a;
    try { a = lerArgumentos(argv); }
    catch (erro) { process.stderr.write(`${erro.message}\n\n${AJUDA}\n`); return 2; }
    if (a.ajuda) { process.stdout.write(AJUDA + '\n'); return 0; }
    if (a.matriz) {
        const m = rodarMatriz(a);
        process.stdout.write((a.json ? JSON.stringify({ n: m.n, celulas: m.celulas }, null, 2) : imprimirMatriz(a, m)) + '\n');
        process.stderr.write(linhaDeDesempenho(m.desempenho) + '\n');
        return 0;
    }
    const r = lote({ fase: a.fase, dificuldade: a.dificuldade, habilidade: a.habilidade, personagem: a.personagem, n: a.n || N_LOTE, semente: a.semente, tetoSegundos: a.teto });
    process.stdout.write((a.json ? JSON.stringify({ resumos: r.resumos, agregado: r.agregado }, null, 2) : imprimirLote(a, r)) + '\n');
    process.stderr.write(linhaDeDesempenho(r.desempenho) + '\n');
    return 0;
}

module.exports = { simular, agregar, lote, estatistica, percentil, TETO_PADRAO, FASES_JOGAVEIS };

if (require.main === module) process.exitCode = principal(process.argv.slice(2));
