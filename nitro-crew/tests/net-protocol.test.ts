// Protocolo do online: entrada compacta, validação de tudo o que chega da rede.
import { describe, expect, it } from 'vitest';
import { CARS } from '../src/core/data/cars';
import { TRACKS } from '../src/core/track';
import {
  decodeInput, encodeInput, normalizeRoomCode, normalizeServerUrl, packRecords, parseServerMessage, quantizeSteer,
  TAKEOVER_BIT, unpackRecords, type ContentRules, type RoomSettings, type StartConfig,
} from '../src/net/protocol';

const RULES: ContentRules = { cars: CARS.map((c) => c.id), tracks: TRACKS.map((t) => t.id) };
const SETTINGS: RoomSettings = {
  trackId: TRACKS[0].id, laps: 3, versus: false, difficulty: 'profissional', totalCars: 12, manualGear: false,
  assists: { sharedNitro: true, tow: true, teamDraft: true, catchup: true }, delay: 3,
};
const START: StartConfig = { ...SETTINGS, seed: 123456, seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 1, name: 'Bia', car: 'trovao' }] };
const parse = (m: unknown) => parseServerMessage(typeof m === 'string' ? m : JSON.stringify(m), RULES);

describe('entrada compacta', () => {
  it('bits e volante em int8 vão e voltam', () => {
    const input = { steer: -0.5, throttle: true, brake: false, nitro: true, gearUp: false, gearDown: true };
    const { bits, steer } = encodeInput(input);
    expect(bits).toBe(1 + 4 + 16);
    expect(steer).toBe(-64);
    expect(decodeInput(bits, steer)).toEqual({ ...input, steer: -64 / 127 });
  });

  it('o volante é quantizado de forma estável (decodificar e codificar de novo não muda nada)', () => {
    for (let q = -127; q <= 127; q++) expect(encodeInput(decodeInput(0, q)).steer).toBe(q);
    expect(quantizeSteer(2)).toBe(127);
    expect(quantizeSteer(-9)).toBe(-127);
    expect(quantizeSteer(Number.NaN)).toBe(0);
    expect(decodeInput(0, 127).steer).toBe(1);
  });

  it('registros vão para a lista plana e voltam', () => {
    const recs = [{ tick: 10, seat: 0, bits: 5, steer: -3 }, { tick: 11, seat: 3, bits: TAKEOVER_BIT, steer: 0 }];
    expect(unpackRecords(packRecords(recs))).toEqual(recs);
  });

  it('registros fora do formato são recusados por inteiro', () => {
    const bad: unknown[] = [
      null, 'x', [], [1, 2, 3], [1.5, 0, 0, 0], [-1, 0, 0, 0], [1, 4, 0, 0], [1, 0, 256, 0], [1, 0, 0, 128],
      [1, 0, 32, 0], // bit que não existe
      [1, 0, TAKEOVER_BIT | 1, 0], // tomada misturada com pedal
      [1, 0, TAKEOVER_BIT, 5], // tomada com volante
      [1, 0, '1', 0],
      new Array(4 * 65).fill(0), // mais registros que o limite por mensagem
    ];
    for (const d of bad) expect(unpackRecords(d), JSON.stringify(d)?.slice(0, 40)).toBeNull();
  });
});

