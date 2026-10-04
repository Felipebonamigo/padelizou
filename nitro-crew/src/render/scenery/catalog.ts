// Catálogo do cenário: que modelo cada sprite vira em cada bioma/país, as receitas de decoração de
// beira (o que nasce além do alcance do carro) e o registro que monta cada modelo pelo id (com cache).
// Puro (sem DOM): o layout e os testes usam; scenery.ts só instancia.
import type { SceneryId, SpriteKind, TrackDef } from '../../core/types';
import { hash2 } from '../noise';
import { bandPoints, type Model } from './geom';
import { LANDMARKS } from './landmarks';
import { onLandmarkPartsChange } from './landmarks/parts';
import type { LandmarkDef } from './landmarks/types';
import * as P from './props';
import { smoothModel } from './smooth';
import * as S from './structures';
import * as V from './vegetation';

export type Region = 'br' | 'us' | 'jp' | 'eu' | 'za' | 'au' | 'no' | 'med';

export function regionOf(def: TrackDef): Region {
  switch (def.country) {
    case 'Brasil': return 'br';
    case 'Estados Unidos': return 'us';
    case 'Japão': return 'jp';
    case 'Europa': return 'eu';
    case 'África do Sul': return 'za';
    case 'Austrália': return 'au';
    case 'Escandinávia': return 'no';
    case 'Mediterrâneo': return 'med';
    default: return 'br';
  }
}

// ───────────────────────────── Registro de modelos ─────────────────────────────

const G = { trop: ['#1f7a3a', '#2c8f40', '#3aa345'], mango: ['#2a8a3a', '#3a9a40', '#2f7f38'], oak: ['#3a7a32', '#4a8a38', '#356f30'], dark: ['#23643a', '#2d7442', '#1f5a34'] };
const SK = { lush: ['#3a8a36', '#4a9a3c'], dry: ['#8a8a3c', '#9a9245'], alp: ['#4f7a3a', '#5a8a40'], coast: ['#4a9a44', '#d6368f'] };

