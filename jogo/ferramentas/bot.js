// PUNHOS DE SHAOLIN — o jogador artificial do simulador de balanceamento (só Node).
//
//     const bot = Bot.criarBot({ habilidade: 'medio', semente: 7 });
//     Motor.passo(mundo, 1 / 60, [bot.decidir(mundo)]);
//
// `decidir(mundo)` olha o mundo como a tela mostraria e devolve a entrada DAQUELE quadro no
// formato de `Motor.entradaVazia()`. A borda `apertou` é calculada aqui, igual ao `entrada.js`
// do navegador: apertar é um quadro com a tecla e `apertou` ligados; o bot solta no seguinte.
// Um botão de toque (soco, chute, especial, pulo, agarrar) nunca fica segurado, então dois
// socos seguidos custam pelo menos dois quadros — como num dedo de verdade.
//
// Ele joga como gente, não como máquina que lê a memória do motor — e só com o que está NA TELA
// (`naTela`): quem está em `mundo.inimigos` além da borda não existe pra ele. Escolhe o alvo (prioriza o
// atordoado de pouca vida, pra finalizar), alinha a profundidade, chega no alcance, emenda a
// sequência quando o golpe acerta, defende quando um inimigo perto começa um golpe — mas com
// ATRASO DE REAÇÃO (curto contra quem ele já vigiava) e CHANCE, da habilidade —, pula e chuta às vezes, usa o especial com
// chi e gente perto, busca chá com pouca vida e anda (ou corre) pra direita quando não há onda.
// O CENÁRIO que mata (`faseDef.perigos`, desenhado na tela): ele não entra andando numa zona — trata
// como parede e contorna, como o motor faz com a IA — e, quando o inimigo está de frente com a zona logo
// atrás, chuta pra derrubá-lo lá dentro. Chá caído numa zona não existe pra ele.
//
// Acaso: gerador semeado próprio (`Motor.criarRng`), NUNCA `Math.random` — mesma semente, mesma
// luta. O conferidor `conferir-simulacao-do-shaolin.js` envenena `Math.random` pra garantir.
'use strict';

const path = require('path');
const Motor = require(path.join(__dirname, '..', 'js', 'motor.js'));

const TOL = Motor.TOLERANCIA_Y;

// ── AS TRÊS HABILIDADES ───────────────────────────────────────────────────────────────────
// reacao: segundos entre VER o golpe inimigo começar e apertar defender (sorteado no intervalo)
// defende: chance de tentar defender um golpe que viu · corrente: chance de emendar o próximo soco
// mira: quanto de TOLERANCIA_Y ele aceita como "alinhado" (acima de 1 erra golpe de vez em quando)
// alcance: quanto do alcance do soco ele acha que tem (acima de 1 soca o vento)
// ritmo: pausa máxima entre um golpe e o próximo · finaliza: chance de ir agarrar o atordoado
// especial: chance de usar o especial quando a situação pede · chaAbaixo: fração da vida que manda buscar chá
// pulaChuta: chance por decisão de ataque de trocar o soco por pulo + chute · malabarismo: vontade de
// pular atrás de quem foi lançado (sorteada a cada quadro com ÷8: bom ~85% dos lançamentos, novato ~15%) · agarra: chance de agarrar e arremessar quem está colado
// contraArmadura: chance de chutar (derruba) em vez de socar quem tem armadura
// corre: usa dois toques pra correr nas distâncias longas e na investida
// reacaoAtenta: a reação quando o golpe vem de quem ele JÁ estava vigiando (inimigo na tela,
// alinhado, de frente, chegando no alcance em que a IA ataca). É a antecipação: quem joga não
// reage a 0,16 s "do nada", mas vê o inimigo encostar e está com o dedo no botão. Só com `reacao`,
// que é mais lenta que o arranque dos golpes comuns (garra 0,16 s, sombra 0,18 s, mestre 0,12 s),
// o novato e o medio quase nunca bloqueavam. A taxa EFETIVA de bloqueio sai no resumo (README).
// (Guarda levantada à toa, por tempo, foi tentada e piora o bot: parado de guarda ele não bate e
// o grupo cerca — a antecipação que funciona é a do reflexo, não a da postura.)
const HABILIDADES = Object.freeze({
    novato: Object.freeze({ reacaoAtenta: [0.04, 0.14], reacao: [0.32, 0.55], defende: 0.3, corrente: 0.45, mira: 1.05, alcance: 1.12, ritmo: 0.45, finaliza: 0.35, especial: 0.3, chaAbaixo: 0.25, pulaChuta: 0.04, malabarismo: 0.05, agarra: 0.04, chute: 0.25, contraArmadura: 0.2, corre: false }),
    medio: Object.freeze({ reacaoAtenta: [0.04, 0.12], reacao: [0.18, 0.32], defende: 0.6, corrente: 0.75, mira: 0.8, alcance: 0.97, ritmo: 0.2, finaliza: 0.7, especial: 0.7, chaAbaixo: 0.4, pulaChuta: 0.05, malabarismo: 0.25, agarra: 0.1, chute: 0.2, contraArmadura: 0.45, corre: true }),
    bom: Object.freeze({ reacaoAtenta: [0.03, 0.08], reacao: [0.08, 0.16], defende: 0.85, corrente: 0.95, mira: 0.6, alcance: 0.88, ritmo: 0.06, finaliza: 0.95, especial: 1, chaAbaixo: 0.5, pulaChuta: 0.06, malabarismo: 0.5, agarra: 0.15, chute: 0.15, contraArmadura: 0.6, corre: true }),
});

