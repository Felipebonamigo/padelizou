// Pintura do carro (pedido do dono: "os carros também têm que poder trocar de cor"): a paleta, o save por
// assento e carro, quem corre com qual pintura (rivais de fábrica; dois carros iguais ficam distinguíveis), a
// corrida que não carrega cosmético no estado, e a cor chegando aos dois renderizadores e aos desenhos dos
// menus. Roda em Node: sem DOM nem WebGL, com um canvas 2D falso onde o desenho precisa de um.
import * as THREE from 'three';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createChampionship } from '../src/core/championship';
import { CARS, carDef } from '../src/core/data/cars';
import { CUPS } from '../src/core/data/cups';
import { hashRace } from '../src/core/serialize';
import { createRace } from '../src/core/sim/race';
import { getTrack } from '../src/core/track';
import type { CoreMode, HumanEntry, RaceConfig, RaceState } from '../src/core/types';
import { saveCupProgress, sanitizeSavedCup } from '../src/game/career-save';
import { DEFAULT_SAVE, DEFAULT_SETTINGS, type CarColors, type RenderFrame, type SaveData, type Settings } from '../src/game/contracts';
import {
  nextPaint, ORIGINAL_PAINT, PAINT_IDS, PAINTS, paintColors, paintedCar, paintFromSave, racePaints, sanitizeSeatPaints, seatPaint,
  setSeatPaint, stepSeatPaint, withoutPaint, withPaint,
} from '../src/game/paints';
import { sanitizeSave } from '../src/game/save';
import { setLanguage, t } from '../src/i18n';
import { Cars } from '../src/render/cars';
import { carPaint, defaultAccent } from '../src/render/cars/paints';
import { buildRoadFrame } from '../src/render/roadframe';
import { SpriteAtlas } from '../src/render-pseudo3d/sprites';
import '../src/ui/strings';
import type { LobbySeat, ScreenApi } from '../src/ui/screens/common';
import { carSilhouetteMarkup } from '../src/ui/screens/icons';
import { lobbyHumans } from '../src/ui/screens/lobby';
import { tutorialHumans } from '../src/ui/screens/tutorial';
import { human } from './helpers';

const NO_ASSISTS = { sharedNitro: false, tow: false, teamDraft: false, catchup: false };

function rgb(hex: string): [number, number, number] {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)) as [number, number, number];
}

/** Distância de cor "redmean" (0..~765): aproxima a percepção sem converter de espaço de cor. */
function dist(a: string, b: string): number {
  const [r1, g1, b1] = rgb(a); const [r2, g2, b2] = rgb(b);
  const rm = (r1 + r2) / 2;
  return Math.sqrt((2 + rm / 256) * (r1 - r2) ** 2 + 4 * (g1 - g2) ** 2 + (2 + (255 - rm) / 256) * (b1 - b2) ** 2);
}

function freshSave(): SaveData {
  return sanitizeSave({});
}

function config(humans: HumanEntry[], over: Partial<RaceConfig> = {}): RaceConfig {
  return {
    trackId: 'copacabana', laps: 2, humans, totalCars: 8, difficulty: 'profissional', manualGear: false,
    assists: NO_ASSISTS, seed: 42, ...over,
  };
}

function race(humans: HumanEntry[], over: Partial<RaceConfig> = {}): RaceState {
  return createRace(config(withoutPaint(humans), over), getTrack('copacabana'));
}

const painted = (seat: number, carId: string, paint?: string): HumanEntry => withPaint(human(seat, 0, carId), paint);

// ───────────────────────────── Paleta ─────────────────────────────

