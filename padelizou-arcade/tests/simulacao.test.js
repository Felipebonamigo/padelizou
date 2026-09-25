'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Partida, Quadra } = require('./carregar.js');

// Roda a partida inteira com os quatro jogadores na IA, em tempo simulado, com semente fixa.
function jogarAteOFim(opcoes, limiteDeSegundos = 3600) {
  const partida = new Partida({ humano: false, semente: 42, ...opcoes });
  const passo = 1 / 120;
  let t = 0;
  const bola = partida.bola;
  while (!partida.acabou && t < limiteDeSegundos) {
    partida.avancar(passo);
    t += passo;
    assert.ok(Number.isFinite(bola.x) && Number.isFinite(bola.y) && Number.isFinite(bola.z), 'bola com NaN');
    assert.ok(Math.abs(bola.x) <= 5.01 && Math.abs(bola.y) <= 10.01, `bola fora da quadra em (${bola.x}, ${bola.y})`);
    for (const j of partida.jogadores) {
      assert.ok(Math.abs(j.x) <= 4.7 && Math.abs(j.y) <= 9.7, `jogador fora da quadra: ${j.nome}`);
      assert.ok(Quadra.ladoDe(j.y) === j.lado, `${j.nome} atravessou a rede`);
    }
  }
  return { partida, segundos: t };
}

test('uma partida de um set entre IAs termina, com pontos de todo tipo e sem bola perdida', () => {
  const { partida, segundos } = jogarAteOFim({ setsParaVencer: 1, dificuldade: 'medio' });
  assert.equal(partida.acabou, true, `a partida não terminou em ${segundos.toFixed(0)} s simulados`);
  assert.ok(partida.placar.vencedor === 0 || partida.placar.vencedor === 1);
  const e = partida.estatisticas;
  assert.ok(e.pontos >= 24, `poucos pontos: ${e.pontos}`);
  assert.ok(e.maiorRally >= 4, `nenhum rally de verdade: maior foi ${e.maiorRally}`);
  assert.ok(e.motivos.doisQuiques > 0, 'ninguém venceu ponto por dois quiques: ' + JSON.stringify(e.motivos));
  assert.ok(!e.motivos.bolaMorta, 'bola morta é o caminho de segurança e não devia acontecer: ' + JSON.stringify(e.motivos));
  assert.ok(segundos / e.pontos < 60, `pontos lentos demais: ${(segundos / e.pontos).toFixed(1)} s por ponto`);
});

test('a IA saca dentro da caixa na maior parte das vezes', () => {
  const { partida } = jogarAteOFim({ setsParaVencer: 1, dificuldade: 'dificil' });
  const e = partida.estatisticas;
  const saques = e.pontos + e.faltas + e.lets;
  assert.ok(e.faltas / saques < 0.25, `faltas demais: ${e.faltas} em ${saques} saques`);
  assert.ok(e.pontos > 0);
});

test('o placar da simulação bate com a soma dos eventos ouvidos', () => {
  const partida = new Partida({ humano: false, semente: 7, setsParaVencer: 1 });
  const contagem = { ponto: 0, game: 0, set: 0, partida: 0, golpe: 0 };
  partida.ouvir((tipo) => { if (tipo in contagem) contagem[tipo] += 1; });
  let t = 0;
  while (!partida.acabou && t < 3600) { partida.avancar(1 / 120); t += 1 / 120; }
  const pontosDecididos = contagem.ponto + contagem.game + contagem.set + contagem.partida;
  assert.equal(pontosDecididos, partida.estatisticas.pontos);
  assert.equal(contagem.partida, 1);
  assert.equal(contagem.golpe, partida.estatisticas.golpes);
  const games = partida.placar.setsAnteriores[0].games;
  assert.equal(contagem.game + contagem.partida + contagem.set, games[0] + games[1]);
});

test('a mesma semente reproduz a mesma partida', () => {
  const a = jogarAteOFim({ semente: 99 }, 1200).partida;
  const b = jogarAteOFim({ semente: 99 }, 1200).partida;
  assert.equal(a.placar.resumo(), b.placar.resumo());
  assert.deepEqual(a.estatisticas, b.estatisticas);
});

test('o humano parado perde pontos, mas o parceiro cobre parte deles', () => {
  const partida = new Partida({ humano: true, semente: 3, setsParaVencer: 1, dificuldade: 'facil' });
  let t = 0;
  while (!partida.acabou && t < 900) { partida.avancar(1 / 120); t += 1 / 120; }
  assert.ok(partida.estatisticas.pontos > 10, 'com o humano parado o jogo devia seguir sozinho (saque automático)');
  assert.ok(partida.jogadores[1].golpes > 0, 'o parceiro nunca bateu na bola');
});