// Espelho da régua do motor (`levantando` → atordoado com "FINALIZE!"): chefe abaixo de 10% da
// vida, o resto abaixo de 22%. O motor não exporta esse número; se mudar lá, muda aqui.
function finalizavel(i) {
    return i.estado === 'atordoado' && i.vida <= i.vidaMax * (i.def.chefe ? 0.1 : 0.22);
}
// As teclas com forma fixa (o bot escreve nelas por nome). Se o motor ganhar ou trocar um botão,
// o bot para na hora com a lista das duas — melhor que simular com uma tecla que nunca é apertada.
function teclasSoltas() {
    return { esquerda: false, direita: false, cima: false, baixo: false, soco: false, chute: false, especial: false, pular: false, agarrar: false, defender: false };
}
if (Object.keys(teclasSoltas()).join() !== Motor.BOTOES.join()) {
    throw new Error(`bot.js: os botões do motor mudaram (${Motor.BOTOES.join()}); atualize teclasSoltas e emitir (${Object.keys(teclasSoltas()).join()})`);
}
// A TELA é o que o bot vê: [camera.x, camera.x + LARGURA], o mesmo recorte que `principal.js`
// desenha. Um corpo conta se um pedaço dele aparece. Quem está em `mundo.inimigos` mas fora
// daqui (o chefe esperando além da borda, o reforço que ainda vai entrar) não existe pra ele —
// como não existe pra quem joga.
function naTela(mundo, x, meia) {
    const c = mundo.camera.x;
    return x + meia > c && x - meia < c + Motor.LARGURA;
}
function corpoNaTela(mundo, ent) { return naTela(mundo, ent.x, Motor.MEIA_LARGURA * (ent.escala || 1)); }
const MEIA_COISA_PEQUENA = 12;   // flecha, chá: o desenho tem uns 24 px de largura
// Quanto antes de o inimigo entrar no alcance dele o bot já o vigia (px): quem joga vê o inimigo
// chegando, não só parado no alcance. E por quanto tempo depois de vigiar ainda está atento (s).
const FOLGA_DA_VIGIA = 40, ATENTO_POR = 0.3;
// O cenário: quanto à frente (s) ele olha antes de dar o passo, e por quanto tempo (s) mantém o desvio
// escolhido (senão o alinhamento com o alvo puxava a profundidade de volta e ele tremia na quina).
const OLHAR_A_FRENTE = 0.15, DESVIO_POR = 0.4;
const PX_POR_Y = Motor.CHAO_BASE - Motor.CHAO_TOPO;
// O voo do derrubado (vz 280 do motor, gravidade 2000): 0,28 s — o chute leva o inimigo recuo × 0,28 px.
const VOO_DO_DERRUBADO = 0.28;
function podeAgir(ent) { return ent.estado === 'parado' || ent.estado === 'andando'; }
function sinal(v) { return v > 0 ? 1 : v < 0 ? -1 : 0; }

