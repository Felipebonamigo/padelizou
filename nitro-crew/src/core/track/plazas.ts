// Praças da cidade: nas pistas city_night a receita do cenário (builder.ts) enche os dois lados de prédios e torres
// na beira da pista — um paredão contínuo, e os marcos turísticos (MASP, Ópera de Arame, Torre de TV…, postos pelo
// renderizador a 30–330 m) ficavam atrás dele, invisíveis da pista. Aqui o núcleo decide, de forma determinística,
// trechos de um lado onde o builder não põe prédio, torre nem outdoor (só poste, moita e placa de curva): uma praça
// ou avenida aberta com o marco no fundo. O layout do cenário (render/scenery/layout.ts) põe cada marco perto/longe
// na praça dele, e quem chega vê o marco nos ~120 m antes (docs/VISUAL.md, "Marcos turísticos").
// Puro e sem trigonometria: o builder chama durante a montagem da pista (a colisão depende disto).
import { placeOf } from '../data/places';
import { hashString } from '../rng';
import type { Track } from '../types';

/**
 * Quantas praças por volta cada marco das pistas de cidade pede: o `perLap` do registro de marcos do renderizador
 * (render/scenery/landmarks/) para os de perto e de longe; 0 para os do horizonte (a 400 m+, passam por cima dos
 * prédios). O núcleo não importa o renderizador, então o número mora aqui também — e `tests/landmarks-pracas.test.ts`
 * confere que bate com o registro em toda pista de cidade (marco novo numa pista de cidade entra aqui).
 */
export const LANDMARK_PLAZAS: Readonly<Record<string, number>> = {
  // Sampa, BH, Curitiba, Camboriú, Brasília, Centro-Oeste
  masp: 2, ponte_estaiada: 2, igreja_pampulha: 2, opera_de_arame: 2, predios_camboriu: 0, roda_gigante: 2,
  torre_tv_brasilia: 2, catedral_brasilia: 2, congresso_nacional: 2, monumento_campo_grande: 2, centro_geodesico: 2,
  // Norte e Nordeste
  marco_zero_recife: 1, ponte_mauricio: 1, parque_do_povo: 1, ponte_dos_ingleses: 1, jangada: 4, casario_azulejos: 2,
  ponte_rio_negro: 1, monumento_garimpeiro: 1, palacio_araguaia: 1, ponte_palmas: 1,
  // Mundial
  placa_las_vegas: 1, piramide_luxor: 1, torre_stratosphere: 1, castelo_osaka: 1, tsutenkaku: 1, torre_eiffel: 1,
  arco_triunfo: 1, opera_sydney: 1, harbour_bridge: 1, coliseu: 1, cupula_sao_pedro: 1,
};

/** Marco de cidade fora da tabela: duas praças (o perLap mais comum); o teste acusa a diferença. */
const DEFAULT_PLAZAS = 2;
/**
 * Onde fica (segmentos depois da largada) o marco da primeira praça e o passo entre os primeiros de cada marco.
 * A primeira praça começa em PLAZA_FIRST − PLAZA_BEFORE = 28: depois das arquibancadas (1–24) e do trecho sem prédio
 * da largada (30), e o terceiro marco (Las Vegas) ainda fica a ≤ 300 segmentos (tests/landmarks.test.ts).
 */
export const PLAZA_FIRST = 76;
export const PLAZA_STEP = 64;
/**
 * Comprimento da praça antes e depois do segmento do marco: os ~120 m (30 segmentos) de quem chega mais a meia
 * pegada de um marco de perto (o MASP tem 88 m: ±11 segmentos) e a folga da busca do layout (±4); depois, a outra
 * metade. 61 segmentos ≈ 244 m — menos que PLAZA_STEP, então as praças de dois marcos seguidos não se encostam.
 */
export const PLAZA_BEFORE = 48;
export const PLAZA_AFTER = 12;
/** Até quanto (segmentos) a praça anda do lugar ideal fugindo de curva em S (a primeira, ainda a ≤ 150 da largada). */
const PLAZA_SHIFT = 64;

export interface Plaza {
  /** Id do marco (places.ts) e qual das repetições na volta (0 = a primeira, perto da largada). */
  landmark: string;
  rep: number;
  /** Lado da pista (−1 esquerda, +1 direita). */
  side: -1 | 1;
  /** Segmento onde o marco deve ficar ao lado da pista. */
  at: number;
  /** Primeiro e último segmento da praça (inclusive; `to` pode passar de n — conte com a volta). */
  from: number;
  to: number;
}

