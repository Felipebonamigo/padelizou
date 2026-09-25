// PUNHOS DE SHAOLIN — a loja do Templo: o que o karma compra entre uma fase e outra.
//
// É o coração do Shaolin Monks: terminar uma fase dá KARMA (pelos pontos ganhos nela) e o karma
// ensina golpes e melhorias que ficam pra sempre no salvamento. Este arquivo só decide PREÇO e
// COMPRA; o EFEITO de cada melhoria mora no motor (`Motor.MELHORIAS`), que é a camada de baixo e
// não importa nada daqui. O conferidor (`conferir-loja-do-shaolin.js`) garante que todo id deste
// catálogo é conhecido pelo motor — um item à venda que o motor ignora seria karma jogado fora.
//
// Onde o karma e os golpes ficam guardados é o `progresso.js` (`dados.karma`, `dados.golpes`).
// Puro e sem tela: roda no Node do mesmo jeito que o motor.
(function (raiz, fabrica) {
    const Loja = fabrica();
    if (typeof module !== 'undefined' && module.exports) module.exports = Loja;
    else { raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {}; raiz.PunhosDeShaolin.Loja = Loja; }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    // Texto pro jogador (pt-BR) — agrupado aqui pra tradução da Fase 3 achar num lugar só.
    // Preços são o primeiro chute; o balanceamento (simulador) ajusta depois.
    const CATALOGO = [
        { id: 'sequencia_cinco', preco: 150, nome: 'Sequência de Cinco', descricao: 'A corrente de socos vira cinco golpes antes do lançador.' },
        { id: 'contra_golpe', preco: 200, nome: 'Contra-golpe', descricao: 'Defenda em cima da hora (até 0,15 s antes do golpe) pra aparar: zero dano, o atacante fica tonto.' },
        { id: 'especial_aereo', preco: 250, nome: 'Especial no Ar', descricao: 'O especial apertado no pulo vira um golpe aéreo: mergulho em chamas ou pancada ao cair.' },
        { id: 'agarrao_costas', preco: 200, nome: 'Agarrão pelas Costas', descricao: 'Agarre um inimigo de costas pra um suplex na hora, até em quem tem armadura. Chefe não.' },
        { id: 'vigor', preco: 150, nome: 'Vigor', descricao: '+20% de vida máxima.' },
        { id: 'respiracao', preco: 180, nome: 'Respiração do Templo', descricao: '+3 de chi por segundo parado, andando ou defendendo.' },
        { id: 'punhos_de_ferro', preco: 300, nome: 'Punhos de Ferro', descricao: '+15% de dano em todos os seus golpes.' },
    ];
    const POR_ID = new Map(CATALOGO.map(d => [d.id, d]));     // Map, não objeto: '__proto__' e 'constructor' não viram item
    const PONTOS_POR_KARMA = 100;

    // Pontos ganhos NA fase → karma. Qualquer coisa que não seja número finito e positivo vale zero.
    function karmaDaFase(pontos) {
        if (typeof pontos !== 'number' || !Number.isFinite(pontos) || pontos <= 0) return 0;
        return Math.floor(pontos / PONTOS_POR_KARMA);
    }

    // `progresso`: o de `Progresso.criar` — lê/escreve `dados.karma` e `dados.golpes` e chama `salvar()`.
    function criar(progresso) {
        const dados = progresso.dados;
        const aprendido = id => dados.golpes.includes(id);

        function itens() {
            return CATALOGO.map(d => {
                const ja = aprendido(d.id);
                return Object.assign({}, d, { aprendido: ja, podeComprar: !ja && dados.karma >= d.preco });
            });
        }
        // Devolve { ok, motivo } — motivo: 'desconhecido' | 'aprendido' | 'sem_karma'. Quando dá certo,
        // devolve também o evento `golpe-aprendido`, que quem chamou repassa pras conquistas.
        function comprar(id) {
            const def = POR_ID.get(id);
            if (!def) return { ok: false, motivo: 'desconhecido' };
            if (aprendido(id)) return { ok: false, motivo: 'aprendido' };
            if (dados.karma < def.preco) return { ok: false, motivo: 'sem_karma' };
            dados.karma -= def.preco;
            dados.golpes.push(id);
            progresso.salvar();
            return { ok: true, id, evento: { tipo: 'golpe-aprendido', id, aprendidos: liberados().length, total: CATALOGO.length } };
        }
        // O que o motor recebe em `criarMundo({ liberados })`: só o que está no catálogo HOJE —
        // golpe guardado por uma versão antiga, que saiu do jogo, não vaza pro mundo.
        function liberados() { return dados.golpes.filter(id => POR_ID.has(id)); }
        // Soma o karma de uma fase concluída e grava. Devolve quanto entrou.
        function receber(pontosDaFase) {
            const ganho = karmaDaFase(pontosDaFase);
            if (ganho > 0) { dados.karma += ganho; progresso.salvar(); }
            return ganho;
        }
        // Fim de uma fase: a pontuação do mundo é da PARTIDA inteira, então o karma sai da diferença
        // entre o fim e o começo desta fase — senão cada fase pagaria de novo o karma das anteriores.
        function receberDaFase(pontuacaoNoComeco, pontuacaoNoFim) { return receber(pontuacaoNoFim - pontuacaoNoComeco); }
        function saldo() { return dados.karma; }
        return { itens, comprar, liberados, receber, receberDaFase, saldo };
    }

    return { CATALOGO, PONTOS_POR_KARMA, karmaDaFase, criar };
});