/** Id → construtor. Id: "<família>:<parâmetros>" — um modelo por id, montado uma vez. */
const BUILDERS: Record<string, () => Model> = {
  // Árvores (R de `tree`).
  'tree:giant:0': () => V.tropicalGiant(1), 'tree:giant:1': () => V.tropicalGiant(2, G.dark),
  'tree:mango:0': () => V.roundTree(3, G.mango, SK.lush), 'tree:mango:1': () => V.roundTree(4, ['#3a9a3a', '#4aa842', '#2f8a36'], SK.lush, '#5a3e28', 1.1),
  'tree:banana': () => V.banana(5), 'tree:fern': () => V.treeFern(6),
  'tree:acacia:0': () => V.acacia(7), 'tree:acacia:1': () => V.acacia(8), 'tree:baobab': () => V.baobab(9),
  'tree:marula': () => V.roundTree(10, ['#6a8a36', '#7a9a3c', '#5f8030'], SK.dry, '#5a4430', 0.9),
  'tree:oak': () => V.roundTree(11, G.oak, SK.lush, '#5a4430', 1.05), 'tree:linden': () => V.roundTree(12, ['#4a9a3a', '#5aa842', '#3f8a36'], SK.lush),
  'tree:poplar': () => V.poplar(13),
  'tree:ipe-pink': () => V.roundTree(14, ['#e85aa8', '#f07ab8', '#d8489a'], SK.lush, '#5a3e28'),
  'tree:ipe-yellow': () => V.roundTree(15, ['#f5c518', '#ffd84a', '#e8b010'], SK.lush, '#5a3e28'),
  'tree:aspen-gold': () => V.aspen(16, ['#e8b52a', '#f2c94c', '#d9a020']), 'tree:aspen-green': () => V.aspen(17, ['#6aa84f', '#7ab85a', '#5a9a45']),
  'tree:maple-red': () => V.roundTree(18, ['#d4442a', '#e05a32', '#c0382a'], SK.alp, '#4a3428', 0.95),
  'tree:maple-orange': () => V.roundTree(19, ['#e8822a', '#f09a3a', '#d8702a'], SK.alp, '#4a3428', 0.95),
  'tree:beech-autumn': () => V.roundTree(20, ['#c8962a', '#d9a83a', '#b8862a'], SK.alp, '#5a4a3a'),
  'tree:eucalyptus': () => V.eucalyptus(21), 'tree:olive': () => V.olive(22), 'tree:cypress': () => V.cypress(23),
  'tree:street': () => V.roundTree(24, ['#3a8a3a', '#4a9a40', '#357f36'], ['#8a8a86', '#9a9a94'], '#5a4430', 0.85),
  // Coníferas (R de `pine`).
  'pine:spruce': () => V.conifer(31, '#2a6b42', 5, 1), 'pine:fir': () => V.conifer(32, '#347a66', 6, 0.95),
  'pine:snow': () => V.conifer(33, '#2c6e46', 5, 1, true), 'pine:slim': () => V.conifer(34, '#33784c', 6, 1.25),
  'pine:cedar': () => V.conifer(35, '#285f40', 7, 1.2, false, 1.4), 'pine:larch': () => V.conifer(36, '#d9a83a', 5, 1.1),
  // Palmeiras (R de `palm`) e o que as substitui fora dos trópicos (mesma pegada).
  'palm:coco-lean': () => V.coconutPalm(41, 1.6), 'palm:coco': () => V.coconutPalm(42, 0.6), 'palm:royal': () => V.royalPalm(43),
  'palm:date': () => V.datePalm(44), 'palm:fan': () => V.fanPalm(45),
  'palm:spruce': () => V.conifer(46, '#1f5a3a', 5, 0.9, false, 0.5, V.collisionHalfM('palm')),
  'palm:snowspruce': () => V.conifer(47, '#23603c', 5, 0.9, true, 0.5, V.collisionHalfM('palm')),
  'palm:birch': () => V.aspen(48, ['#6aa84f', '#e8c84a', '#7ab85a'], V.collisionHalfM('palm')),
  'palm:blackpine': () => V.roundTree(49, G.dark, SK.lush, '#3a2a22', 0.9, V.collisionHalfM('palm')),
  'palm:eucalyptus': () => V.eucalyptus(50, V.collisionHalfM('palm')),
  // Deserto (R de `cactus`).
  'cactus:saguaro2': () => V.saguaro(51, 2), 'cactus:saguaro3': () => V.saguaro(52, 3), 'cactus:barrel': () => V.barrelCluster(53),
  'cactus:pear': () => V.pricklyPear(54), 'cactus:termite': () => V.termiteMound(55), 'cactus:spinifex': () => V.spinifex(56),
  'cactus:euphorbia': () => V.euphorbia(57), 'cactus:aloe': () => V.rosette(58, '#6a9a6a', '#e8502a', 2.2),
  'cactus:agave': () => V.rosette(59, '#7a9a8a', '#f2c33a', 4.2),
  // Arbustos (não sólidos).
  'bush:fern': () => V.fernClump(61), 'bush:heliconia': () => V.shrub(62, ['#2f8a38', '#3f9a3c'], '#e8402a'),
  'bush:dry': () => V.shrub(63, ['#8a8a3c', '#9a9245', '#7a7a38'], null), 'bush:dry-yellow': () => V.shrub(64, ['#7f8a3a', '#8f9a42'], '#f2c33a'),
  'bush:juniper': () => V.shrub(65, ['#2f5a3a', '#3a6a42'], null, 1.1, 0.8), 'bush:heather': () => V.shrub(66, ['#5a7a3a', '#6a8a42'], '#b86ac8', 1.1, 0.7),
  'bush:bougainvillea': () => V.shrub(67, ['#d6368f', '#3f8a3a', '#e84a9a'], '#ff7ab6'), 'bush:agapanthus': () => V.shrub(68, ['#3f8a3a', '#4a9a40'], '#6a8aff'),
  'bush:sage': () => V.shrub(69, ['#9aa67a', '#8a9670', '#a8b488'], null, 1.0, 0.75), 'bush:hedge': () => V.hedge(70),
  'bush:green': () => V.shrub(71, ['#3a8a36', '#4a9a3c', '#2f7a34'], null),
  // Pedras (R de `boulder`).
  'rock:sandstone': () => V.rocks(81, 'sandstone'), 'rock:granite': () => V.rocks(82, 'granite'), 'rock:snowy': () => V.rocks(83, 'snowy'),
  'rock:mossy': () => V.rocks(84, 'mossy'), 'rock:kopje': () => V.rocks(85, 'kopje'), 'rock:basalt': () => V.rocks(86, 'basalt'), 'rock:lava': () => V.rocks(87, 'lava'),
  'rock:sandstone2': () => V.rocks(88, 'sandstone'), 'rock:granite2': () => V.rocks(89, 'granite'),
  // Objetos de pista.
  'lamp': () => P.streetLamp(), 'sign:left': () => P.chevronSign(-1), 'sign:right': () => P.chevronSign(1),
  'stand:0': () => P.grandstand(0), 'stand:1': () => P.grandstand(1), 'gantry': () => P.startGantry(),
  'pitsign': () => P.pitSign(), 'cone': () => P.trafficCone(),
  ...Object.fromEntries([0, 1, 2, 3, 4, 5, 6, 7].map((v) => [`billboard:${v}`, () => P.billboard(v)])),
  ...Object.fromEntries([0, 1, 2, 3, 4, 5].map((v) => [`garage:${v}`, () => P.pitGarages(v)])),
  // Forração e silhuetas de longe.
  ...Object.fromEntries([0, 1, 2].map((k) => [`tuft:green:${k}`, () => V.grassTuft(100 + k, ['#3f8f36', '#4f9a3a', '#2f7f34'][k])])),
  ...Object.fromEntries([0, 1, 2].map((k) => [`tuft:dry:${k}`, () => V.grassTuft(110 + k, ['#c9b25a', '#b5a050', '#d8c070'][k], 0.8, 9)])),
  ...Object.fromEntries([0, 1].map((k) => [`tuft:sage:${k}`, () => V.grassTuft(120 + k, ['#9aa67a', '#8a9a68'][k], 0.45)])),
  'flowers:meadow': () => V.flowerPatch(130, '#4f9a3a', ['#ffffff', '#ffd23f', '#e8502a']),
  'flowers:alpine': () => V.flowerPatch(131, '#4f8a3a', ['#b86ac8', '#ffffff', '#6a8aff']),
  'flowers:tropical': () => V.flowerPatch(132, '#2f8a38', ['#e8402a', '#ffb000', '#ff5fb0']),
  'flowers:desert': () => V.flowerPatch(133, '#8a9a58', ['#f2c33a', '#e8702a']),
  'pebbles:grey': () => V.pebbles(140, '#9a9690'), 'pebbles:red': () => V.pebbles(141, '#b8643a'), 'pebbles:dark': () => V.pebbles(142, '#4a4648'),
  'far:round-green': () => V.farRound('#3a8a3a'), 'far:round-dark': () => V.farRound('#23643a', 7, 2.8), 'far:round-dry': () => V.farRound('#7a8a3a', 5, 2.4),
  'far:round-pink': () => V.farRound('#e85aa8'), 'far:round-gold': () => V.farRound('#e0b030', 6, 1.8), 'far:round-red': () => V.farRound('#d4502a', 5.5, 2.2),
  'far:round-olive': () => V.farRound('#8ea56a', 4, 1.8), 'far:flat-acacia': () => V.farRound('#6f8f35', 5.5, 2.6),
  'far:cone-dark': () => V.farCone('#1f5a3a'), 'far:cone-blue': () => V.farCone('#2f6b5e', 10, 1.8), 'far:cone-cypress': () => V.farCone('#2f5a36', 9, 1.1),
  'far:cone-gold': () => V.farCone('#d9a83a', 9, 1.6), 'far:cone-snow': () => V.farCone('#dfe8f0', 9, 1.7), 'far:poplar': () => V.farCone('#3f8a3a', 11, 1.3),
  'far:palm': () => V.farPalm('#3f9a3a'), 'far:palm-dry': () => V.farPalm('#6a9a3a', 10),
  'far:rock-red': () => V.farRock('#c8683a', 1), 'far:rock-grey': () => V.farRock('#8d8f94', 2), 'far:rock-ochre': () => V.farRock('#a39480', 3),
  'far:rock-dark': () => V.farRock('#4a4648', 4, 5, 3),
  'far:boulder-red': () => V.farRock('#c8683a', 5, 2.3, 1.7), 'far:boulder-grey': () => V.farRock('#8d8f94', 6, 2.3, 1.7),
  'far:boulder-ochre': () => V.farRock('#a39480', 7, 2.3, 1.7), 'far:boulder-dark': () => V.farRock('#4a4648', 8, 2.3, 1.7),
  'far:saguaro': () => V.farSaguaro(),
  // Decoração de beira e pontos de referência.
  ...Object.fromEntries((['ranch', 'rail', 'armco', 'stone', 'railing', 'hedge'] as S.FenceStyle[]).map((s) => [`fence:${s}`, () => S.fence(s, 7)])),
  'pole': () => S.utilityPole(), 'wires': () => S.wires(),
  'turbine': () => S.windTurbine(), 'rotor': () => S.turbineRotor(), 'torii': () => S.torii(), 'pagoda': () => S.pagoda(),
  'chapel:white': () => S.chapel(1), 'chapel:red': () => S.chapel(2, '#b0352b', '#3a3a3a'),
  'sailboat:0': () => S.sailboat(1), 'sailboat:1': () => S.sailboat(2), 'yacht': () => S.yacht(1),
  'fishboat:0': () => S.fishingBoat(0), 'fishboat:1': () => S.fishingBoat(1), 'fishboat:2': () => S.fishingBoat(2),
  'umbrella:0': () => S.beachUmbrella(0), 'umbrella:1': () => S.beachUmbrella(1), 'umbrella:2': () => S.beachUmbrella(2),
  'windmill': () => S.stoneWindmill(), 'pier': () => S.pier(),
  'mesa:0': () => V.mesa(1, ['#c8683a', '#e08a50', '#b35a32', '#d97a45'], 40, 55), 'mesa:1': () => V.mesa(2, ['#b85a32', '#d97a45', '#c8683a'], 26, 70),
  'mesa:karoo': () => V.mesa(3, ['#9a7a5a', '#b08a62', '#8a6a4a'], 60, 30), 'mesa:etna': () => V.mesa(4, ['#4a4446', '#5a5254', '#3a3638'], 70, 40),
  'stack:0': () => V.seaStack(1, 22), 'stack:1': () => V.seaStack(2, 30),
  'searock:dark': () => S.seaRock(5, '#5b5a5e', 4), 'searock:sand': () => S.seaRock(6, '#c9a070', 5),
  'termite': () => V.termiteMound(90), 'house:red': () => S.gableHouse(1, '#b0352b', '#3a3a3a'),
  'house:yellow': () => S.gableHouse(2, '#e8c14a', '#3a3a3a'), 'house:white': () => S.gableHouse(3, '#f2f2ea', '#3a4a5a'),
  'house:farm': () => S.farmhouse(4), 'house:farm-pink': () => S.farmhouse(5, '#b8552e', '#f2c8c0'),
  'house:chalet': () => S.chalet(6), 'house:japan': () => S.japaneseHouse(7), 'house:rondavel': () => S.rondavels(8),
  'house:cubes-white': () => S.cubeHouses(9, ['#ffffff', '#f6f4ee'], true), 'house:cubes-pastel': () => S.cubeHouses(10, ['#f2c46b', '#f08a7a', '#f5e0b0', '#e8a0a0'], false),
  'beam': () => S.lighthouseBeam(), 'fan': () => S.windpumpFan(),
};

