// Acessibilidade: direção assistida (núcleo, determinística, online), paletas de cores dos
// jogadores para daltonismo e as opções (saneamento, aplicação no #hud/#ui).
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { applyAccessibility, LARGE_TEXT_SCALE } from '../src/access/apply';
import { assistedHumans } from '../src/access/humans';
import { COLOR_PALETTES, seatColor, seatColors, type ColorPalette } from '../src/core/data/drivers';
import { OFFROAD_X } from '../src/core/constants';
import { deserializeRace, hashRace, serializeRace } from '../src/core/serialize';
import { assistInput, assistLevelOf, ASSIST_LEVELS } from '../src/core/sim/assist';
import { createRace, stepRace } from '../src/core/sim/race';
import { getTrack, segmentAt, TRACKS } from '../src/core/track';
import { NEUTRAL_INPUT, type AssistLevel, type HumanEntry, type PlayerInput, type RaceConfig, type RaceState, type Track } from '../src/core/types';
import { DEFAULT_SETTINGS } from '../src/game/contracts';
import { CONTENT_RULES, raceConfigFrom } from '../src/game/online-session';
import { sanitizeSettings } from '../src/game/settings';
import { Lockstep } from '../src/net/lockstep';
import { parseClientInfo, parseStartConfig, type InputRecord, type StartConfig } from '../src/net/protocol';
import { human, humanCar, NO_ASSISTS, skipCountdown, syntheticTrack } from './helpers';

type Pilot = (state: RaceState, seat: number) => PlayerInput;
const input = (p: Partial<PlayerInput>): PlayerInput => ({ ...NEUTRAL_INPUT, ...p });
const handsOff: Pilot = () => input({ throttle: true });
/** Iniciante: pé no fundo e volante só reagindo a quanto já saiu do meio (sem antecipar a curva). */
const novice: Pilot = (state, seat) => input({ throttle: true, steer: Math.max(-1, Math.min(1, -humanCar(state, seat).x * 2)) });

/** A pista difícil (5) com mais curva somada: a prova mais dura para quem não esterça. */
function hardestTrack(): Track {
  let best: Track | null = null;
  let bestCurve = -1;
  for (const def of TRACKS.filter((d) => d.difficulty === 5)) {
    const t = getTrack(def.id);
    const total = t.segments.reduce((a, s) => a + Math.abs(s.curve), 0) / t.segments.length;
    if (total > bestCurve) { bestCurve = total; best = t; }
  }
  if (!best) throw new Error('sem pista difícil');
  return best;
}
const HARD = hardestTrack();

function config(track: Track, humans: HumanEntry[], extra: Partial<RaceConfig> = {}): RaceConfig {
  return { trackId: track.def.id, laps: 2, humans, totalCars: 1, difficulty: 'profissional', manualGear: false, assists: NO_ASSISTS, seed: 5, ...extra };
}

function withLevel(seat: number, level: AssistLevel, teamId = 0, carId = 'falcao'): HumanEntry {
  return level === 'none' ? human(seat, teamId, carId) : { ...human(seat, teamId, carId), assist: level };
}

interface Outcome { state: RaceState; grass: number; grassOutsidePit: number; pits: number; finished: boolean }

/** Corre até o humano do assento 0 terminar (ou o limite), contando entradas na grama dele. */
function drive(track: Track, cfg: RaceConfig, pilot: Pilot, maxTicks = 60 * 60 * 10): Outcome {
  const state = createRace(cfg, track);
  const me = humanCar(state, 0);
  let grass = 0; let grassOutsidePit = 0; let pits = 0;
  for (let i = 0; i < maxTicks && !me.finished; i++) {
    const inputs: PlayerInput[] = [];
    for (const h of cfg.humans) inputs[h.seat] = pilot(state, h.seat);
    stepRace(state, track, inputs);
    for (const e of state.events) {
      if (e.type === 'offroad' && e.entering && e.carId === me.id) {
        grass++;
        // Entrar e sair do box passa pela faixa entre o asfalto e o box: o único "fora" aceitável.
        if (!segmentAt(track, me.z).pit) grassOutsidePit++;
      }
      if (e.type === 'pit_enter' && e.carId === me.id) pits++;
    }
  }
  return { state, grass, grassOutsidePit, pits, finished: me.finished };
}

