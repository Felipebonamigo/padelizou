// Save de antes da onda G (02/10): a Copa Brasil (copacabana, transpantaneira, serra_do_mar, sampa_noite) deixou de
// existir e virou a Expedição Brasil — 27 copas de estado (br_<uf>) — e o Mundial. Quem já jogava tem de continuar
// abrindo o jogo sem erro e sem perder o que conquistou (docs/PISTAS.md, "Migração").
import { describe, expect, it } from 'vitest';
import { settleCareerRace } from '../src/core/career';
import { applyRaceResult, createChampionship, nextTrackId } from '../src/core/championship';
import { POINTS_TABLE } from '../src/core/constants';
import { CUPS, cupDef, currentCupId, stageCups } from '../src/core/data/cups';
import { AI_TEAM_ID_BASE } from '../src/core/data/drivers';
import { getTrack, trackDef } from '../src/core/track';
import type { RaceResultRow } from '../src/core/types';
import { lapFingerprint } from '../src/game/content-version';
import type { SaveData } from '../src/game/contracts';
import { GHOST_SAMPLE_TICKS, makeGhostRecord, traceEnd } from '../src/game/ghost';
import { ghostFor, sanitizeGhostStore } from '../src/game/ghost-store';
import { isCupUnlocked, markCupCompleted, recordFromOtherVersion, sanitizeSave } from '../src/game/save';

/** Save como o jogo gravava até a onda F: copa `brasil`, conquista COPA_BRASIL, carreira e copa salvas nela. */
function oldSave(): Record<string, unknown> {
  const humans = [{ seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: '#ffd23f' }];
  // O createChampionship de hoje recusa 'brasil'; o save antigo é JSON puro, montado à mão como era gravado.
  const champ = (raceIndex: number) => ({
    cupId: 'brasil', raceIndex, standings: [{ key: 'seat:0', name: 'Ana', seat: 0, teamId: 0, points: 20, wins: 1, positions: [1, 0, 0, 0] }],
    teams: [], coop: false, eliminated: false, completed: false, lastRace: null, lastVerdict: 'qualified',
  });
  return {
    cupsCompleted: ['brasil', 'eua', 'japao'],
    achievements: ['PRIMEIRA_VITORIA', 'COPA_BRASIL', 'COPA_EUA', 'COPA_JAPAO'],
    bestLaps: { copacabana: { ticks: 3900, name: 'Ana', carId: 'falcao', date: '2026-09-30T10:00:00.000Z' } },
    bestRaces: { 'sampa_noite:4': { ticks: 16000, name: 'Ana', carId: 'falcao', date: '2026-09-30T10:00:00.000Z' } },
    racesRun: 40, racesWon: 12,
    career: {
      version: 1, coop: false, drivers: [{ name: 'Ana', garage: { carId: 'falcao', owned: [], upgrades: { falcao: { engine: 2, turbo: 1, tires: 0, brakes: 0, tank: 0, nitro: 0 } } }, earnings: 30000 }],
      wallets: [5200], cupId: 'brasil', champ: champ(1), rosterSeed: 77, attempts: 2, completed: false, racesRun: 9, lastReport: null,
    },
    cupInProgress: { champ: champ(2), cupSeed: 99, humans },
  };
}