describe('mensagens do servidor', () => {
  it('JSON quebrado, tipo desconhecido e campos faltando viram null', () => {
    for (const raw of ['{', 'null', '[]', '"oi"', '{"t":"nada"}', '{"t":5}', JSON.stringify({ t: 'welcome', room: 'abc', id: 0, token: 'x' })]) {
      expect(parseServerMessage(raw, RULES), raw).toBeNull();
    }
    expect(parseServerMessage(42, RULES)).toBeNull();
    expect(parseServerMessage('x'.repeat(300 * 1024), RULES)).toBeNull();
  });

  it('boas-vindas, erro, pong e evento de par válidos passam', () => {
    expect(parse({ t: 'welcome', room: 'ABCDE', id: 2, token: 'a1b2c3d4e5f6', rejoined: true })).toEqual({ t: 'welcome', room: 'ABCDE', id: 2, token: 'a1b2c3d4e5f6', rejoined: true });
    expect(parse({ t: 'error', code: 'full' })).toEqual({ t: 'error', code: 'full' });
    expect(parse({ t: 'error', code: 'explodiu' })).toBeNull();
    expect(parse({ t: 'pong', n: 7 })).toEqual({ t: 'pong', n: 7 });
    expect(parse({ t: 'peer', id: 1, e: 'lost' })).toEqual({ t: 'peer', id: 1, e: 'lost' });
    expect(parse({ t: 'peer', id: 1, e: 'sumiu' })).toBeNull();
  });

  it('código de sala: 5 letras do alfabeto sem I e O', () => {
    expect(parse({ t: 'welcome', room: 'ABCDI', id: 0, token: 'a1b2c3d4e5f6' })).toBeNull();
    expect(parse({ t: 'welcome', room: 'ABCD', id: 0, token: 'a1b2c3d4e5f6' })).toBeNull();
    expect(normalizeRoomCode(' kx 7qa')).toBeNull();
    expect(normalizeRoomCode(' kx zqa ')).toBe('KXZQA');
  });

  it('sala: info alheia fora do formato não derruba a sala, só some', () => {
    const room = {
      code: 'KXZQA', host: 0, started: false, settings: SETTINGS,
      clients: [
        { id: 0, seats: 1, connected: true, info: { players: [{ name: 'Ana', car: 'falcao' }], ready: true } },
        { id: 1, seats: 2, connected: false, info: { players: [{ name: 'B', car: 'carro_que_nao_existe' }], ready: false } },
      ],
    };
    const m = parse({ t: 'room', room });
    expect(m?.t).toBe('room');
    if (m?.t !== 'room') return;
    expect(m.room.clients[0].info?.players[0]).toEqual({ name: 'Ana', car: 'falcao' });
    expect(m.room.clients[1].info).toBeNull();
    expect(m.room.clients[1].connected).toBe(false);
    expect(m.room.settings).toEqual(SETTINGS);
    // Mais de 4 clientes, ou assentos locais demais, é mentira do servidor.
    expect(parse({ t: 'room', room: { ...room, clients: [...room.clients, ...room.clients, ...room.clients] } })).toBeNull();
    expect(parse({ t: 'room', room: { ...room, clients: [{ ...room.clients[0], seats: 3 }] } })).toBeNull();
  });

  it('nomes perdem caracteres de controle e são cortados', () => {
    const room = { code: 'KXZQA', host: 0, started: false, settings: null, clients: [{ id: 0, seats: 1, connected: true, info: { players: [{ name: ' \u0000Ana\u0007 Maria da Silva Sauro ', car: 'falcao' }], ready: false } }] };
    const m = parse({ t: 'room', room });
    expect(m?.t === 'room' && m.room.clients[0].info?.players[0].name).toBe('Ana Maria da');
  });

  it('largada: pista e carros precisam existir, assentos únicos, no máximo 2 por cliente', () => {
    expect(parse({ t: 'start', from: 0, cfg: START })).toEqual({ t: 'start', from: 0, cfg: START });
    const bad: Array<Partial<StartConfig> & Record<string, unknown>> = [
      { trackId: 'pista_fantasma' },
      { laps: 0 }, { laps: 9 }, { delay: 0 }, { delay: 99 }, { seed: -1 }, { difficulty: 'impossivel' as never },
      { seats: [] },
      { seats: [{ seat: 0, client: 0, name: 'A', car: 'falcao' }, { seat: 0, client: 1, name: 'B', car: 'falcao' }] },
      { seats: [{ seat: 0, client: 0, name: 'A', car: 'falcao' }, { seat: 1, client: 0, name: 'B', car: 'falcao' }, { seat: 2, client: 0, name: 'C', car: 'falcao' }] },
      { seats: [{ seat: 4, client: 0, name: 'A', car: 'falcao' }] },
      { seats: [{ seat: 0, client: 0, name: '   ', car: 'falcao' }] },
      { totalCars: 1 },
      { assists: { sharedNitro: true } as never },
    ];
    for (const patch of bad) expect(parse({ t: 'start', from: 0, cfg: { ...START, ...patch } }), JSON.stringify(patch)).toBeNull();
    expect(parse({ t: 'start', cfg: START })).toBeNull(); // sem remetente
  });

  it('entradas e hash repassados', () => {
    expect(parse({ t: 'i', from: 1, d: [5, 1, 1, -20] })).toEqual({ t: 'i', from: 1, records: [{ tick: 5, seat: 1, bits: 1, steer: -20 }] });
    expect(parse({ t: 'i', from: 1, d: [5, 1, 1] })).toBeNull();
    expect(parse({ t: 'h', from: 1, k: 60, h: 4294967295 })).toEqual({ t: 'h', from: 1, k: 60, h: 4294967295 });
    expect(parse({ t: 'h', from: 1, k: 60, h: -1 })).toBeNull();
  });

  it('snapshot: estado em texto, configuração válida, buffer e tomadas conferidos', () => {
    const snap = { tick: 600, state: '{}', start: START, inputs: [600, 1, 1, 0, 601, 1, 1, 0], ai: [2, 580] };
    expect(parse({ t: 'snap', from: 0, snap })).toEqual({ t: 'snap', from: 0, snap });
    expect(parse({ t: 'snap', from: 0, snap: { ...snap, inputs: [1, 2] } })).toBeNull();
    expect(parse({ t: 'snap', from: 0, snap: { ...snap, ai: [9, 1] } })).toBeNull();
    expect(parse({ t: 'snap', from: 0, snap: { ...snap, state: 7 } })).toBeNull();
    // Buffer grande (mais que um pacote de entradas) passa.
    const big = Array.from({ length: 200 }, (_, i) => [600 + i, 1, 1, 0]).flat();
    expect(parse({ t: 'snap', from: 0, snap: { ...snap, inputs: big } })).not.toBeNull();
  });
});

describe('endereço do servidor', () => {
  it('aceita host:porta, http(s) e ws(s); recusa o resto', () => {
    expect(normalizeServerUrl('localhost:8787')).toBe('ws://localhost:8787');
    expect(normalizeServerUrl(' ws://10.0.0.5:8787 ')).toBe('ws://10.0.0.5:8787');
    expect(normalizeServerUrl('https://relay.exemplo.com.br/nc')).toBe('wss://relay.exemplo.com.br/nc');
    expect(normalizeServerUrl('WSS://Relay.Exemplo.com')).toBe('wss://Relay.Exemplo.com');
    for (const bad of ['', '   ', 'ftp://x', 'ws://', 'com espaço', 42, null, 'x'.repeat(300)]) expect(normalizeServerUrl(bad)).toBeNull();
  });
});
