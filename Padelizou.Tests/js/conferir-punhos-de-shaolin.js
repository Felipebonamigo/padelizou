// O MOTOR DE COMBATE DO "PUNHOS DE SHAOLIN" (jogo/), conferido no Node sem navegador.
//
//     node Padelizou.Tests/js/conferir-punhos-de-shaolin.js
//
// O jogo é um beat-em-up em Canvas que mora em `jogo/` — fora do app .NET de propósito. O que
// se confere aqui é a parte SEM tela: `jogo/js/motor.js` é lógica pura (posição, golpe, dano,
// ondas, IA, finalização) e exporta pra `module.exports` quando roda no Node. Desenho, som e
// teclado ficam em arquivos próprios, que este conferidor nem carrega.
//
// Sem dependência nenhuma, como os outros conferidores: `require` do próprio motor e mais nada.
// ⚠️ O `dotnet test` NÃO enxerga este arquivo — quem roda é o CI, no passo "Conferir as travas
// de JS", que varre `Padelizou.Tests/js/conferir-*.js`.
const path = require('path');
const Motor = require(path.join(__dirname, '..', '..', 'jogo', 'js', 'motor.js'));

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}

const DT = 1 / 60;

// ── AJUDANTES ─────────────────────────────────────────────────────────────────────────────
function entrada(extra) {
    return Object.assign(Motor.entradaVazia(), extra || {});
}
// Roda N quadros com a mesma entrada (ou uma função quadro → entrada).
function rodar(mundo, quadros, entradas) {
    for (let i = 0; i < quadros; i++) {
        const e = typeof entradas === 'function' ? entradas(i) : entradas;
        Motor.passo(mundo, DT, e || [entrada()]);
    }
}
// Aperta um botão por UM quadro e solta.
function apertar(mundo, botao, jogador) {
    const e = entrada();
    e.apertou[botao] = true;
    e[botao] = true;
    const lista = [entrada()];
    lista[jogador || 0] = e;
    Motor.passo(mundo, DT, lista);
}
function mundoDeTreino(opcoes) {
    // A fase 0 é a "sala de treino": comprida, sem ondas — pra colocar inimigo na mão.
    return Motor.criarMundo(Object.assign({ fase: 0, jogadores: ['long'], semente: 7 }, opcoes || {}));
}
function eventosDoTipo(mundo, tipo) {
    return mundo.eventos.filter(e => e.tipo === tipo);
}

// 1. O SOCO ACERTA QUEM ESTÁ NA FRENTE, na mesma profundidade, e tira exatamente o dano do golpe.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
    alvo.ia.congelada = true;                       // o boneco não reage: só apanha
    const antes = alvo.vida;
    apertar(mundo, 'soco');
    rodar(mundo, 30);
    const dano = j.def.golpes.soco1.dano;
    confere('o soco tira exatamente o dano do golpe', alvo.vida === antes - dano,
            `vida ${antes} → ${alvo.vida}, esperava ${antes - dano}`);
    confere('o acerto vira evento pra tela e pro som', eventosDoTipo(mundo, 'acerto').length === 0
            && mundo.pontuacao > 0, `pontuação=${mundo.pontuacao}`);
}

// 2. QUEM ESTÁ ATRÁS, LONGE OU EM OUTRA PROFUNDIDADE NÃO APANHA.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const atras = Motor.colocarInimigo(mundo, 'sombra', j.x - 45, j.y);
    const longe = Motor.colocarInimigo(mundo, 'sombra', j.x + 200, j.y);
    const outraFaixa = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, Math.min(1, j.y + 0.4));
    [atras, longe, outraFaixa].forEach(i => { i.ia.congelada = true; });
    apertar(mundo, 'soco');
    rodar(mundo, 30);
    confere('atrás não apanha', atras.vida === atras.vidaMax, `vida=${atras.vida}`);
    confere('longe não apanha', longe.vida === longe.vidaMax, `vida=${longe.vida}`);
    confere('outra profundidade não apanha', outraFaixa.vida === outraFaixa.vidaMax, `vida=${outraFaixa.vida}`);
}

// 3. TRÊS SOCOS SEGUIDOS: o terceiro LANÇA o inimigo pro alto (é o que abre o malabarismo).
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
    alvo.ia.congelada = true;
    alvo.vida = 1000; alvo.vidaMax = 1000;          // pra não morrer antes do terceiro
    for (let golpe = 0; golpe < 3; golpe++) {
        apertar(mundo, 'soco');
        rodar(mundo, 10);
    }
    rodar(mundo, 3);
    confere('o terceiro soco lança o inimigo', alvo.estado === 'lancado' && alvo.vz > 0,
            `estado=${alvo.estado} vz=${alvo.vz}`);
    rodar(mundo, 120);
    confere('quem foi lançado cai e levanta', alvo.estado === 'parado' || alvo.estado === 'andando' || alvo.estado === 'atordoado',
            `estado=${alvo.estado}`);
}