describe('save antigo com a Copa Brasil', () => {
  it('abre sem erro; a copa concluída vira a Copa Rio de Janeiro e o carimbo do RJ', () => {
    const s = sanitizeSave(oldSave());
    expect(s.cupsCompleted).toEqual(['br_rj', 'eua', 'japao']);
    expect(s.stamps).toEqual(['RJ']);
    // Recordes e contagens são por pista: nada muda.
    expect(s.bestLaps.copacabana.ticks).toBe(3900);
    expect(s.bestRaces['sampa_noite:4'].ticks).toBe(16000);
    expect(s.racesRun).toBe(40);
  });

  it('a conquista COPA_BRASIL vira COPA_BR_RJ (a que existe hoje), sem duplicar', () => {
    const s = sanitizeSave(oldSave());
    expect(s.achievements).toContain('COPA_BR_RJ');
    expect(s.achievements).not.toContain('COPA_BRASIL');
    expect(sanitizeSave({ achievements: ['COPA_BRASIL', 'COPA_BR_RJ'] }).achievements).toEqual(['COPA_BR_RJ']);
  });

  it('carreira na Copa Brasil segue na Copa Rio de Janeiro, com o dinheiro e a garagem; a copa em andamento recomeça', () => {
    const s = sanitizeSave(oldSave());
    expect(s.career).not.toBeNull();
    const c = s.career!;
    expect(c.cupId).toBe('br_rj');
    expect(c.champ).toBeNull();
    expect(c.wallets).toEqual([5200]);
    expect(c.drivers[0].garage.upgrades.falcao.engine).toBe(2);
    expect(c.attempts).toBe(1);
  });

  it('campeonato normal em andamento na Copa Brasil: some do save (as corridas não batem mais), sem erro', () => {
    expect(sanitizeSave(oldSave()).cupInProgress).toBeNull();
  });

  it('copa já concluída continua aberta, mesmo que a anterior na fila nova não esteja: o Mundial de quem já o jogava', () => {
    const s = sanitizeSave(oldSave());
    // eua exige br_to (fim da Expedição), que esse jogador nunca correu — mas ele já tinha vencido a eua.
    expect(isCupUnlocked(s, 'eua', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'japao', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'europa', CUPS)).toBe(true); // exige japao, concluída
    expect(isCupUnlocked(s, 'africa_do_sul', CUPS)).toBe(false);
    // Na Expedição ele segue do RJ (carimbado) para São Paulo.
    expect(isCupUnlocked(s, 'br_sp', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'br_mg', CUPS)).toBe(false);
  });
});

// ───────────────────────────── Saves de antes da onda G, um por situação ─────────────────────────────
// O formato é o de `SaveData`/`CareerState` até 954bb92^ (git show 954bb92^:nitro-crew/src/game/contracts.ts): sem
// `stamps`, recordes com `fp` (onda E), copas da lista antiga de 8 — brasil, eua, …, mediterraneo, cada uma exigindo
// a anterior. Nada aqui passa pelas funções de hoje antes do sanitizeSave: é o JSON como estava no disco.

const OLD_CUPS: Readonly<Record<string, readonly string[]>> = {
  brasil: ['copacabana', 'transpantaneira', 'serra_do_mar', 'sampa_noite'],
  eua: ['rota_66', 'rochosas', 'canion', 'las_vegas'],
  japao: ['baia_toquio', 'yanbaru', 'monte_fuji', 'osaka_neon'],
  europa: ['autobahn', 'paris', 'passo_alpino', 'monaco_noite'],
  africa_do_sul: ['kruger', 'karoo', 'drakensberg', 'boa_esperanca'],
  australia: ['outback', 'great_ocean', 'daintree', 'sydney'],
  escandinavia: ['atlantico', 'laponia', 'trollstigen', 'tromso'],
  mediterraneo: ['amalfi', 'santorini', 'etna', 'roma'],
};
const OLD_ORDER = Object.keys(OLD_CUPS);
const OLD_TRACKS = OLD_ORDER.flatMap((c) => OLD_CUPS[c]);

/** O isCupUnlocked de antes da onda G: aberta a primeira e a que vem depois de uma concluída. */
function openedBefore(done: readonly string[]): string[] {
  return OLD_ORDER.filter((c, i) => i === 0 || done.includes(OLD_ORDER[i - 1]));
}

const ANA = { seat: 0, name: 'Ana', carId: 'falcao', teamId: 0, color: '#ffd23f' };
const ana = () => [{ ...ANA }];