describe('direção assistida no núcleo', () => {
  it(`completa: sem tocar no volante, completa as voltas em ${HARD.def.name} sem ir à grama (sem assistência, vai)`, () => {
    const assisted = drive(HARD, config(HARD, [withLevel(0, 'full')], { timeTrial: true }), handsOff);
    expect(assisted.finished).toBe(true);
    expect(assisted.grass).toBe(0);
    const bare = drive(HARD, config(HARD, [withLevel(0, 'none')], { timeTrial: true }), handsOff, 60 * 60 * 2);
    expect(bare.grass).toBeGreaterThan(3);
  }, 60_000);

  it('completa: numa corrida inteira com a IA e combustível, vai sozinha ao box e só sai do asfalto nele', () => {
    const track = getTrack('copacabana');
    const out = drive(track, config(track, [withLevel(0, 'full')], { laps: track.def.laps, totalCars: 12 }), handsOff);
    expect(out.finished).toBe(true);
    expect(out.pits).toBeGreaterThanOrEqual(1);
    expect(humanCar(out.state).fuel).toBeGreaterThan(0);
    expect(out.grassOutsidePit).toBe(0);
  }, 60_000);

  it('freio automático: o iniciante que não antecipa as curvas vai bem menos à grama', () => {
    const bare = drive(HARD, config(HARD, [withLevel(0, 'none')], { timeTrial: true }), novice);
    const braked = drive(HARD, config(HARD, [withLevel(0, 'brake')], { timeTrial: true }), novice);
    expect(bare.grass).toBeGreaterThanOrEqual(5);
    expect(braked.finished).toBe(true);
    expect(braked.grass).toBeLessThanOrEqual(Math.floor(bare.grass / 5));
  }, 60_000);

  it('volante assistido: com as mãos fora do volante, segura o carro no asfalto na pista difícil', () => {
    const out = drive(HARD, config(HARD, [withLevel(0, 'steer')], { timeTrial: true }), handsOff);
    expect(out.finished).toBe(true);
    expect(out.grass).toBe(0);
  }, 60_000);

  it('sem assistência (ou ausente) a entrada chega à física como está', () => {
    const { state, track } = { state: createRace(config(HARD, [human(0)]), HARD), track: HARD };
    skipCountdown(state, track);
    const car = humanCar(state);
    const p = input({ throttle: true, steer: 0.7, nitro: true });
    expect(assistLevelOf(state, 0)).toBe('none');
    expect(assistLevelOf(state, 3)).toBe('none');
    expect(assistInput(state, track, car, p, 'none')).toBe(p);
  });
});

