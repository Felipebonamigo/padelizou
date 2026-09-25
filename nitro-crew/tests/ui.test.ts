// Partes puras da interface: saneamento de opções e progresso, recordes e mapeamento de entrada.
// Roda em Node, sem DOM — createInput/createMenus não são instanciados aqui.
import { describe, expect, it } from 'vitest';
import { CUPS } from '../src/core/data/cups';
import type { HumanEntry, RaceResultRow } from '../src/core/types';
import { DEFAULT_SAVE, DEFAULT_SETTINGS } from '../src/game/contracts';
import { bestRaceKey, isCupUnlocked, loadSave, markCupCompleted, recordRaceResults, rememberLobby, sanitizeSave, saveSave } from '../src/game/save';
import { loadSettings, sanitizeSettings, saveSettings } from '../src/game/settings';
import {
  applyDeadzone, closeEdges, edge, isEditableTarget, mapGamepad, mapKeyboard, MENU_REPEAT_MS, NEUTRAL_RAW, NO_EDGES,
  repeatEdge, shortGamepadName, toPlayerInput, USED_KEY_CODES,
} from '../src/ui/input';

// ───────────────────────────── Opções ─────────────────────────────

describe('sanitizeSettings', () => {
  it('lixo vira o padrão', () => {
    for (const raw of [null, undefined, 42, 'x', [], true, {}]) expect(sanitizeSettings(raw)).toEqual(DEFAULT_SETTINGS);
  });

  it('não compartilha o objeto de assistências do padrão', () => {
    const s = sanitizeSettings({});
    s.assists.tow = false;
    expect(DEFAULT_SETTINGS.assists.tow).toBe(true);
  });

  it('valores fora da faixa são trazidos para a borda', () => {
    const s = sanitizeSettings({ masterVolume: 5, musicVolume: -1, sfxVolume: 0.25, totalCars: 50, quickLaps: 0 });
    expect(s.masterVolume).toBe(1);
    expect(s.musicVolume).toBe(0);
    expect(s.sfxVolume).toBe(0.25);
    expect(s.totalCars).toBe(20);
    expect(s.quickLaps).toBe(2);
    expect(sanitizeSettings({ totalCars: 3 }).totalCars).toBe(8);
    expect(sanitizeSettings({ totalCars: 12.6 }).totalCars).toBe(13);
    expect(sanitizeSettings({ quickLaps: 9 }).quickLaps).toBe(8);
  });

  it('tipo errado, NaN e enumeração inválida caem no padrão', () => {
    const s = sanitizeSettings({ language: 'de', quality: 'ultra', difficulty: 'impossível', masterVolume: 'alto', totalCars: NaN, fullscreen: 'sim', music: 7 });
    expect(s.language).toBe('pt');
    expect(s.quality).toBe('high');
    expect(s.difficulty).toBe('profissional');
    expect(s.masterVolume).toBe(DEFAULT_SETTINGS.masterVolume);
    expect(s.totalCars).toBe(20);
    expect(s.fullscreen).toBe(false);
    expect(s.music).toBe('auto');
  });

  it('funde parcial com o padrão, inclusive dentro das assistências', () => {
    const s = sanitizeSettings({ language: 'en', assists: { tow: false, catchup: 'x' }, music: 'tema_noite' });
    expect(s.language).toBe('en');
    expect(s.assists).toEqual({ sharedNitro: true, tow: false, teamDraft: true, catchup: true });
    expect(s.music).toBe('tema_noite');
    expect(s.quality).toBe('high');
  });

  it('sem localStorage (Node), carregar dá o padrão e gravar não lança', () => {
    expect(loadSettings()).toEqual(DEFAULT_SETTINGS);
    expect(() => saveSettings(DEFAULT_SETTINGS)).not.toThrow();
  });
});

// ───────────────────────────── Progresso ─────────────────────────────