function criarBot(opcoes) {
    const o = opcoes || {};
    const hab = HABILIDADES[o.habilidade || 'medio'];
    if (!hab) throw new Error(`habilidade desconhecida: ${o.habilidade} (use ${Object.keys(HABILIDADES).join(', ')})`);
    const indice = o.indice || 0;
    // Semente própria, derivada da da luta: o bot não mexe no `mundo.rng` (senão a IA inimiga
    // mudaria só porque o bot pensou diferente).
    const rng = Motor.criarRng(((o.semente == null ? 1 : o.semente) * 2654435761 + 97) >>> 0);

    let anterior = teclasSoltas();
    const ameacas = new Map();          // chave do golpe/projétil → { reagirEm, defender }
    const decisoes = new Map();         // chave do golpe do próprio jogador → emenda ou não
    let alvoId = null;                  // alvo atual (com histerese pra não ficar trocando)
    let esperaAte = 0;                  // pausa entre golpes (o "ritmo" da habilidade)
    let estavaAtacando = false;
    let plano = null;                   // 'pulo' enquanto um pulo-chute está no ar
    let finalizarId = null, finalizarDecidido = null;
    let proximaFaxina = 0;
    const vigiado = new Map();          // id do inimigo → último instante em que ele ameaçava
    let desvio = null;                  // { y: ±1, ate } enquanto contorna uma zona do cenário
    let mundoAtual = null, jAtual = null;

    // Sorteia UMA vez por chave e lembra. A faxina roda ANTES de gravar: limpar depois apagaria a
    // decisão recém-tomada, e o quadro seguinte sortearia de novo o mesmo golpe (e gastaria um
    // número a mais do rng). Teto de 64 chaves: só vale a do golpe em curso, as velhas não voltam.
    function decidirUmaVez(chave, chance) {
        if (!decisoes.has(chave)) {
            if (decisoes.size >= 64) decisoes.clear();
            decisoes.set(chave, rng.chance(chance));
        }
        return decisoes.get(chave);
    }

    // ── TECLAS ───────────────────────────────────────────────────────────────────────────
    function tocar(quer, b) {
        // Tecla que estava apertada no quadro anterior precisa soltar antes: aqui ela fica solta
        // e o toque sai no próximo quadro em que o bot ainda quiser.
        if (!anterior[b]) quer[b] = true;
    }
    function segurarDirecao(quer, mx, my) {
        if (mx > 0) quer.direita = true; else if (mx < 0) quer.esquerda = true;
        if (my > 0) quer.baixo = true; else if (my < 0) quer.cima = true;
    }
    // A zona como parede: o passo que (olhando OLHAR_A_FRENTE à frente) entraria numa zona não é dado.
    // Entraria pela profundidade: segue só em x. Pelo x: segue só na profundidade — e, se ele só queria
    // ir em x, contorna pela borda de profundidade mais perto que existe (e mantém esse desvio um pouco).
    function evitarPerigos(quer) {
        const mundo = mundoAtual, j = jAtual;
        const lista = mundo && mundo.faseDef.perigos;
        if (!lista || !lista.length || !j || j.z > 0 || !podeAgir(j)) return;
        const t = mundo.tempo;
        let mx = (quer.direita ? 1 : 0) - (quer.esquerda ? 1 : 0), my = (quer.baixo ? 1 : 0) - (quer.cima ? 1 : 0);
        if (desvio && t < desvio.ate && my === -desvio.y) my = desvio.y;
        if (!mx && !my) return;
        if (Motor.perigoEm(mundo, j.x, j.y)) return;          // já dentro: o motor põe pra fora
        const vel = j.correndo ? j.def.corrida : j.def.velocidade;
        const dx = mx * vel * OLHAR_A_FRENTE, dy = my * vel * 0.55 / PX_POR_Y * OLHAR_A_FRENTE;
        // O CAMINHO todo, não só a ponta: na diagonal, a ponta pode passar da quina e o meio cortar a zona.
        const cruza = (ax, ay) => { for (const f of [0.25, 0.5, 0.75, 1]) { const z = Motor.perigoEm(mundo, j.x + ax * f, j.y + ay * f); if (z) return z; } return null; };
        const z = cruza(dx, dy);
        if (z) {
            if (my && !cruza(dx, 0)) my = 0;                                  // entraria pela profundidade
            else {
                mx = 0;                                                       // entraria pelo x
                if (!my || cruza(0, dy)) {
                    const sobe = z.y0 > 0 ? j.y - z.y0 : Infinity, desce = z.y1 < 1 ? z.y1 - j.y : Infinity;
                    my = sobe === Infinity && desce === Infinity ? 0 : sobe <= desce ? -1 : 1;
                    desvio = { y: my, ate: t + DESVIO_POR };
                }
            }
        }
        quer.direita = mx > 0; quer.esquerda = mx < 0; quer.baixo = my > 0; quer.cima = my < 0;
    }
    function naZona(mundo, coisa) { return !!Motor.perigoEm(mundo, coisa.x, coisa.y); }

    function emitir(quer) {
        evitarPerigos(quer);
        // Escrito por extenso de propósito: o laço sobre `Motor.BOTOES` com chave variável era
        // metade do custo do bot no perfil (acesso "megamórfico"). `teclasSoltas` confere, ao
        // carregar, que esta lista é a do motor.
        const a = anterior;
        const e = {
            esquerda: !!quer.esquerda, direita: !!quer.direita, cima: !!quer.cima, baixo: !!quer.baixo,
            soco: !!quer.soco, chute: !!quer.chute, especial: !!quer.especial, pular: !!quer.pular,
            agarrar: !!quer.agarrar, defender: !!quer.defender, apertou: null,
        };
        e.apertou = {
            esquerda: e.esquerda && !a.esquerda, direita: e.direita && !a.direita, cima: e.cima && !a.cima, baixo: e.baixo && !a.baixo,
            soco: e.soco && !a.soco, chute: e.chute && !a.chute, especial: e.especial && !a.especial, pular: e.pular && !a.pular,
            agarrar: e.agarrar && !a.agarrar, defender: e.defender && !a.defender,
        };
        anterior = {
            esquerda: e.esquerda, direita: e.direita, cima: e.cima, baixo: e.baixo, soco: e.soco, chute: e.chute,
            especial: e.especial, pular: e.pular, agarrar: e.agarrar, defender: e.defender,
        };
        return e;
    }

    // ── O QUE ELE VÊ ─────────────────────────────────────────────────────────────────────
    function limites(mundo) {
        const L = Motor.LARGURA, comp = mundo.faseDef.comprimento;
        return mundo.travado
            ? { esq: Math.max(20, mundo.travaX + 24), dir: Math.min(comp - 20, mundo.travaX + L - 24) }
            : { esq: Math.max(20, mundo.camera.x + 16), dir: comp - 20 };
    }
    function alcanceDe(j, golpe) { return golpe.alcance + (j.escala - 1) * 20; }

    // Golpes inimigos que começaram perto (ou projéteis vindo na linha): cada um é visto UMA vez,
    // e nessa hora o bot sorteia se vai defender e quando (reação).
    function atualizarAmeacas(mundo, j) {
        const t = mundo.tempo;
        for (const i of mundo.inimigos) {
            if (i.estado !== 'atacando' || !i.golpe || !corpoNaTela(mundo, i)) continue;
            const g = i.golpe;
            if (i.quadro > g.inicio + (g.ativo || 0)) continue;
            const dx = j.x - i.x;
            if (g.tipo === 'projetil') continue;   // o projétil vira ameaça quando sai (abaixo)
            const alcance = (g.alcance || 0) + (i.escala - 1) * 20 + 18 * j.escala + (g.avanco ? g.avanco * (g.ativo || 0) : 0) + 30;
            if (Math.abs(i.y - j.y) > TOL + 0.03 || Math.abs(dx) > alcance) continue;
            if (!g.dosDoisLados && sinal(dx) !== i.virado && dx !== 0) continue;
            const chave = `g${i.id}@${(t - i.quadro).toFixed(3)}`;
            if (!ameacas.has(chave)) {
                const atento = vigiado.has(i.id) && t - vigiado.get(i.id) <= ATENTO_POR;
                const reacao = atento ? hab.reacaoAtenta : hab.reacao;
                ameacas.set(chave, {
                    fonte: i, inicio: t - i.quadro, fimEm: t - i.quadro + g.inicio + (g.ativo || 0) + 0.05,
                    reagirEm: t + rng.entre(reacao[0], reacao[1]), defender: rng.chance(hab.defende),
                });
            }
        }
        for (const p of mundo.projeteis) {
            if (p.time !== 'inimigo' || Math.abs(p.y - j.y) > TOL + 0.02 || !naTela(mundo, p.x, MEIA_COISA_PEQUENA)) continue;
            const dx = j.x - p.x;
            if (sinal(dx) !== sinal(p.vx) || Math.abs(dx) > 320) continue;
            const chave = `p${p.id}`;
            if (!ameacas.has(chave)) {
                ameacas.set(chave, {
                    fonte: p.dono, projetil: p, fimEm: t + Math.abs(dx) / Math.abs(p.vx) + 0.1,
                    reagirEm: t + rng.entre(hab.reacao[0], hab.reacao[1]), defender: rng.chance(hab.defende),
                });
            }
        }
        // Faxina uma vez por segundo de jogo, não a cada quadro: o mapa é pequeno e o laço custa.
        if (t >= proximaFaxina) {
            proximaFaxina = t + 1;
            for (const [chave, a] of ameacas) if (t > a.fimEm + 2) ameacas.delete(chave);
        }
    }
    // Quem pode bater a qualquer momento: na tela, vivo, livre pra agir, alinhado, de frente e
    // chegando na distância em que a IA dele ataca (`ia.alcance × escala + 8`, que se aprende
    // apanhando). O bot anota quando viu cada um assim — é quem ele está vigiando.
    function vigiar(mundo, j) {
        const t = mundo.tempo;
        for (const i of mundo.inimigos) {
            if (!Motor.vivo(i) || !podeAgir(i) || Math.abs(i.y - j.y) > TOL || !corpoNaTela(mundo, i)) continue;
            const dx = j.x - i.x;
            if (sinal(dx) !== i.virado && dx !== 0) continue;
            if (Math.abs(dx) <= i.def.ia.alcance * i.escala + 8 + FOLGA_DA_VIGIA) vigiado.set(i.id, t);
        }
        // Faxina junto com a das ameaças: quem não é visto há um tempo sai do mapa.
        if (t >= proximaFaxina) for (const [id, visto] of vigiado) if (t - visto > 2) vigiado.delete(id);
    }

    // A ameaça que manda defender AGORA: já passou o tempo de reação e o golpe ainda não acabou.
    function ameacaAtiva(mundo) {
        const t = mundo.tempo;
        for (const a of ameacas.values()) {
            if (!a.defender || t < a.reagirEm || t > a.fimEm) continue;
            if (a.projetil) { if (mundo.projeteis.includes(a.projetil)) return a; continue; }
            const f = a.fonte;
            if (f.estado === 'atacando' && Math.abs((t - f.quadro) - a.inicio) < 1e-6) return a;
        }
        return null;
    }

    function escolherAlvo(mundo, j, lim) {
        let melhor = null, melhorNota = Infinity;
        for (const i of mundo.inimigos) {
            if (!Motor.vivo(i) || i.agarradoPor || !corpoNaTela(mundo, i)) continue;
            let nota = Math.abs(i.x - j.x) + Math.abs(i.y - j.y) * 400;
            if (finalizavel(i)) nota -= 400;
            else if (i.vida <= i.vidaMax * 0.3) nota -= 80;
            // Fora do pedaço de chão onde o jogador pode ficar: só se não houver outro.
            if (i.x < lim.esq - 60 || i.x > lim.dir + 60) nota += 3000;
            if (i.id === alvoId) nota -= 60;
            if (nota < melhorNota) { melhor = i; melhorNota = nota; }
        }
        alvoId = melhor ? melhor.id : null;
        return melhor;
    }

    // ── DECIDIR O QUADRO ─────────────────────────────────────────────────────────────────
    function decidir(mundo) {
        const quer = teclasSoltas();
        const j = mundo.jogadores[indice];
        mundoAtual = mundo; jAtual = j;
        if (!j || j.estado === 'morto') { plano = null; return emitir(quer); }
        const t = mundo.tempo;
        const lim = limites(mundo);
        vigiar(mundo, j);
        atualizarAmeacas(mundo, j);

        // Agarrando: uma ou duas joelhadas e arremessa.
        if (j.estado === 'agarrando') {
            if (j.quadro > 0.15 && j.quadro < 0.5 && rng.chance(0.08)) tocar(quer, 'chute');
            else if (j.quadro >= 0.5) tocar(quer, 'soco');
            return emitir(quer);
        }

        const alvo = escolherAlvo(mundo, j, lim);

        // No ar: chuta quando o alvo chega perto (ou quando começa a cair).
        if (j.estado === 'pulando' || j.z > 0) {
            if (j.estado === 'pulando' && plano === 'pulo') {
                const perto = alvo && Math.abs(alvo.x - j.x) < 90 && Math.abs(alvo.y - j.y) < TOL;
                if (perto || j.vz < -200) { tocar(quer, 'chute'); plano = null; }
                if (alvo) segurarDirecao(quer, sinal(alvo.x - j.x), 0);
            }
            return emitir(quer);
        }
        plano = null;

        // Atacando: emendar a sequência se o golpe acertou (decidido UMA vez por golpe).
        if (j.estado === 'atacando') {
            estavaAtacando = true;
            const g = j.golpe;
            if (g && g.proximo && j.acertou && j.quadro >= g.inicio + g.ativo) {
                const chave = `${j.golpeNome}@${(t - j.quadro).toFixed(3)}`;
                if (decidirUmaVez(chave, hab.corrente)) tocar(quer, 'soco');
            }
            return emitir(quer);
        }

        const ameaca = ameacaAtiva(mundo);
        if (j.estado === 'defendendo') {
            if (ameaca) { quer.defender = true; segurarDirecao(quer, sinal(ameaca.fonte.x - j.x), 0); }
            return emitir(quer);
        }
        if (!podeAgir(j)) return emitir(quer);

        if (estavaAtacando) { estavaAtacando = false; esperaAte = t + rng.entre(0, hab.ritmo); }
        if (ameaca && ameaca.fonte) {
            quer.defender = true;
            segurarDirecao(quer, sinal(ameaca.fonte.x - j.x), 0);
            return emitir(quer);
        }

        // Pouca vida: chá no chão, ou um vaso de chá por quebrar, se ninguém está em cima.
        const inimigoColado = alvo && Math.abs(alvo.x - j.x) < 150 && Math.abs(alvo.y - j.y) < 0.3;
        if (j.vida < j.vidaMax * hab.chaAbaixo && !inimigoColado) {
            const cha = maisPerto(j, mundo.itens.filter(it => it.tipo === 'cha' && it.x >= lim.esq - 10 && it.x <= lim.dir + 10 && naTela(mundo, it.x, MEIA_COISA_PEQUENA) && !naZona(mundo, it)));
            if (cha) { irAte(quer, j, cha.x, cha.y, 0); return emitir(quer); }
            const vaso = maisPerto(j, mundo.objetos.filter(v => v.estado === 'parado' && v.item === 'cha' && v.x >= lim.esq && v.x <= lim.dir && corpoNaTela(mundo, v)));
            if (vaso && !mundo.travado) { baterEm(quer, j, vaso, 0.5, 0.5); return emitir(quer); }
        }

        if (!alvo) {
            // Sem inimigo NA TELA: pega o que estiver no chão e caminha pra direita (correndo, se
            // sabe). Com a tela travada, vai pro meio — é o que atrai quem ainda está lá fora.
            const util = maisPerto(j, mundo.itens.filter(it => it.x >= lim.esq && it.x <= lim.dir && it.x > j.x - 200 && naTela(mundo, it.x, MEIA_COISA_PEQUENA) && !naZona(mundo, it)
                && ((it.tipo === 'cha' && j.vida < j.vidaMax * 0.85) || (it.tipo === 'pergaminho' && j.chi < 70))));
            if (util) { irAte(quer, j, util.x, util.y, 0); return emitir(quer); }
            // Na Arena (uma tela só), entre as ondas, também: o meio é onde se espera a próxima.
            if (mundo.travado || mundo.faseDef.infinita) { irAte(quer, j, mundo.travaX + Motor.LARGURA / 2, 0.5, 20); return emitir(quer); }
            andarPraDireita(quer, j);
            return emitir(quer);
        }

        lutar(mundo, quer, j, alvo, lim);
        return emitir(quer);
    }

    function maisPerto(j, lista) {
        let melhor = null, d = Infinity;
        for (const c of lista) { const dd = Math.abs(c.x - j.x) + Math.abs(c.y - j.y) * 400; if (dd < d) { d = dd; melhor = c; } }
        return melhor;
    }
    function irAte(quer, j, x, y, folgaX) {
        const dx = x - j.x, dy = y - j.y;
        segurarDirecao(quer, Math.abs(dx) > Math.max(6, folgaX) ? sinal(dx) : 0, Math.abs(dy) > TOL * 0.4 ? sinal(dy) : 0);
    }
    function correr(quer, j, direcao) {
        // Dois toques na direção: solta um quadro e aperta de novo até o motor pôr `correndo`.
        const b = direcao > 0 ? 'direita' : 'esquerda';
        if (j.correndo) { quer[b] = true; return; }
        if (!anterior[b]) quer[b] = true;
    }
    function andarPraDireita(quer, j) {
        if (hab.corre) correr(quer, j, 1); else quer.direita = true;
        if (Math.abs(j.y - 0.5) > 0.1) quer[j.y < 0.5 ? 'baixo' : 'cima'] = true;
    }

    // Chegar no alvo alinhado e bater. `fatorMira`/`fatorAlcance` sobrescrevem a habilidade (vaso).
    function baterEm(quer, j, alvo, fatorMira, fatorAlcance) {
        const golpe = j.def.golpes.soco1;
        const meia = Motor.MEIA_LARGURA * (alvo.escala || 1);
        const dx = alvo.x - j.x, dy = alvo.y - j.y;
        const lado = sinal(dx) || j.virado;
        const alcance = (alcanceDe(j, golpe) + meia) * (fatorAlcance || hab.alcance) - 6;
        const alinhado = Math.abs(dy) <= TOL * (fatorMira || hab.mira);
        if (alinhado && Math.abs(dx) <= alcance) {
            if (j.virado !== lado && Math.abs(dx) > 4) { segurarDirecao(quer, lado, 0); return false; }
            tocar(quer, 'soco');
            return true;
        }
        const destino = alvo.x - lado * Math.max(20, alcance * 0.6);
        irAte(quer, j, destino, alvo.y, 4);
        return false;
    }

    function lutar(mundo, quer, j, alvo, lim) {
        const t = mundo.tempo;
        const dx = alvo.x - j.x, dy = alvo.y - j.y;
        const lado = sinal(dx) || j.virado;
        const dist = Math.abs(dx);
        const alinhado = Math.abs(dy) <= TOL * hab.mira;
        const especial = j.def.golpes.especial;

        // FINALIZAR: atordoado de pouca vida. Decide uma vez por alvo se vai tentar.
        if (finalizavel(alvo)) {
            if (finalizarId !== alvo.id) { finalizarId = alvo.id; finalizarDecidido = rng.chance(hab.finaliza); }
            if (finalizarDecidido) {
                const dentro = dist <= 48 && Math.abs(dy) < TOL * 0.8;
                if (dentro && (j.virado === lado || dist < 4)) { tocar(quer, 'agarrar'); return; }
                if (dentro) { segurarDirecao(quer, lado, 0); return; }
                irAte(quer, j, alvo.x - lado * 30, alvo.y, 3);
                return;
            }
        }

        // No chão, levantando ou invulnerável: não adianta bater — chega perto e espera.
        if (alvo.estado === 'caido' || alvo.estado === 'levantando' || alvo.invulneravel > 0 || alvo.estado === 'finalizado') {
            irAte(quer, j, alvo.x - lado * 70, alvo.y, 10);
            return;
        }

        // Lançado: malabarismo (pula atrás e chuta) ou espera cair.
        if (alvo.estado === 'lancado' || alvo.z > 0) {
            if (alvo.z > 20 && dist < 110 && Math.abs(dy) < TOL && t >= esperaAte && rng.chance(hab.malabarismo / 8)) {
                segurarDirecao(quer, lado, 0); tocar(quer, 'pular'); plano = 'pulo';
                return;
            }
            irAte(quer, j, alvo.x - lado * 60, alvo.y, 10);
            return;
        }

        if (t < esperaAte) { if (!alinhado) irAte(quer, j, j.x, alvo.y, 999); return; }

        // ESPECIAL: com chi e gente perto (giro), ou alvo na linha (projétil); sempre com chefe. O
        // projétil também vai em quem aparece na beirada da tela mas está além do chão onde o
        // jogador pode ficar (o arqueiro na borda): visível, e o soco não chega.
        if (j.chi >= especial.chi && alinhado) {
            const pertos = mundo.inimigos.filter(i => Motor.vivo(i) && corpoNaTela(mundo, i) && Math.abs(i.x - j.x) < 130 && Math.abs(i.y - j.y) < TOL).length;
            const querEspecial = especial.tipo === 'projetil'
                ? (dist > 90 && dist < 520 && (pertos >= 2 || alvo.def.chefe || alvo.x < lim.esq || alvo.x > lim.dir))
                : (pertos >= 2 || (alvo.def.chefe && dist < 110));
            if (querEspecial) {
                if (decidirUmaVez(`esp@${Math.floor(t)}`, hab.especial)) {
                    if (especial.tipo === 'projetil' && j.virado !== lado) { segurarDirecao(quer, lado, 0); return; }
                    tocar(quer, 'especial');
                    return;
                }
            }
        }

        const soco = j.def.golpes.soco1, chute = j.def.golpes.chute, investida = j.def.golpes.investida;
        const meia = Motor.MEIA_LARGURA * alvo.escala;
        const alcSoco = (alcanceDe(j, soco) + meia) * hab.alcance - 6;
        const alcChute = (alcanceDe(j, chute) + meia) * hab.alcance - 6;

        // INVESTIDA: correndo atrás de quem foge (arqueiro) ou de longe, aperta soco sem parar.
        if (hab.corre && j.correndo && alinhado && dist <= alcanceDe(j, investida) + meia + investida.avanco * investida.ativo * 0.6 && dist > alcSoco) {
            segurarDirecao(quer, lado, 0);
            tocar(quer, 'soco');
            return;
        }

        // Empurrar pro cenário: inimigo comum de frente, com a zona onde o chute o derruba. Sem sorteio —
        // é o "quando for fácil" que quem joga não deixa passar.
        if (alinhado && dist <= alcChute && !alvo.def.chefe && podeAgir(alvo) || alinhado && dist <= alcChute && !alvo.def.chefe && alvo.estado === 'atingido') {
            const pouso = alvo.x + lado * chute.recuo * VOO_DO_DERRUBADO;
            if (Motor.perigoEm(mundo, pouso, alvo.y)) {
                if (j.virado !== lado && dist > 4) { segurarDirecao(quer, lado, 0); return; }
                tocar(quer, 'chute');
                return;
            }
        }

        if (alinhado && dist <= alcChute) {
            if (j.virado !== lado && dist > 4) { segurarDirecao(quer, lado, 0); return; }
            const escolha = rng.proximo();
            // Contra armadura, o soco leve não interrompe: quem sabe prefere o chute, que derruba.
            const chanceDeChute = alvo.def.armadura ? Math.max(hab.chute, hab.contraArmadura) : hab.chute;
            if (dist <= alcSoco) {
                const pegavel = !alvo.def.chefe && !alvo.def.armadura && dist <= 46;
                if (pegavel && escolha < hab.agarra) { tocar(quer, 'agarrar'); return; }
                if (escolha < hab.agarra + hab.pulaChuta) { segurarDirecao(quer, lado, 0); tocar(quer, 'pular'); plano = 'pulo'; return; }
                tocar(quer, escolha < hab.agarra + hab.pulaChuta + chanceDeChute ? 'chute' : 'soco');
                return;
            }
            // Entre o alcance do soco e o do chute: o sorteio é por QUADRO, então a chance é pequena
            // (senão ele sempre chutaria antes de chegar perto).
            if (escolha < chanceDeChute * 0.08) { tocar(quer, 'chute'); return; }
        }

        // Aproximar: ponto a uma fração do alcance, do lado em que já está; longe, corre.
        const destino = alvo.x - lado * Math.max(24, alcSoco * 0.65);
        const ddx = destino - j.x;
        const my = Math.abs(dy) > TOL * hab.mira * 0.5 ? sinal(dy) : 0;
        if (hab.corre && Math.abs(ddx) > 220 && Math.abs(dy) < TOL * 2) { correr(quer, j, sinal(ddx)); segurarDirecao(quer, 0, my); return; }
        segurarDirecao(quer, Math.abs(ddx) > 5 ? sinal(ddx) : 0, my);
    }

    return { decidir, habilidade: hab };
}

module.exports = { HABILIDADES, criarBot };
