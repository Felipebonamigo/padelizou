'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Arbitro, Quadra } = require('./carregar.js');

// Time 0 joga no lado +1; time 1 no lado -1.
function rallyDoTime0() { const a = new Arbitro(); a.registrarGolpe(0); return a; }

test('dois quiques do lado de quem recebe: ponto de quem bateu', () => {
  const a = rallyDoTime0();
  assert.equal(a.processar({ tipo: 'cruzouRede', para: -1 }), null);
  assert.equal(a.processar({ tipo: 'quique', x: 0, y: -5, lado: -1 }), null);
  assert.equal(a.processar({ tipo: 'parede', qual: 'fundo', lado: -1 }), null);   // depois do quique, pode
  assert.deepEqual(a.processar({ tipo: 'quique', x: 0, y: -7, lado: -1 }), { tipo: 'ponto', para: 0, motivo: 'doisQuiques' });
});

test('bola na parede do outro lado antes de quicar: ponto de quem recebia', () => {
  const a = rallyDoTime0();
  a.processar({ tipo: 'cruzouRede', para: -1 });
  assert.deepEqual(a.processar({ tipo: 'parede', qual: 'fundo', lado: -1 }), { tipo: 'ponto', para: 1, motivo: 'paredeSemQuicar' });
});

test('bater na própria parede antes de cruzar é permitido no rally', () => {
  const a = rallyDoTime0();
  assert.equal(a.processar({ tipo: 'parede', qual: 'fundo', lado: 1 }), null);
  assert.equal(a.processar({ tipo: 'cruzouRede', para: -1 }), null);
  assert.equal(a.processar({ tipo: 'quique', x: 0, y: -5, lado: -1 }), null);
});

test('bola que para na rede e cai do próprio lado: ponto de quem recebia, motivo rede', () => {
  const a = rallyDoTime0();
  assert.equal(a.processar({ tipo: 'rede' }), null);
  assert.deepEqual(a.processar({ tipo: 'quique', x: 0, y: 0.5, lado: 1 }), { tipo: 'ponto', para: 1, motivo: 'rede' });
});

test('bola que quica do próprio lado sem tocar a rede: não passou', () => {
  const a = rallyDoTime0();
  assert.deepEqual(a.processar({ tipo: 'quique', x: 0, y: 3, lado: 1 }), { tipo: 'ponto', para: 1, motivo: 'naoPassou' });
});

test('bola que sai por cima: contra quem bateu se não quicou, a favor se já tinha quicado', () => {
  const semQuique = rallyDoTime0();
  semQuique.processar({ tipo: 'cruzouRede', para: -1 });
  assert.deepEqual(semQuique.processar({ tipo: 'saiu', lado: -1 }), { tipo: 'ponto', para: 1, motivo: 'fora' });

  const comQuique = rallyDoTime0();
  comQuique.processar({ tipo: 'cruzouRede', para: -1 });
  comQuique.processar({ tipo: 'quique', x: 0, y: -5, lado: -1 });
  assert.deepEqual(comQuique.processar({ tipo: 'saiu', lado: -1 }), { tipo: 'ponto', para: 0, motivo: 'fora' });
});

test('bola que quica e volta pela rede sem ninguém tocar: ponto de quem bateu', () => {
  const a = rallyDoTime0();
  a.processar({ tipo: 'cruzouRede', para: -1 });
  a.processar({ tipo: 'quique', x: 0, y: -8, lado: -1 });
  a.processar({ tipo: 'parede', qual: 'fundo', lado: -1 });
  assert.deepEqual(a.processar({ tipo: 'cruzouRede', para: 1 }), { tipo: 'ponto', para: 0, motivo: 'voltouPeloVidro' });
});

test('o mesmo time não bate duas vezes; quem recebe o saque precisa deixar quicar', () => {
  const a = rallyDoTime0();
  assert.equal(a.podeGolpear(0), false);
  assert.equal(a.podeGolpear(1), true);

  const s = new Arbitro();
  s.iniciarSaque(0, Quadra.caixaDeSaque(-1, true));
  assert.equal(s.podeGolpear(1), false, 'não pode voleiar o saque');
  s.processar({ tipo: 'cruzouRede', para: -1 });
  s.processar({ tipo: 'quique', x: -2.5, y: -3.5, lado: -1 });
  assert.equal(s.podeGolpear(1), true);
});

test('saque fora da caixa é falta; a segunda falta é ponto; ponto novo zera as faltas', () => {
  const caixa = Quadra.caixaDeSaque(-1, true);   // lado -1, direita de quem está lá: x < 0
  const s = new Arbitro();
  s.iniciarSaque(0, caixa);
  s.processar({ tipo: 'cruzouRede', para: -1 });
  assert.deepEqual(s.processar({ tipo: 'quique', x: 2, y: -3, lado: -1 }), { tipo: 'falta', motivo: 'foraDaCaixa' });
  assert.equal(s.faltas, 1);
  s.iniciarSaque(0, caixa);
  s.processar({ tipo: 'cruzouRede', para: -1 });
  assert.deepEqual(s.processar({ tipo: 'quique', x: -2, y: -8, lado: -1 }), { tipo: 'ponto', para: 1, motivo: 'duplaFalta' });
  s.novoPonto();
  assert.equal(s.faltas, 0);
});

test('saque na caixa depois de tocar a rede é let; saque na caixa limpo segue o jogo', () => {
  const caixa = Quadra.caixaDeSaque(-1, true);
  const s = new Arbitro();
  s.iniciarSaque(0, caixa);
  s.processar({ tipo: 'rede' });
  assert.deepEqual(s.processar({ tipo: 'quique', x: -2, y: -3, lado: -1 }), { tipo: 'let' });
  assert.equal(s.faltas, 0);

  const limpo = new Arbitro();
  limpo.iniciarSaque(0, caixa);
  limpo.processar({ tipo: 'cruzouRede', para: -1 });
  assert.equal(limpo.processar({ tipo: 'quique', x: -2, y: -3, lado: -1 }), null);
  assert.equal(limpo.processar({ tipo: 'parede', qual: 'lateral', lado: -1 }), null);
});

test('no saque, bola na rede que cai do próprio lado ou na própria parede é falta, não ponto', () => {
  const caixa = Quadra.caixaDeSaque(-1, true);
  const s = new Arbitro();
  s.iniciarSaque(0, caixa);
  s.processar({ tipo: 'rede' });
  assert.deepEqual(s.processar({ tipo: 'quique', x: 0, y: 0.3, lado: 1 }), { tipo: 'falta', motivo: 'rede' });
  s.iniciarSaque(0, caixa);
  assert.deepEqual(s.processar({ tipo: 'parede', qual: 'fundo', lado: 1 }), { tipo: 'ponto', para: 1, motivo: 'duplaFalta' });
});

test('a caixa de saque diagonal fica do lado certo', () => {
  // Sacador do time 0 (lado +1) à direita (x > 0) saca pra caixa da direita de quem está no lado -1: x < 0.
  const caixa = Quadra.caixaDeSaque(-1, true);
  assert.deepEqual(caixa, { xMin: -5, xMax: 0, yMin: -6.95, yMax: 0 });
  assert.equal(Quadra.dentroDaCaixa(-2.5, -3.5, caixa), true);
  assert.equal(Quadra.dentroDaCaixa(2.5, -3.5, caixa), false);
  assert.equal(Quadra.dentroDaCaixa(-2.5, -7.5, caixa), false);
  const esquerda = Quadra.caixaDeSaque(1, false);
  assert.deepEqual(esquerda, { xMin: -5, xMax: 0, yMin: 0, yMax: 6.95 });
});
