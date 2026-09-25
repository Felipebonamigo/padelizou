'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Fisica, Quadra } = require('./carregar.js');
const { Bola, calcularGolpe } = Fisica;

function simular(bola, segundos, passo = 1 / 120) {
  const eventos = [];
  for (let t = 0; t < segundos && bola.emJogo && !bola.parada; t += passo) bola.avancar(passo, eventos);
  return eventos;
}

test('bola solta do alto quica, perde energia e acaba rolando parada', () => {
  const b = new Bola();
  b.posicionar(1, 4, 2);
  b.lancar({ vx: 0, vy: 0, vz: 0 });
  const eventos = simular(b, 6);
  const quiques = eventos.filter((e) => e.tipo === 'quique');
  assert.ok(quiques.length >= 3, `esperava vários quiques, veio ${quiques.length}`);
  assert.equal(quiques[0].lado, 1);
  assert.equal(b.z, 0);
  assert.equal(b.parada, true);
  assert.ok(Number.isFinite(b.x) && Number.isFinite(b.y));
});

test('a parede lateral devolve a bola pra dentro, com evento de parede', () => {
  const b = new Bola();
  b.posicionar(4, 5, 1);
  b.lancar({ vx: 10, vy: 0, vz: 3 });
  const eventos = simular(b, 1);
  const parede = eventos.find((e) => e.tipo === 'parede');
  assert.ok(parede, 'faltou o evento de parede');
  assert.equal(parede.qual, 'lateral');
  assert.ok(b.x < 5 && b.x > -5, `bola fora da quadra: x=${b.x}`);
});

test('a parede de fundo também devolve, e a bola alta demais sai da quadra', () => {
  const baixa = new Bola();
  baixa.posicionar(0, -9, 1);
  baixa.lancar({ vx: 0, vy: -8, vz: 2 });
  const evBaixa = simular(baixa, 0.5);
  assert.ok(evBaixa.some((e) => e.tipo === 'parede' && e.qual === 'fundo'));
  assert.equal(baixa.emJogo, true);

  const alta = new Bola();
  alta.posicionar(0, -9, 3.9);
  alta.lancar({ vx: 0, vy: -8, vz: 6 });
  const evAlta = simular(alta, 0.5);
  assert.ok(evAlta.some((e) => e.tipo === 'saiu'), 'bola acima de 4 m devia sair');
  assert.equal(alta.emJogo, false);
});

test('bola baixa bate na rede e volta; bola alta cruza com evento de cruzouRede', () => {
  const baixa = new Bola();
  baixa.posicionar(0, 3, 0.5);
  baixa.lancar({ vx: 0, vy: -10, vz: 1 });
  const evBaixa = simular(baixa, 0.6);
  assert.ok(evBaixa.some((e) => e.tipo === 'rede'), 'faltou o evento de rede');
  assert.ok(baixa.y > 0, 'depois da rede a bola devia ficar do lado de onde veio');

  const alta = new Bola();
  alta.posicionar(0, 3, 1.5);
  alta.lancar({ vx: 0, vy: -10, vz: 3 });
  const evAlta = simular(alta, 0.6);
  const cruzou = evAlta.find((e) => e.tipo === 'cruzouRede');
  assert.ok(cruzou, 'faltou o evento de cruzouRede');
  assert.equal(cruzou.para, -1);
  assert.ok(cruzou.z > Quadra.ALTURA_DA_REDE);
});

test('calcularGolpe leva a bola por cima da rede até perto do alvo', () => {
  const casos = [
    { origem: { x: 2, y: 7, z: 1 }, alvo: { x: -2, y: -6 }, tempoDeVoo: 0.9 },
    { origem: { x: -3, y: -8, z: 0.8 }, alvo: { x: 3, y: 6.5 }, tempoDeVoo: 1.0 },
    { origem: { x: 0, y: 2, z: 0.6 }, alvo: { x: 0, y: -7 }, tempoDeVoo: 0.5 },   // curto e baixo: precisa subir o arco
    { origem: { x: 1, y: 4, z: 1 }, alvo: { x: -1, y: -8 }, tempoDeVoo: 1.7 },    // lob
  ];
  for (const caso of casos) {
    const v = calcularGolpe(caso.origem, caso.alvo, { tempoDeVoo: caso.tempoDeVoo });
    const b = new Bola();
    b.posicionar(caso.origem.x, caso.origem.y, caso.origem.z);
    b.lancar(v);
    const eventos = simular(b, 3);
    const indiceDoQuique = eventos.findIndex((e) => e.tipo === 'quique');
    assert.ok(indiceDoQuique >= 0, 'não quicou');
    const ateOQuique = eventos.slice(0, indiceDoQuique);   // depois do quique a bola volta pelas paredes; não interessa
    const cruzou = ateOQuique.find((e) => e.tipo === 'cruzouRede');
    const rede = ateOQuique.find((e) => e.tipo === 'rede');
    assert.ok(cruzou && !rede, `caso ${JSON.stringify(caso)}: não passou a rede limpo`);
    const quique = eventos[indiceDoQuique];
    const erro = Math.hypot(quique.x - caso.alvo.x, quique.y - caso.alvo.y);
    assert.ok(erro < 0.6, `caso ${JSON.stringify(caso)}: caiu a ${erro.toFixed(2)} m do alvo (${quique.x.toFixed(2)}, ${quique.y.toFixed(2)})`);
  }
});

test('calcularGolpe com ignorarRede deixa a bola baixa (é como a IA erra na rede)', () => {
  const v = calcularGolpe({ x: 0, y: 6, z: 0.9 }, { x: 0, y: -0.3 }, { tempoDeVoo: 0.45, ignorarRede: true });
  const b = new Bola();
  b.posicionar(0, 6, 0.9);
  b.lancar(v);
  const eventos = simular(b, 2);
  assert.ok(eventos.some((e) => e.tipo === 'rede'), 'a bola devia parar na rede');
});
