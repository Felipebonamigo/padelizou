/* Cola tudo: menu, laço de jogo com passo fixo, HUD, pausa, fim de partida. */
(function () {
  'use strict';
  const Padel = window.Padel;
  const { Partida, Renderizador, Entrada, Som } = Padel;

  const PASSO = 1 / 120;
  const CHAVE_DAS_OPCOES = 'padelizou-arcade.opcoes';

  const $ = (id) => document.getElementById(id);
  const el = {
    canvas: $('quadra'), palco: $('palco'), mensagem: $('mensagem'),
    menu: $('menu'), pausa: $('pausa'), fim: $('fim'),
    jogar: $('jogar'), continuar: $('continuar'), sairParaOMenu: $('sairParaOMenu'),
    jogarDeNovo: $('jogarDeNovo'), voltarAoMenu: $('voltarAoMenu'),
    botaoPausa: $('botaoPausa'), botaoSom: $('botaoSom'), botaoAcao: $('botaoAcao'),
    joystick: $('joystick'), knob: $('joystickKnob'),
    setsCasa: $('setsCasa'), gamesCasa: $('gamesCasa'), pontosCasa: $('pontosCasa'), saqueCasa: $('saqueCasa'),
    setsFora: $('setsFora'), gamesFora: $('gamesFora'), pontosFora: $('pontosFora'), saqueFora: $('saqueFora'),
    resultado: $('resultado'), detalhesDoFim: $('detalhesDoFim'), dica: $('dica'),
  };

  const parametros = new URLSearchParams(location.search);
  const modoAutomatico = parametros.get('auto') === '1';   // 4 IAs: demonstração e teste

  let partida = null;
  let renderizador = null;
  let entrada = null;
  let pausado = false;
  let ultimoQuadro = 0;
  let acumulador = 0;
  let opcoesAtuais = null;
  let ultimaMensagem = '';

  function lerOpcoesDoMenu() {
    const dificuldade = (document.querySelector('input[name="dificuldade"]:checked') || {}).value || 'medio';
    const pontoDeOuro = $('pontoDeOuro').checked;
    const setsParaVencer = Number(($('formato').value || '1'));
    return { dificuldade, pontoDeOuro, setsParaVencer };
  }

  function guardarOpcoes(opcoes) {
    try { localStorage.setItem(CHAVE_DAS_OPCOES, JSON.stringify({ ...opcoes, som: Som.ligado })); } catch { /* sem storage: segue sem lembrar */ }
  }

  function restaurarOpcoes() {
    let salvas = null;
    try { salvas = JSON.parse(localStorage.getItem(CHAVE_DAS_OPCOES) || 'null'); } catch { salvas = null; }
    if (!salvas) return;
    const radio = document.querySelector(`input[name="dificuldade"][value="${salvas.dificuldade}"]`);
    if (radio) radio.checked = true;
    if (typeof salvas.pontoDeOuro === 'boolean') $('pontoDeOuro').checked = salvas.pontoDeOuro;
    if (salvas.setsParaVencer) $('formato').value = String(salvas.setsParaVencer);
    if (typeof salvas.som === 'boolean') { Som.ligado = salvas.som; atualizarBotaoDeSom(); }
  }

  function atualizarBotaoDeSom() {
    el.botaoSom.textContent = Som.ligado ? '🔊' : '🔇';
    el.botaoSom.setAttribute('aria-label', Som.ligado ? 'Desligar som' : 'Ligar som');
  }

  function mostrarTela(tela) {
    for (const t of [el.menu, el.pausa, el.fim]) t.classList.toggle('oculta', t !== tela);
  }

  function novaPartida(opcoes) {
    opcoesAtuais = opcoes;
    const semente = parametros.has('semente') ? Number(parametros.get('semente')) : undefined;
    partida = new Partida({ ...opcoes, humano: !modoAutomatico, semente });
    partida.ouvir((tipo, dados) => {
      Som.tocar(tipo, dados);
      if (tipo === 'fim') mostrarFim(dados.vencedor);
    });
    pausado = false;
    acumulador = 0;
    ultimaMensagem = '';
    mostrarTela(null);
    atualizarHud();
  }

  function mostrarFim(vencedor) {
    const venceu = vencedor === 0;
    el.resultado.textContent = modoAutomatico
      ? (venceu ? 'Casa venceu' : 'Visitantes venceram')
      : (venceu ? 'Você venceu! 🏆' : 'Perdeu desta vez');
    const e = partida.estatisticas;
    el.detalhesDoFim.textContent = `${partida.placar.resumo()} · ${e.pontos} pontos · maior rally: ${e.maiorRally} golpes`;
    mostrarTela(el.fim);
  }

  function alternarPausa(forcar) {
    if (!partida || partida.acabou) return;
    pausado = typeof forcar === 'boolean' ? forcar : !pausado;
    mostrarTela(pausado ? el.pausa : null);
  }

  function atualizarHud() {
    const p = partida.placar;
    el.setsCasa.textContent = p.sets[0];
    el.setsFora.textContent = p.sets[1];
    el.gamesCasa.textContent = p.games[0];
    el.gamesFora.textContent = p.games[1];
    el.pontosCasa.textContent = p.textoDosPontos(0);
    el.pontosFora.textContent = p.textoDosPontos(1);
    const sacador = p.sacador.time;
    el.saqueCasa.classList.toggle('sacando', sacador === 0);
    el.saqueFora.classList.toggle('sacando', sacador === 1);
    document.body.classList.toggle('ponto-decisivo', p.emPontoDecisivo);
    document.body.classList.toggle('tie-break', p.emTieBreak);

    const m = partida.mensagem;
    const texto = m ? m.texto : '';
    if (texto !== ultimaMensagem) {
      ultimaMensagem = texto;
      el.mensagem.textContent = texto;
      el.mensagem.classList.toggle('visivel', Boolean(texto));
      el.mensagem.classList.toggle('destaque', Boolean(m && m.destaque));
      el.mensagem.classList.toggle('suave', Boolean(m && m.suave));
    }
    const humanoSaca = partida.estado === 'saque' && partida.sacador.humano;
    el.botaoAcao.textContent = humanoSaca ? 'SACAR' : 'LOB';
    el.dica.textContent = humanoSaca
      ? 'Espaço saca (← → escolhem o canto da caixa)'
      : 'Setas/WASD movem · segure Espaço pra dar lob · ← → no golpe escolhem o canto · ↑ ataca, ↓ joga fundo';
  }

  function quadro(agora) {
    requestAnimationFrame(quadro);
    if (!partida) return;
    const dt = Math.min(0.1, (agora - ultimoQuadro) / 1000 || 0);
    ultimoQuadro = agora;

    const leitura = entrada.ler();
    if (leitura.pausa) alternarPausa();
    if (pausado || partida.acabou) { renderizador.desenhar(partida); return; }

    acumulador += dt;
    let passos = 0;
    while (acumulador >= PASSO && passos < 24) {
      partida.avancar(PASSO, leitura);
      leitura.acaoPressionada = false;   // "apertou" vale num passo só
      acumulador -= PASSO;
      passos += 1;
    }
    renderizador.desenhar(partida);
    atualizarHud();
  }

  function iniciar() {
    renderizador = new Renderizador(el.canvas);
    entrada = new Entrada({ areaDoToque: el.palco, botaoAcao: el.botaoAcao, joystick: el.joystick, knob: el.knob });
    entrada.aoInteragir = () => Som.garantir();
    restaurarOpcoes();
    atualizarBotaoDeSom();

    el.jogar.addEventListener('click', () => { Som.garantir(); const o = lerOpcoesDoMenu(); guardarOpcoes(o); novaPartida(o); });
    el.jogarDeNovo.addEventListener('click', () => { Som.garantir(); novaPartida(opcoesAtuais || lerOpcoesDoMenu()); });
    el.voltarAoMenu.addEventListener('click', () => { partida = null; mostrarTela(el.menu); });
    el.continuar.addEventListener('click', () => alternarPausa(false));
    el.sairParaOMenu.addEventListener('click', () => { partida = null; pausado = false; mostrarTela(el.menu); });
    el.botaoPausa.addEventListener('click', () => alternarPausa());
    el.botaoSom.addEventListener('click', () => { Som.ligado = !Som.ligado; Som.garantir(); atualizarBotaoDeSom(); if (opcoesAtuais) guardarOpcoes(opcoesAtuais); });
    document.addEventListener('visibilitychange', () => { if (document.hidden) alternarPausa(true); });
    window.addEventListener('resize', () => renderizador.redimensionar());
    if (window.ResizeObserver) new ResizeObserver(() => renderizador.redimensionar()).observe(el.palco);

    // Só desenha a quadra vazia atrás do menu.
    const vitrine = new Partida({ humano: false, semente: 1 });
    renderizador.desenhar(vitrine);
    mostrarTela(el.menu);
    if (modoAutomatico) novaPartida(lerOpcoesDoMenu());
    requestAnimationFrame((t) => { ultimoQuadro = t; requestAnimationFrame(quadro); });
  }

  window.PadelizouArcade = {
    get partida() { return partida; },
    novaPartida,
    alternarPausa,
  };

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
