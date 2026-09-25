// PROGRESSO, CONQUISTAS E DIFICULDADE do "Punhos de Shaolin" (jogo/), conferidos no Node.
//
//     node Padelizou.Tests/js/conferir-conquistas-do-shaolin.js
//
// Fase 0 do `jogo/CRONOGRAMA.md`: antes de existir conta na Steam, o jogo precisa salvar
// progresso (com versão e migração), saber o que é uma conquista e ter três dificuldades. Os três
// são lógica pura — `progresso.js`, `conquistas.js` e a tabela de dificuldade no `motor.js` —
// e é por isso que dá pra conferir aqui, sem navegador, do mesmo jeito que o combate.
const path = require('path');
const raiz = path.join(__dirname, '..', '..', 'jogo', 'js');
const Motor = require(path.join(raiz, 'motor.js'));
const Progresso = require(path.join(raiz, 'progresso.js'));
const Conquistas = require(path.join(raiz, 'conquistas.js'));
const Loja = require(path.join(raiz, 'loja.js'));

const falhas = [];
function confere(nome, condicao, detalhe) {
    console.log(`${condicao ? '  ok  ' : ' FALHA'} · ${nome}${condicao ? '' : ' → ' + detalhe}`);
    if (!condicao) falhas.push(nome);
}

// Uma "plataforma" falsa: guarda o que foi salvo e anota o que a Steam receberia.
function plataformaFalsa(guardado) {
    const p = { guardado: guardado === undefined ? null : guardado, conquistadas: [], estatisticas: [] };
    p.carregar = () => p.guardado;
    p.salvar = dados => { p.guardado = JSON.parse(JSON.stringify(dados)); };
    p.conquistar = id => p.conquistadas.push(id);
    p.estatistica = (nome, valor) => p.estatisticas.push([nome, valor]);
    return p;
}

// ── PROGRESSO ─────────────────────────────────────────────────────────────────────────────
{
    const padrao = Progresso.padrao();
    confere('o padrão nasce na fase 1, normal, com versão', padrao.versao >= 1 && padrao.faseAlcancada === 1 && padrao.dificuldade === 'normal',
            JSON.stringify(padrao));

    // Migração: um salvamento antigo (só recorde), com lixo, volume fora da faixa e fase impossível.
    const velho = Progresso.normalizar({ recorde: 500, lixo: 1, opcoes: { musica: 4, efeitos: -1, tremor: 'sim' }, faseAlcancada: 9, dificuldade: 'insano', conquistas: { finalizador: 123 } });
    confere('migrar mantém o recorde antigo', velho.recorde === 500, `recorde=${velho.recorde}`);
    confere('migrar descarta chave desconhecida', !('lixo' in velho), 'lixo sobreviveu');
    confere('migrar prende o volume entre 0 e 1', velho.opcoes.musica === 1 && velho.opcoes.efeitos === 0, JSON.stringify(velho.opcoes));
    confere('migrar rejeita fase além da última', velho.faseAlcancada === Motor.FASES.length - 1, `fase=${velho.faseAlcancada}`);
    confere('migrar rejeita dificuldade inventada', velho.dificuldade === 'normal', velho.dificuldade);
    confere('migrar mantém conquista já ganha', velho.conquistas.finalizador === 123, JSON.stringify(velho.conquistas));
    confere('migrar não deixa opção sem valor', typeof velho.opcoes.tremor === 'boolean' && typeof velho.opcoes.telaCheia === 'boolean', JSON.stringify(velho.opcoes));
    confere('normalizar aguenta nulo', Progresso.normalizar(null).faseAlcancada === 1, 'quebrou com null');
    confere('qualidade visual só aceita alta ou média', Progresso.normalizar({ opcoes: { qualidade: 'ultra' } }).opcoes.qualidade === 'alta'
            && Progresso.normalizar({ opcoes: { qualidade: 'media' } }).opcoes.qualidade === 'media', 'valor inventado passou');

    const plat = plataformaFalsa({ recorde: 900, faseAlcancada: 2 });
    const prog = Progresso.criar(plat);
    confere('criar carrega o que a plataforma guardou', prog.dados.recorde === 900 && prog.dados.faseAlcancada === 2, JSON.stringify(prog.dados));
    prog.registrarFase(3);
    prog.registrarFase(1);                       // voltar de fase NUNCA apaga o avanço
    confere('a fase alcançada só sobe', prog.dados.faseAlcancada === 3 && plat.guardado.faseAlcancada === 3, `fase=${prog.dados.faseAlcancada}`);
    prog.registrarRecorde(100);
    prog.registrarRecorde(2000);
    confere('o recorde só sobe', prog.dados.recorde === 2000 && plat.guardado.recorde === 2000, `recorde=${prog.dados.recorde}`);
    prog.opcao('musica', 0.3);
    confere('opção muda e é gravada', prog.dados.opcoes.musica === 0.3 && plat.guardado.opcoes.musica === 0.3, JSON.stringify(plat.guardado.opcoes));
    prog.apagar();
    confere('apagar volta ao padrão mas guarda as opções', prog.dados.faseAlcancada === 1 && prog.dados.recorde === 0 && prog.dados.opcoes.musica === 0.3, JSON.stringify(prog.dados));

    const quebrada = plataformaFalsa(null);
    quebrada.carregar = () => { throw new Error('disco ruim'); };
    quebrada.salvar = () => { throw new Error('disco ruim'); };
    const prog2 = Progresso.criar(quebrada);
    prog2.registrarRecorde(10);
    confere('plataforma que explode não derruba o jogo', prog2.dados.recorde === 10, 'quebrou');
}

