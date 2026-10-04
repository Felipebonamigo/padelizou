// Validador de um modelo de carro: as regras do briefing de arte que se medem na malha (pegada, rodas,
// faróis e lanternas, escapamento, orçamento, pinturas em uso). Vale para o carro procedural e para o
// carregado de um .glb (gltf.ts): o mesmo critério de aceite para os dois. Puro, sem DOM.
// Coordenadas do jogo: x para a direita, y para cima, −z é a frente; metros.
import { CARS } from '../../core/data/cars';
import type { CarModel } from './model';
import { LIVERY_A, LIVERY_B, liveryFor } from './paints';

/** Pegada (do briefing e do tests/car-models.test.ts): casco em |x| ≤ 1,0 e |z| ≤ 2,35, até 1,95 de altura. */
export const CAR_LIMITS = {
  halfWidth: 1.0, halfLength: 2.35, maxHeight: 1.95, minGround: 0.04,
  minLength: 4.2, minLengthMicro: 3.2, minWidth: 1.8,
  minWheelRadius: 0.26, maxShellTriangles: 3500,
} as const;

const m1 = (v: number) => v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const int = (v: number) => v.toLocaleString('pt-BR');

/** Problemas do modelo, em português (vazio = aceito). */
export function checkCarModel(m: CarModel): string[] {
  const out: string[] = [];
  const L = CAR_LIMITS;
  const g = m.shell;
  g.computeBoundingBox();
  const bb = g.boundingBox;
  if (!bb) return ['malha vazia'];
  const length = bb.max.z - bb.min.z;
  const width = bb.max.x - bb.min.x;
  if (bb.max.z > L.halfLength || bb.min.z < -L.halfLength) out.push(`comprimento ${m1(length)} m sai da pegada (|z| até ${m1(L.halfLength)} m do centro)`);
  if (bb.max.x > L.halfWidth || bb.min.x < -L.halfWidth) out.push(`largura ${m1(width)} m sai da pegada (|x| até ${m1(L.halfWidth)} m do centro)`);
  if (bb.max.y >= L.maxHeight) out.push(`altura ${m1(bb.max.y)} m passa de ${m1(L.maxHeight)} m`);
  if (bb.min.y <= L.minGround) out.push(`a carroceria encosta no chão (ponto mais baixo ${m1(bb.min.y)} m)`);
  const minLength = m.body === 'micro' ? L.minLengthMicro : L.minLength;
  if (length <= minLength) out.push(`comprimento ${m1(length)} m é curto demais (mais de ${m1(minLength)} m; a colisão é de 4,0 m)`);
  if (width <= L.minWidth) out.push(`largura ${m1(width)} m é estreita demais (mais de ${m1(L.minWidth)} m; a colisão é de 1,82 m)`);

  const [front, rear] = m.axles;
  if (!(front.z < 0)) out.push('o eixo da frente tem de ficar na frente (−z no jogo, +z no glTF)');
  if (!(rear.z > 0)) out.push('o eixo de trás tem de ficar atrás (+z no jogo, −z no glTF)');
  for (const [name, a] of [['da frente', front], ['de trás', rear]] as const) {
    if (a.x + a.w / 2 > 0.99) out.push(`as rodas ${name} saem da largura (centro a ${m1(a.x)} m, pneu de ${m1(a.w)} m)`);
    if (a.r <= L.minWheelRadius) out.push(`as rodas ${name} são pequenas demais (raio ${m1(a.r)} m; mais de ${m1(L.minWheelRadius)} m)`);
    if (Math.abs(a.z) + a.r >= 2.2) out.push(`as rodas ${name} passam da ponta do carro`);
  }

  // Faróis (aMat.z) na frente, lanternas (aMat.w) atrás.
  const pos = g.getAttribute('position'); const mat = g.getAttribute('aMat');
  let heads = 0; let tails = 0; let headBack = false; let tailFront = false;
  for (let i = 0; i < pos.count; i++) {
    if (mat.getZ(i) > 0.2) { heads++; if (pos.getZ(i) >= 0.7) headBack = true; }
    if (mat.getW(i) > 0.5) { tails++; if (pos.getZ(i) <= 0.5) tailFront = true; }
  }
  if (heads === 0) out.push('sem farol (material headlight)');
  if (tails === 0) out.push('sem lanterna (material taillight)');
  if (headBack) out.push('farol na traseira (o farol fica na frente: +z no glTF)');
  if (tailFront) out.push('lanterna na frente (a lanterna fica atrás: −z no glTF)');

  if (m.exhausts.length < 1 || m.exhausts.length > 2) out.push(`${m.exhausts.length} saídas de escapamento (são 1 ou 2)`);
  for (const e of m.exhausts) {
    if (e[2] <= bb.max.z - 0.25) out.push('o escapamento tem de ficar na traseira');
    if (Math.abs(e[0]) >= 0.95) out.push('o escapamento sai da largura do carro');
  }

  if (m.triangles > L.maxShellTriangles) out.push(`${int(m.triangles)} triângulos no casco (até ${int(L.maxShellTriangles)})`);

  // As pinturas que os carros deste estilo usam têm de existir na malha (senão dois carros saem iguais).
  const paint = g.getAttribute('aPaint');
  const layers = new Set<number>();
  for (let i = 0; i < paint.count; i++) layers.add(Math.round(paint.getZ(i) * 3));
  for (const def of CARS.filter((c) => c.body === m.body)) {
    const liv = liveryFor(def, CARS, m.liveries);
    if (liv & LIVERY_A && !layers.has(1) && !layers.has(3)) out.push(`o ${def.name} usa a faixa A (material stripe_a), que o modelo não tem`);
    if (liv & LIVERY_B && !layers.has(2) && !layers.has(3)) out.push(`o ${def.name} usa a faixa B (material stripe_b), que o modelo não tem`);
  }
  return out;
}
