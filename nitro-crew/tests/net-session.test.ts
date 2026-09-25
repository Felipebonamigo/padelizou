// Partes puras da sessão online: numeração dos assentos globais e a corrida montada da largada.
import { describe, expect, it } from 'vitest';
import { SEAT_COLORS } from '../src/core/data/drivers';
import { DEFAULT_SETTINGS } from '../src/game/contracts';
import { CONTENT_FINGERPRINT, assignSeats, contentFingerprint, raceConfigFrom, seatName } from '../src/game/online-session';
import { sanitizeSettings } from '../src/game/settings';
import type { RoomView, StartConfig } from '../src/net/protocol';

function room(clients: Array<{ id: number; players: Array<[string, string]>; connected?: boolean }>): RoomView {
  return {
    code: 'ABCDE', host: clients[0]?.id ?? 0, started: false, settings: null,
    clients: clients.map((c) => ({ id: c.id, seats: c.players.length, connected: c.connected ?? true, info: { ready: true, players: c.players.map(([name, car]) => ({ name, car })) } })),
  };
}

describe('assentos globais', () => {
  it('seguem a ordem dos computadores (id no relay) e dos jogadores de cada um', () => {
    const seats = assignSeats(room([
      { id: 3, players: [['Caio', 'tornado']] },
      { id: 0, players: [['Ana', 'falcao'], ['Bia', 'trovao']] },
    ]));
    expect(seats).toEqual([
      { seat: 0, client: 0, name: 'Ana', car: 'falcao' },
      { seat: 1, client: 0, name: 'Bia', car: 'trovao' },
      { seat: 2, client: 3, name: 'Caio', car: 'tornado' },
    ]);
  });

  it('nunca passam de 4', () => {
    const seats = assignSeats(room([
      { id: 0, players: [['A', 'falcao'], ['B', 'falcao']] },
      { id: 1, players: [['C', 'falcao'], ['D', 'falcao']] },
      { id: 2, players: [['E', 'falcao']] },
    ]));
    expect(seats.map((s) => s.seat)).toEqual([0, 1, 2, 3]);
  });

  it('nome padrão (P1…P4) acompanha o assento recebido; nome escolhido fica como está', () => {
    // Cada computador começa com "P1": o convidado no assento 2 não pode aparecer como P1.
    const seats = assignSeats(room([
      { id: 0, players: [['P1', 'falcao']] },
      { id: 1, players: [['P1', 'trovao'], ['P2', 'tornado']] },
      { id: 2, players: [['Pedro', 'camelo']] },
    ]));
    expect(seats.map((s) => s.name)).toEqual(['P1', 'P2', 'P3', 'Pedro']);
    expect(seatName('P1', 3)).toBe('P4');
    expect(seatName('p1', 3)).toBe('p1');
    expect(seatName('P12', 0)).toBe('P12');
  });
});

describe('corrida da largada', () => {
  it('todos os computadores montam a mesma configuração, com a cor do assento', () => {
    const cfg: StartConfig = {
      trackId: 'copacabana', laps: 2, versus: true, difficulty: 'campeao', totalCars: 1, manualGear: true,
      assists: { sharedNitro: false, tow: true, teamDraft: false, catchup: true }, delay: 4, seed: 1234,
      seats: [{ seat: 0, client: 0, name: 'Ana', car: 'falcao' }, { seat: 1, client: 5, name: 'Bia', car: 'trovao' }],
    };
    const rc = raceConfigFrom(cfg);
    expect(rc).toMatchObject({ trackId: 'copacabana', laps: 2, difficulty: 'campeao', manualGear: true, seed: 1234, totalCars: 2 });
    expect(rc.humans).toEqual([
      { seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: SEAT_COLORS[0] },
      { seat: 1, name: 'Bia', carId: 'trovao', teamId: 1, color: SEAT_COLORS[1] },
    ]);
    expect(raceConfigFrom({ ...cfg, versus: false }).humans.map((h) => h.teamId)).toEqual([0, 0]);
    expect(JSON.stringify(raceConfigFrom(cfg))).toBe(JSON.stringify(raceConfigFrom(JSON.parse(JSON.stringify(cfg)) as StartConfig)));
  });
});

describe('impressão do conteúdo (vai no create/join)', () => {
  it('igual para o mesmo conteúdo, qualquer que seja a ordem das chaves; muda com uma pista nova ou um número da física', () => {
    const base = { constants: { TICK_RATE: 60, GRIP: 0.8 }, cars: [{ id: 'falcao', topSpeed: 100 }], tracks: [{ id: 'copacabana' }] };
    const fp = contentFingerprint(base);
    expect(fp).toMatch(/^[0-9a-f]{8}$/);
    expect(contentFingerprint({ tracks: [{ id: 'copacabana' }], cars: [{ topSpeed: 100, id: 'falcao' }], constants: { GRIP: 0.8, TICK_RATE: 60 } })).toBe(fp);
    expect(contentFingerprint({ ...base, tracks: [{ id: 'copacabana' }, { id: 'pista_nova' }] })).not.toBe(fp);
    expect(contentFingerprint({ ...base, constants: { TICK_RATE: 60, GRIP: 0.81 } })).not.toBe(fp);
    expect(CONTENT_FINGERPRINT).toMatch(/^[0-9a-f]{8}$/);
  });
});

describe('endereço do servidor nas opções', () => {
  it('padrão ws://localhost:8787; salvo é normalizado; lixo volta ao padrão', () => {
    expect(DEFAULT_SETTINGS.serverUrl).toBe('ws://localhost:8787');
    expect(sanitizeSettings({}).serverUrl).toBe('ws://localhost:8787');
    expect(sanitizeSettings({ serverUrl: '192.168.0.10:8787' }).serverUrl).toBe('ws://192.168.0.10:8787');
    expect(sanitizeSettings({ serverUrl: 'https://relay.exemplo.com.br' }).serverUrl).toBe('wss://relay.exemplo.com.br');
    for (const bad of ['javascript:alert(1)', 'file:///etc/passwd', '', 42, null, 'ws://a b']) {
      expect(sanitizeSettings({ serverUrl: bad }).serverUrl).toBe('ws://localhost:8787');
    }
  });
});