/** Lado de dentro de curva logo à frente (a mesma conta de `innerCurve` do layout): marco alto ali esconderia a pista. */
function innerCurve(track: Track, i: number, side: number): boolean {
  const segs = track.segments; const n = segs.length;
  let count = 0;
  for (let d = -25; d < 55; d++) if (segs[((i + d) % n + n) % n].curve * side > 1.2) count++;
  return count >= 18;
}

function hasPit(track: Track, from: number, to: number): boolean {
  const n = track.segments.length;
  for (let i = from; i <= to; i++) if (track.segments[((i % n) + n) % n].pit) return true;
  return false;
}

/**
 * As praças da pista: vazio fora da cidade e na cidade sem marcos. Uma por instância de cada marco de perto/longe
 * (na ordem de places.ts): a primeira em PLAZA_FIRST + j·PLAZA_STEP depois da largada, as outras a cada 1/praças da
 * volta. O lado sai de um hash do id (sem Math.random), evitando o de dentro de curva e o lado do box.
 */
export function cityPlazas(track: Track): Plaza[] {
  if (track.def.scenery !== 'city_night') return [];
  const ids = placeOf(track.def.id)?.landmarks ?? [];
  const n = track.segments.length;
  const out: Plaza[] = [];
  let slot = 0;
  for (const id of ids) {
    const count = LANDMARK_PLAZAS[id] ?? DEFAULT_PLAZAS;
    if (count <= 0) continue;
    const first = track.startIndex + PLAZA_FIRST + slot * PLAZA_STEP;
    slot++;
    for (let rep = 0; rep < count; rep++) {
      const ideal = first + Math.floor((rep * n) / count + 0.5);
      const pref: -1 | 1 = ((hashString(`${track.def.id}:${id}:${rep}`) >>> 16) & 1) === 0 ? -1 : 1; // bit alto: o baixo do FNV só conta a paridade dos caracteres
      out.push(choose(track, out, id, rep, ideal, pref));
    }
  }
  return out;
}

/** A praça já escolhida cobre o segmento `i` (com a volta)? */
function taken(plazas: readonly Plaza[], n: number, from: number, to: number): boolean {
  for (const p of plazas) {
    for (let i = from; i <= to; i++) {
      const d = ((i - p.from) % n + n) % n;
      if (d <= p.to - p.from) return true;
    }
  }
  return false;
}

/**
 * Lugar da praça: o segmento ideal e o lado do hash, ou o outro lado se o primeiro é o de dentro de curva ou o do box;
 * em curva em S (os dois lados de dentro) anda de 8 em 8 segmentos para a frente e para trás (até PLAZA_SHIFT; a
 * primeira de cada marco só para a frente, para não entrar na largada). Sem lugar, fica no ideal (o layout acha outro).
 */
function choose(track: Track, done: readonly Plaza[], landmark: string, rep: number, ideal: number, pref: -1 | 1): Plaza {
  const n = track.segments.length;
  const make = (at: number, side: -1 | 1): Plaza => {
    const from = ((at - PLAZA_BEFORE) % n + n) % n;
    return { landmark, rep, side, at: ((at % n) + n) % n, from, to: from + PLAZA_BEFORE + PLAZA_AFTER };
  };
  for (let o = 0; o <= 2 * PLAZA_SHIFT; o += 8) {
    const off = o === 0 ? 0 : (o % 16 === 8 ? 1 : -1) * Math.ceil(o / 16) * 8;
    if (rep === 0 && off < 0) continue;
    const at = ideal + off;
    if (taken(done, n, at - PLAZA_BEFORE, at + PLAZA_AFTER)) continue;
    for (const side of [pref, pref === 1 ? -1 : 1] as const) {
      if (innerCurve(track, at, side)) continue;
      if (side === 1 && hasPit(track, at - PLAZA_BEFORE, at + PLAZA_AFTER)) continue;
      return make(at, side);
    }
  }
  return make(ideal, hasPit(track, ideal - PLAZA_BEFORE, ideal + PLAZA_AFTER) ? -1 : pref);
}

/**
 * Máscara por segmento: bit 1 = praça à esquerda, bit 2 = à direita (o builder consulta em O(1) por sprite).
 */
export function plazaMask(track: Track, plazas: readonly Plaza[] = cityPlazas(track)): Uint8Array {
  const n = track.segments.length;
  const mask = new Uint8Array(n);
  for (const p of plazas) for (let i = p.from; i <= p.to; i++) mask[i % n] |= p.side < 0 ? 1 : 2;
  return mask;
}