describe('o jogador sempre por cima da assistência', () => {
  const straight = syntheticTrack([{ op: 'straight', length: 900 }], 'reta-assist');

  function racingOn(track: Track, level: AssistLevel): { state: RaceState; track: Track } {
    const state = createRace(config(track, [withLevel(0, level)]), track);
    skipCountdown(state, track);
    return { state, track };
  }

  it('o freio do jogador vale em todo nível; a assistência nunca acelera por ele; o nitro é dele', () => {
    for (const level of ASSIST_LEVELS) {
      const { state, track } = racingOn(straight, level);
      const car = humanCar(state);
      car.speed = car.stats.topSpeed * 0.5;
      expect(assistInput(state, track, car, input({ brake: true, throttle: true }), level).brake).toBe(true);
      expect(assistInput(state, track, car, input({ throttle: false }), level).throttle).toBe(false);
      expect(assistInput(state, track, car, input({ throttle: true, nitro: true }), level).nitro).toBe(true);
    }
  });

  it('completa: volante do jogador sobrepõe a faixa automática (vai para onde ele manda, até a borda)', () => {
    const run = (steer: number) => {
      const { state, track } = racingOn(straight, 'full');
      for (let i = 0; i < 60 * 4; i++) stepRace(state, track, [input({ throttle: true, steer })]);
      return humanCar(state).x;
    };
    const free = run(0);
    const left = run(-1);
    expect(Math.abs(free)).toBeLessThanOrEqual(0.56);
    expect(left).toBeLessThan(-0.6);
    expect(left).toBeGreaterThan(-OFFROAD_X);
  });

  it('freio automático não mexe no volante; volante assistido segura na borda quem esterça para fora', () => {
    const { state, track } = racingOn(straight, 'brake');
    const car = humanCar(state);
    expect(assistInput(state, track, car, input({ throttle: true, steer: -0.8 }), 'brake').steer).toBe(-0.8);
    const s = racingOn(straight, 'steer');
    let worst = 0;
    for (let i = 0; i < 60 * 5; i++) { stepRace(s.state, s.track, [input({ throttle: true, steer: -1 })]); worst = Math.min(worst, humanCar(s.state).x); }
    expect(worst).toBeLessThan(-0.7);
    expect(worst).toBeGreaterThan(-OFFROAD_X);
  });

  it('volante assistido não tranca o box: no trecho dele, quem esterça para a direita entra', () => {
    // A pista sintética tem box automático nos segmentos 4..39, logo depois da largada.
    const s = racingOn(straight, 'steer');
    let pitted = false;
    for (let i = 0; i < 60 * 6 && !pitted; i++) { stepRace(s.state, s.track, [input({ throttle: true, steer: 1 })]); pitted = humanCar(s.state).inPit; }
    expect(pitted).toBe(true);
  });

  it('freio automático: antes de uma curva forte tira o pé/freia; longe dela, deixa acelerar', () => {
    const track = syntheticTrack([{ op: 'straight', length: 60 }, { op: 'curve', length: 80, curve: 6 }, { op: 'straight', length: 400 }], 'curva-assist');
    const state = createRace(config(track, [withLevel(0, 'brake')]), track);
    skipCountdown(state, track);
    const car = humanCar(state);
    car.speed = car.stats.topSpeed;
    car.z = 62 * 200; // 3 segmentos antes da curva (que entra em rampa)
    const near = assistInput(state, track, car, input({ throttle: true }), 'brake');
    expect(near.throttle).toBe(false);
    expect(near.brake).toBe(true);
    car.z = 300 * 200;
    expect(assistInput(state, track, car, input({ throttle: true }), 'brake').throttle).toBe(true);
  });
});

