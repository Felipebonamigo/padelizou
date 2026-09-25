// PUNHOS DE SHAOLIN — progresso: o que fica guardado entre partidas.
//
// Fase alcançada, recorde, dificuldade, opções, conquistas, estatísticas, o karma e os golpes
// aprendidos no Templo (`loja.js` decide o preço; aqui só se guarda). Tudo passa por
// `normalizar`: o salvamento de hoje não é o formato de amanhã, e um JSON velho, truncado ou
// editado à mão nunca pode derrubar o jogo — vira padrão no que faltar, e o que sobrar é ignorado.
//
// Onde se guarda é problema da PLATAFORMA (`plataforma.js`): localStorage no navegador, arquivo
// no Electron, Steam Cloud por cima disso. Este arquivo só conhece `carregar()` e `salvar(dados)`,
// e por isso roda no Node — ver `conferir-conquistas-do-shaolin.js`.
(function (raiz, fabrica) {
    const Progresso = fabrica();
    if (typeof module !== 'undefined' && module.exports) module.exports = Progresso;
    else { raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {}; raiz.PunhosDeShaolin.Progresso = Progresso; }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const VERSAO = 1;
    const ULTIMA_FASE = 4;
    const DIFICULDADES = ['facil', 'normal', 'dificil'];
    const TETO_DE_GOLPES = 64;                   // ids guardados em `golpes`; o catálogo da loja tem 7

    function padrao() {
        return {
            versao: VERSAO,
            faseAlcancada: 1,
            recorde: 0,
            dificuldade: 'normal',
            opcoes: { musica: 0.7, efeitos: 0.8, tremor: true, telaCheia: false, idioma: 'pt-BR', qualidade: 'alta' },
            conquistas: {},
            karma: 0,
            golpes: [],
            estatisticas: { inimigos: 0, finalizacoes: 0, maiorCombo: 0, itens: 0, mortes: 0, tempoJogado: 0, fasesConcluidas: 0, vitorias: 0, partidas: 0 },
        };
    }

    const prender = (v, a, b, senao) => (typeof v === 'number' && !Number.isNaN(v) ? Math.min(b, Math.max(a, v)) : senao);
    const inteiro = (v, senao) => (Number.isInteger(v) && v >= 0 ? v : senao);

    // Aceita QUALQUER coisa e devolve um progresso válido — é a migração.
    function normalizar(bruto) {
        const base = padrao();
        const b = bruto && typeof bruto === 'object' ? bruto : {};
        const o = b.opcoes && typeof b.opcoes === 'object' ? b.opcoes : {};
        const c = b.conquistas && typeof b.conquistas === 'object' ? b.conquistas : {};
        const e = b.estatisticas && typeof b.estatisticas === 'object' ? b.estatisticas : {};
        const dados = {
            versao: VERSAO,
            faseAlcancada: prender(inteiro(b.faseAlcancada, 1), 1, ULTIMA_FASE, 1),
            recorde: inteiro(b.recorde, 0),
            dificuldade: DIFICULDADES.includes(b.dificuldade) ? b.dificuldade : 'normal',
            opcoes: {
                musica: prender(o.musica, 0, 1, base.opcoes.musica),
                efeitos: prender(o.efeitos, 0, 1, base.opcoes.efeitos),
                tremor: typeof o.tremor === 'boolean' ? o.tremor : base.opcoes.tremor,
                telaCheia: typeof o.telaCheia === 'boolean' ? o.telaCheia : base.opcoes.telaCheia,
                idioma: typeof o.idioma === 'string' && o.idioma.length <= 8 ? o.idioma : base.opcoes.idioma,
                qualidade: o.qualidade === 'media' ? 'media' : 'alta',
            },
            conquistas: {},
            karma: inteiro(b.karma, 0),
            golpes: [],
            estatisticas: {},
        };
        for (const id of Object.keys(c)) if (/^[a-z_]+$/.test(id) && (c[id] === true || typeof c[id] === 'number')) dados.conquistas[id] = c[id] === true ? 1 : c[id];
        // Golpes: só texto no formato de id, sem repetição. Se o id ainda existe no catálogo é a
        // loja que decide (`Loja.liberados`) — um id que saiu do jogo fica guardado, mas não vale.
        // atalho: no máximo TETO_DE_GOLPES ids de até 32 letras (o catálogo tem 7); o resto é ignorado.
        // Sem teto, uma lista editada à mão com 60 mil ids travava o boot por segundos.
        if (Array.isArray(b.golpes)) {
            const vistos = new Set();
            for (const id of b.golpes) {
                if (dados.golpes.length >= TETO_DE_GOLPES) break;
                if (typeof id === 'string' && id.length <= 32 && /^[a-z_]+$/.test(id) && !vistos.has(id)) { vistos.add(id); dados.golpes.push(id); }
            }
        }
        for (const chave of Object.keys(base.estatisticas)) dados.estatisticas[chave] = inteiro(e[chave], 0);
        return dados;
    }

    // `plataforma`: { carregar(): objeto|null, salvar(objeto) }. Qualquer erro dela é engolido
    // com aviso — o disco ruim tira o salvamento, nunca a partida.
    function criar(plataforma) {
        let guardado = null;
        try { guardado = plataforma.carregar(); } catch (erro) { avisar('carregar', erro); }
        const dados = normalizar(guardado);

        function avisar(o, erro) { if (typeof console !== 'undefined') console.warn(`progresso: não deu pra ${o}`, erro && erro.message); }
        function salvar() {
            try { plataforma.salvar(dados); return true; } catch (erro) { avisar('salvar', erro); return false; }
        }
        function registrarFase(numero) {
            if (Number.isInteger(numero) && numero > dados.faseAlcancada) { dados.faseAlcancada = Math.min(ULTIMA_FASE, numero); salvar(); }
        }
        function registrarRecorde(pontos) {
            if (Number.isInteger(pontos) && pontos > dados.recorde) { dados.recorde = pontos; salvar(); return true; }
            return false;
        }
        function opcao(nome, valor) {
            if (!(nome in dados.opcoes)) return;
            const temp = normalizar(Object.assign({}, dados, { opcoes: Object.assign({}, dados.opcoes, { [nome]: valor }) }));
            dados.opcoes[nome] = temp.opcoes[nome];
            salvar();
        }
        function dificuldade(valor) {
            if (DIFICULDADES.includes(valor)) { dados.dificuldade = valor; salvar(); }
        }
        function somar(estatistica, quanto) {
            if (estatistica in dados.estatisticas) dados.estatisticas[estatistica] += (quanto == null ? 1 : quanto);
        }
        function maximo(estatistica, valor) {
            if (estatistica in dados.estatisticas && valor > dados.estatisticas[estatistica]) dados.estatisticas[estatistica] = valor;
        }
        // Apagar o progresso guarda as opções: quem apaga quer recomeçar o jogo, não reconfigurar o som.
        function apagar() {
            const opcoes = dados.opcoes;
            Object.assign(dados, padrao());
            dados.opcoes = opcoes;
            salvar();
        }
        return { dados, salvar, registrarFase, registrarRecorde, opcao, dificuldade, somar, maximo, apagar };
    }

    return { VERSAO, ULTIMA_FASE, DIFICULDADES, padrao, normalizar, criar };
});