// ── CONQUISTAS ────────────────────────────────────────────────────────────────────────────
function mundoCom(jogadores, fase) {
    return Motor.criarMundo({ fase: fase == null ? 1 : fase, jogadores: jogadores || ['long'], semente: 1 });
}
function conquistasNovas(plat, dados) {
    const prog = Progresso.criar(plat);
    if (dados) Object.assign(prog.dados.estatisticas, dados);
    const ganhas = [];
    const c = Conquistas.criar({ progresso: prog, plataforma: plat, aoDesbloquear: def => ganhas.push(def.id) });
    return { c, prog, ganhas };
}
{
    confere('há pelo menos 12 conquistas, todas com id, nome e descrição', Conquistas.LISTA.length >= 12
            && Conquistas.LISTA.every(d => d.id && d.nome && d.descricao), `${Conquistas.LISTA.length}`);
    // Minúscula, algarismo e sublinhado ('arena_10'): o formato que o `normalizar` do progresso guarda.
    confere('ids são únicos e sem espaço', new Set(Conquistas.LISTA.map(d => d.id)).size === Conquistas.LISTA.length
            && Conquistas.LISTA.every(d => /^[a-z0-9_]+$/.test(d.id)), Conquistas.LISTA.map(d => d.id).join(','));

    const plat = plataformaFalsa();
    const { c, prog, ganhas } = conquistasNovas(plat);
    const m = mundoCom();
    c.processar([{ tipo: 'morte', time: 'inimigo', id: 'sombra' }], m);
    c.processar([{ tipo: 'morte', time: 'inimigo', id: 'sombra' }], m);
    confere('primeiro inimigo morto desbloqueia UMA vez', ganhas.filter(g => g === 'primeiro_sangue').length === 1 && plat.conquistadas.includes('primeiro_sangue'),
            ganhas.join(','));
    confere('a conquista fica gravada no progresso', typeof prog.dados.conquistas.primeiro_sangue === 'number' && plat.guardado.conquistas.primeiro_sangue,
            JSON.stringify(prog.dados.conquistas));
    c.processar([{ tipo: 'finalizacao' }], m);
    confere('finalização desbloqueia o finalizador', ganhas.includes('finalizador'), ganhas.join(','));

    m.jogadores[0].combo = 10;
    c.processar([], m);
    confere('combo de 10 desbloqueia', ganhas.includes('dez_golpes') && !ganhas.includes('vinte_golpes'), ganhas.join(','));
    m.jogadores[0].combo = 20;
    c.processar([], m);
    confere('combo de 20 desbloqueia', ganhas.includes('vinte_golpes'), ganhas.join(','));

    c.processar([{ tipo: 'chefe', id: 'mestreSombra' }], m);
    c.processar([{ tipo: 'morte', time: 'inimigo', id: 'mestreSombra' }], m);
    confere('matar o chefe desbloqueia o dele', ganhas.includes('mestre_sombra'), ganhas.join(','));
    confere('chefe sem morrer é monge de ferro', ganhas.includes('monge_de_ferro'), ganhas.join(','));

    // Morrer durante o chefe cancela o monge de ferro (em outro jogo).
    const b = conquistasNovas(plataformaFalsa());
    const m2 = mundoCom();
    b.c.processar([{ tipo: 'chefe', id: 'graoPresa' }], m2);
    b.c.processar([{ tipo: 'morte', time: 'jogador' }], m2);
    b.c.processar([{ tipo: 'morte', time: 'inimigo', id: 'graoPresa' }], m2);
    confere('morrer no chefe cancela o monge de ferro', b.ganhas.includes('grao_presa') && !b.ganhas.includes('monge_de_ferro'), b.ganhas.join(','));

    c.processar([{ tipo: 'atropelou', quantidade: 1 }], m);
    confere('atropelar um só não vale', !ganhas.includes('atropelador'), ganhas.join(','));
    c.processar([{ tipo: 'atropelou', quantidade: 2 }], m);
    confere('atropelar dois vale', ganhas.includes('atropelador'), ganhas.join(','));

    for (let k = 0; k < 10; k++) c.processar([{ tipo: 'item', nome: 'cha' }], m);
    confere('dez itens no total é colecionador', ganhas.includes('colecionador') && prog.dados.estatisticas.itens === 10,
            `itens=${prog.dados.estatisticas.itens}`);

    // Fim de fase: intocável só sem dano, dupla só com dois, templo livre só na última.
    const m3 = mundoCom(['long', 'shen'], 2);
    c.concluirFase(m3);
    confere('fase sem dano, em dupla', ganhas.includes('intocavel') && ganhas.includes('dupla') && !ganhas.includes('templo_livre'), ganhas.join(','));
    const d = conquistasNovas(plataformaFalsa());
    const m4 = mundoCom(['long'], 4);
    m4.jogadores[0].danoLevado = 5;
    d.c.concluirFase(m4);
    confere('com dano não é intocável; última fase é templo livre', !d.ganhas.includes('intocavel') && d.ganhas.includes('templo_livre') && !d.ganhas.includes('dupla'), d.ganhas.join(','));

    // Veterano é cumulativo entre partidas: 99 guardados + 1 agora.
    const e = conquistasNovas(plataformaFalsa(), { inimigos: 99 });
    e.c.processar([{ tipo: 'morte', time: 'inimigo', id: 'sombra' }], mundoCom());
    confere('cem inimigos ao longo das partidas é veterano', e.ganhas.includes('veterano') && e.prog.dados.estatisticas.inimigos === 100, e.ganhas.join(','));

    // Conquista já ganha em outra sessão não dispara de novo.
    const platJa = plataformaFalsa({ conquistas: { finalizador: 1 } });
    const f = conquistasNovas(platJa);
    f.c.processar([{ tipo: 'finalizacao' }], mundoCom());
    confere('conquista de outra sessão não avisa de novo', !f.ganhas.includes('finalizador') && platJa.conquistadas.length === 0, f.ganhas.join(','));
}