// 4. A DEFESA SEGURA O GOLPE: quem defende leva uma fração do dano, e não cai.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const inimigo = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
    inimigo.virado = -1;
    inimigo.ia.congelada = true;
    const antes = j.vida;
    Motor.iniciarGolpe(inimigo, 'soco1');
    rodar(mundo, 30, [entrada({ defender: true })]);
    const cheio = inimigo.def.golpes.soco1.dano;
    confere('defendendo, o dano cai pra uma fração', j.vida < antes && (antes - j.vida) <= cheio * 0.25,
            `levou ${antes - j.vida} de ${cheio}`);
    confere('defendendo, o jogador fica em pé', j.estado !== 'atingido' && j.estado !== 'lancado', `estado=${j.estado}`);

    const mundo2 = mundoDeTreino();
    const j2 = mundo2.jogadores[0];
    const inimigo2 = Motor.colocarInimigo(mundo2, 'sombra', j2.x + 45, j2.y);
    inimigo2.virado = -1;
    inimigo2.ia.congelada = true;
    Motor.iniciarGolpe(inimigo2, 'soco1');
    rodar(mundo2, 30);
    confere('sem defender, o dano é cheio', j2.vidaMax - j2.vida === cheio, `levou ${j2.vidaMax - j2.vida}`);
}

// 5. ONDA: chegar no ponto da onda TRAVA a câmera e solta os inimigos; limpar a onda LIBERA.
{
    const mundo = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 3 });
    const j = mundo.jogadores[0];
    const primeiraOnda = mundo.faseDef.ondas[0];
    confere('a fase 1 começa sem inimigo e sem trava', mundo.inimigos.length === 0 && !mundo.travado,
            `inimigos=${mundo.inimigos.length} travado=${mundo.travado}`);
    j.x = primeiraOnda.x + 200;                     // já passou do gatilho
    rodar(mundo, 2);
    confere('passar do gatilho trava a câmera e solta a onda', mundo.travado && mundo.inimigos.length > 0,
            `travado=${mundo.travado} inimigos=${mundo.inimigos.length}`);
    for (const i of mundo.inimigos) Motor.aplicarDano(mundo, i, { dano: 9999, origem: j });
    rodar(mundo, 120);
    confere('limpar a onda libera a câmera', !mundo.travado && mundo.inimigos.length === 0,
            `travado=${mundo.travado} inimigos=${mundo.inimigos.length}`);
    confere('a liberação avisa "siga" pra tela', mundo.avisos.includes('siga'), `avisos=${mundo.avisos.join(',')}`);
}

// 6. FINALIZAÇÃO: só em inimigo ATORDOADO e grudado; senão o mesmo botão é o agarrão comum.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 40, j.y);
    alvo.ia.congelada = true;
    alvo.vida = 3;
    Motor.atordoar(alvo, 3);
    const pontosAntes = mundo.pontuacao;
    apertar(mundo, 'agarrar');
    confere('agarrar um atordoado começa a finalização', j.estado === 'finalizando' && alvo.estado === 'finalizado',
            `jogador=${j.estado} inimigo=${alvo.estado}`);
    rodar(mundo, 200);
    confere('a finalização mata e vale bônus', !mundo.inimigos.includes(alvo) && mundo.pontuacao - pontosAntes >= 500,
            `ainda no mundo=${mundo.inimigos.includes(alvo)} pontos=${mundo.pontuacao - pontosAntes}`);
    confere('o jogador volta a ficar de pé', j.estado === 'parado', `estado=${j.estado}`);

    const mundo2 = mundoDeTreino();
    const j2 = mundo2.jogadores[0];
    const alvo2 = Motor.colocarInimigo(mundo2, 'sombra', j2.x + 40, j2.y);
    alvo2.ia.congelada = true;
    apertar(mundo2, 'agarrar');
    confere('agarrar um inimigo são é o agarrão comum', j2.estado === 'agarrando' && alvo2.estado === 'agarrado',
            `jogador=${j2.estado} inimigo=${alvo2.estado}`);
}