describe('paleta de pinturas', () => {
  afterEach(() => setLanguage('pt'));

  it('a primeira opção é a Original (a de fábrica); 16 ao todo, sem id repetido', () => {
    expect(PAINT_IDS[0]).toBe(ORIGINAL_PAINT);
    expect(PAINT_IDS).toHaveLength(16);
    expect(new Set(PAINT_IDS).size).toBe(16);
    expect(PAINTS.map((p) => p.id)).toEqual(PAINT_IDS.slice(1));
  });

  it('cada pintura é um par cor + detalhe já combinado, com contraste entre os dois', () => {
    for (const p of PAINTS) {
      expect(p.color, p.id).toMatch(/^#[0-9a-f]{6}$/);
      expect(p.accent, p.id).toMatch(/^#[0-9a-f]{6}$/);
      expect(dist(p.color, p.accent), `${p.id}: detalhe some na cor`).toBeGreaterThanOrEqual(200);
    }
  });

  it('as cores da paleta são diferentes entre si', () => {
    for (let i = 0; i < PAINTS.length; i++) {
      for (let j = i + 1; j < PAINTS.length; j++) {
        expect(dist(PAINTS[i].color, PAINTS[j].color), `${PAINTS[i].id} × ${PAINTS[j].id}`).toBeGreaterThanOrEqual(60);
      }
    }
  });

  it('nenhuma pintura repete a de fábrica de um carro (pela cor ou, se a cor é parecida, pelo detalhe)', () => {
    for (const car of CARS) {
      const accent = car.accent ?? defaultAccent(car.color);
      for (const p of PAINTS) {
        const ok = dist(car.color, p.color) >= 50 || dist(accent, p.accent) >= 200;
        expect(ok, `${car.id} de fábrica × ${p.id}`).toBe(true);
      }
    }
  });

  it('cada pintura tem nome em PT e EN', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const id of PAINT_IDS) expect(t(`ui.paint.${id}`), `${lang} ${id}`).not.toBe(`ui.paint.${id}`);
      expect(t('ui.lobby.paint')).not.toBe('ui.lobby.paint');
    }
    setLanguage('pt');
    expect(t('ui.paint.original')).toBe('Original');
  });

  it('paintColors: a original (e o que não existe) é a de fábrica (null); as outras dão o par', () => {
    expect(paintColors(ORIGINAL_PAINT)).toBeNull();
    expect(paintColors(undefined)).toBeNull();
    expect(paintColors('roxo-que-nao-existe')).toBeNull();
    const p = PAINTS[3];
    expect(paintColors(p.id)).toEqual({ color: p.color, accent: p.accent });
  });

  it('nextPaint anda pela paleta em círculo, nos dois sentidos', () => {
    expect(nextPaint(ORIGINAL_PAINT, 1)).toBe(PAINT_IDS[1]);
    expect(nextPaint(ORIGINAL_PAINT, -1)).toBe(PAINT_IDS[PAINT_IDS.length - 1]);
    expect(nextPaint(PAINT_IDS[PAINT_IDS.length - 1], 1)).toBe(ORIGINAL_PAINT);
    expect(nextPaint('nao-existe', 1)).toBe(PAINT_IDS[1]);
  });

  it('paintedCar troca cor e detalhe para os desenhos; a Original devolve o carro como é', () => {
    const def = carDef('falcao');
    const p = PAINTS[0];
    expect(paintedCar(def, p.id)).toMatchObject({ id: 'falcao', body: 'gt', color: p.color, accent: p.accent });
    expect(paintedCar(def, ORIGINAL_PAINT)).toBe(def);
    expect(def.color).toBe('#f2f2f2'); // o carro dos dados não muda
  });
});

// ───────────────────────────── Save ─────────────────────────────

