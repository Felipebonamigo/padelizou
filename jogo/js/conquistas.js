// PUNHOS DE SHAOLIN — conquistas. Definidas AQUI, em código, com o id que vai pro painel da
// Steam na Fase 4 do cronograma. A Steam só espelha: quem decide que uma conquista foi ganha é
// este arquivo, lendo os eventos do motor — e por isso ele roda no Node e tem conferidor.
//
// `criar({ progresso, plataforma, aoDesbloquear })`:
//   - `progresso` guarda o que já foi ganho e as estatísticas cumulativas (veterano, colecionador);
//   - `plataforma.conquistar(id)` é a ponte pra Steam (ou nada, no navegador);
//   - `aoDesbloquear(def)` é o aviso na tela. Só dispara na PRIMEIRA vez, nesta ou em outra sessão.
(function (raiz, fabrica) {
    const Conquistas = fabrica();
    if (typeof module !== 'undefined' && module.exports) module.exports = Conquistas;
    else { raiz.PunhosDeShaolin = raiz.PunhosDeShaolin || {}; raiz.PunhosDeShaolin.Conquistas = Conquistas; }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const LISTA = [
        { id: 'primeiro_sangue', nome: 'Primeiro Sangue', descricao: 'Derrote o primeiro inimigo.' },
        { id: 'finalizador', nome: 'Finalizador', descricao: 'Execute uma finalização.' },
        { id: 'dez_golpes', nome: 'Dez Golpes', descricao: 'Emende um combo de 10 golpes.' },
        { id: 'vinte_golpes', nome: 'Vinte Golpes', descricao: 'Emende um combo de 20 golpes.' },
        { id: 'atropelador', nome: 'Atropelador', descricao: 'Arremesse um inimigo em cima de dois outros.' },
        { id: 'colecionador', nome: 'Colecionador', descricao: 'Pegue 10 itens ao longo das partidas.' },
        { id: 'veterano', nome: 'Veterano', descricao: 'Derrote 100 inimigos ao longo das partidas.' },
        { id: 'mestre_sombra', nome: 'A Sombra Cai', descricao: 'Derrote o Mestre Sombra.' },
        { id: 'grao_presa', nome: 'Sem Presas', descricao: 'Derrote o Grão-Presa.' },
        { id: 'gigante', nome: 'Quanto Maior', descricao: 'Derrote o Gigante do Poço.' },
        { id: 'feiticeiro', nome: 'O Feitiço Quebrado', descricao: 'Derrote o Feiticeiro.' },
        { id: 'monge_de_ferro', nome: 'Monge de Ferro', descricao: 'Derrote um chefe sem morrer durante a luta.' },
        { id: 'intocavel', nome: 'Intocável', descricao: 'Termine uma fase sem levar dano.' },
        { id: 'dupla', nome: 'Em Dupla', descricao: 'Termine uma fase com dois jogadores.' },
        { id: 'templo_livre', nome: 'O Templo Está Livre', descricao: 'Termine a campanha.' },
        { id: 'aprendiz', nome: 'Aprendiz', descricao: 'Aprenda o primeiro golpe no Templo.' },
        { id: 'mestre_do_templo', nome: 'Mestre do Templo', descricao: 'Aprenda todos os golpes do Templo.' },
        { id: 'rio', nome: 'A Corrente do Rio', descricao: 'Termine uma fase com a Lian.' },
        { id: 'guarda_quebrada', nome: 'Guarda Quebrada', descricao: 'Quebre a guarda de um Monge Renegado com um chute.' },
    ];
    const POR_ID = Object.fromEntries(LISTA.map(d => [d.id, d]));
    const CHEFES = { mestreSombra: 'mestre_sombra', graoPresa: 'grao_presa', gigante: 'gigante', feiticeiro: 'feiticeiro' };
    const ULTIMA_FASE = 4;

    function criar(opcoes) {
        const progresso = opcoes.progresso;
        const plataforma = opcoes.plataforma || {};
        const aoDesbloquear = opcoes.aoDesbloquear || (() => { });
        const est = progresso.dados.estatisticas;
        let chefeSemMorte = false;

        function desbloquear(id) {
            const def = POR_ID[id];
            if (!def || progresso.dados.conquistas[id]) return false;
            progresso.dados.conquistas[id] = Date.now() || 1;
            progresso.salvar();
            try { if (plataforma.conquistar) plataforma.conquistar(id); } catch (erro) { if (typeof console !== 'undefined') console.warn('conquista: a plataforma recusou', id, erro && erro.message); }
            aoDesbloquear(def);
            return true;
        }

        // Chamar a cada passo do motor com `mundo.eventos` — e com o `evento` que a compra na loja
        // devolve (`golpe-aprendido`, com `aprendidos` e `total`), sem mundo.
        function processar(eventos, mundo) {
            for (const ev of eventos) {
                switch (ev.tipo) {
                    case 'morte':
                        if (ev.time === 'inimigo') {
                            est.inimigos++;
                            desbloquear('primeiro_sangue');
                            if (est.inimigos >= 100) desbloquear('veterano');
                            if (CHEFES[ev.id]) { desbloquear(CHEFES[ev.id]); if (chefeSemMorte) desbloquear('monge_de_ferro'); chefeSemMorte = false; }
                        } else if (ev.time === 'jogador') { est.mortes++; chefeSemMorte = false; }
                        break;
                    case 'finalizacao': est.finalizacoes++; desbloquear('finalizador'); break;
                    case 'chefe': chefeSemMorte = true; break;
                    case 'atropelou': if (ev.quantidade >= 2) desbloquear('atropelador'); break;
                    case 'item': est.itens++; if (est.itens >= 10) desbloquear('colecionador'); break;
                    case 'golpe-aprendido': desbloquear('aprendiz'); if (ev.aprendidos >= ev.total) desbloquear('mestre_do_templo'); break;
                    case 'guarda-quebrada': if (ev.id === 'renegado') desbloquear('guarda_quebrada'); break;
                    default: break;
                }
            }
            if (mundo) {
                for (const j of mundo.jogadores) {
                    if (j.combo > est.maiorCombo) est.maiorCombo = j.combo;
                    if (j.combo >= 10) desbloquear('dez_golpes');
                    if (j.combo >= 20) desbloquear('vinte_golpes');
                }
            }
        }

        // Chamar quando `mundo.concluida` vira verdadeiro.
        function concluirFase(mundo) {
            est.fasesConcluidas++;
            if (mundo.jogadores.every(j => !j.danoLevado)) desbloquear('intocavel');
            if (mundo.jogadores.length >= 2) desbloquear('dupla');
            if (mundo.jogadores.some(j => j.personagem === 'lian')) desbloquear('rio');
            if (mundo.fase >= ULTIMA_FASE) { est.vitorias++; desbloquear('templo_livre'); }
            progresso.salvar();
        }

        function ganhas() { return LISTA.filter(d => progresso.dados.conquistas[d.id]); }

        return { processar, concluirFase, desbloquear, ganhas, lista: LISTA };
    }

    return { LISTA, POR_ID, criar };
});