const cache = new Map<string, Model>();

/** Prefixo dos marcos turísticos no registro de modelos: "lm:<id de places.ts>" (landmarks/index.ts). */
export const LANDMARK_PREFIX = 'lm:';

/** Definição do marco de um id de modelo "lm:<id>" (undefined se não for marco ou não tiver modelo). */
export function landmarkOf(modelId: string): LandmarkDef | undefined {
  if (!modelId.startsWith(LANDMARK_PREFIX)) return undefined;
  const key = modelId.slice(LANDMARK_PREFIX.length);
  return Object.prototype.hasOwnProperty.call(LANDMARKS, key) ? LANDMARKS[key] : undefined;
}

/** Modelo pelo id (montado na primeira vez; o chamador não libera: o cache vive o jogo inteiro). */
export function getModel(id: string): Model {
  let m = cache.get(id);
  if (m) return m;
  const lm = landmarkOf(id);
  const file = lm ? landmarkFiles.get(id.slice(LANDMARK_PREFIX.length)) : undefined;
  if (file) { m = smoothModel(id, file); cache.set(id, m); return m; } // o arquivo convertido também (bicho, estátua)
  const b = BUILDERS[id] ?? dynamicBuilder(id) ?? (lm ? lm.build : undefined);
  if (!b) throw new Error(`modelo desconhecido: ${id}`);
  m = smoothModel(id, b()); // normal suave com o vinco da família (smooth.ts), uma vez por modelo
  cache.set(id, m);
  return m;
}

/** Faixa de altura (m) em que o carro encosta num objeto: do para-choque ao teto. */
export const CAR_BAND: [number, number] = [0.05, 1.3];

const fronts = new Map<string, number>();

/**
 * O ponto mais à frente (+X, o lado que olha a pista) das partes sólidas do modelo na altura do carro
 * (escala 1). É o que o layout encosta na borda de colisão dos prédios, arquibancadas, postes e box.
 */
export function modelFrontX(id: string): number {
  let v = fronts.get(id);
  if (v !== undefined) return v;
  let front = -Infinity;
  for (const part of getModel(id).parts) {
    if (part.mat === 'glow' || part.mat === 'beacon' || part.mat === 'cone') continue;
    bandPoints(part.geometry, CAR_BAND[0], CAR_BAND[1], (x) => { if (x > front) front = x; });
  }
  v = Number.isFinite(front) ? front : 0;
  fronts.set(id, v);
  return v;
}

const bandRadii = new Map<string, number>();

/** Raio horizontal (m, escala 1) do que o modelo ocupa perto do chão (faixa do carro com folga para escalas baixas). */
export function modelBandRadius(id: string): number {
  let v = bandRadii.get(id);
  if (v !== undefined) return v;
  let r = 0;
  for (const part of getModel(id).parts) {
    if (part.mat === 'glow' || part.mat === 'beacon' || part.mat === 'cone') continue;
    bandPoints(part.geometry, CAR_BAND[0], CAR_BAND[1] / 0.6, (x, _y, z) => { r = Math.max(r, Math.hypot(x, z)); });
  }
  bandRadii.set(id, r);
  return r;
}

export interface ModelBounds { minX: number; maxX: number; minY: number; maxY: number; minZ: number; maxZ: number }
const bounds = new Map<string, ModelBounds>();

/** Caixa do modelo inteiro (m, escala 1), todas as partes. */
export function modelBounds(id: string): ModelBounds {
  let v = bounds.get(id);
  if (v) return v;
  v = { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity, minZ: Infinity, maxZ: -Infinity };
  for (const part of getModel(id).parts) {
    const pos = part.geometry.attributes.position;
    for (let i = 0; i < pos.count; i++) {
      const x = pos.getX(i); const y = pos.getY(i); const z = pos.getZ(i);
      if (x < v.minX) v.minX = x; if (x > v.maxX) v.maxX = x;
      if (y < v.minY) v.minY = y; if (y > v.maxY) v.maxY = y;
      if (z < v.minZ) v.minZ = z; if (z > v.maxZ) v.maxZ = z;
    }
  }
  bounds.set(id, v);
  return v;
}

const heights = new Map<string, number>();

/** Altura do modelo (m, escala 1). */
export function modelHeight(id: string): number {
  let v = heights.get(id);
  if (v !== undefined) return v;
  v = 0;
  for (const part of getModel(id).parts) {
    const pos = part.geometry.attributes.position;
    for (let i = 0; i < pos.count; i++) v = Math.max(v, pos.getY(i));
  }
  heights.set(id, v);
  return v;
}