describe('sanitizeSave', () => {
  it('lixo vira o padrão', () => {
    for (const raw of [null, undefined, 'x', 3, [], {}]) expect(sanitizeSave(raw)).toEqual(DEFAULT_SAVE);
    expect(loadSave()).toEqual(DEFAULT_SAVE);
    expect(() => saveSave(DEFAULT_SAVE)).not.toThrow();
  });

  it('descarta recordes inválidos e mantém os válidos', () => {
    const s = sanitizeSave({
      bestLaps: {
        copacabana: { ticks: 3600, name: 'Ana', carId: 'trovao', date: '2026-01-01' },
        rota_66: { ticks: -1, name: 'x', carId: 'falcao', date: '' },
        canion: { ticks: 'rápido' },
        autobahn: 5,
      },
      bestRaces: { 'copacabana:3': { ticks: 12000.4, name: 42, carId: 'tornado' } },
    });
    expect(Object.keys(s.bestLaps)).toEqual(['copacabana']);
    expect(s.bestLaps.copacabana).toEqual({ ticks: 3600, name: 'Ana', carId: 'trovao', date: '2026-01-01' });
    expect(s.bestRaces['copacabana:3']).toEqual({ ticks: 12000, name: '?', carId: 'tornado', date: '' });
  });

  it('listas e contagens: só strings, sem repetição, contagem nunca negativa', () => {
    const s = sanitizeSave({ cupsCompleted: ['brasil', 3, 'brasil', ''], achievements: 'x', racesRun: -3, racesWon: 2.7 });
    expect(s.cupsCompleted).toEqual(['brasil']);
    expect(s.achievements).toEqual([]);
    expect(s.racesRun).toBe(0);
    expect(s.racesWon).toBe(3);
  });

  it('nomes e carros por assento: sempre quatro, preenchendo com o padrão', () => {
    const s = sanitizeSave({ seatNames: ['Fê', '', 'Um nome muito comprido mesmo'], seatCars: ['trovao', 'delorean'] });
    expect(s.seatNames).toEqual(['Fê', 'P2', 'Um nome muit', 'P4']);
    expect(s.seatCars).toEqual(['trovao', 'trovao', 'tornado', 'camelo']);
  });
});

describe('isCupUnlocked / markCupCompleted', () => {
  it('a primeira copa está sempre aberta; as outras exigem a anterior', () => {
    const save = sanitizeSave({});
    expect(isCupUnlocked(save, 'brasil', CUPS)).toBe(true);
    expect(isCupUnlocked(save, 'eua', CUPS)).toBe(false);
    expect(isCupUnlocked(save, 'inexistente', CUPS)).toBe(false);
    expect(markCupCompleted(save, 'brasil')).toBe(true);
    expect(markCupCompleted(save, 'brasil')).toBe(false);
    expect(save.cupsCompleted).toEqual(['brasil']);
    expect(isCupUnlocked(save, 'eua', CUPS)).toBe(true);
    expect(isCupUnlocked(save, 'japao', CUPS)).toBe(false);
  });
});

function row(seat: number, position: number, totalTicks: number, bestLapTicks: number, finished = true): RaceResultRow {
  return { carId: seat >= 0 ? 100 + seat : position, seat, name: seat >= 0 ? `P${seat + 1}` : `IA${position}`, teamId: seat >= 0 ? 0 : 100, carDefId: 'falcao', position, finished, totalTicks, bestLapTicks, points: 0 };
}
const humans: HumanEntry[] = [
  { seat: 0, name: 'Fê', carId: 'trovao', teamId: 0, color: '#fff' },
  { seat: 1, name: 'Bia', carId: 'tornado', teamId: 0, color: '#fff' },
];