describe('save: pintura por assento e por carro', () => {
  it('save antigo, sem o campo, carrega com a original em todo assento e carro', () => {
    const old = sanitizeSave({ seatNames: ['Ana', 'Bia', 'P3', 'P4'], seatCars: ['saci', 'tatu', 'falcao', 'trovao'], racesRun: 3 });
    expect(old.seatPaints).toEqual([{}, {}, {}, {}]);
    for (let seat = 0; seat < 4; seat++) for (const car of CARS) expect(seatPaint(old, seat, car.id)).toBe(ORIGINAL_PAINT);
  });

  it('grava e volta (JSON) por assento e por carro', () => {
    const save = freshSave();
    setSeatPaint(save, 0, 'falcao', 'rubi');
    setSeatPaint(save, 1, 'falcao', 'cobalto');
    setSeatPaint(save, 0, 'trovao', 'onix');
    const back = sanitizeSave(JSON.parse(JSON.stringify(save)));
    expect(seatPaint(back, 0, 'falcao')).toBe('rubi');
    expect(seatPaint(back, 1, 'falcao')).toBe('cobalto');
    expect(seatPaint(back, 0, 'trovao')).toBe('onix');
    expect(seatPaint(back, 2, 'falcao')).toBe(ORIGINAL_PAINT);
    expect(seatPaint(back, 1, 'trovao')).toBe(ORIGINAL_PAINT);
  });

  it('lixo some sem derrubar o resto', () => {
    const raw = JSON.parse('[{"falcao":"rubi","naoexiste":"rubi","trovao":"roxo","tornado":7,"__proto__":"rubi","saci":"original"},"x",null,{"camelo":"gelo"},{"falcao":"rubi"}]');
    expect(sanitizeSeatPaints(raw)).toEqual([{ falcao: 'rubi' }, {}, {}, { camelo: 'gelo' }]);
    expect(sanitizeSave({ seatPaints: 'x' }).seatPaints).toEqual([{}, {}, {}, {}]);
    expect(sanitizeSave({ seatPaints: [{ falcao: 'rubi' }] }).seatPaints).toEqual([{ falcao: 'rubi' }, {}, {}, {}]);
  });

  it('voltar à Original tira o carro do mapa (a original não ocupa o save)', () => {
    const save = freshSave();
    setSeatPaint(save, 0, 'falcao', 'rubi');
    setSeatPaint(save, 0, 'falcao', ORIGINAL_PAINT);
    expect(save.seatPaints[0]).toEqual({});
  });

  it('gravar a escolha não mexe no save padrão (objeto compartilhado)', () => {
    const save: SaveData = { ...DEFAULT_SAVE };
    setSeatPaint(save, 0, 'falcao', 'rubi');
    expect(seatPaint(save, 0, 'falcao')).toBe('rubi');
    expect(DEFAULT_SAVE.seatPaints[0]).toEqual({});
  });

  it('stepSeatPaint anda pela paleta e guarda só para aquele assento e aquele carro', () => {
    const save = freshSave();
    expect(stepSeatPaint(save, 2, 'saci', 1)).toBe(PAINT_IDS[1]);
    expect(stepSeatPaint(save, 2, 'saci', 1)).toBe(PAINT_IDS[2]);
    expect(stepSeatPaint(save, 2, 'saci', -1)).toBe(PAINT_IDS[1]);
    expect(seatPaint(save, 2, 'saci')).toBe(PAINT_IDS[1]);
    expect(seatPaint(save, 2, 'tatu')).toBe(ORIGINAL_PAINT);
    expect(seatPaint(save, 3, 'saci')).toBe(ORIGINAL_PAINT);
    expect(stepSeatPaint(save, 0, 'falcao', -1)).toBe(PAINT_IDS[PAINT_IDS.length - 1]);
  });

  it('copa salva ("Continuar") guarda a pintura de cada humano; a de antes, sem o campo, continua valendo', () => {
    const save = freshSave();
    const humans = [painted(0, 'falcao', 'rubi'), human(1, 0, 'trovao')];
    saveCupProgress(save, createChampionship(CUPS[0].id, humans), 7, humans);
    const back = sanitizeSavedCup(JSON.parse(JSON.stringify(save.cupInProgress)));
    expect(back?.humans[0].paint).toBe('rubi');
    expect(back?.humans[1]).not.toHaveProperty('paint');
    const old = JSON.parse(JSON.stringify(save.cupInProgress)) as { humans: Array<Record<string, unknown>> };
    delete old.humans[0].paint;
    old.humans[1].paint = 'cor-que-nao-existe';
    const oldBack = sanitizeSavedCup(old);
    expect(oldBack?.humans.map((h) => h.carId)).toEqual(['falcao', 'trovao']);
    expect(oldBack?.humans.some((h) => 'paint' in h)).toBe(false);
  });
});

// ───────────────────────────── Quem corre com qual pintura ─────────────────────────────