/** Marcos que vieram de um .glb (landmarks/assets.ts, antes de o renderizador nascer): id do marco → modelo. */
const landmarkFiles = new Map<string, Model>();

/** Troca (ou, com null, devolve ao procedural) o modelo de um marco; os caches esquecem o que mediram dele. */
export function setLandmarkOverride(id: string, model: Model | null): void {
  if (model) landmarkFiles.set(id, model); else landmarkFiles.delete(id);
  const key = LANDMARK_PREFIX + id;
  for (const c of [cache, fronts, bandRadii, bounds, heights]) c.delete(key);
}

// Peça baixada (landmarks/parts.ts: o tuiuiú, a girafa…) registrada ou tirada depois de um marco montado: os marcos
// e as medidas deles são remontados no próximo uso (a peça entra pelo construtor, não pelo arquivo do marco inteiro).
onLandmarkPartsChange(() => {
  for (const c of [cache, fronts, bandRadii, bounds, heights]) for (const k of [...c.keys()]) if (k.startsWith(LANDMARK_PREFIX)) c.delete(k);
});

/** De onde vem o modelo do marco: do arquivo da arte ou do código. */
export function landmarkSource(id: string): 'glb' | 'procedural' {
  return landmarkFiles.has(id) ? 'glb' : 'procedural';
}

/** Prédios e torres têm parâmetros (andares, raio) no próprio id: "bld:<tipo>:<andares>:<semente>". */
function dynamicBuilder(id: string): (() => Model) | undefined {
  const [fam, type, a, b] = id.split(':');
  const n = Number(a); const seed = Number(b ?? 0);
  if (fam === 'bld') {
    switch (type) {
      case 'glass': return () => S.glassTower(seed, n, ['#a8c8e8', '#8fb0d0', '#c8d8e8', '#b8c8d0'][seed % 4]);
      case 'glassgold': return () => S.glassTower(seed, n, '#ffe6a8');
      case 'apt': return () => S.apartmentBlock(seed, n, ['#f2efe8', '#e8d8c0', '#d8e0e8', '#f0d8c8'][seed % 4]);
      case 'aptbeach': return () => S.apartmentBlock(seed, n, ['#ffffff', '#f4ecd8', '#e8f0f4', '#f8e0d0'][seed % 4], 20, 14);
      case 'classic': return () => S.classicBuilding(seed, n, ['#f2e8d4', '#ecdcc0', '#f4ead8', '#e8d4b8'][seed % 4]);
      case 'classicwarm': return () => S.classicBuilding(seed, n, ['#e8b888', '#f0c8a0', '#e0a878', '#f2d0a8'][seed % 4]);
      case 'brick': return () => S.brickMidrise(seed, n, ['#b8705a', '#a86a58', '#c8886a', '#9a6050'][seed % 4], ['#ff3fd0', '#3fe8ff', '#ffd23f', '#ff5a3a'][seed % 4]);
      case 'warehouse': return () => S.warehouse(seed, ['#c8ccd2', '#d8c8a8', '#a8b8c8'][seed % 3]);
      default: return undefined;
    }
  }
  if (fam === 'tower') {
    const R = n;
    switch (type) {
      case 'tv': return () => S.tvTower(seed, R);
      case 'mast': return () => S.latticeMast(seed, R);
      case 'spire': return () => S.officeSpire(seed, R, ['#a8c8e8', '#8fb0d0', '#c8d8e8', '#ffe6a8'][seed % 4]);
      case 'dome': return () => S.domeBuilding(seed, R);
      case 'lighthouse': return () => S.lighthouse(seed, R, seed % 2 ? '#1a1a1a' : '#d63a3a');
      case 'water': return () => S.waterTower(seed, R);
      case 'windpump': return () => S.windpumpTower(seed, R);
      default: return undefined;
    }
  }
  return undefined;
}

/**
 * Silhueta de longe de um modelo de vegetação/pedra (LOD: a partir de ~90 segmentos, uns 360 m, o detalhe
 * vira poucos pixels). null = sem silhueta (o objeto é pequeno: some antes).
 */
export function farModelFor(id: string): string | null {
  if (id.startsWith('pine:')) return id === 'pine:snow' ? 'far:cone-snow' : id === 'pine:larch' ? 'far:cone-gold' : id === 'pine:fir' ? 'far:cone-blue' : 'far:cone-dark';
  if (id.startsWith('palm:')) {
    if (id.includes('spruce')) return id.includes('snow') ? 'far:cone-snow' : 'far:cone-dark';
    return ({ 'palm:birch': 'far:round-green', 'palm:blackpine': 'far:round-dark', 'palm:eucalyptus': 'far:round-olive' } as Record<string, string>)[id] ?? 'far:palm';
  }
  if (id.startsWith('tree:')) {
    const map: Record<string, string> = {
      'tree:ipe-pink': 'far:round-pink', 'tree:ipe-yellow': 'far:round-gold', 'tree:aspen-gold': 'far:round-gold', 'tree:beech-autumn': 'far:round-gold',
      'tree:maple-red': 'far:round-red', 'tree:maple-orange': 'far:round-red', 'tree:acacia:0': 'far:flat-acacia', 'tree:acacia:1': 'far:flat-acacia',
      'tree:baobab': 'far:round-dry', 'tree:marula': 'far:round-dry', 'tree:olive': 'far:round-olive', 'tree:eucalyptus': 'far:round-olive',
      'tree:cypress': 'far:cone-cypress', 'tree:poplar': 'far:poplar', 'tree:giant:0': 'far:round-dark', 'tree:giant:1': 'far:round-dark',
    };
    return map[id] ?? 'far:round-green';
  }
  if (id.startsWith('rock:')) return id.startsWith('rock:sandstone') ? 'far:boulder-red' : id === 'rock:lava' || id === 'rock:basalt' ? 'far:boulder-dark' : id === 'rock:kopje' ? 'far:boulder-ochre' : 'far:boulder-grey';
  if (id.startsWith('cactus:saguaro')) return 'far:saguaro';
  return null;
}

// ───────────────────────────── Sprites → modelos ─────────────────────────────

/** Como o sprite é posto: redondo (centro, giro), olhando a pista (+X), prédio (fachada na borda) ou painel (+Z). */
export type SpriteMode = 'round' | 'faces' | 'building' | 'panel';

export interface SpriteVisual {
  /** Modelos (com deslocamento local e giro próprio, para cata-vento/farol). */
  models: Array<{ id: string; dx?: number; dy?: number; dz?: number; spin?: number; spinY?: number }>;
  mode: SpriteMode;
  /** Giro aleatório máximo (rad) em volta do eixo vertical; π = qualquer. */
  yawJitter: number;
  /** Variação de altura (fração): a largura segue a colisão, a altura varia. */
  heightJitter: number;
  /** Escala uniforme extra (prédios: o id já traz o tamanho). */
  uniform?: boolean;
}

