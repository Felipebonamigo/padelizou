// Telas da Expedição Brasil (onda G): copas em duas etapas (as 5 regiões e o Mundial), grade de pistas com copas
// de 3 e de 4 pistas, passaporte com os 27 carimbos e o cartão-postal de cada estado. Só as partes puras das telas.
import { afterEach, describe, expect, it } from 'vitest';
import { BRAZIL_REGIONS, CUPS } from '../src/core/data/cups';
import { TRACKS } from '../src/core/track/tracks';
import { DEFAULT_SAVE } from '../src/game/contracts';
import { isCupUnlocked } from '../src/game/save';
import { setLanguage, t } from '../src/i18n';
import '../src/i18n/core';
import { raggedGridMove, trackName } from '../src/ui/screens/common';
import { landmarkName, passportRows, postcardFrom, stateLandmarks } from '../src/ui/screens/passport';
import { cupTabs, startTab, trackGridRows } from '../src/ui/screens/select';

afterEach(() => setLanguage('pt'));

describe('grade de linhas desiguais (copas de 3 e de 4 pistas)', () => {
  // Linhas: copa de 4, copa de 3, copa de 4. Índices: 0–3 | 4–6 | 7–10.
  const rows = [4, 3, 4];
  it('↓ da 4ª coluna para uma copa de 3 para na 3ª (não pula para a copa seguinte)', () => {
    expect(raggedGridMove(rows, 3, 'down')).toBe(6);
    expect(raggedGridMove(rows, 6, 'down')).toBe(9);
    expect(raggedGridMove(rows, 1, 'down')).toBe(5);
  });
  it('↑ mantém a coluna quando cabe e prende na última quando não', () => {
    expect(raggedGridMove(rows, 10, 'up')).toBe(6);
    expect(raggedGridMove(rows, 5, 'up')).toBe(1);
    expect(raggedGridMove(rows, 2, 'up')).toBe(2);
  });
  it('← → andam só dentro da linha; ↓ na última linha não sai do lugar', () => {
    expect(raggedGridMove(rows, 6, 'right')).toBe(6);
    expect(raggedGridMove(rows, 4, 'left')).toBe(4);
    expect(raggedGridMove(rows, 5, 'right')).toBe(6);
    expect(raggedGridMove(rows, 8, 'down')).toBe(8);
  });
  it('a grade de pistas tem uma linha por copa, do tamanho real dela', () => {
    const lens = trackGridRows({ tracks: TRACKS }, CUPS);
    expect(lens).toHaveLength(CUPS.length);
    expect(lens.reduce((a, b) => a + b, 0)).toBe(CUPS.reduce((a, c) => a + c.trackIds.length, 0));
    expect(lens[0]).toBe(3);
    expect(lens[lens.length - 1]).toBe(4);
  });
});

describe('copas em duas etapas', () => {
  it('cinco abas de região da Expedição Brasil, na ordem, e o Mundial por último', () => {
    const tabs = cupTabs(CUPS);
    expect(tabs.map((x) => x.id)).toEqual([...BRAZIL_REGIONS.map((r) => r.id), 'mundial']);
    for (const [i, r] of BRAZIL_REGIONS.entries()) expect(tabs[i].cups.map((c) => c.state)).toEqual(r.states);
    expect(tabs[5].cups.every((c) => c.stage === 'mundial')).toBe(true);
    expect(tabs.reduce((a, x) => a + x.cups.length, 0)).toBe(CUPS.length);
  });

  it('a tela abre na aba e na copa da fronteira do jogador', () => {
    const tabs = cupTabs(CUPS);
    const at = (done: string[]) => {
      const save = { ...DEFAULT_SAVE, cupsCompleted: done };
      return startTab(tabs, { save, isCupUnlocked: (id) => isCupUnlocked(save, id, CUPS) });
    };
    expect(at([])).toEqual({ tab: 0, cup: 0 });
    expect(at(['br_rj', 'br_sp', 'br_mg', 'br_es'])).toEqual({ tab: 1, cup: 0 });
    expect(at(['br_rj', 'br_sp', 'br_mg', 'br_es', 'br_pr'])).toEqual({ tab: 1, cup: 1 });
    expect(at(CUPS.filter((c) => c.stage === 'brasil').map((c) => c.id))).toEqual({ tab: 5, cup: 0 });
    expect(at(CUPS.map((c) => c.id))).toEqual({ tab: 5, cup: 6 });
  });
});

describe('passaporte', () => {
  it('27 carimbos em 5 linhas (uma por região), cada um com a copa do estado', () => {
    const rows = passportRows(CUPS);
    expect(rows.map((r) => r.region)).toEqual(BRAZIL_REGIONS.map((r) => r.id));
    expect(rows.map((r) => r.stamps.length)).toEqual([4, 3, 4, 9, 7]);
    for (const r of rows) for (const s of r.stamps) expect(s.cup.state).toBe(s.state);
  });

  it('marcos do estado: os das 3 pistas, sem repetir, na ordem da copa', () => {
    const rj = CUPS.find((c) => c.id === 'br_rj')!;
    expect(stateLandmarks(rj)).toEqual(['cristo_redentor', 'pao_de_acucar', 'casario_paraty', 'escuna', 'dedo_de_deus']);
    const al = CUPS.find((c) => c.id === 'br_al')!;
    expect(stateLandmarks(al).filter((l) => l === 'coqueiral')).toHaveLength(1);
  });

  it('todo marco do Brasil tem nome em PT e EN (o cartão-postal mostra o nome, nunca o id)', () => {
    for (const cup of CUPS.filter((c) => c.stage === 'brasil')) {
      for (const id of stateLandmarks(cup)) {
        for (const lang of ['pt', 'en'] as const) {
          setLanguage(lang);
          const name = landmarkName(id);
          expect(name, `${lang} ${id}`).not.toContain('passport.');
          expect(name, `${lang} ${id}`).not.toContain('_');
        }
      }
    }
  });
});

describe('nomes de pista e de copa pelas strings do núcleo', () => {
  it('trackName usa core.track.<id> (EN traduz a descrição)', () => {
    setLanguage('en');
    expect(trackName('copacabana')).toBe(t('core.track.copacabana'));
    expect(trackName('copacabana')).not.toBe('Orla de Copacabana');
    setLanguage('pt');
    expect(trackName('copacabana')).toBe('Orla de Copacabana');
  });

  it('toda pista e toda copa têm nome nos dois idiomas', () => {
    for (const lang of ['pt', 'en'] as const) {
      setLanguage(lang);
      for (const d of TRACKS) expect(trackName(d.id), d.id).not.toContain('core.track');
      for (const c of CUPS) expect(t(`core.cup.${c.id}`), c.id).not.toContain('core.cup');
    }
  });
});

describe('título do cartão-postal', () => {
  // Defeito visto na captura: "Lembranças de CEARÁ". Em português o estado pede a contração dele (do Ceará, da
  // Bahia, de São Paulo); em inglês, "Greetings from".
  it('usa a contração de cada estado em português', () => {
    expect(postcardFrom('CE')).toBe('Lembranças do');
    expect(postcardFrom('BA')).toBe('Lembranças da');
    expect(postcardFrom('PB')).toBe('Lembranças da');
    expect(postcardFrom('SP')).toBe('Lembranças de');
    expect(postcardFrom('MG')).toBe('Lembranças de');
    expect(postcardFrom('RJ')).toBe('Lembranças do');
    expect(postcardFrom('DF')).toBe('Lembranças do');
  });
  it('em inglês não muda', () => {
    setLanguage('en');
    expect(postcardFrom('CE')).toBe('Greetings from');
  });
});