function lobbyApi(save: SaveData, seats: Array<Partial<LobbySeat> & { seat: number; carIndex: number }>): ScreenApi {
  const settings: Settings = { ...DEFAULT_SETTINGS, seatAssists: [...DEFAULT_SETTINGS.seatAssists] };
  const lobbySeats: Array<LobbySeat | null> = [null, null, null, null];
  for (const s of seats) lobbySeats[s.seat] = { device: 'kb1', name: `P${s.seat + 1}`, ready: true, cursor: 0, ...s };
  return {
    ctx: { cars: CARS, save, settings },
    lobby: { mode: 'quick', versus: false, seats: lobbySeats },
  } as unknown as ScreenApi;
}

describe('quem corre com qual pintura', () => {
  it('o lobby manda a pintura que o assento escolheu para aquele carro (a Original não vai no objeto)', () => {
    const save = freshSave();
    setSeatPaint(save, 0, 'falcao', 'rubi');
    setSeatPaint(save, 1, 'trovao', 'gelo'); // escolhida para outro carro: não vale para o Falcão do P2
    const falcao = CARS.findIndex((c) => c.id === 'falcao');
    const humans = lobbyHumans(lobbyApi(save, [{ seat: 0, carIndex: falcao }, { seat: 1, carIndex: falcao }]));
    expect(humans[0].paint).toBe('rubi');
    expect(humans[1]).not.toHaveProperty('paint');
  });

  it('tutorial: a pintura guardada do carro que o assento lembra', () => {
    const save = freshSave();
    setSeatPaint(save, 1, save.seatCars[1], 'gelo');
    const humans = tutorialHumans({ cars: CARS, save, settings: DEFAULT_SETTINGS as Settings }, [0, 1]);
    expect(humans[0]).not.toHaveProperty('paint');
    expect(humans[1].paint).toBe('gelo');
  });

  it('carreira e humanos montados do save: paintFromSave usa a escolha do assento para aquele carro', () => {
    const save = freshSave();
    setSeatPaint(save, 1, 'saci', 'limao');
    setSeatPaint(save, 0, 'trovao', 'onix');
    const out = paintFromSave([human(0, 0, 'falcao'), human(1, 0, 'saci')], save);
    expect(out[0]).not.toHaveProperty('paint');
    expect(out[1].paint).toBe('limao');
  });

  it('withoutPaint tira a pintura sem mexer no resto nem no original', () => {
    const list = [{ ...painted(0, 'falcao', 'rubi'), assist: 'brake' as const }, human(1)];
    const out = withoutPaint(list);
    expect(out[0]).not.toHaveProperty('paint');
    expect(out[0]).toMatchObject({ seat: 0, carId: 'falcao', assist: 'brake' });
    expect(list[0].paint).toBe('rubi');
  });

  it('o estado da corrida não carrega pintura: mesma impressão de quem correu de fábrica', () => {
    const withPaints = race([painted(0, 'falcao', 'rubi'), painted(1, 'trovao', 'cobalto')]);
    const plain = race([human(0, 0, 'falcao'), human(1, 0, 'trovao')]);
    expect(JSON.stringify(withPaints)).not.toContain('paint');
    expect(hashRace(withPaints)).toBe(hashRace(plain));
  });
});

