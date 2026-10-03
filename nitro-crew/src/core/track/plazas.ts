// Praças da cidade e mirantes do litoral: nas pistas city_night a receita do cenário (builder.ts) enche os dois lados
// de prédios e torres na beira da pista — um paredão contínuo, e os marcos turísticos (MASP, Ópera de Arame, Torre de
// TV…, postos pelo renderizador a 30–330 m) ficavam atrás dele, invisíveis da pista. No litoral a receita põe prédio e
// torre dos dois lados também (menos denso: um a cada ~17 segmentos por lado), e o marco de perto só aparecia nas
// brechas (a jangada de Maceió 0,3 s na tela, o cassino de Mônaco 0,8). Aqui o núcleo decide, de forma determinística,
// trechos de um lado onde o builder não põe prédio, torre nem outdoor (só poste, moita, palmeira e placa de curva): uma
// praça ou avenida aberta com o marco no fundo, ou um mirante na beira-mar. O layout do cenário
// (render/scenery/layout.ts) põe cada marco perto/longe no trecho dele (docs/VISUAL.md, "Marcos turísticos").
// Puro e sem trigonometria: o builder chama durante a montagem da pista (a colisão depende disto).
import { placeOf } from '../data/places';
import { hashString } from '../rng';
import type { Track } from '../types';
import { centerline, sightNeed, SIGHT_MODEL, type Centerline } from './sightline';
import { FIRST_WINDOW, START_STANDS, startZoneEnd } from './startzone';

/** O que o núcleo precisa saber de um marco para abrir a praça dele (o registro do renderizador tem o resto). */
export interface PlazaSpec {
  /** Praças por volta: o `perLap` do registro (render/scenery/landmarks/) para perto e longe; 0 no horizonte. */
  readonly count: number;
  readonly place: 'near' | 'far' | 'skyline';
  /** O lado do registro: no litoral o mar fica à direita, e o marco 'sea' vai para ele; o resto, para a terra. */
  readonly side: 'land' | 'sea' | 'any';
}

const near = (count: number, side: PlazaSpec['side'] = 'any'): PlazaSpec => ({ count, place: 'near', side });
const far = (count: number, side: PlazaSpec['side'] = 'any'): PlazaSpec => ({ count, place: 'far', side });
/** O do horizonte (a 400 m+) passa por cima dos prédios: não pede praça. */
const SKYLINE: PlazaSpec = { count: 0, place: 'skyline', side: 'any' };

/**
 * Os marcos das pistas de cidade e de litoral. O núcleo não importa o renderizador, então o dado mora aqui também — e
 * `tests/landmarks-pracas.test.ts` confere que bate com o registro (perLap, lugar e lado) em toda pista desses biomas:
 * marco novo numa pista de cidade ou de litoral entra aqui.
 */