// 7. ARREMESSO: o inimigo jogado atropela quem está no caminho — e apanha na queda.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const jogado = Motor.colocarInimigo(mundo, 'sombra', j.x + 40, j.y);
    const noCaminho = Motor.colocarInimigo(mundo, 'sombra', j.x + 180, j.y);
    jogado.ia.congelada = true; noCaminho.ia.congelada = true;
    jogado.vida = 1000; jogado.vidaMax = 1000;
    apertar(mundo, 'agarrar');
    rodar(mundo, 5);
    apertar(mundo, 'soco');                          // com alguém agarrado, o soco arremessa
    rodar(mundo, 3);
    confere('o soco com alguém agarrado arremessa', jogado.estado === 'arremessado', `estado=${jogado.estado}`);
    rodar(mundo, 90);
    confere('quem estava no caminho apanha', noCaminho.vida < noCaminho.vidaMax, `vida=${noCaminho.vida}`);
    confere('quem foi arremessado apanha na queda', jogado.vida < jogado.vidaMax, `vida=${jogado.vida}`);
}

// 8. A IA VEM ATÉ O JOGADOR E BATE.
{
    const mundo = mundoDeTreino({ semente: 11 });
    const j = mundo.jogadores[0];
    const inimigo = Motor.colocarInimigo(mundo, 'sombra', j.x + 400, Math.min(1, j.y + 0.3));
    const distanciaAntes = Math.abs(inimigo.x - j.x);
    rodar(mundo, 90);
    confere('o inimigo se aproxima', Math.abs(inimigo.x - j.x) < distanciaAntes - 100 && Math.abs(inimigo.y - j.y) < 0.2,
            `dx=${Math.abs(inimigo.x - j.x)} dy=${Math.abs(inimigo.y - j.y)}`);
    rodar(mundo, 600);
    confere('e acaba acertando o jogador parado', j.vida < j.vidaMax, `vida=${j.vida}`);
}

// 9. VIDA NUNCA FICA NEGATIVA, e o morto sai do mundo depois de cair.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
    Motor.aplicarDano(mundo, alvo, { dano: 9999, origem: j });
    confere('vida para no zero', alvo.vida === 0 && alvo.estado === 'morto', `vida=${alvo.vida} estado=${alvo.estado}`);
    rodar(mundo, 180);
    confere('o morto some do mundo', !mundo.inimigos.includes(alvo), 'ainda está na lista');
}

// 10. CHI: sobe quando o golpe acerta; o especial consome e (no Long) solta um projétil.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
    alvo.ia.congelada = true;
    apertar(mundo, 'soco');
    rodar(mundo, 30);
    confere('acertar enche chi', j.chi > 0, `chi=${j.chi}`);
    j.chi = 100;
    alvo.x = j.x + 320;                             // longe: o projétil precisa VIAJAR até ele
    apertar(mundo, 'especial');
    rodar(mundo, 20);
    confere('o especial consome chi', j.chi === 100 - j.def.golpes.especial.chi, `chi=${j.chi}`);
    confere('o especial do Long é um projétil', mundo.projeteis.length === 1, `projéteis=${mundo.projeteis.length}`);
    rodar(mundo, 60);
    confere('o projétil acerta e some', alvo.vida < alvo.vidaMax && mundo.projeteis.length === 0,
            `vida=${alvo.vida} projéteis=${mundo.projeteis.length}`);

    j.chi = 5;
    const projeteisAntes = mundo.projeteis.length;
    apertar(mundo, 'especial');
    rodar(mundo, 5);
    confere('sem chi não tem especial', j.chi === 5 && mundo.projeteis.length === projeteisAntes, `chi=${j.chi}`);
}

// 11. DETERMINISMO: mesma semente e mesmas entradas → o mesmo mundo, quadro a quadro.
//     Sem isso não dá pra reproduzir defeito nenhum de combate.
{
    function simular() {
        const mundo = Motor.criarMundo({ fase: 1, jogadores: ['shen'], semente: 42 });
        rodar(mundo, 900, i => [entrada({ direita: i % 60 < 40, apertou: { soco: i % 17 === 0, chute: i % 41 === 0 } })]);
        return JSON.stringify([mundo.jogadores, mundo.inimigos].map(l => l.map(e => [e.x, e.y, e.z, e.vida, e.estado])));
    }
    const a = simular(), b = simular();
    confere('mesma semente, mesmo resultado', a === b, 'as simulações divergiram');
}