describe('recordRaceResults', () => {
  it('primeira corrida cria os dois recordes com o melhor humano e conta corrida e vitória', () => {
    const save = sanitizeSave({});
    const results = [row(1, 1, 9000, 2900), row(0, 2, 9100, 2800), row(-1, 3, 9200, 2950)];
    const news = recordRaceResults(save, results, humans, 'copacabana', 3);
    expect(news).toEqual([{ seat: 0, kind: 'lap' }, { seat: 1, kind: 'race' }]);
    expect(save.bestLaps.copacabana).toMatchObject({ ticks: 2800, name: 'Fê', carId: 'trovao' });
    expect(save.bestRaces[bestRaceKey('copacabana', 3)]).toMatchObject({ ticks: 9000, name: 'Bia', carId: 'tornado' });
    expect(save.bestLaps.copacabana.date).not.toBe('');
    expect(save.racesRun).toBe(1);
    expect(save.racesWon).toBe(1);
  });

  it('só é recorde quando melhora; empate e piora não contam', () => {
    const save = sanitizeSave({});
    recordRaceResults(save, [row(0, 1, 9000, 2800)], humans, 'copacabana', 3);
    expect(recordRaceResults(save, [row(0, 2, 9500, 2800)], humans, 'copacabana', 3)).toEqual([]);
    expect(recordRaceResults(save, [row(0, 3, 9500, 3000)], humans, 'copacabana', 3)).toEqual([]);
    expect(save.bestLaps.copacabana.ticks).toBe(2800);
    expect(save.bestRaces['copacabana:3'].ticks).toBe(9000);
    expect(recordRaceResults(save, [row(0, 5, 9600, 2700)], humans, 'copacabana', 3)).toEqual([{ seat: 0, kind: 'lap' }]);
    expect(save.racesRun).toBe(4);
    expect(save.racesWon).toBe(1);
  });

  it('a IA não bate recorde; volta -1 e corrida não terminada não contam; a chave inclui as voltas', () => {
    const save = sanitizeSave({});
    expect(recordRaceResults(save, [row(-1, 1, 8000, 2500), row(0, 2, 9000, -1, false)], humans, 'rota_66', 5)).toEqual([]);
    expect(save.bestLaps.rota_66).toBeUndefined();
    expect(save.bestRaces['rota_66:5']).toBeUndefined();
    expect(save.racesWon).toBe(0);
    expect(recordRaceResults(save, [row(0, 2, 9000, 2600)], humans, 'rota_66', 5)).toEqual([{ seat: 0, kind: 'lap' }, { seat: 0, kind: 'race' }]);
    expect(Object.keys(save.bestRaces)).toEqual(['rota_66:5']);
  });
});

describe('rememberLobby', () => {
  it('guarda nome e carro por assento e ignora nome vazio', () => {
    const save = sanitizeSave({});
    rememberLobby(save, [{ seat: 2, name: '  Zé  ', carId: 'camelo', teamId: 0, color: '#fff' }, { seat: 0, name: '   ', carId: 'trovao', teamId: 0, color: '#fff' }]);
    expect(save.seatNames).toEqual(['P1', 'P2', 'Zé', 'P4']);
    expect(save.seatCars).toEqual(['trovao', 'trovao', 'camelo', 'camelo']);
  });
});

// ───────────────────────────── Entrada ─────────────────────────────

function buttons(...pressed: number[]): boolean[] {
  const b = new Array<boolean>(17).fill(false);
  for (const i of pressed) b[i] = true;
  return b;
}

describe('applyDeadzone', () => {
  it('zera abaixo da zona morta e reescala o resto até 1', () => {
    expect(applyDeadzone(0)).toBe(0);
    expect(applyDeadzone(0.19)).toBe(0);
    expect(applyDeadzone(-0.19)).toBe(0);
    expect(applyDeadzone(1)).toBe(1);
    expect(applyDeadzone(-1)).toBe(-1);
    expect(applyDeadzone(0.6)).toBeCloseTo(0.5);
    expect(applyDeadzone(-0.6)).toBeCloseTo(-0.5);
    expect(applyDeadzone(NaN)).toBe(0);
    expect(applyDeadzone(2)).toBe(1);
  });
});