export const LANDMARK_PLAZAS: Readonly<Record<string, PlazaSpec>> = {
  // Cidade · Sampa, BH, Curitiba, Camboriú, Brasília, Centro-Oeste
  masp: near(2), ponte_estaiada: far(2), igreja_pampulha: near(2), opera_de_arame: near(2), predios_camboriu: SKYLINE,
  roda_gigante: near(2), torre_tv_brasilia: far(2), catedral_brasilia: near(2), congresso_nacional: far(2),
  monumento_campo_grande: near(2), centro_geodesico: near(2),
  // Cidade · Norte e Nordeste
  marco_zero_recife: near(1, 'land'), ponte_mauricio: near(1), parque_do_povo: near(1, 'land'), ponte_dos_ingleses: near(1),
  jangada: near(4, 'sea'), casario_azulejos: near(2, 'land'), ponte_rio_negro: far(1), monumento_garimpeiro: near(1),
  palacio_araguaia: near(1, 'land'), ponte_palmas: near(1),
  // Cidade · Mundial
  placa_las_vegas: near(1), piramide_luxor: far(1), torre_stratosphere: far(1), castelo_osaka: far(1), tsutenkaku: far(1),
  torre_eiffel: far(1), arco_triunfo: far(1), opera_sydney: far(1), harbour_bridge: far(1), coliseu: far(1),
  cupula_sao_pedro: far(1),
  // Litoral · Sudeste e Sul
  cristo_redentor: SKYLINE, pao_de_acucar: SKYLINE, casario_paraty: near(3), escuna: near(2, 'sea'),
  farol_ilhabela: near(2, 'sea'), veleiro_canal: near(3, 'sea'), dunas_itaunas: far(2, 'land'),
  convento_penha: far(2, 'land'), terceira_ponte: far(1, 'sea'), ponte_hercilio_luz: far(2, 'sea'), igreja_acoriana: near(2),
  usina_gasometro: far(2, 'sea'), ponte_jk: far(1, 'sea'),
  // Litoral · Nordeste e Norte
  igreja_quadrado: near(1, 'land'), coqueiral: near(6), farol_da_barra: far(1, 'sea'), elevador_lacerda: far(1, 'land'),
  casario_pelourinho: near(2, 'land'), arcos_atalaia: near(2, 'sea'), ponte_aracaju: far(1, 'sea'),
  igrejas_olinda: far(1, 'land'), bonecos_olinda: near(3), morro_do_pico: SKYLINE, dois_irmaos: far(1, 'sea'),
  farol_cabo_branco: far(1, 'sea'), estacao_cabo_branco: near(1, 'land'), forte_reis_magos: near(1, 'sea'),
  ponte_newton_navarro: far(1, 'sea'), morro_do_careca: far(1, 'sea'), pedra_furada_jeri: near(1, 'sea'),
  duna_por_do_sol: far(2), ver_o_peso: near(1, 'sea'), estacao_docas: near(1, 'sea'), teatro_amazonas: near(1, 'land'),
  barco_regional: near(3, 'sea'),
  // Litoral · Mundial
  rainbow_bridge: far(1, 'sea'), torre_toquio: far(1, 'land'), cassino_monte_carlo: near(1, 'land'),
  farol_cape_point: far(1, 'sea'), table_mountain: SKYLINE, arco_great_ocean: far(1, 'sea'),
  ponte_storseisundet: far(1, 'sea'), catedral_artica: far(1, 'land'), positano: far(1, 'land'),
  cupula_azul: near(1, 'land'), moinho_santorini: near(1, 'land'),
};

/** Marco fora da tabela: duas praças de perto (o mais comum); o teste acusa a diferença. */
const DEFAULT_PLAZA = near(2);

/**
 * A frente da praça do lado do marco, em segmentos antes dele: o de perto pede o corredor de quem chega livre (os ~120 m
 * em que ele cresce no quadro, mais a folga da busca e meia fachada); o de longe, só a vizinhança. O resto da praça é o
 * que a linha de visada pede (sightline.ts): de perto a ~50 m, a visada cruza a faixa dos prédios do lado dele desde o
 * meio do caminho; de longe, perto do carro — até ~380 m antes dele, e do outro lado se a aproximação faz curva.
 */
export const PLAZA_FRONT = { near: 48, far: 24 } as const;
/** Depois do marco: a outra metade da pegada (o MASP tem 88 m: ±11 segmentos). */
export const PLAZA_AFTER = 12;
/**
 * Lateral de referência (m) do marco na conta da visada: o de perto fica com a borda a 30–80 m e o centro a ~50; o de
 * longe com a borda a 120–225 m e os pontos de amostra a ~150 (render/scenery/layout.ts, LANDMARK_LAT).
 */
export const PLAZA_LAT = { near: 50, far: 150 } as const;
/**
 * Onde, depois do fim da largada, fica o primeiro de cada marco (o ideal; a escolha anda pela janela atrás de vista):
 * longe o bastante para a câmera ver o marco o mínimo já depois das arquibancadas e do box.
 */
export const PLAZA_LEAD = { near: 64, far: 100 } as const;
/** A primeira praça começa no máximo logo depois das arquibancadas (até o segmento 24 da largada). */
export const PLAZA_FROM = START_STANDS + 4;
/** Passo mínimo entre o primeiro de um marco e o primeiro do seguinte (segmentos). */
export const PLAZA_STEP = 64;
/**
 * Até quanto (segmentos) a praça anda do lugar ideal atrás de vista, fugindo de curva, do box ou de outra praça: no
 * máximo 128, e nunca mais que 1/(4·praças) da volta — duas vizinhas, cada uma andando isso na direção da outra, ainda
 * ficam a ≥ 1/(2·praças) da volta, como o layout pede (tests/landmarks.test.ts). O primeiro de cada marco anda só
 * dentro da janela dele.
 */