/** Copa (ChampionshipState) como era gravada: Ana e dois da IA com os pontos das corridas já feitas. */
function oldChamp(cupId: string, raceIndex: number): Record<string, unknown> {
  const pos = (p: number) => Array.from({ length: 4 }, (_, i) => (i < raceIndex ? p : 0));
  return {
    cupId, raceIndex, coop: false, eliminated: false, completed: false, lastVerdict: raceIndex > 0 ? 'qualified' : null,
    standings: [
      { key: 'Zé Turbo', name: 'Zé Turbo', seat: -1, teamId: 100, points: 20 * raceIndex, wins: raceIndex, positions: pos(1) },
      { key: 'seat:0', name: 'Ana', seat: 0, teamId: 0, points: 15 * raceIndex, wins: 0, positions: pos(2) },
    ],
    teams: [{ teamId: 100, name: 'Equipe Trovoada', points: 20 * raceIndex, isHuman: false }, { teamId: 0, name: 'Ana', points: 15 * raceIndex, isHuman: true }],
    lastRace: null,
  };
}

function oldCareer(over: Record<string, unknown>): Record<string, unknown> {
  return {
    version: 1, coop: false, wallets: [8400], rosterSeed: 1234, attempts: 1, completed: false, racesRun: 4,
    drivers: [{ name: 'Ana', earnings: 21000, garage: { carId: 'curupira', owned: ['curupira'], upgrades: { curupira: { engine: 1, turbo: 1, tires: 0, brakes: 1, tank: 0, nitro: 0 }, falcao: { engine: 1, turbo: 0, tires: 0, brakes: 0, tank: 1, nitro: 0 } } } }],
    cupId: 'eua', champ: null, lastReport: null,
    ...over,
  };
}

const oldRecord = (ticks: number) => ({ ticks, name: 'Ana', carId: 'falcao', date: '2026-09-29T21:00:00.000Z', fp: '0badf00d' });

/** O save inteiro, como o jogo gravava até a onda F. */
function preG(over: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    cupsCompleted: [], bestLaps: {}, bestRaces: {}, achievements: [], racesRun: 0, racesWon: 0,
    seatNames: ['Ana', 'P2', 'P3', 'P4'], seatCars: ['falcao', 'trovao', 'tornado', 'camelo'],
    stats: { totals: {}, players: [] }, carsUnlocked: [], career: null, cupInProgress: null, tutorialDone: true,
    ...over,
  };
}

/** Resultado de corrida com Ana na posição dada e a IA no resto (20 carros). */
function resultsWith(position: number): RaceResultRow[] {
  const rows: RaceResultRow[] = [];
  let ai = 0;
  for (let p = 1; p <= 20; p++) {
    if (p === position) rows.push({ carId: 19, seat: 0, name: 'Ana', teamId: 0, carDefId: 'falcao', position: p, finished: true, totalTicks: 20000, bestLapTicks: 5000, points: POINTS_TABLE[p - 1] ?? 0 });
    else { rows.push({ carId: ai, seat: -1, name: `IA${ai}`, teamId: AI_TEAM_ID_BASE + Math.floor(ai / 2), carDefId: 'falcao', position: p, finished: true, totalTicks: 20000, bestLapTicks: 5000, points: POINTS_TABLE[p - 1] ?? 0 }); ai++; }
  }
  return rows;
}

/** Ida e volta pelo disco: o que a próxima abertura do jogo lê depois de o save migrado ser gravado. */
const reopened = (s: SaveData): SaveData => sanitizeSave(JSON.parse(JSON.stringify(s)));