describe('racePaints: a pintura de cada carro na pista', () => {
  const human0 = (s: RaceState, seat: number) => s.cars.findIndex((c) => c.seat === seat);

  it('os rivais da IA ficam com a de fábrica; cada humano com a que escolheu (Original = fábrica)', () => {
    const humans = [painted(0, 'falcao', 'rubi'), painted(1, 'trovao')];
    const s = race(humans);
    const paints = racePaints(s.cars, humans);
    expect(paints).toHaveLength(s.cars.length);
    s.cars.forEach((c, i) => { if (c.seat < 0) expect(paints[i], `IA ${c.name}`).toBeNull(); });
    expect(paints[human0(s, 0)]).toEqual(paintColors('rubi'));
    expect(paints[human0(s, 1)]).toBeNull();
  });

  it('dois humanos com o mesmo carro e a mesma pintura: o segundo ganha a próxima livre da paleta (só na corrida)', () => {
    const both = [painted(0, 'falcao'), painted(1, 'falcao')];
    const s = race(both);
    const p = racePaints(s.cars, both);
    expect(p[human0(s, 0)]).toBeNull();
    expect(p[human0(s, 1)]).toEqual(paintColors(PAINT_IDS[1]));
    expect(both[1]).not.toHaveProperty('paint'); // a escolha dele não muda

    const rubis = [painted(0, 'falcao', 'rubi'), painted(1, 'falcao', 'rubi')];
    const r = race(rubis);
    expect(racePaints(r.cars, rubis)[human0(r, 1)]).toEqual(paintColors(nextPaint('rubi', 1)));
  });

  it('a troca do repetido não toma a pintura que outro escolheu de propósito', () => {
    const humans = [painted(0, 'falcao'), painted(1, 'falcao'), painted(2, 'falcao', PAINT_IDS[1])];
    const s = race(humans);
    const p = racePaints(s.cars, humans);
    expect(p[human0(s, 0)]).toBeNull();
    expect(p[human0(s, 1)]).toEqual(paintColors(PAINT_IDS[2]));
    expect(p[human0(s, 2)]).toEqual(paintColors(PAINT_IDS[1]));
  });

  it('carros diferentes com a mesma pintura (ou o mesmo carro com pinturas diferentes) ficam como escolhidos', () => {
    const humans = [painted(0, 'falcao', 'rubi'), painted(1, 'trovao', 'rubi'), painted(2, 'falcao', 'gelo'), painted(3, 'trovao')];
    const s = race(humans);
    const p = racePaints(s.cars, humans);
    expect(p[human0(s, 0)]).toEqual(paintColors('rubi'));
    expect(p[human0(s, 1)]).toEqual(paintColors('rubi'));
    expect(p[human0(s, 2)]).toEqual(paintColors('gelo'));
    expect(p[human0(s, 3)]).toBeNull();
  });

  it('revezamento: o carro da dupla tem a pintura de quem larga com ele; escolta: o VIP fica com a cor dele', () => {
    const humans = [painted(0, 'falcao', 'cobalto'), painted(1, 'saci', 'rubi')];
    const relay = race(humans, { mode: 'relay' as CoreMode, totalCars: 10 });
    const p = racePaints(relay.cars, humans);
    expect(p[human0(relay, 0)]).toEqual(paintColors('cobalto'));
    expect(p.filter((x) => x !== null)).toHaveLength(1);
    const escort = race(humans, { mode: 'escort' as CoreMode, totalCars: 10 });
    const e = racePaints(escort.cars, humans);
    const vip = escort.party?.vipId ?? -1;
    expect(vip).toBeGreaterThanOrEqual(0);
    expect(e[vip]).toBeNull();
    expect(e[human0(escort, 1)]).toEqual(paintColors('rubi'));
  });
});

// ───────────────────────────── A cor chega aos desenhos ─────────────────────────────

/** Canvas 2D falso: todo método é um no-op; guarda cada `fillStyle` usado (o que o desenho pintou). */
function fakeCanvasDocument(): { fills: string[] } {
  const fills: string[] = [];
  const ctx = new Proxy({} as Record<string | symbol, unknown>, {
    get(target, key) {
      if (key in target) return target[key];
      if (key === 'createImageData' || key === 'getImageData') return (w: number, h: number) => ({ width: w, height: h, data: new Uint8ClampedArray(Math.max(1, w * h * 4)) });
      if (key === 'measureText') return () => ({ width: 10 });
      if (key === 'createLinearGradient' || key === 'createRadialGradient') return () => ({ addColorStop() {} });
      return () => undefined;
    },
    set(target, key, value) {
      if (key === 'fillStyle' && typeof value === 'string') fills.push(value);
      target[key] = value;
      return true;
    },
  });
  vi.stubGlobal('document', {
    createElement: () => ({ width: 0, height: 0, style: {}, getContext: () => ctx }),
  });
  return { fills };
}

