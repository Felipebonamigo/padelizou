// Validador de um modelo de marco turístico: as regras da convenção (types.ts) que se medem na malha — partes e
// materiais, cor por vértice, malha não indexada, orçamento de triângulos e tamanho do lugar, base no chão e origem
// na pegada. Vale para o marco procedural e para o carregado de um .glb (gltf.ts): o mesmo critério de aceite para
// os dois (o procedural passa: tests/landmark-gltf.test.ts). Puro, sem DOM.
import type { MatKey, Model } from '../geom';
import type { LandmarkDef } from './types';

type Place = LandmarkDef['place'];

/**
 * Limites por lugar. Orçamento e altura mínima são os do tests/landmarks.test.ts (docs/DESEMPENHO.md); a altura e a
 * pegada máximas pegam o arquivo fora de escala (em centímetros, ou em polegadas) — folga de ~2× sobre o maior marco
 * procedural de cada lugar (perto 69 m de altura e 720 m de pegada; longe 807 m e 1,4 km; horizonte 798 m e 2,1 km).
 */
export const LANDMARK_LIMITS = {
  budget: { near: 3500, far: 5000, skyline: 2500 },
  minHeight: { near: 6, far: 25, skyline: 90 },
  maxHeight: { near: 150, far: 1500, skyline: 3000 },
  maxExtent: { near: 1500, far: 3000, skyline: 6000 },
  maxParts: 4,
} as const;

/** Materiais que um marco pode usar (geom.ts). `panel` (atlas de outdoors) não entra num marco. */
export const LANDMARK_MATS: readonly MatKey[] = ['flat', 'glow', 'beacon', 'cone', 'office', 'apartment', 'classic', 'house'];
/** Fachadas: precisam de uv (janelas em metros, `structures.FACADE_TILE`). */
const FACADES: readonly MatKey[] = ['office', 'apartment', 'classic', 'house'];

const m1 = (v: number) => v.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const int = (v: number) => v.toLocaleString('pt-BR');
const PLACE_PT: Record<Place, string> = { near: 'perto', far: 'longe', skyline: 'horizonte' };

/** Problemas do modelo, em português (vazio = aceito). */
export function checkLandmarkModel(m: Model, place: Place): string[] {
  const out: string[] = [];
  const L = LANDMARK_LIMITS;
  if (m.parts.length === 0) return ['modelo sem nenhuma parte'];
  if (m.parts.length > L.maxParts) out.push(`${m.parts.length} partes (até ${L.maxParts}: uma por material)`);
  let tris = 0;
  let minX = Infinity; let maxX = -Infinity; let minY = Infinity; let maxY = -Infinity; let minZ = Infinity; let maxZ = -Infinity;
  let bad = false;
  for (const part of m.parts) {
    const g = part.geometry;
    const label = `parte "${part.mat}"`;
    if (!LANDMARK_MATS.includes(part.mat)) out.push(`material "${part.mat}" não vale num marco (${LANDMARK_MATS.join(', ')})`);
    if (g.index) out.push(`${label}: malha indexada (o marco usa faces planas, sem vértice compartilhado)`);
    const pos = g.getAttribute('position');
    if (!pos || pos.count === 0) { out.push(`${label}: sem triângulos`); continue; }
    if (!g.index && pos.count % 3 !== 0) out.push(`${label}: ${int(pos.count)} vértices não fecham triângulos`);
    // `cone` é o facho de luz da noite: só posição e normal, a cor vem do material.
    if (part.mat !== 'cone' && !g.getAttribute('color')) out.push(`${label}: sem cor por vértice (COLOR_0) — passe pelo conversor ou pinte os vértices`);
    if (FACADES.includes(part.mat) && !g.getAttribute('uv')) out.push(`${label}: fachada sem uv (as janelas precisam)`);
    tris += (g.index ? g.index.count : pos.count) / 3;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i); const y = pos.getY(i); const z = pos.getZ(i);
      if (!Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) { bad = true; continue; }
      if (x < minX) minX = x; if (x > maxX) maxX = x;
      if (y < minY) minY = y; if (y > maxY) maxY = y;
      if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
    }
  }
  if (bad) out.push('vértices com coordenada inválida (NaN ou infinito)');
  if (tris > L.budget[place]) out.push(`${int(tris)} triângulos (até ${int(L.budget[place])} num marco de ${PLACE_PT[place]})`);
  if (!Number.isFinite(maxY)) return out;

  const height = maxY;
  const extent = Math.max(maxX - minX, maxZ - minZ);
  if (height < L.minHeight[place]) out.push(`altura ${m1(height)} m (mínimo ${L.minHeight[place]} m num marco de ${PLACE_PT[place]}, para ler de longe) — escala errada? use --height`);
  if (height > L.maxHeight[place]) out.push(`altura ${m1(height)} m passa de ${int(L.maxHeight[place])} m num marco de ${PLACE_PT[place]} — escala errada (centímetros?)`);
  if (extent > L.maxExtent[place]) out.push(`pegada de ${m1(extent)} m passa de ${int(L.maxExtent[place])} m — escala errada (centímetros?)`);
  // Base: y = 0 é o chão; o que fica abaixo (alicerce, saia) cobre o declive, mas não mais fundo que a altura do marco.
  if (minY > 0.01) out.push(`a base começa a ${m1(minY)} m do chão (tem de tocar y = 0)`);
  if (minY < -Math.max(1, height)) out.push(`a parte abaixo do chão (${m1(-minY)} m) passa da altura do marco`);
  // Origem no centro da pegada: no mínimo dentro dela (o layout gira o marco em torno da origem).
  if (minX > 0 || maxX < 0 || minZ > 0 || maxZ < 0) out.push(`a origem fica fora da pegada (x ${m1(minX)} a ${m1(maxX)} m, z ${m1(minZ)} a ${m1(maxZ)} m): centre o modelo`);
  return out;
}