function pickV<T>(list: readonly T[], variant: number): T {
  return list[((variant % list.length) + list.length) % list.length];
}

const TREES: Record<SceneryId, Partial<Record<Region, string[]>> & { _: string[] }> = {
  tropical: { _: ['tree:giant:0', 'tree:mango:0', 'tree:banana', 'tree:fern'], jp: ['tree:giant:1', 'tree:mango:1', 'tree:fern', 'tree:banana'], au: ['tree:giant:0', 'tree:fern', 'tree:giant:1', 'tree:banana'] },
  savanna: { _: ['tree:acacia:0', 'tree:baobab', 'tree:acacia:1', 'tree:marula'], eu: ['tree:oak', 'tree:poplar', 'tree:linden', 'tree:poplar'], br: ['tree:ipe-pink', 'tree:mango:0', 'tree:ipe-yellow', 'tree:marula'] },
  alpine: { _: ['tree:aspen-gold', 'tree:aspen-green', 'tree:oak', 'tree:aspen-gold'], jp: ['tree:maple-red', 'tree:oak', 'tree:maple-orange', 'tree:maple-red'], eu: ['tree:beech-autumn', 'tree:aspen-green', 'tree:oak', 'tree:beech-autumn'], no: ['tree:aspen-green', 'tree:aspen-gold', 'tree:aspen-green', 'tree:beech-autumn'] },
  coast: { _: ['tree:mango:0', 'tree:linden', 'tree:mango:1', 'tree:oak'], med: ['tree:olive', 'tree:cypress', 'tree:olive', 'tree:cypress'], au: ['tree:eucalyptus', 'tree:mango:0', 'tree:eucalyptus', 'tree:oak'] },
  desert: { _: ['tree:marula', 'tree:acacia:0', 'tree:marula', 'tree:acacia:1'], au: ['tree:eucalyptus', 'tree:eucalyptus', 'tree:acacia:0', 'tree:eucalyptus'], med: ['tree:olive', 'tree:olive', 'tree:cypress', 'tree:olive'] },
  city_night: { _: ['tree:street', 'tree:street', 'tree:linden', 'tree:street'] },
};

const PINES: Partial<Record<Region, string[]>> & { _: string[] } = {
  _: ['pine:spruce', 'pine:fir', 'pine:snow', 'pine:slim'], jp: ['pine:cedar', 'pine:spruce', 'pine:cedar', 'pine:snow'], no: ['pine:snow', 'pine:spruce', 'pine:larch', 'pine:snow'],
  za: ['pine:spruce', 'pine:fir', 'pine:slim', 'pine:fir'], eu: ['pine:spruce', 'pine:fir', 'pine:snow', 'pine:larch'],
};

const PALMS: Partial<Record<Region, string[]>> & { _: string[] } = {
  _: ['palm:coco-lean', 'palm:coco', 'palm:royal', 'palm:date'], med: ['palm:fan', 'palm:date', 'palm:fan', 'palm:date'], eu: ['palm:fan', 'palm:date', 'palm:fan', 'palm:coco'],
  no: ['palm:spruce', 'palm:birch', 'palm:snowspruce', 'palm:birch'], jp: ['palm:blackpine', 'palm:date', 'palm:blackpine', 'palm:coco'],
  au: ['palm:eucalyptus', 'palm:coco', 'palm:eucalyptus', 'palm:fan'], za: ['palm:date', 'palm:fan', 'palm:coco', 'palm:date'], us: ['palm:fan', 'palm:fan', 'palm:date', 'palm:fan'],
};

const CACTI: Partial<Record<Region, string[]>> & { _: string[] } = {
  _: ['cactus:saguaro2', 'cactus:saguaro3', 'cactus:barrel', 'cactus:pear'], au: ['cactus:termite', 'cactus:spinifex', 'cactus:termite', 'cactus:spinifex'],
  za: ['cactus:euphorbia', 'cactus:aloe', 'cactus:euphorbia', 'cactus:aloe'], med: ['cactus:pear', 'cactus:agave', 'cactus:pear', 'cactus:agave'],
};

const BUSHES: Record<SceneryId, string[]> = {
  tropical: ['bush:fern', 'bush:heliconia', 'bush:fern', 'bush:green'], savanna: ['bush:dry', 'bush:dry-yellow', 'bush:dry', 'bush:green'],
  alpine: ['bush:juniper', 'bush:heather', 'bush:juniper', 'bush:green'], coast: ['bush:bougainvillea', 'bush:green', 'bush:agapanthus', 'bush:green'],
  desert: ['bush:sage', 'bush:dry', 'bush:sage', 'bush:dry-yellow'], city_night: ['bush:hedge', 'bush:hedge', 'bush:green', 'bush:hedge'],
};

function rockIds(biome: SceneryId, region: Region): string[] {
  switch (biome) {
    case 'desert': return region === 'med' ? ['rock:lava', 'rock:lava', 'rock:basalt', 'rock:lava'] : region === 'za' ? ['rock:kopje', 'rock:sandstone', 'rock:kopje', 'rock:sandstone2'] : ['rock:sandstone', 'rock:sandstone2', 'rock:sandstone', 'rock:kopje'];
    case 'alpine': return region === 'za' ? ['rock:granite', 'rock:mossy', 'rock:granite2', 'rock:kopje'] : ['rock:granite', 'rock:snowy', 'rock:granite2', 'rock:mossy'];
    case 'tropical': return ['rock:mossy', 'rock:granite', 'rock:mossy', 'rock:basalt'];
    case 'savanna': return ['rock:kopje', 'rock:granite', 'rock:kopje', 'rock:granite2'];
    case 'coast': return ['rock:basalt', 'rock:granite', 'rock:basalt', 'rock:granite2'];
    case 'city_night': return ['rock:granite', 'rock:granite2', 'rock:granite', 'rock:basalt'];
  }
}

/** Andares por escala do sprite (três classes, para poucos modelos distintos). */
function floorsClass(scale: number, lo: number, hi: number): number {
  const t = Math.max(0, Math.min(1, (scale - 1.2) / 2.0));
  const k = t < 0.34 ? 0 : t < 0.67 ? 1 : 2;
  return Math.round(lo + (hi - lo) * (k / 2));
}