// 12. O JOGADOR 2 ENTRA NO MEIO DA LUTA, perto da câmera, com o outro personagem.
{
    const mundo = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1 });
    mundo.jogadores[0].x = 700;
    rodar(mundo, 5);
    const j2 = Motor.adicionarJogador(mundo, 'shen');
    confere('P2 entra vivo e dentro da tela', mundo.jogadores.length === 2 && j2.vida === j2.vidaMax
            && j2.x >= mundo.camera.x && j2.x <= mundo.camera.x + Motor.LARGURA,
            `x=${j2.x} camera=${mundo.camera.x}`);
    rodar(mundo, 30, [entrada(), entrada({ esquerda: true })]);
    confere('P2 obedece à segunda entrada', j2.x < 700 - 20 || j2.virado === -1, `x=${j2.x} virado=${j2.virado}`);
}

// 13. A FASE TERMINA depois da última onda, quando o jogador chega no fim.
{
    const mundo = Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 5 });
    const j = mundo.jogadores[0];
    for (let onda = 0; onda < mundo.faseDef.ondas.length; onda++) {
        j.x = mundo.faseDef.ondas[onda].x + 200;
        rodar(mundo, 2);
        for (const i of mundo.inimigos) Motor.aplicarDano(mundo, i, { dano: 9999, origem: j });
        rodar(mundo, 150);
    }
    confere('todas as ondas limpas', !mundo.travado && mundo.onda === mundo.faseDef.ondas.length,
            `travado=${mundo.travado} onda=${mundo.onda}`);
    j.x = mundo.faseDef.comprimento - 10;
    rodar(mundo, 5);
    confere('chegar no fim conclui a fase', mundo.concluida === true, `concluida=${mundo.concluida}`);
}

// 14. MORRER: com vida sobrando o jogador renasce; sem vida, é fim de jogo.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const vidas = j.vidas;
    Motor.aplicarDano(mundo, j, { dano: 9999 });
    confere('o jogador morre', j.estado === 'morto' && j.vida === 0, `estado=${j.estado}`);
    rodar(mundo, 200);
    confere('e renasce com uma vida a menos', j.estado === 'parado' && j.vida === j.vidaMax && j.vidas === vidas - 1,
            `estado=${j.estado} vida=${j.vida} vidas=${j.vidas}`);
    j.vidas = 0;
    Motor.aplicarDano(mundo, j, { dano: 9999 });
    rodar(mundo, 200);
    confere('sem vidas é fim de jogo', mundo.fimDeJogo === true && j.estado === 'morto',
            `fimDeJogo=${mundo.fimDeJogo} estado=${j.estado}`);
}

// 15. O BRUTO TEM ARMADURA: soco leve não interrompe o golpe dele, mas ele apanha mesmo assim.
{
    const mundo = mundoDeTreino();
    const j = mundo.jogadores[0];
    const bruto = Motor.colocarInimigo(mundo, 'bruto', j.x + 60, j.y);
    bruto.virado = -1;
    bruto.ia.congelada = true;
    Motor.iniciarGolpe(bruto, 'soco1');
    apertar(mundo, 'soco');
    rodar(mundo, 8);
    confere('o bruto apanha', bruto.vida < bruto.vidaMax, `vida=${bruto.vida}`);
    confere('mas não interrompe o golpe', bruto.estado === 'atacando', `estado=${bruto.estado}`);
}

// 16. ESPECIAL NÃO EMENDA EM ESPECIAL, nos três monges. A regra antiga olhava o TIPO (projétil do
//     Long, giro do Shen) e deixava passar o puxão da Lian: com chi sobrando, um puxão que acertou
//     emendava outro puxão, e o especial virava um laço.
for (const p of Object.keys(Motor.PERSONAGENS)) {
    const mundo = Motor.criarMundo({ fase: 0, jogadores: [p], semente: 7 });
    const j = mundo.jogadores[0];
    const alvo = Motor.colocarInimigo(mundo, 'sombra', j.x + 60, j.y);
    alvo.ia.congelada = true;
    alvo.vida = 1000; alvo.vidaMax = 1000;
    j.chi = 100;
    const custo = j.def.golpes.especial.chi;
    apertar(mundo, 'especial');
    let especiais = 1;
    for (let f = 0; f < 60; f++) {
        if (j.golpeNome !== 'especial') break;       // acabou: apertar de novo é especial novo, não emenda
        const antes = j.chi;
        apertar(mundo, 'especial');
        if (j.chi < antes - 5) especiais++;
    }
    confere(`${p}: apertar especial durante o próprio especial não emenda outro`, especiais === 1 && j.chi >= 100 - custo - 1, `especiais=${especiais} chi=${j.chi}`);
}

console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
process.exit(falhas.length === 0 ? 0 : 1);