describe('determinismo com assistência', () => {
  const pilot: Pilot = (state, seat) => {
    const phase = (state.tick + seat * 53) % 240;
    return input({ throttle: phase % 97 !== 0, brake: phase > 228, steer: phase < 60 ? 0.4 : phase < 120 ? -0.5 : 0, nitro: state.tick % 700 === 100 + seat * 20 });
  };
  const track = getTrack('monaco_noite');
  const cfg = (): RaceConfig => config(track, [withLevel(0, 'full'), withLevel(1, 'brake', 0, 'trovao'), withLevel(2, 'steer', 0, 'tornado')], { totalCars: 10, laps: 3, assists: { ...NO_ASSISTS, sharedNitro: true } });
  const run = (state: RaceState, from: number, to: number) => {
    for (let t = from; t < to; t++) stepRace(state, track, [0, 1, 2].map((s) => pilot(state, s)));
  };

  it('duas corridas iguais dão o mesmo hash; salvar e retomar no meio também', () => {
    const a = createRace(cfg(), track);
    const b = createRace(cfg(), track);
    run(a, 0, 3000);
    run(b, 0, 1500);
    const resumed = deserializeRace(serializeRace(b));
    run(resumed, 1500, 3000);
    expect(hashRace(a)).toBe(hashRace(resumed));
    expect(serializeRace(a)).toBe(serializeRace(resumed));
    // A assistência está na config (e portanto no estado salvo) de cada humano.
    expect(resumed.config.humans.map((h) => h.assist)).toEqual(['full', 'brake', 'steer']);
  }, 30_000);

  it('online: dois computadores em lockstep em memória, com a assistência vinda da largada, ficam iguais', () => {
    const start: StartConfig = {
      trackId: 'monaco_noite', laps: 2, versus: true, difficulty: 'profissional', totalCars: 6, manualGear: false,
      assists: NO_ASSISTS, delay: 3, seed: 77,
      seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 1, name: 'Bia', car: 'trovao', assist: 'full' }],
    };
    // A largada viaja como JSON e cada computador a valida e monta a mesma config.
    const wire = JSON.parse(JSON.stringify(start)) as unknown;
    const parsed = parseStartConfig(wire, CONTENT_RULES);
    expect(parsed?.seats[1].assist).toBe('full');
    const rc = raceConfigFrom(parsed as StartConfig);
    expect(rc.humans.map((h) => h.assist)).toEqual([undefined, 'full']);

    const queue: Array<{ to: number; at: number; records: InputRecord[] }> = [];
    let now = 0;
    const clients = [0, 1].map((id) => {
      const state = createRace(rc, track);
      const ls = new Lockstep({ seats: [0, 1], localSeats: [id], delay: 3 }, {
        sendInputs: (records) => queue.push({ to: 1 - id, at: now + 2 + (now % 3), records: records.map((r) => ({ ...r })) }),
        sendHash: () => undefined,
      });
      return { id, state, ls, grass: 0 };
    });
    // Assento 0 pilota de verdade; assento 1 (completa) só acelera.
    for (let frame = 0; frame < 20_000 && clients.some((c) => c.state.tick < 2400); frame++) {
      now++;
      for (const c of clients) {
        c.ls.setLocalInput(c.id, c.id === 0 ? pilot(c.state, 0) : input({ throttle: true }));
        for (let k = 0; k < 2 && c.state.tick < 2400; k++) {
          if (!c.ls.step(c.state, now, (inputs) => {
            stepRace(c.state, track, inputs);
            const car = humanCar(c.state, 1);
            for (const e of c.state.events) if (e.type === 'offroad' && e.entering && e.carId === car.id) c.grass++;
          })) break;
        }
      }
      for (let i = queue.length - 1; i >= 0; i--) if (queue[i].at <= now) { const p = queue.splice(i, 1)[0]; clients[p.to].ls.receive(p.records); }
    }
    expect(clients[0].state.tick).toBe(2400);
    expect(hashRace(clients[0].state)).toBe(hashRace(clients[1].state));
    expect(serializeRace(clients[0].state)).toBe(serializeRace(clients[1].state));
    expect(clients[1].grass).toBe(0);
  }, 30_000);

  it('protocolo: a assistência do jogador é opcional, e valor desconhecido invalida a mensagem', () => {
    const info = (assist: unknown) => parseClientInfo({ ready: true, players: [{ name: 'Ana', car: 'falcao', assist }] }, CONTENT_RULES);
    expect(info(undefined)?.players[0]).toEqual({ name: 'Ana', car: 'falcao' });
    expect(info('none')?.players[0]).toEqual({ name: 'Ana', car: 'falcao' });
    expect(info('steer')?.players[0]).toEqual({ name: 'Ana', car: 'falcao', assist: 'steer' });
    expect(info('turbo')).toBeNull();
    expect(info(3)).toBeNull();
  });
});

// ───────────────────────────── Cores dos jogadores ─────────────────────────────

// Simulação de daltonismo de Machado, Oliveira e Fernandes (2009), severidade 1, em RGB linear.
type Mat = number[][];
const VISION: Record<'normal' | 'protan' | 'deutan' | 'tritan', Mat> = {
  normal: [[1, 0, 0], [0, 1, 0], [0, 0, 1]],
  protan: [[0.152286, 1.052583, -0.204868], [0.114503, 0.786281, 0.099216], [-0.003882, -0.048116, 1.051998]],
  deutan: [[0.367322, 0.860646, -0.227968], [0.280085, 0.672501, 0.047413], [-0.01182, 0.04294, 0.968881]],
  tritan: [[1.255528, -0.076749, -0.178779], [-0.078411, 0.930809, 0.147602], [0.004733, 0.691367, 0.3039]],
};
function toLinear(hex: string): number[] {
  return [1, 3, 5].map((i) => { const c = parseInt(hex.slice(i, i + 2), 16) / 255; return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4; });
}
function lab(hex: string, m: Mat): number[] {
  const l = toLinear(hex);
  const c = m.map((r) => Math.min(1, Math.max(0, r[0] * l[0] + r[1] * l[1] + r[2] * l[2])));
  const f = (t: number) => (t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116);
  const X = f((0.4124 * c[0] + 0.3576 * c[1] + 0.1805 * c[2]) / 0.95047);
  const Y = f(0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]);
  const Z = f((0.0193 * c[0] + 0.1192 * c[1] + 0.9505 * c[2]) / 1.08883);
  return [116 * Y - 16, 500 * (X - Y), 200 * (Y - Z)];
}
const deltaE = (a: number[], b: number[]) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
function minPairDelta(colors: readonly string[], m: Mat): number {
  let min = Infinity;
  for (let i = 0; i < colors.length; i++) for (let j = i + 1; j < colors.length; j++) min = Math.min(min, deltaE(lab(colors[i], m), lab(colors[j], m)));
  return min;
}
/** Cinza dos carros da IA no minimapa (hud.css): nenhum jogador pode se confundir com ele. */
const AI_DOT = '#9aa3b5';