describe('mapGamepad', () => {
  it('sem nada apertado é neutro', () => {
    expect(mapGamepad([], [])).toEqual(NEUTRAL_RAW);
    expect(mapGamepad(buttons(), [0, 0])).toEqual(NEUTRAL_RAW);
  });

  it('mapeamento "standard": A/RT aceleram, X/B/LT freiam, RB nitro, Y/LB marchas, Start pausa', () => {
    expect(mapGamepad(buttons(0), [])).toMatchObject({ throttle: true, confirm: true, brake: false });
    expect(mapGamepad(buttons(7), [])).toMatchObject({ throttle: true, confirm: false });
    expect(mapGamepad(buttons(1), [])).toMatchObject({ brake: true, back: true });
    expect(mapGamepad(buttons(2), [])).toMatchObject({ brake: true, back: false });
    expect(mapGamepad(buttons(6), [])).toMatchObject({ brake: true });
    expect(mapGamepad(buttons(5), [])).toMatchObject({ nitro: true });
    expect(mapGamepad(buttons(3), [])).toMatchObject({ gearUp: true, gearDown: false });
    expect(mapGamepad(buttons(4), [])).toMatchObject({ gearDown: true, gearUp: false });
    expect(mapGamepad(buttons(9), [])).toMatchObject({ start: true, pause: true, confirm: false });
  });

  it('direção: eixo 0 com zona morta, d-pad vence o analógico, eixo 1 e d-pad navegam', () => {
    expect(mapGamepad(buttons(), [0.1, 0])).toMatchObject({ steer: 0, left: false, right: false });
    const analog = mapGamepad(buttons(), [0.8, 0]);
    expect(analog.steer).toBeCloseTo(0.75);
    expect(analog).toMatchObject({ right: true, left: false });
    expect(mapGamepad(buttons(), [-1, 0])).toMatchObject({ steer: -1, left: true });
    expect(mapGamepad(buttons(14), [0.8, 0])).toMatchObject({ steer: -1, left: true, right: true });
    expect(mapGamepad(buttons(15), [])).toMatchObject({ steer: 1, right: true });
    expect(mapGamepad(buttons(12), [])).toMatchObject({ up: true, down: false });
    expect(mapGamepad(buttons(13), [])).toMatchObject({ down: true });
    expect(mapGamepad(buttons(), [0, -1])).toMatchObject({ up: true, down: false });
    expect(mapGamepad(buttons(), [0, 0.7])).toMatchObject({ down: true, up: false });
    expect(mapGamepad(buttons(), [0, 0.3])).toMatchObject({ down: false, up: false });
  });
});

describe('mapKeyboard', () => {
  const keys = (...codes: string[]) => new Set(codes);

  it('kb1: setas, Espaço nitro/confirma, M/N marchas, Enter confirma, Esc volta e pausa', () => {
    expect(mapKeyboard(keys(), 'kb1')).toEqual(NEUTRAL_RAW);
    expect(mapKeyboard(keys('ArrowUp'), 'kb1')).toMatchObject({ throttle: true, up: true, brake: false });
    expect(mapKeyboard(keys('ArrowDown'), 'kb1')).toMatchObject({ brake: true, down: true });
    expect(mapKeyboard(keys('ArrowLeft'), 'kb1')).toMatchObject({ steer: -1, left: true });
    expect(mapKeyboard(keys('ArrowRight'), 'kb1')).toMatchObject({ steer: 1, right: true });
    expect(mapKeyboard(keys('ArrowLeft', 'ArrowRight'), 'kb1')).toMatchObject({ steer: 0, left: true, right: true });
    expect(mapKeyboard(keys('Space'), 'kb1')).toMatchObject({ nitro: true, confirm: true });
    expect(mapKeyboard(keys('KeyM'), 'kb1')).toMatchObject({ gearUp: true });
    expect(mapKeyboard(keys('KeyN'), 'kb1')).toMatchObject({ gearDown: true });
    expect(mapKeyboard(keys('Enter'), 'kb1')).toMatchObject({ confirm: true, nitro: false });
    expect(mapKeyboard(keys('Escape'), 'kb1')).toMatchObject({ back: true, pause: true });
    expect(mapKeyboard(keys('Backspace'), 'kb1')).toMatchObject({ back: true, pause: false });
    expect(mapKeyboard(keys('KeyW', 'KeyF'), 'kb1')).toEqual(NEUTRAL_RAW);
  });

  it('kb2: WASD, F nitro/confirma, E/Q marchas, Esc volta — e Enter/Espaço não são dele', () => {
    expect(mapKeyboard(keys('KeyW'), 'kb2')).toMatchObject({ throttle: true, up: true });
    expect(mapKeyboard(keys('KeyS'), 'kb2')).toMatchObject({ brake: true, down: true });
    expect(mapKeyboard(keys('KeyA'), 'kb2')).toMatchObject({ steer: -1, left: true });
    expect(mapKeyboard(keys('KeyD'), 'kb2')).toMatchObject({ steer: 1, right: true });
    expect(mapKeyboard(keys('KeyF'), 'kb2')).toMatchObject({ nitro: true, confirm: true });
    expect(mapKeyboard(keys('KeyE'), 'kb2')).toMatchObject({ gearUp: true });
    expect(mapKeyboard(keys('KeyQ'), 'kb2')).toMatchObject({ gearDown: true });
    expect(mapKeyboard(keys('Escape'), 'kb2')).toMatchObject({ back: true, pause: true });
    expect(mapKeyboard(keys('Enter', 'Space', 'ArrowUp'), 'kb2')).toEqual(NEUTRAL_RAW);
    expect(mapKeyboard(keys('Space'), 'kb2').start).toBe(false);
  });

  it('as teclas usadas (as que recebem preventDefault) incluem setas, espaço e WASD', () => {
    for (const code of ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'KeyW', 'KeyA', 'KeyS', 'KeyD', 'KeyF', 'KeyM', 'KeyN', 'KeyE', 'KeyQ', 'Enter', 'Escape']) {
      expect(USED_KEY_CODES.has(code), code).toBe(true);
    }
    expect(USED_KEY_CODES.has('F5')).toBe(false);
    expect(USED_KEY_CODES.has('KeyR')).toBe(false);
  });
});