function buildingId(biome: SceneryId, region: Region, trackId: string, variant: number, scale: number): string {
  const v = ((variant % 4) + 4) % 4;
  const f = (lo: number, hi: number) => floorsClass(scale, lo, hi);
  if (biome === 'city_night') {
    const byRegion: Record<Region, string[]> = {
      br: [`bld:apt:${f(4, 14)}:${v}`, `bld:brick:${f(3, 8)}:${v}`, `bld:glass:${f(8, 22)}:${v}`, `bld:apt:${f(5, 16)}:${v + 1}`],
      us: [`bld:glassgold:${f(10, 26)}:${v}`, `bld:brick:${f(3, 8)}:${v}`, `bld:glass:${f(10, 26)}:${v}`, `bld:apt:${f(5, 12)}:${v}`],
      jp: [`bld:brick:${f(4, 10)}:${v}`, `bld:glass:${f(8, 22)}:${v}`, `bld:apt:${f(5, 14)}:${v}`, `bld:brick:${f(4, 10)}:${v + 2}`],
      eu: [`bld:classic:${f(4, 6)}:${v}`, `bld:classic:${f(4, 6)}:${v + 1}`, `bld:classic:${f(4, 6)}:${v + 2}`, `bld:apt:${f(4, 9)}:${v}`],
      med: [`bld:classicwarm:${f(3, 5)}:${v}`, `bld:classicwarm:${f(3, 5)}:${v + 1}`, `bld:classic:${f(3, 5)}:${v}`, `bld:classicwarm:${f(3, 5)}:${v + 2}`],
      au: [`bld:glass:${f(10, 26)}:${v}`, `bld:glass:${f(8, 20)}:${v + 1}`, `bld:apt:${f(5, 12)}:${v}`, `bld:classic:${f(4, 6)}:${v}`],
      za: [`bld:glass:${f(8, 18)}:${v}`, `bld:apt:${f(4, 10)}:${v}`, `bld:classic:${f(3, 5)}:${v}`, `bld:apt:${f(4, 10)}:${v + 1}`],
      no: [`bld:apt:${f(3, 8)}:${v}`, `bld:classic:${f(3, 5)}:${v}`, `bld:glass:${f(6, 14)}:${v}`, `bld:apt:${f(3, 8)}:${v + 1}`],
    };
    return byRegion[region][v];
  }
  if (biome === 'coast') {
    if (trackId === 'santorini') return pickV(['house:cubes-white', 'house:cubes-white', 'house:cubes-pastel', 'house:cubes-white'], v);
    const byRegion: Partial<Record<Region, string[]>> = {
      br: [`bld:aptbeach:${f(6, 14)}:${v}`, `bld:aptbeach:${f(8, 16)}:${v + 1}`, `bld:glass:${f(8, 16)}:${v}`, `bld:aptbeach:${f(5, 12)}:${v + 2}`],
      med: ['house:cubes-pastel', 'house:cubes-white', 'house:cubes-pastel', 'house:farm-pink'],
      eu: [`bld:classicwarm:${f(3, 6)}:${v}`, `bld:aptbeach:${f(5, 10)}:${v}`, `bld:classic:${f(3, 6)}:${v}`, `bld:glass:${f(6, 12)}:${v}`],
      no: ['house:red', 'house:yellow', 'house:white', 'house:red'], za: ['house:white', 'house:farm', 'house:white', 'house:red'],
      au: ['house:white', 'house:yellow', 'house:farm', 'house:white'], jp: [`bld:warehouse:0:${v}`, `bld:apt:${f(4, 10)}:${v}`, 'house:japan', `bld:warehouse:0:${v + 1}`],
      us: ['house:white', 'house:yellow', `bld:apt:${f(4, 8)}:${v}`, 'house:white'],
    };
    return pickV(byRegion[region] ?? byRegion.au ?? [], v);
  }
  if (biome === 'alpine') {
    const byRegion: Partial<Record<Region, string[]>> = { jp: ['house:japan', 'house:japan', 'house:chalet', 'house:japan'], za: ['house:rondavel', 'house:farm', 'house:rondavel', 'house:rondavel'], no: ['house:red', 'house:red', 'house:yellow', 'house:white'] };
    return pickV(byRegion[region] ?? ['house:chalet', 'house:chalet', 'house:red', 'house:chalet'], v);
  }
  if (biome === 'tropical') return pickV(region === 'jp' ? ['house:japan', 'house:farm', 'house:japan', 'house:japan'] : ['house:farm', 'house:farm-pink', 'house:farm', 'house:white'], v);
  return pickV(['house:farm', 'house:white', 'house:farm-pink', 'house:farm'], v);
}

function towerId(biome: SceneryId, region: Region, variant: number): { id: string; extra?: SpriteVisual['models'] } {
  const R = V.collisionHalfM('tower');
  const v = ((variant % 2) + 2) % 2;
  const seed = variant % 7;
  if (biome === 'coast') {
    if (v === 0) return { id: `tower:lighthouse:${R}:${region === 'no' || region === 'za' ? 1 : 0}`, extra: [{ id: 'beam', dy: 2.6 + 20 + 1.4, spinY: 0.9 }] };
    return { id: `tower:mast:${R}:${seed}` };
  }
  if (biome === 'desert') {
    if (v === 0) return { id: `tower:windpump:${R}:${seed}`, extra: [{ id: 'fan', dy: 11 + hash2(seed, 1) * 3 + 0.6, dz: 0.6, spin: 2.2 }] };
    return { id: `tower:water:${R}:${seed}` };
  }
  if (biome === 'city_night') {
    if (region === 'eu' || region === 'med') return { id: v === 0 ? `tower:dome:${R}:${seed % 3}` : `tower:spire:${R}:${seed % 3}` };
    return { id: v === 0 ? `tower:tv:${R}:${seed % 3}` : `tower:spire:${R}:${region === 'us' ? 3 : seed % 3}` };
  }
  return { id: v === 0 ? `tower:mast:${R}:${seed}` : `tower:water:${R}:${seed}` };
}

