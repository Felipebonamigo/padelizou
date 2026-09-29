// Versão do conteúdo do jogo: impressões (hash de 8 dígitos hex) do que decide uma corrida. Duas:
// - CONTENT_FINGERPRINT (online): o jogo inteiro; vai no create/join e o relay só junta quem tem a mesma.
// - lapFingerprint(pista, carro) (fantasma e recordes): só o que decide uma volta daquele carro naquela pista.
//   Uma pista nova, um ajuste de outro carro ou um enfeite trocado não invalidam o fantasma de ninguém.
// Puro: nada de DOM, armazenamento ou relógio.
import * as SIM_CONSTANTS from '../core/constants';
import { CARS } from '../core/data/cars';
import { AI_DRIVERS, DRIVER_PERSONALITY, NEUTRAL_TUNING, PERSONALITY_TUNING } from '../core/data/drivers';
import { hashString } from '../core/rng';
import { DIFFICULTY_SKILL, DIFFICULTY_SPEED } from '../core/sim/ai';
import { effectiveStats } from '../core/sim/stats';
import { getTrack, TRACKS } from '../core/track';
import { SPRITE_HALF_WIDTH } from '../core/track/sprites';
import type { Track } from '../core/types';
import { PROTOCOL_VERSION } from '../net/protocol';

/** JSON com as chaves de todo objeto em ordem (a ordem de declaração não muda a impressão). */
function canonicalJson(value: unknown): string {
  return JSON.stringify(value, (_key, v: unknown) => {
    if (typeof v !== 'object' || v === null || Array.isArray(v)) return v;
    return Object.fromEntries(Object.entries(v).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)));
  });
}

/** Impressão de 8 dígitos hex de um conteúdo qualquer (JSON). */
export function contentFingerprint(content: unknown): string {
  return hashString(canonicalJson(content)).toString(16).padStart(8, '0');
}

/**
 * Impressão deste jogo: carros, pistas, constantes da simulação e os dados da IA (habilidade, ritmo, elenco e
 * personalidades). Vai no
 * create/join e o relay só põe na mesma sala quem tem a mesma. Sem isso, com o mesmo
 * PROTOCOL_VERSION, um build com uma pista nova largava nela e o outro descartava a largada calado
 * (id desconhecido) — o anfitrião corria esperando por ele para sempre. Mudança só no código da
 * física não entra aqui: essa aparece como dessincronia (hash a cada segundo).
 */
export function fingerprintContent(): Record<string, unknown> {
  return {
    protocol: PROTOCOL_VERSION, constants: SIM_CONSTANTS, cars: CARS, tracks: TRACKS, aiSkill: DIFFICULTY_SKILL,
    // Dados que decidem a pilotagem da IA sem estar em constants.ts: elenco, personalidade de cada um e ritmo.
    aiSpeed: DIFFICULTY_SPEED, aiDrivers: AI_DRIVERS, driverPersonality: DRIVER_PERSONALITY,
    personalityTuning: PERSONALITY_TUNING, neutralTuning: NEUTRAL_TUNING,
  };
}

export const CONTENT_FINGERPRINT = contentFingerprint(fingerprintContent());

// ───────────────────────────── Impressão de uma volta ─────────────────────────────

/**
 * Revisão da física de quem pilota: suba quando uma mudança de CÓDIGO (não de dado) mudar como o carro anda —
 * fórmula da física, batida no cenário, contagem da volta, freio da direção assistida. Constantes, carro e traçado
 * já entram na impressão sozinhos. `tests/sim-golden.test.ts` prende esta revisão ao hash do contra-relógio de
 * referência (o carro sozinho na pista): mudou um sem o outro, o teste falha.
 */
export const PHYSICS_REVISION = 1;

/** Impressão bem formada (8 dígitos hex minúsculos). */
export function isFingerprint(v: unknown): v is string {
  return typeof v === 'string' && /^[0-9a-f]{8}$/.test(v);
}

/**
 * FNV-1a sobre os bits exatos de uma lista de números (8 hex). Sem passar por texto: com número → texto as 32
 * pistas custavam ~250 ms (frio, na tela de recordes); assim, ~40 ms. Cada passo é bijetivo, então trocar um
 * único número sempre muda o hash.
 */
function hashNumbers(values: readonly number[]): string {
  const words = new Uint32Array(Float64Array.from(values).buffer);
  let h = 0x811c9dc5;
  for (let i = 0; i < words.length; i++) { h ^= words[i]; h = Math.imul(h, 0x01000193) >>> 0; }
  return h.toString(16).padStart(8, '0');
}

/**
 * O traçado como a simulação o lê: comprimento, largada e, por segmento, curva, box e os obstáculos sólidos (lado
 * e meia largura — o que a batida usa, collisions.ts). Fica de fora o que é só de exibição: nome da pista, zebra,
 * relevo (a física não lê a altura) e enfeites não sólidos, para a arte da Fase 2 poder trocá-los sem apagar fantasma.
 */
function trackLayout(track: Track): string {
  const v: number[] = [track.length, track.startIndex, track.segments.length];
  for (const s of track.segments) {
    const solid = s.sprites.filter((sp) => sp.solid);
    v.push(s.curve, s.pit ? 1 : 0, solid.length);
    for (const sp of solid) v.push(sp.x, SPRITE_HALF_WIDTH[sp.kind] * sp.scale);
  }
  return hashNumbers(v);
}

function knownTrack(trackId: string): boolean {
  return TRACKS.some((t) => t.id === trackId);
}

/** Comprimento da pista em unidades de mundo; null se o jogo não tem a pista. */
export function trackLength(trackId: string): number | null {
  return knownTrack(trackId) ? getTrack(trackId).length : null;
}

/**
 * O que decide uma volta de um carro de fábrica numa pista: revisão da física, TODAS as constantes da simulação
 * (de propósito conservador — as só da IA também entram: separar à mão erra fácil, o freio da direção assistida
 * usa uma constante "da IA"; o custo é um ajuste só de IA também renovar os fantasmas), os atributos do carro
 * como a física os usa (effectiveStats: nome, cor e preço ficam de fora) e o traçado. null se a pista ou o
 * carro não existem.
 */
export function lapContent(trackId: string, carId: string): Record<string, unknown> | null {
  const car = CARS.find((c) => c.id === carId);
  if (!car || !knownTrack(trackId)) return null;
  return { physics: PHYSICS_REVISION, constants: SIM_CONSTANTS, car: effectiveStats(car), track: trackLayout(getTrack(trackId)) };
}

/** O conteúdo é fixo durante a execução: a impressão de cada par pista × carro é calculada uma vez. */
const lapCache = new Map<string, string>();

/** Impressão de uma volta daquele carro naquela pista (fantasma e recordes); null se a pista ou o carro não existem. */
export function lapFingerprint(trackId: string, carId: string): string | null {
  const key = `${trackId}\n${carId}`;
  const hit = lapCache.get(key);
  if (hit !== undefined) return hit;
  const content = lapContent(trackId, carId);
  if (!content) return null;
  const fp = contentFingerprint(content);
  lapCache.set(key, fp);
  return fp;
}