describe('bordas', () => {
  it('edge é só a subida', () => {
    expect(edge(false, true)).toBe(true);
    expect(edge(true, true)).toBe(false);
    expect(edge(true, false)).toBe(false);
    expect(edge(false, false)).toBe(false);
  });

  it('closeEdges fecha todas as bordas de uma vez e ignora o que continua segurado', () => {
    const now = { ...NEUTRAL_RAW, nitro: true, gearUp: true, confirm: true, up: true };
    expect(closeEdges(NEUTRAL_RAW, now)).toEqual({ ...NO_EDGES, nitro: true, gearUp: true, confirm: true, up: true });
    expect(closeEdges(now, now)).toEqual(NO_EDGES);
    expect(closeEdges(now, NEUTRAL_RAW)).toEqual(NO_EDGES);
  });

  it('repeatEdge: borda ao apertar e a cada 180 ms enquanto segura; soltar zera', () => {
    let r = repeatEdge(true, null, 1000);
    expect(r).toEqual({ edge: true, last: 1000 });
    r = repeatEdge(true, r.last, 1000 + MENU_REPEAT_MS - 1);
    expect(r).toEqual({ edge: false, last: 1000 });
    r = repeatEdge(true, r.last, 1000 + MENU_REPEAT_MS);
    expect(r).toEqual({ edge: true, last: 1000 + MENU_REPEAT_MS });
    r = repeatEdge(false, r.last, 2000);
    expect(r).toEqual({ edge: false, last: null });
    expect(repeatEdge(true, r.last, 2001).edge).toBe(true);
  });

  it('toPlayerInput junta o segurado (volante, acelerador, freio) com as bordas (nitro, marchas)', () => {
    const raw = { ...NEUTRAL_RAW, steer: -0.5, throttle: true, nitro: true, gearUp: true };
    const edges = { ...NO_EDGES, gearUp: true };
    expect(toPlayerInput(raw, edges)).toEqual({ steer: -0.5, throttle: true, brake: false, nitro: false, gearUp: true, gearDown: false });
  });
});

describe('utilidades da entrada', () => {
  it('nome curto do gamepad corta o sufixo entre parênteses e limita o tamanho', () => {
    expect(shortGamepadName('Xbox 360 Controller (XInput STANDARD GAMEPAD Vendor: 045e Product: 028e)')).toBe('Xbox 360 Controller');
    expect(shortGamepadName('')).toBe('');
    expect(shortGamepadName('Um controle com um nome absurdamente comprido').length).toBeLessThanOrEqual(28);
  });

  it('isEditableTarget reconhece campos de texto por duck typing', () => {
    expect(isEditableTarget(null)).toBe(false);
    expect(isEditableTarget({} as EventTarget)).toBe(false);
    expect(isEditableTarget({ tagName: 'INPUT' } as unknown as EventTarget)).toBe(true);
    expect(isEditableTarget({ tagName: 'textarea' } as unknown as EventTarget)).toBe(true);
    expect(isEditableTarget({ tagName: 'DIV', isContentEditable: true } as unknown as EventTarget)).toBe(true);
    expect(isEditableTarget({ tagName: 'BUTTON' } as unknown as EventTarget)).toBe(false);
  });
});
