'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Regras } = require('./carregar.js');
const { Placar } = Regras;

function pontos(placar, time, quantos) { let ev; for (let i = 0; i < quantos; i++) ev = placar.pontoPara(time); return ev; }
function ganharGame(placar, time) { return pontos(placar, time, 4); }

test('os pontos contam 0, 15, 30, 40', () => {
  const p = new Placar();
  assert.equal(p.textoDosPontos(0), '0');
  p.pontoPara(0); assert.equal(p.textoDosPontos(0), '15');
  p.pontoPara(0); assert.equal(p.textoDosPontos(0), '30');
  p.pontoPara(0); assert.equal(p.textoDosPontos(0), '40');
  assert.equal(p.textoDosPontos(1), '0');
});

test('quatro pontos seguidos fecham o game', () => {
  const p = new Placar();
  pontos(p, 0, 3);
  const ev = p.pontoPara(0);
  assert.deepEqual(ev, { tipo: 'game', time: 0 });
  assert.deepEqual(p.games, [1, 0]);
  assert.deepEqual(p.pontos, [0, 0]);
});

test('com ponto de ouro, 40-40 decide no próximo ponto', () => {
  const p = new Placar({ pontoDeOuro: true });
  pontos(p, 0, 3); pontos(p, 1, 3);
  assert.equal(p.emPontoDecisivo, true);
  assert.equal(p.textoDosPontos(0), '40');
  const ev = p.pontoPara(1);
  assert.equal(ev.tipo, 'game');
  assert.deepEqual(p.games, [0, 1]);
});

test('sem ponto de ouro, 40-40 vai pra vantagem e volta a iguais', () => {
  const p = new Placar({ pontoDeOuro: false });
  pontos(p, 0, 3); pontos(p, 1, 3);
  assert.equal(p.emPontoDecisivo, false);
  assert.equal(p.pontoPara(0).tipo, 'ponto');
  assert.equal(p.textoDosPontos(0), 'AD');
  assert.equal(p.textoDosPontos(1), '40');
  assert.equal(p.pontoPara(1).tipo, 'ponto');
  assert.equal(p.textoDosPontos(0), '40');
  assert.equal(p.textoDosPontos(1), '40');
  p.pontoPara(1);
  assert.equal(p.pontoPara(1).tipo, 'game');
  assert.deepEqual(p.games, [0, 1]);
});

test('o set fecha em 6 games com dois de diferença; 6-5 continua', () => {
  const p = new Placar();
  for (let i = 0; i < 5; i++) { ganharGame(p, 0); ganharGame(p, 1); }
  assert.deepEqual(p.games, [5, 5]);
  assert.equal(ganharGame(p, 0).tipo, 'game');
  assert.deepEqual(p.games, [6, 5]);
  assert.equal(p.emTieBreak, false);
  const ev = ganharGame(p, 0);
  assert.equal(ev.tipo, 'partida');
  assert.deepEqual(p.setsAnteriores, [{ games: [7, 5], tieBreak: null }]);
  assert.equal(p.vencedor, 0);
  assert.equal(p.resumo(), '7-5');
});

test('6-6 abre tie-break, que vai a 7 com dois de diferença e fecha o set em 7-6', () => {
  const p = new Placar();
  for (let i = 0; i < 6; i++) { ganharGame(p, 0); ganharGame(p, 1); }
  assert.equal(p.emTieBreak, true);
  assert.equal(p.textoDosPontos(0), '0');
  pontos(p, 0, 6); pontos(p, 1, 6);
  assert.equal(p.textoDosPontos(0), '6');
  assert.equal(p.pontoPara(0).tipo, 'ponto');        // 7-6 não fecha
  assert.equal(p.pontoPara(1).tipo, 'ponto');        // 7-7
  p.pontoPara(1);
  const ev = p.pontoPara(1);                          // 9-7
  assert.equal(ev.tipo, 'partida');
  assert.equal(p.vencedor, 1);
  assert.deepEqual(p.setsAnteriores, [{ games: [6, 7], tieBreak: [7, 9] }]);
  assert.equal(p.resumo(), '6-7(7)');
});

test('melhor de três: precisa de dois sets', () => {
  const p = new Placar({ setsParaVencer: 2 });
  for (let i = 0; i < 6; i++) ganharGame(p, 0);
  assert.deepEqual(p.sets, [1, 0]);
  assert.equal(p.acabou, false);
  for (let i = 0; i < 6; i++) ganharGame(p, 1);
  assert.deepEqual(p.sets, [1, 1]);
  for (let i = 0; i < 5; i++) ganharGame(p, 0);
  const ev = ganharGame(p, 0);
  assert.equal(ev.tipo, 'partida');
  assert.equal(p.vencedor, 0);
  assert.equal(p.resumo(), '6-0 0-6 6-0');
  assert.deepEqual(p.pontoPara(1), { tipo: 'encerrada', time: 0 });
});

test('o saque alterna entre os times a cada game e entre os jogadores do time', () => {
  const p = new Placar({ timeQueSaca: 0 });
  assert.deepEqual(p.sacador, { time: 0, jogador: 0 });
  ganharGame(p, 0);
  assert.deepEqual(p.sacador, { time: 1, jogador: 0 });
  ganharGame(p, 0);
  assert.deepEqual(p.sacador, { time: 0, jogador: 1 });
  ganharGame(p, 1);
  assert.deepEqual(p.sacador, { time: 1, jogador: 1 });
  ganharGame(p, 1);
  assert.deepEqual(p.sacador, { time: 0, jogador: 0 });
});

test('o lado do saque é a direita com soma de pontos par, esquerda com ímpar', () => {
  const p = new Placar();
  assert.equal(p.ladoDoSaque, 'direita');
  p.pontoPara(0); assert.equal(p.ladoDoSaque, 'esquerda');
  p.pontoPara(1); assert.equal(p.ladoDoSaque, 'direita');
  p.pontoPara(1); assert.equal(p.ladoDoSaque, 'esquerda');
});

test('no tie-break quem abriu saca um ponto e depois trocam de dois em dois', () => {
  const p = new Placar({ timeQueSaca: 0 });
  for (let i = 0; i < 6; i++) { ganharGame(p, 0); ganharGame(p, 1); }
  // 12 games: o time 0 abriu o 1º, o time 1 o 2º… o 13º (tie-break) é do time 0.
  assert.equal(p.sacador.time, 0);
  p.pontoPara(0); assert.equal(p.sacador.time, 1);
  p.pontoPara(0); assert.equal(p.sacador.time, 1);
  p.pontoPara(0); assert.equal(p.sacador.time, 0);
  p.pontoPara(1); assert.equal(p.sacador.time, 0);
  p.pontoPara(1); assert.equal(p.sacador.time, 1);
});

test('depois do tie-break, o set seguinte começa com o time que NÃO abriu o tie-break', () => {
  const p = new Placar({ timeQueSaca: 0, setsParaVencer: 2 });
  for (let i = 0; i < 6; i++) { ganharGame(p, 0); ganharGame(p, 1); }
  assert.equal(p.sacador.time, 0);
  const ev = pontos(p, 0, 7);
  assert.equal(ev.tipo, 'set');
  assert.equal(p.sacador.time, 1);
  assert.deepEqual(p.games, [0, 0]);
});