/** Distâncias mínimas exigidas (ΔE CIE76): entre os 4 jogadores e de cada um ao cinza da IA. */
const MIN_PAIR = 30;
const MIN_VS_AI = 20;
const TARGETS: Record<ColorPalette, Array<keyof typeof VISION>> = {
  default: ['normal'], deutan: ['normal', 'protan', 'deutan'], tritan: ['normal', 'tritan'],
};

describe('paletas dos jogadores', () => {
  for (const palette of COLOR_PALETTES) {
    it(`${palette}: 4 cores claras, distantes entre si e do cinza da IA na visão a que se destina`, () => {
      const colors = seatColors(palette);
      expect(colors).toHaveLength(4);
      for (const c of colors) {
        expect(c).toMatch(/^#[0-9a-f]{6}$/);
        expect(lab(c, VISION.normal)[0]).toBeGreaterThanOrEqual(55); // legível no vidro escuro do HUD
      }
      for (const vision of TARGETS[palette]) {
        expect(minPairDelta(colors, VISION[vision]), `${palette} em ${vision}`).toBeGreaterThanOrEqual(MIN_PAIR);
        for (const c of colors) expect(deltaE(lab(c, VISION[vision]), lab(AI_DOT, VISION[vision])), `${c} × IA em ${vision}`).toBeGreaterThanOrEqual(MIN_VS_AI);
      }
    });
  }

  it('as paletas de daltonismo resolvem o que a padrão não resolve', () => {
    const def = seatColors('default');
    expect(minPairDelta(def, VISION.protan)).toBeLessThan(MIN_PAIR);
    expect(minPairDelta(def, VISION.tritan)).toBeLessThan(MIN_PAIR);
  });

  it('seatColor: cor do assento na paleta; assento fora da faixa ou paleta desconhecida caem no padrão', () => {
    expect(seatColor(2, 'tritan')).toBe(seatColors('tritan')[2]);
    expect(seatColor(0)).toBe(seatColors('default')[0]);
    expect(seatColor(7, 'deutan')).toBe('#ffffff');
    expect(seatColors('xyz' as ColorPalette)).toEqual(seatColors('default'));
  });

  it('nenhuma cor de jogador fixa: tudo passa por seatColor/seatColors (e a cor mostrada não vem do HumanEntry)', () => {
    const files: string[] = [];
    const walk = (dir: string) => {
      for (const name of readdirSync(dir)) {
        const p = join(dir, name);
        if (statSync(p).isDirectory()) walk(p);
        else if (p.endsWith('.ts')) files.push(p);
      }
    };
    walk(join(__dirname, '..', 'src'));
    walk(join(__dirname, '..', 'scripts'));
    const offenders: string[] = [];
    for (const f of files) {
      const text = readFileSync(f, 'utf8');
      if (/SEAT_COLORS|SEAT_PALETTES/.test(text) && !f.endsWith(join('data', 'drivers.ts'))) offenders.push(`${f}: tabela de cores dos assentos fora de drivers.ts`);
      // Telas e HUD mostram a cor da paleta atual, não a gravada na config/save (que é a de quem montou a corrida).
      if (/[/\\](ui|render)[/\\]/.test(f) || f.endsWith('session.ts')) {
        for (const line of text.split('\n')) if (/\b(h|x|human|humans\[\d\])\??\.color\b/.test(line)) offenders.push(`${f}: ${line.trim()}`);
      }
      for (const hex of [...seatColors('deutan'), ...seatColors('tritan')]) if (text.toLowerCase().includes(hex) && !f.endsWith(join('data', 'drivers.ts'))) offenders.push(`${f}: ${hex}`);
    }
    expect(offenders).toEqual([]);
  });
});

// ───────────────────────────── Opções ─────────────────────────────

describe('opções de acessibilidade', () => {
  it('padrões: sem assistência, paleta padrão, HUD 100%, texto normal, efeitos ligados', () => {
    const s = sanitizeSettings(undefined);
    expect(s.seatAssists).toEqual(['none', 'none', 'none', 'none']);
    expect(s).toMatchObject({ colorPalette: 'default', hudScale: 1, largeText: false, reduceEffects: false });
    expect(s.seatAssists).not.toBe(DEFAULT_SETTINGS.seatAssists);
  });

  it('saneamento: valores inválidos voltam ao padrão, faixa do HUD 80–150% em passos de 10%', () => {
    const s = sanitizeSettings({ seatAssists: ['full', 'turbo', 7, 'steer', 'brake'], colorPalette: 'rgb', hudScale: 9, largeText: 'sim', reduceEffects: true });
    expect(s.seatAssists).toEqual(['full', 'none', 'none', 'steer']);
    expect(s.colorPalette).toBe('default');
    expect(s.hudScale).toBe(1.5);
    expect(s.largeText).toBe(false);
    expect(s.reduceEffects).toBe(true);
    expect(sanitizeSettings({ hudScale: 0.1 }).hudScale).toBe(0.8);
    expect(sanitizeSettings({ hudScale: 1.23 }).hudScale).toBe(1.2);
    expect(sanitizeSettings({ hudScale: 'x' }).hudScale).toBe(1);
    expect(sanitizeSettings({ seatAssists: 'full' }).seatAssists).toEqual(['none', 'none', 'none', 'none']);
    expect(sanitizeSettings({ colorPalette: 'tritan' }).colorPalette).toBe('tritan');
  });

  it('a config da corrida leva a assistência de cada assento; a do lobby/online vence; "nenhuma" não entra', () => {
    const humans = [human(0), human(1), { ...human(2), assist: 'brake' as const }];
    const out = assistedHumans(humans, ['full', 'none', 'steer', 'none']);
    expect(out.map((h) => h.assist)).toEqual(['full', undefined, 'brake']);
    expect('assist' in out[1]).toBe(false);
    expect(assistedHumans([{ ...human(0), assist: 'none' }], ['full'])[0]).toEqual(human(0));
  });

  it('aplicação: --hud-scale no #hud, --text-scale no #ui e a classe de reduzir efeitos nos dois', () => {
    const fake = () => {
      const props: Record<string, string> = {};
      const classes = new Set<string>();
      return {
        props, classes,
        el: {
          style: { setProperty: (k: string, v: string) => { props[k] = v; } },
          classList: { toggle: (c: string, on?: boolean) => { if (on) classes.add(c); else classes.delete(c); return !!on; } },
        } as unknown as HTMLElement,
      };
    };
    const hud = fake(); const ui = fake();
    applyAccessibility({ hudScale: 1.3, largeText: true, reduceEffects: true }, hud.el, ui.el);
    expect(hud.props['--hud-scale']).toBe('1.3');
    expect(ui.props['--text-scale']).toBe(String(LARGE_TEXT_SCALE));
    expect([...hud.classes]).toEqual(['reduce-fx']);
    expect([...ui.classes].sort()).toEqual(['large-text', 'reduce-fx']);
    applyAccessibility({ hudScale: 1, largeText: false, reduceEffects: false }, hud.el, ui.el);
    expect(ui.props['--text-scale']).toBe('1');
    expect(hud.classes.size + ui.classes.size).toBe(0);
  });
});
