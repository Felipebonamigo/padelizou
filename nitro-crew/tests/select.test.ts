// Partes puras das telas de copas e de pistas (src/ui/screens/select.ts): status da copa, onde o
// cursor começa, dificuldade média e a regra que a grade de pistas usa para ↑↓ trocarem de copa.
import { describe, expect, it } from 'vitest';
import { CUPS } from '../src/core/data/cups';
import { TRACKS } from '../src/core/track/tracks';
import type { TrackDef } from '../src/core/types';
import { DEFAULT_SAVE } from '../src/game/contracts';
import { isCupUnlocked } from '../src/game/save';
import { cupDifficulty, cupStatus, cupTracks, frontierCupIndex, TRACK_GRID_COLS } from '../src/ui/screens/select';

const ctxWith = (cupsCompleted: string[]) => {
  const save = { ...DEFAULT_SAVE, cupsCompleted };
  return { save, isCupUnlocked: (id: string) => isCupUnlocked(save, id, CUPS) };
};

describe('tela de copas', () => {
  it('status: concluída, aberta (a próxima da fila) e travada (o resto)', () => {
    const ctx = ctxWith(['brasil', 'eua']);
    expect(CUPS.map((c) => cupStatus(ctx, c))).toEqual(['done', 'done', 'open', 'locked', 'locked', 'locked', 'locked', 'locked']);
    expect(CUPS.map((c) => cupStatus(ctxWith([]), c))).toEqual(['open', ...new Array(CUPS.length - 1).fill('locked')]);
  });

  it('o cursor começa na primeira copa aberta; com tudo concluído, na última', () => {
    expect(frontierCupIndex(['open', 'locked', 'locked'])).toBe(0);
    expect(frontierCupIndex(['done', 'done', 'open', 'locked'])).toBe(2);
    expect(frontierCupIndex(['done', 'done', 'done'])).toBe(2);
    expect(frontierCupIndex([])).toBe(0);
    const all = CUPS.map((c) => c.id);
    expect(frontierCupIndex(CUPS.map((c) => cupStatus(ctxWith(all.slice(0, 4)), c)))).toBe(4);
    expect(frontierCupIndex(CUPS.map((c) => cupStatus(ctxWith(all), c)))).toBe(CUPS.length - 1);
  });

  it('dificuldade da copa: média das pistas, arredondada; copa vazia é 0', () => {
    const d = (n: number) => ({ difficulty: n }) as TrackDef;
    expect(cupDifficulty([d(1), d(1), d(2), d(2)])).toBe(2);
    expect(cupDifficulty([d(3), d(4), d(4), d(5)])).toBe(4);
    expect(cupDifficulty([])).toBe(0);
  });

  it('pistas da copa na ordem da copa, ignorando id desconhecido', () => {
    const brasil = CUPS[0];
    expect(cupTracks({ tracks: TRACKS }, brasil).map((d) => d.id)).toEqual(brasil.trackIds);
    expect(cupTracks({ tracks: TRACKS }, { ...brasil, trackIds: ['copacabana', 'nao_existe'] }).map((d) => d.id)).toEqual(['copacabana']);
  });
});

describe('tela de pistas', () => {
  it('a grade tem uma copa por linha: toda copa tem exatamente TRACK_GRID_COLS pistas', () => {
    // Se uma copa tiver pista a mais ou a menos, ↑↓ deixam de trocar de copa na mesma coluna.
    for (const c of CUPS) expect(cupTracks({ tracks: TRACKS }, c), c.id).toHaveLength(TRACK_GRID_COLS);
  });
});