/** O visual de um sprite do núcleo neste bioma/país. */
export function spriteVisual(kind: SpriteKind, variant: number, scale: number, def: TrackDef): SpriteVisual {
  const biome = def.scenery; const region = regionOf(def);
  const round = (id: string, yaw = Math.PI, hj = 0.18): SpriteVisual => ({ models: [{ id }], mode: 'round', yawJitter: yaw, heightJitter: hj });
  switch (kind) {
    case 'tree': { const t = TREES[biome]; return round(pickV(t[region] ?? t._, variant)); }
    case 'pine': return round(pickV(PINES[region] ?? PINES._, variant));
    case 'palm': return round(pickV(PALMS[region] ?? PALMS._, variant));
    case 'cactus': {
      const id = pickV(CACTI[region] ?? CACTI._, variant);
      // Saguaro e palma têm braços em X: giro pequeno, para a largura na pista continuar a de colisão.
      return round(id, id.startsWith('cactus:saguaro') || id === 'cactus:pear' ? 0.3 : Math.PI, 0.15);
    }
    case 'bush': return round(pickV(BUSHES[biome], variant), BUSHES[biome][0] === 'bush:hedge' ? 0 : Math.PI, 0.25);
    case 'boulder': return round(pickV(rockIds(biome, region), variant), Math.PI, 0.12);
    case 'building': return { models: [{ id: buildingId(biome, region, def.id, variant, scale) }], mode: 'building', yawJitter: 0, heightJitter: 0, uniform: true };
    case 'tower': {
      const t = towerId(biome, region, variant);
      return { models: [{ id: t.id }, ...(t.extra ?? [])], mode: 'round', yawJitter: 0, heightJitter: 0, uniform: true };
    }
    case 'lamp': return { models: [{ id: 'lamp' }], mode: 'faces', yawJitter: 0, heightJitter: 0 };
    case 'billboard': return { models: [{ id: `billboard:${((variant % 8) + 8) % 8}` }], mode: 'panel', yawJitter: 0, heightJitter: 0 };
    case 'sign_left': return { models: [{ id: 'sign:left' }], mode: 'panel', yawJitter: 0, heightJitter: 0 };
    case 'sign_right': return { models: [{ id: 'sign:right' }], mode: 'panel', yawJitter: 0, heightJitter: 0 };
    case 'grandstand': return { models: [{ id: `stand:${variant % 2}` }], mode: 'faces', yawJitter: 0, heightJitter: 0 };
    case 'banner_start': return { models: [{ id: 'gantry' }], mode: 'panel', yawJitter: 0, heightJitter: 0 };
    case 'pit_wall': return { models: [{ id: `garage:${variant % 6}` }], mode: 'faces', yawJitter: 0, heightJitter: 0 };
    case 'pit_sign': return { models: [{ id: 'pitsign' }], mode: 'panel', yawJitter: 0, heightJitter: 0 };
    case 'cone': return { models: [{ id: 'cone' }], mode: 'round', yawJitter: Math.PI, heightJitter: 0 };
  }
}

// ───────────────────────────── Receitas de decoração ─────────────────────────────

export interface Grove { near: string[]; far: string; weight: number }

export interface DressingRecipe {
  /** Cerca na divisa do alcance do carro (dos dois lados, menos o mar). */
  fence: S.FenceStyle | null;
  /** Postes com fios (um lado). */
  poles: boolean;
  /** Forração da faixa onde o carro anda (baixa, atravessável). */
  cover: Array<[string, number]>;
  coverDensity: number;
  /** Árvores além da divisa: espécies (perto) + silhueta (longe). */
  groves: Grove[];
  /** Árvores por lado por segmento num trecho de mata. */
  groveDensity: number;
  /** Fração do trecho que é clareira (sem árvores altas). */
  clearing: number;
  /** Soltos além da divisa (pedras, moitas, cupinzeiros). */
  scatter: Array<[string, number]>;
  scatterDensity: number;
  /** Pontos de referência raros: id, peso, distância lateral mínima e máxima (m). */
  landmarks: Array<[string, number, number, number]>;
  landmarkEvery: number;
  /** Litoral: barcos no mar (lado direito) e guarda-sóis na areia. */
  boats: string[];
  umbrellas: boolean;
}

const green3 = ['tuft:green:0', 'tuft:green:1', 'tuft:green:2'];