describe('save de antes da onda G: ninguém perde o que tinha aberto', () => {
  it('com 0 a 8 copas concluídas na ordem antiga, toda copa que estava aberta continua aberta (e continua depois de gravar e reabrir)', () => {
    for (let k = 0; k <= OLD_ORDER.length; k++) {
      const done = OLD_ORDER.slice(0, k);
      const s = sanitizeSave(preG({ cupsCompleted: done }));
      for (const save of [s, reopened(s)]) {
        for (const old of openedBefore(done)) {
          expect(isCupUnlocked(save, currentCupId(old), CUPS), `${k} copas concluídas (${done.join(',') || 'nenhuma'}): ${old} estava aberta`).toBe(true);
        }
      }
    }
  });

  it('quem só tinha vencido a Copa Brasil segue com os EUA abertos — sem precisar da Expedição inteira (br_to)', () => {
    const s = sanitizeSave(preG({ cupsCompleted: ['brasil'], achievements: ['COPA_BRASIL'] }));
    expect(isCupUnlocked(s, 'eua', CUPS)).toBe(true);
    expect(isCupUnlocked(reopened(s), 'eua', CUPS)).toBe(true);
    // Sem presente: o que estava fechado continua fechado.
    expect(isCupUnlocked(s, 'japao', CUPS)).toBe(false);
    expect(isCupUnlocked(s, 'br_mg', CUPS)).toBe(false);
    // Quem nunca venceu a Copa Brasil não ganha os EUA por isso.
    expect(isCupUnlocked(sanitizeSave(preG()), 'eua', CUPS)).toBe(false);
    // A Expedição de hoje não abre os EUA: só a herança da copa antiga.
    expect(isCupUnlocked(sanitizeSave({ cupsCompleted: ['br_rj'], stamps: ['RJ'] }), 'eua', CUPS)).toBe(false);
    // Lixo não lança nem abre nada: nome de propriedade do Object como copa, copa que não existe, não-string.
    const junk = sanitizeSave({ cupsCompleted: ['constructor', '__proto__', 'toString'], cupsUnlocked: ['eua', 'xx', 3, 'eua'] });
    expect(junk.cupsUnlocked).toEqual(['eua']);
    expect(sanitizeSave({ cupsCompleted: ['constructor'] }).cupsUnlocked).toEqual([]);
  });

  it('quem tinha vencido as 8 copas fica com o Mundial inteiro aberto e concluído, e a Expedição segue de São Paulo', () => {
    const s = sanitizeSave(preG({ cupsCompleted: OLD_ORDER, achievements: OLD_ORDER.map((c) => `COPA_${c.toUpperCase()}`) }));
    for (const c of stageCups('mundial')) {
      expect(isCupUnlocked(s, c.id, CUPS), c.id).toBe(true);
      expect(s.cupsCompleted, c.id).toContain(c.id);
      expect(s.achievements, c.id).toContain(`COPA_${c.id.toUpperCase()}`);
    }
    expect(s.stamps).toEqual(['RJ']);
    expect(isCupUnlocked(s, 'br_sp', CUPS)).toBe(true);
    expect(isCupUnlocked(s, 'br_mg', CUPS)).toBe(false);
  });

  it('campeonato no meio dos EUA (Copa Brasil vencida): continua da corrida em que parou e, eliminado, pode recomeçar a copa', () => {
    const s = sanitizeSave(preG({ cupsCompleted: ['brasil'], cupInProgress: { champ: oldChamp('eua', 2), cupSeed: 4242, humans: ana() } }));
    const saved = s.cupInProgress;
    expect(saved?.champ.cupId).toBe('eua');
    expect(saved?.champ.raceIndex).toBe(2);
    expect(saved?.cupSeed).toBe(4242);
    if (!saved) return;
    expect(nextTrackId(saved.champ)).toBe('canion');
    applyRaceResult(saved.champ, resultsWith(3), saved.humans);
    expect(nextTrackId(saved.champ)).toBe('las_vegas');
    expect(saved.champ.standings.find((r) => r.key === 'seat:0')?.points).toBe(15 * 2 + POINTS_TABLE[2]);
    // Eliminado na última: a copa sai do save — e tem de continuar aberta para ele tentar de novo.
    applyRaceResult(saved.champ, resultsWith(12), saved.humans);
    expect(saved.champ.eliminated).toBe(true);
    expect(isCupUnlocked(s, 'eua', CUPS)).toBe(true);
  });

  it('campeonato no meio do Japão (Brasil e EUA vencidas): continua e, vencido, abre a Europa', () => {
    const s = sanitizeSave(preG({ cupsCompleted: ['brasil', 'eua'], cupInProgress: { champ: oldChamp('japao', 3), cupSeed: 7, humans: ana() } }));
    const saved = s.cupInProgress;
    expect(saved?.champ.raceIndex).toBe(3);
    if (!saved) return;
    expect(nextTrackId(saved.champ)).toBe('osaka_neon');
    applyRaceResult(saved.champ, resultsWith(1), saved.humans);
    expect(saved.champ.completed).toBe(true);
    markCupCompleted(s, 'japao');
    expect(s.stamps).toEqual(['RJ']);
    expect(isCupUnlocked(s, 'europa', CUPS)).toBe(true);
  });

  it('carreira no meio de uma copa do Mundial: mesma copa, mesma corrida, dinheiro e garagem; as corridas seguem sem erro', () => {
    const s = sanitizeSave(preG({ cupsCompleted: ['brasil', 'eua'], career: oldCareer({ cupId: 'japao', champ: oldChamp('japao', 1), racesRun: 9 }) }));
    const c = s.career;
    expect(c?.cupId).toBe('japao');
    expect(c?.champ?.raceIndex).toBe(1);
    expect(c?.wallets).toEqual([8400]);
    expect(c?.drivers[0].garage.carId).toBe('curupira');
    expect(c?.drivers[0].garage.upgrades.curupira.engine).toBe(1);
    if (!c) return;
    let report = settleCareerRace(c, resultsWith(2)).report;
    expect(report.trackId).toBe('yanbaru');
    expect(c.wallets[0]).toBeGreaterThan(8400);
    settleCareerRace(c, resultsWith(2));
    report = settleCareerRace(c, resultsWith(2)).report;
    expect(report.cupCompleted).toBe(true);
    expect(c.cupId).toBe('europa');
    // Carreira concluída antes da onda G (no Mediterrâneo, a última de antes e de hoje) continua concluída.
    expect(sanitizeSave(preG({ career: oldCareer({ cupId: 'mediterraneo', completed: true }) })).career?.completed).toBe(true);
  });

  it('nenhum id de copa que deixou de existir sobra no save migrado (a garagem e o menu mostram o nome de cada uma)', () => {
    // Carreira que acabou de vencer a Copa Brasil (o relatório da garagem é da última corrida dela) e já está nos EUA;
    // e uma carreira que estava na própria Copa Brasil.
    const brasilReport = { cupId: 'brasil', trackId: 'sampa_noite', raceIndex: 3, rows: [{ driver: 0, position: 2, prize: 4500 }], teamBonus: 0, teamRank: 0, verdict: 'qualified', cupCompleted: true, careerCompleted: false };
    const saves = [
      preG({ cupsCompleted: ['brasil'], career: oldCareer({ cupId: 'eua', lastReport: brasilReport }) }),
      preG({ career: oldCareer({ cupId: 'brasil', champ: oldChamp('brasil', 2), lastReport: { ...brasilReport, raceIndex: 1, trackId: 'transpantaneira', cupCompleted: false } }) }),
      preG({ cupsCompleted: ['brasil'], cupInProgress: { champ: oldChamp('brasil', 1), cupSeed: 1, humans: ana() } }),
    ];
    const known = new Set(CUPS.map((c) => c.id));
    for (const raw of saves) {
      const s = sanitizeSave(raw);
      const ids = [
        ...s.cupsCompleted, ...s.cupsUnlocked, s.career?.cupId, s.career?.champ?.cupId, s.career?.lastReport?.cupId,
        s.cupInProgress?.champ.cupId,
      ].filter((x): x is string => x !== undefined);
      for (const id of ids) expect(known.has(id), `copa "${id}" no save migrado`).toBe(true);
    }
  });

  it('recordes e fantasmas das 32 pistas de antes ficam: os recordes com o tempo, os fantasmas na loja', () => {
    const bestLaps = Object.fromEntries(OLD_TRACKS.map((id, i) => [id, oldRecord(4000 + i)]));
    const bestRaces = Object.fromEntries(OLD_TRACKS.map((id, i) => [`${id}:${trackDef(id).laps}`, oldRecord(16000 + i)]));
    const s = reopened(sanitizeSave(preG({ bestLaps, bestRaces })));
    OLD_TRACKS.forEach((id, i) => {
      expect(s.bestLaps[id]?.ticks, id).toBe(4000 + i);
      expect(s.bestRaces[`${id}:${trackDef(id).laps}`]?.ticks, id).toBe(16000 + i);
      // Impressão de outra versão: o recorde fica, com a marca "versão anterior" (docs/FANTASMA.md).
      expect(recordFromOtherVersion(s.bestLaps[id], id), id).toBe(true);
    });

    // Fantasma: a loja é por id de pista, e nenhum id mudou. O de uma pista que a onda G não tocou continua valendo
    // como rival; o de Sampa, não — as praças (core/track/plazas.ts) tiraram prédios da beira, a colisão mudou.
    const ghostOf = (trackId: string, fp: string | null) => {
      const length = getTrack(trackId).length;
      const n = 400;
      const samples = Array.from({ length: n }, (_, i) => ({ z: 8 + ((length - 16) * i) / (n - 1), x: 0, speed: 6000, steer: 0, nitro: false }));
      const trace = { sampleTicks: GHOST_SAMPLE_TICKS, t0: 1, samples };
      return makeGhostRecord(trackId, traceEnd(trace), trace, { name: 'Ana', carId: 'falcao' }, '2026-09-29T21:00:00.000Z', fp);
    };
    const copa = ghostOf('copacabana', lapFingerprint('copacabana', 'falcao'));
    const sampa = ghostOf('sampa_noite', '0badf00d');
    const store = sanitizeGhostStore(JSON.parse(JSON.stringify({ ghosts: { copacabana: { ...copa, savedAt: 'a' }, sampa_noite: { ...sampa, savedAt: 'b' } } })));
    expect(Object.keys(store.ghosts).sort()).toEqual(['copacabana', 'sampa_noite']);
    expect(ghostFor(store, 'copacabana')?.ticks).toBe(copa?.ticks);
    expect(ghostFor(store, 'sampa_noite')).toBeNull();
  });
});

describe('passaporte: carimbos dos estados', () => {
  it('vencer a copa de um estado carimba o passaporte; copa do Mundial não carimba', () => {
    const s = sanitizeSave({});
    expect(s.stamps).toEqual([]);
    markCupCompleted(s, 'br_sp');
    markCupCompleted(s, 'eua');
    expect(s.stamps).toEqual(['SP']);
    markCupCompleted(s, 'br_sp');
    expect(s.stamps).toEqual(['SP']);
  });

  it('o save conserta os carimbos: lixo sai, e copa de estado concluída sem carimbo ganha o seu', () => {
    const s = sanitizeSave({ stamps: ['RJ', 'XX', 3, 'RJ', 'ba'], cupsCompleted: ['br_pe'] });
    expect(s.stamps).toEqual(['RJ', 'PE']);
    expect(sanitizeSave(JSON.parse(JSON.stringify(s))).stamps).toEqual(['RJ', 'PE']);
  });

  it('a copa legada não existe mais para começar um campeonato', () => {
    expect(() => cupDef('brasil')).toThrow();
    expect(() => createChampionship('brasil', [])).toThrow();
  });
});