const PLAZA_SHIFT = 128;
/** Quanto (segmentos) o layout pode pôr o marco longe do lugar da praça (render/scenery/layout.ts): ±4. */
export const PLAZA_SLACK = 4;

export interface Plaza {
  /** Id do marco (places.ts) e qual das repetições na volta (0 = a primeira, perto da largada). */
  landmark: string;
  rep: number;
  place: 'near' | 'far';
  /** Lado da pista (−1 esquerda, +1 direita). */
  side: -1 | 1;
  /** Segmento onde o marco deve ficar ao lado da pista. */
  at: number;
  /** Primeiro e último segmento aberto do lado do marco (inclusive; `to` pode passar de n — conte com a volta). */
  from: number;
  to: number;
  /** O trecho aberto do outro lado, quando a visada atravessa a pista numa curva (null: nenhum). */
  across: { from: number; to: number } | null;
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

/** Biomas cuja receita (builder.ts) põe prédio e torre na beira da pista: a cidade (paredão) e o litoral. */
function hasPlazas(track: Track): boolean {
  return track.def.scenery === 'city_night' || track.def.scenery === 'coast';
}

/**
 * As praças (cidade) e os mirantes (litoral) da pista: vazio nos outros biomas e onde não há marco de perto/longe. Uma
 * por instância de cada marco de perto/longe (na ordem de places.ts): a primeira de cada marco na janela do primeiro
 * (FIRST_WINDOW, do fim da largada), PLAZA_STEP depois do primeiro do marco anterior; as outras a cada 1/praças da
 * volta. Cada uma escolhe, perto do ideal, o lugar (e na cidade o lado) em que a visada mostra o marco o mínimo
 * abrindo menos prédio (sightline.ts); no litoral o lado é o do registro (o mar à direita).
 */
export function landmarkPlazas(track: Track): Plaza[] {
  if (!hasPlazas(track)) return [];
  const coast = track.def.scenery === 'coast';
  const ids = placeOf(track.def.id)?.landmarks ?? [];
  const n = track.segments.length;
  const zone = track.startIndex + startZoneEnd(track);
  const out: Plaza[] = [];
  let line: Centerline | null = null;
  let lastFirst = -Infinity;
  ids.forEach((id, j) => {
    const spec = LANDMARK_PLAZAS[id] ?? DEFAULT_PLAZA;
    if (spec.count <= 0 || spec.place === 'skyline') return;
    line ??= centerline(track);
    const place = spec.place;
    const first = Math.max(zone + PLAZA_LEAD[place], lastFirst + PLAZA_STEP);
    // O primeiro: o marco dentro da janela dele, a câmera contada depois da largada, a praça até as arquibancadas.
    const window: Window = { lo: zone + PLAZA_SLACK, hi: zone + (j === 0 ? FIRST_WINDOW.primary : FIRST_WINDOW.other) - PLAZA_SLACK, floor: track.startIndex + PLAZA_FROM, cam: zone };
    for (let rep = 0; rep < spec.count; rep++) {
      const ideal = first + Math.floor((rep * n) / spec.count + 0.5);
      const hashed: -1 | 1 = ((hashString(`${track.def.id}:${id}:${rep}`) >>> 16) & 1) === 0 ? -1 : 1; // bit alto: o baixo do FNV só conta a paridade dos caracteres
      const fixed: -1 | 1 | null = coast ? (spec.side === 'sea' ? 1 : -1) : null;
      const sides: ReadonlyArray<-1 | 1> = fixed !== null ? [fixed] : [hashed, hashed === 1 ? -1 : 1];
      const shift = Math.min(PLAZA_SHIFT, Math.floor(n / (4 * spec.count)));
      const got = choose(track, line, out, id, rep, place, ideal, sides, rep === 0 ? window : null, shift);
      if (got) out.push(got.plaza);
      if (rep === 0) lastFirst = got ? got.at : ideal;
    }
  });
  return out;
}

/**
 * Faixa em que o primeiro de cada marco pode ficar, onde a praça dele pode começar e de onde a câmera conta
 * (segmentos desde o 0, sem a volta).
 */
interface Window { lo: number; hi: number; floor: number; cam: number }

/** A frente de outra praça, do mesmo lado, cobre algum segmento de [from, to] (com a volta)? */
function taken(plazas: readonly Plaza[], n: number, side: number, from: number, to: number): boolean {
  for (const p of plazas) {
    if (p.side !== side) continue;
    const pFrom = p.at - PLAZA_FRONT[p.place]; const pLen = PLAZA_FRONT[p.place] + PLAZA_AFTER;
    for (let i = from; i <= to; i++) if (((i - pFrom) % n + n) % n <= pLen) return true;
  }
  return false;
}

/**
 * Lugar da praça: anda de 8 em 8 segmentos em volta do ideal (até `shift`; o primeiro de cada marco só dentro da
 * janela) e, na cidade, pelos dois lados. Fora: marco no box, do lado de dentro de curva (marco alto esconderia a pista)
 * ou na frente de outra praça do mesmo lado. Dos que sobram, mede a visada (sightline.ts) e fica com o que mostra o
 * marco o mínimo abrindo menos prédio (somados os dois lados, mais meio segmento por segmento longe do ideal); se
 * nenhum mostra o mínimo, o que mostra mais. Devolve também o segmento sem a volta (`at`, para o passo do próximo);
 * null se não há lugar válido.
 */
function choose(track: Track, line: Centerline, done: readonly Plaza[], landmark: string, rep: number, place: 'near' | 'far', ideal: number, sides: ReadonlyArray<-1 | 1>, win: Window | null, shift: number): { plaza: Plaza; at: number } | null {
  const n = track.segments.length;
  const depth = SIGHT_MODEL.depthM[track.def.timeOfDay];
  let best: { plaza: Plaza; at: number; seen: number; cost: number } | null = null;
  for (let o = 0; o <= 2 * PLAZA_SHIFT; o += 8) {
    const off = o === 0 ? 0 : (o % 16 === 8 ? 1 : -1) * Math.ceil(o / 16) * 8;
    const at = ideal + off;
    if (win ? at < win.lo || at > win.hi : Math.abs(off) > shift) continue;
    if (hasPit(track, at - PLAZA_SLACK, at + PLAZA_AFTER)) continue;
    for (const side of sides) {
      if (taken(done, n, side, at - PLAZA_FRONT[place], at + PLAZA_AFTER)) continue;
      if (innerCurve(track, at, side)) continue;
      const wrap = ((at % n) + n) % n;
      const k = wrap + n;
      const need = sightNeed(line, k, side * PLAZA_LAT[place], depth, win ? win.cam - at + k : -Infinity);
      const own = need.cross[side < 0 ? 0 : 1]; const other = need.cross[side < 0 ? 1 : 0];
      // Do lado do marco: da visada (ou da frente) até depois dele; do outro, só o que a visada cruza.
      let from = Math.min(-PLAZA_FRONT[place], own[0] - k) + at;
      if (win) from = Math.max(from, win.floor);
      const to = at + PLAZA_AFTER;
      const across = other[0] <= other[1] && other[0] - k + at <= to ? { from: other[0] - k + at, to: Math.min(other[1] - k + at, to) } : null;
      const cost = (to - from + 1) + (across ? across.to - across.from + 1 : 0) + Math.abs(off) / 2;
      const seen = Math.min(need.seenM, SIGHT_MODEL.needM);
      if (!best || seen > best.seen || (seen === best.seen && cost < best.cost)) {
        const f = ((from % n) + n) % n;
        const a = across ? { from: ((across.from % n) + n) % n, to: ((across.from % n) + n) % n + (across.to - across.from) } : null;
        best = { plaza: { landmark, rep, place, side, at: wrap, from: f, to: f + (to - from), across: a }, at, seen, cost };
      }
    }
  }
  // Sem lugar (marco no box ou do lado de dentro de curva em toda a faixa): sem praça — o layout acha outro lugar.
  return best;
}

/**
 * Máscara por segmento: bit 1 = praça à esquerda, bit 2 = à direita (o builder consulta em O(1) por sprite).
 */
export function plazaMask(track: Track, plazas: readonly Plaza[] = landmarkPlazas(track)): Uint8Array {
  const n = track.segments.length;
  const mask = new Uint8Array(n);
  for (const p of plazas) {
    for (let i = p.from; i <= p.to; i++) mask[i % n] |= p.side < 0 ? 1 : 2;
    if (p.across) for (let i = p.across.from; i <= p.across.to; i++) mask[i % n] |= p.side < 0 ? 2 : 1;
  }
  return mask;
}