export function dressingRecipe(def: TrackDef): DressingRecipe {
  const region = regionOf(def);
  const base: DressingRecipe = {
    fence: null, poles: false, cover: green3.map((id) => [id, 1] as [string, number]), coverDensity: 2.2, groves: [], groveDensity: 0.8, clearing: 0.35,
    scatter: [], scatterDensity: 0.15, landmarks: [], landmarkEvery: 160, boats: [], umbrellas: false,
  };
  switch (def.scenery) {
    case 'tropical':
      return { ...base, fence: region === 'jp' ? 'hedge' : null, cover: [['tuft:green:0', 2], ['tuft:green:1', 2], ['flowers:tropical', 1], ['bush:fern', 1]], coverDensity: 2.6,
        groves: [{ near: ['tree:giant:0', 'tree:giant:1'], far: 'far:round-dark', weight: 3 }, { near: ['tree:mango:0', 'tree:mango:1'], far: 'far:round-green', weight: 2 }, { near: ['palm:coco', 'palm:royal'], far: 'far:palm', weight: 2 }, { near: ['tree:fern', 'tree:banana'], far: 'far:round-green', weight: 2 }],
        groveDensity: 1.25, clearing: 0.2, scatter: [['bush:heliconia', 2], ['bush:fern', 2], ['rock:mossy', 1]], scatterDensity: 0.3,
        landmarks: region === 'jp' ? [['torii', 1, 30, 45]] : region === 'br' ? [['chapel:white', 1, 40, 90], ['house:farm', 2, 30, 70]] : [['house:farm', 1, 35, 80]], landmarkEvery: 220 };
    case 'savanna':
      if (region === 'eu') return { ...base, fence: 'rail', poles: true, cover: [['tuft:green:0', 2], ['tuft:green:1', 1], ['flowers:meadow', 1]], coverDensity: 2.2,
        groves: [{ near: ['tree:oak', 'tree:linden'], far: 'far:round-green', weight: 3 }, { near: ['tree:poplar'], far: 'far:poplar', weight: 2 }], groveDensity: 0.7, clearing: 0.55,
        scatter: [['bush:green', 1]], scatterDensity: 0.1, landmarks: [['turbine', 5, 70, 220], ['house:farm', 2, 40, 110], ['chapel:white', 1, 60, 140]], landmarkEvery: 45 };
      if (region === 'br') return { ...base, fence: 'ranch', poles: true, cover: [['tuft:dry:0', 2], ['tuft:green:1', 2], ['flowers:meadow', 1]], coverDensity: 2.4,
        groves: [{ near: ['tree:ipe-pink', 'tree:ipe-yellow'], far: 'far:round-pink', weight: 1 }, { near: ['tree:mango:0', 'tree:marula'], far: 'far:round-green', weight: 2 }, { near: ['palm:coco', 'palm:fan'], far: 'far:palm-dry', weight: 2 }],
        groveDensity: 0.55, clearing: 0.5, scatter: [['bush:dry', 1], ['termite', 1]], scatterDensity: 0.15, landmarks: [['house:farm', 2, 40, 110], ['chapel:white', 1, 60, 150]], landmarkEvery: 200 };
      return { ...base, fence: 'ranch', cover: [['tuft:dry:0', 3], ['tuft:dry:1', 2], ['tuft:dry:2', 2], ['bush:dry', 1]], coverDensity: 2.8,
        groves: [{ near: ['tree:acacia:0', 'tree:acacia:1'], far: 'far:flat-acacia', weight: 4 }, { near: ['tree:baobab'], far: 'far:round-dry', weight: 1 }, { near: ['tree:marula'], far: 'far:round-dry', weight: 1 }],
        groveDensity: 0.35, clearing: 0.5, scatter: [['rock:kopje', 1], ['bush:dry', 2], ['termite', 1]], scatterDensity: 0.25,
        landmarks: [['house:rondavel', 1, 50, 120], ['far:rock-ochre', 2, 60, 160]], landmarkEvery: 180 };
    case 'desert': {
      const rocksFar = region === 'med' ? 'far:rock-dark' : region === 'za' ? 'far:rock-ochre' : 'far:rock-red';
      const mesas: Array<[string, number, number, number]> = region === 'med' ? [['mesa:etna', 1, 180, 300]] : region === 'za' ? [['mesa:karoo', 2, 150, 300]] : [['mesa:0', 2, 140, 300], ['mesa:1', 2, 120, 260]];
      const trees = region === 'au' ? [{ near: ['tree:eucalyptus'], far: 'far:round-olive', weight: 1 }] : region === 'med' ? [{ near: ['tree:olive'], far: 'far:round-olive', weight: 2 }, { near: ['tree:cypress'], far: 'far:cone-cypress', weight: 1 }] : [];
      const scatter: Array<[string, number]> = region === 'au' ? [['termite', 2], ['cactus:spinifex', 3], ['rock:sandstone', 1]] : region === 'za' ? [['cactus:aloe', 2], ['bush:sage', 2], ['rock:kopje', 1]] : region === 'med' ? [['cactus:pear', 2], ['cactus:agave', 1], ['rock:lava', 1]] : [['cactus:saguaro2', 2], ['cactus:saguaro3', 1], ['cactus:barrel', 1], ['rock:sandstone', 1], ['bush:sage', 2]];
      const extra: Array<[string, number, number, number]> = region === 'med' ? [['windmill', 1, 50, 120]] : region === 'us' ? [['tower:water:3:1', 1, 40, 90]] : [['tower:windpump:3:2', 1, 35, 80]];
      return { ...base, fence: region === 'med' ? 'stone' : 'ranch', poles: region !== 'med', cover: region === 'med' ? [['tuft:sage:0', 2], ['pebbles:dark', 2], ['flowers:desert', 1]] : [['tuft:sage:0', 2], ['tuft:sage:1', 1], ['pebbles:red', 2], ['tuft:dry:1', 1]],
        coverDensity: 2.0, groves: trees, groveDensity: trees.length ? 0.2 : 0, clearing: 0.5, scatter, scatterDensity: 0.4,
        landmarks: [...mesas, [rocksFar, 3, 50, 140], ...extra], landmarkEvery: 60 };
    }
    case 'alpine': {
      const pines = region === 'jp' ? ['pine:cedar', 'pine:spruce'] : region === 'no' ? ['pine:snow', 'pine:spruce', 'pine:larch'] : region === 'za' ? ['pine:fir', 'pine:slim'] : ['pine:spruce', 'pine:fir', 'pine:snow'];
      const broad = region === 'jp' ? ['tree:maple-red', 'tree:maple-orange'] : region === 'us' ? ['tree:aspen-gold', 'tree:aspen-green'] : region === 'no' ? ['tree:aspen-green', 'tree:aspen-gold'] : ['tree:beech-autumn', 'tree:aspen-green'];
      const lm: Array<[string, number, number, number]> = region === 'jp' ? [['pagoda', 1, 50, 110], ['house:japan', 2, 35, 90], ['torii', 1, 30, 40]] : region === 'za' ? [['house:rondavel', 2, 40, 100]] : region === 'no' ? [['house:red', 2, 35, 90], ['chapel:red', 1, 50, 120]] : region === 'us' ? [['house:chalet', 1, 40, 100]] : [['house:chalet', 3, 35, 100], ['chapel:white', 1, 50, 130]];
      return { ...base, fence: 'armco', cover: [['tuft:green:0', 2], ['tuft:green:2', 1], ['flowers:alpine', 1], ['pebbles:grey', 1]], coverDensity: 2.2,
        groves: [{ near: pines, far: region === 'no' ? 'far:cone-snow' : 'far:cone-dark', weight: 5 }, { near: broad, far: region === 'jp' ? 'far:round-red' : 'far:round-gold', weight: 1 }],
        groveDensity: 1.4, clearing: 0.25, scatter: [['rock:granite', 2], ['rock:snowy', 1], ['bush:juniper', 2]], scatterDensity: 0.3, landmarks: lm, landmarkEvery: 170 };
    }
    case 'coast': {
      const boats = region === 'eu' ? ['yacht', 'yacht', 'sailboat:0'] : region === 'no' || region === 'za' || region === 'jp' ? ['fishboat:0', 'fishboat:1', 'fishboat:2', 'sailboat:1'] : ['sailboat:0', 'sailboat:1', 'fishboat:1'];
      const groves: Grove[] = region === 'med' ? [{ near: ['tree:olive'], far: 'far:round-olive', weight: 2 }, { near: ['tree:cypress'], far: 'far:cone-cypress', weight: 2 }]
        : region === 'no' ? [{ near: ['pine:spruce', 'pine:snow'], far: 'far:cone-dark', weight: 3 }, { near: ['tree:aspen-green'], far: 'far:round-green', weight: 1 }]
          : region === 'au' ? [{ near: ['tree:eucalyptus'], far: 'far:round-olive', weight: 2 }, { near: ['bush:green'], far: 'far:round-green', weight: 1 }]
            : region === 'jp' ? [{ near: ['palm:blackpine'], far: 'far:round-dark', weight: 2 }]
              : [{ near: ['palm:coco', 'palm:coco-lean'], far: 'far:palm', weight: 3 }, { near: ['tree:mango:0'], far: 'far:round-green', weight: 1 }];
      const lm: Array<[string, number, number, number]> = region === 'med' ? [['house:cubes-white', 3, 32, 70], ['house:cubes-pastel', 3, 32, 80], ['windmill', 1, 40, 90]]
        : region === 'no' ? [['house:red', 3, 30, 70], ['house:yellow', 2, 30, 70]] : region === 'au' ? [['house:white', 1, 35, 80]] : region === 'jp' ? [['torii', 1, 30, 40], ['house:japan', 1, 35, 70]] : region === 'za' ? [['house:white', 2, 35, 80]] : [['house:white', 1, 35, 80]];
      return { ...base, fence: region === 'med' ? 'stone' : 'railing', cover: [['tuft:green:0', 2], ['bush:agapanthus', 1], ['tuft:green:1', 1], ['flowers:meadow', 1]], coverDensity: 2.0,
        groves, groveDensity: 0.9, clearing: 0.35, scatter: [['bush:bougainvillea', 1], ['rock:basalt', 1], ['bush:green', 1]], scatterDensity: 0.2, landmarks: lm, landmarkEvery: region === 'med' || region === 'no' ? 40 : 150,
        boats, umbrellas: region === 'br' || region === 'med' };
    }
    case 'city_night':
      return { ...base, fence: 'railing', cover: [], coverDensity: 0, groves: [{ near: ['tree:street'], far: 'far:round-green', weight: 1 }], groveDensity: 0.25, clearing: 0.5,
        scatter: [['bush:hedge', 1]], scatterDensity: 0.1, landmarks: [], landmarkEvery: 400 };
  }
}
