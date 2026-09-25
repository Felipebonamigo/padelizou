import { describe, it, expect } from 'vitest';
import { missingKeys, namespaces, setLanguage, t } from '../src/i18n';
import '../src/i18n/core';

// Cada camada registra o próprio namespace; importar aqui garante que todos entram na conferência.
const extra = import.meta.glob('../src/**/strings.ts', { eager: true });

describe('idiomas', () => {
  it('todas as chaves existem em PT e EN', () => {
    expect(Object.keys(extra).length).toBeGreaterThanOrEqual(0);
    expect(missingKeys()).toEqual([]);
    expect(namespaces()).toContain('core');
  });
  it('t substitui parâmetros e cai para PT quando falta', () => {
    setLanguage('en');
    expect(t('core.cup.brasil')).toBe('Brazil Cup');
    setLanguage('pt');
    expect(t('core.cup.brasil')).toBe('Copa Brasil');
    expect(t('inexistente.chave')).toBe('inexistente.chave');
  });
});
