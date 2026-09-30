import { describe, expect, it } from 'vitest';
import { DEFAULT_SETTINGS } from '../src/game/contracts';
import { sanitizeSettings } from '../src/game/settings';

describe('estilo visual (Moderno / Retrô)', () => {
  it('padrão é o moderno; "retro" é guardado; lixo volta ao padrão', () => {
    expect(DEFAULT_SETTINGS.renderStyle).toBe('modern');
    expect(sanitizeSettings({}).renderStyle).toBe('modern');
    expect(sanitizeSettings({ renderStyle: 'retro' }).renderStyle).toBe('retro');
    expect(sanitizeSettings({ renderStyle: 'pixel' }).renderStyle).toBe('modern');
    expect(sanitizeSettings({ renderStyle: 3 }).renderStyle).toBe('modern');
  });
});