// ── CONQUISTAS DO TEMPLO: disparadas pela COMPRA na loja ─────────────────────────────────
// A compra devolve um evento `golpe-aprendido` e ele passa pelo mesmo `processar` dos eventos do
// motor — a conquista não sabe da loja, e a loja não sabe da conquista.
try {
    confere('existem as conquistas aprendiz e mestre do templo', !!(Conquistas.POR_ID.aprendiz && Conquistas.POR_ID.mestre_do_templo),
            Object.keys(Conquistas.POR_ID).join(','));
    const t = conquistasNovas(plataformaFalsa({ karma: 99999 }));
    const loja = Loja.criar(t.prog);
    const [primeiro, ...resto] = Loja.CATALOGO.map(d => d.id);
    const r1 = loja.comprar(primeiro);
    t.c.processar([r1.evento]);
    confere('aprender o primeiro golpe é aprendiz (e ainda não é mestre)', t.ganhas.includes('aprendiz') && !t.ganhas.includes('mestre_do_templo'), t.ganhas.join(','));
    for (const id of resto.slice(0, -1)) t.c.processar([loja.comprar(id).evento]);
    confere('faltando um golpe, ainda não é mestre', !t.ganhas.includes('mestre_do_templo'), t.ganhas.join(','));
    t.c.processar([loja.comprar(resto[resto.length - 1]).evento]);
    confere('aprender todos é mestre do templo, e aprendiz avisou uma vez só', t.ganhas.includes('mestre_do_templo') && t.ganhas.filter(g => g === 'aprendiz').length === 1,
            t.ganhas.join(','));
    const pobre = conquistasNovas(plataformaFalsa({ karma: 0 }));
    const recusada = Loja.criar(pobre.prog).comprar(primeiro);
    if (recusada.evento) pobre.c.processar([recusada.evento]);
    confere('compra recusada não dá conquista', recusada.ok === false && !pobre.ganhas.includes('aprendiz'), `${JSON.stringify(recusada)} ${pobre.ganhas.join(',')}`);
    // Salvamento com um golpe que SAIU do jogo (o `normalizar` guarda o id de propósito): ele não
    // conta pro mestre. Contar `golpes.length` em vez dos liberados soltava a conquista um golpe antes.
    const velho = conquistasNovas(plataformaFalsa({ karma: 99999, golpes: ['golpe_que_saiu_do_jogo'] }));
    const lojaVelha = Loja.criar(velho.prog);
    const ids = Loja.CATALOGO.map(d => d.id);
    for (const id of ids.slice(0, -1)) velho.c.processar([lojaVelha.comprar(id).evento]);
    confere('golpe obsoleto no salvamento não adianta o mestre (6 de 7 ainda não é mestre)', !velho.ganhas.includes('mestre_do_templo'), velho.ganhas.join(','));
    velho.c.processar([lojaVelha.comprar(ids[ids.length - 1]).evento]);
    confere('e o 7º golpe, sim, faz o mestre', velho.ganhas.includes('mestre_do_templo'), velho.ganhas.join(','));
} catch (erro) {
    confere('as conquistas do templo (o bloco rodou até o fim)', false, erro && erro.message);
}