describe('a cor chega aos desenhos', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('carPaint (3D) usa a pintura escolhida e mantém a camada de pintura do carro; sem ela, a de fábrica', () => {
    const def = carDef('boitata');
    const order = [2, 4];
    const factory = carPaint(def, CARS, order);
    const mine = carPaint(def, CARS, order, { color: '#123456', accent: '#abcdef' });
    expect(mine).toEqual({ ...factory, color: '#123456', accent: '#abcdef' });
    expect(carPaint(def, CARS, order, null)).toEqual(factory);
    expect(factory.color).toBe(def.color);
    expect(factory.accent).toBe(defaultAccent(def.color));
  });

  it('silhueta dos menus na cor da pintura', () => {
    const p = paintColors('rubi') as CarColors;
    const svg = carSilhouetteMarkup(paintedCar(carDef('tornado'), 'rubi'));
    expect(svg).toContain(`fill="${p.color}"`);
    expect(svg).toContain(`fill="${p.accent}"`);
    expect(svg).not.toContain(`fill="${carDef('tornado').color}"`);
  });

  it('3D: a instância do carro do jogador sai na cor e no detalhe da pintura, e volta à de fábrica sem ela', () => {
    fakeCanvasDocument();
    const humans = [painted(0, 'falcao', 'cobalto')];
    const s = race(humans);
    // Rivais num carro de outro estilo: no lote do GT fica só o carro do jogador.
    for (const c of s.cars) if (c.seat < 0) c.carId = 'trovao';
    const track = getTrack('copacabana');
    const own = s.cars.findIndex((c) => c.seat === 0);
    const cars = new Cars(new THREE.Scene());
    const frame = (paints?: ReadonlyArray<CarColors | null>): RenderFrame => ({
      state: s, track, viewports: [], time: 0, paused: true, coop: false, showHud: false, paints,
      options: { quality: 'high', showMinimap: false, screenShake: false, reduceEffects: false, palette: 'default' },
    });
    const gtColor = () => {
      const batch = (cars as unknown as { styles: Array<{ model: { body: string }; mesh: THREE.InstancedMesh; extra: THREE.InstancedBufferAttribute; count: number }> })
        .styles.find((b) => b.model.body === 'gt');
      if (!batch || !batch.mesh.instanceColor) throw new Error('sem lote do GT');
      expect(batch.count).toBe(1);
      const c = new THREE.Color().fromArray(batch.mesh.instanceColor.array, 0);
      const a = new THREE.Color(batch.extra.getX(0), batch.extra.getY(0), batch.extra.getZ(0));
      return { color: `#${c.getHexString()}`, accent: `#${a.getHexString()}` };
    };
    const draw = (paints?: ReadonlyArray<CarColors | null>) => {
      const f = frame(paints);
      cars.update(f);
      cars.pose(buildRoadFrame(track, s.cars[own].z, 8, 60), s, track, own, [], 0);
    };
    draw(racePaints(s.cars, humans));
    expect(gtColor()).toEqual(paintColors('cobalto'));
    draw(undefined);
    expect(gtColor()).toEqual({ color: carDef('falcao').color, accent: defaultAccent(carDef('falcao').color) });
    cars.dispose();
  });

  it('Retrô: o sprite do carro pintado usa a cor e o detalhe escolhidos; o de fábrica segue igual', () => {
    const { fills } = fakeCanvasDocument();
    const atlas = new SpriteAtlas();
    const p = paintColors('rubi') as CarColors;
    const paintedSprite = atlas.car('falcao', 0, false, false, p);
    const paintFills = fills.splice(0);
    expect(paintFills).toContain(p.color);
    expect(paintFills).toContain(p.accent);
    expect(paintFills).not.toContain(carDef('falcao').color);
    const factorySprite = atlas.car('falcao', 0, false, false);
    expect(fills).toContain(carDef('falcao').color);
    expect(fills).not.toContain(p.color);
    expect(factorySprite).not.toBe(paintedSprite);
    // Em cache por pintura: pedir de novo não redesenha.
    fills.length = 0;
    expect(atlas.car('falcao', 0, false, false, p)).toBe(paintedSprite);
    expect(fills).toHaveLength(0);
  });
});
