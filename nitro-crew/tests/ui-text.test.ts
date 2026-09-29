import { afterEach, describe, expect, it } from 'vitest';
import { setLanguage } from '../src/i18n';
import { lapsText } from '../src/ui/screens/common';
import '../src/ui/strings';

afterEach(() => setLanguage('pt'));

describe('textos com número', () => {
  it('uma volta no singular (a tela de recordes mostrava "1 voltas" na corrida de 1 volta)', () => {
    setLanguage('pt');
    expect(lapsText(1)).toBe('1 volta');
    expect(lapsText(3)).toBe('3 voltas');
    setLanguage('en');
    expect(lapsText(1)).toBe('1 lap');
    expect(lapsText(3)).toBe('3 laps');
  });
});