// ── CONQUISTAS DO CONTEÚDO NOVO (onda 2): a Lian e a guarda quebrada ─────────────────────
try {
    confere('existem as conquistas rio e guarda quebrada', !!(Conquistas.POR_ID.rio && Conquistas.POR_ID.guarda_quebrada), Object.keys(Conquistas.POR_ID).join(','));
    const comLian = conquistasNovas(plataformaFalsa());
    comLian.c.concluirFase(mundoCom(['lian'], 1));
    confere('terminar uma fase com a Lian é "rio"', comLian.ganhas.includes('rio'), comLian.ganhas.join(','));
    const emDupla = conquistasNovas(plataformaFalsa());
    emDupla.c.concluirFase(mundoCom(['long', 'lian'], 2));
    confere('com a Lian de P2 também vale', emDupla.ganhas.includes('rio'), emDupla.ganhas.join(','));
    const semLian = conquistasNovas(plataformaFalsa());
    semLian.c.concluirFase(mundoCom(['long', 'shen'], 2));
    confere('terminar sem a Lian não é "rio"', !semLian.ganhas.includes('rio'), semLian.ganhas.join(','));

    const g = conquistasNovas(plataformaFalsa());
    g.c.processar([{ tipo: 'guarda-quebrada', id: 'sombra' }], mundoCom());
    confere('quebrar a guarda de outro inimigo não conta', !g.ganhas.includes('guarda_quebrada'), g.ganhas.join(','));
    g.c.processar([{ tipo: 'guarda-quebrada', id: 'renegado' }], mundoCom());
    confere('quebrar a guarda de um Monge Renegado desbloqueia', g.ganhas.includes('guarda_quebrada'), g.ganhas.join(','));

    // De ponta a ponta: o evento que o MOTOR emite quando o chute quebra a guarda do renegado.
    const m = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 7 });
    const j = m.jogadores[0];
    const r = Motor.colocarInimigo(m, 'renegado', j.x + 55, j.y);
    r.estado = 'defendendo'; r.quadro = 0; r.ia.defendendoAte = 0.55; r.virado = -1;
    const e = Motor.entradaVazia(); e.chute = true; e.apertou.chute = true;
    const ponta = conquistasNovas(plataformaFalsa());
    Motor.passo(m, 1 / 60, [e]); ponta.c.processar(m.eventos, m);
    for (let k = 0; k < 20; k++) { Motor.passo(m, 1 / 60, [Motor.entradaVazia()]); ponta.c.processar(m.eventos, m); }
    confere('o chute do motor no renegado em guarda dá a conquista', ponta.ganhas.includes('guarda_quebrada'), ponta.ganhas.join(','));
} catch (erro) {
    confere('as conquistas da onda 2 (o bloco rodou até o fim)', false, erro && erro.message);
}

// ── DIFICULDADE E O DANO LEVADO ───────────────────────────────────────────────────────────
{
    const vida = d => Motor.colocarInimigo(Motor.criarMundo({ fase: 1, jogadores: ['long'], semente: 1, dificuldade: d }), 'sombra', 300, 0.5).vidaMax;
    confere('difícil tem mais vida, fácil tem menos', vida('facil') < vida('normal') && vida('normal') < vida('dificil'),
            `facil=${vida('facil')} normal=${vida('normal')} dificil=${vida('dificil')}`);
    confere('dificuldade inventada vira normal', vida('insano') === vida('normal') && vida(undefined) === vida('normal'), `${vida('insano')}`);

    function danoNoJogador(d) {
        const mundo = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1, dificuldade: d });
        const j = mundo.jogadores[0];
        const i = Motor.colocarInimigo(mundo, 'sombra', j.x + 45, j.y);
        i.virado = -1; i.ia.congelada = true;
        Motor.iniciarGolpe(i, 'soco1');
        for (let k = 0; k < 30; k++) Motor.passo(mundo, 1 / 60, [Motor.entradaVazia()]);
        return { levou: j.vidaMax - j.vida, contado: j.danoLevado };
    }
    const f = danoNoJogador('facil'), n = danoNoJogador('normal'), h = danoNoJogador('dificil');
    confere('inimigo bate mais forte no difícil e mais fraco no fácil', f.levou < n.levou && n.levou < h.levou, `f=${f.levou} n=${n.levou} d=${h.levou}`);
    confere('o jogador conta o dano que levou', n.contado === n.levou && n.contado > 0, `contado=${n.contado} levou=${n.levou}`);

    const mundo = Motor.criarMundo({ fase: 0, jogadores: ['long'], semente: 1 });
    const alvo = Motor.colocarInimigo(mundo, 'sombra', 300, 0.5);
    Motor.aplicarDano(mundo, alvo, { dano: 9999, origem: mundo.jogadores[0] });
    const morte = mundo.eventos.find(ev => ev.tipo === 'morte');
    confere('o evento de morte diz QUEM morreu (id da definição)', morte && morte.id === 'sombra' && morte.time === 'inimigo', JSON.stringify(morte));
}

console.log(falhas.length === 0 ? '\nTUDO VERDE' : `\n${falhas.length} FALHA(S): ${falhas.join(' · ')}`);
process.exit(falhas.length === 0 ? 0 : 1);
