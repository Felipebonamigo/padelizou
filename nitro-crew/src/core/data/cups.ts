import type { CupDef } from '../types';

export const CUPS: CupDef[] = [
  { id: 'brasil', name: 'Copa Brasil', country: 'Brasil', flag: '🇧🇷', trackIds: ['copacabana', 'serra_do_mar', 'sampa_noite'], requires: null },
  { id: 'eua', name: 'Copa Estados Unidos', country: 'Estados Unidos', flag: '🇺🇸', trackIds: ['rota_66', 'canion', 'las_vegas'], requires: 'brasil' },
  { id: 'japao', name: 'Copa Japão', country: 'Japão', flag: '🇯🇵', trackIds: ['baia_toquio', 'monte_fuji', 'osaka_neon'], requires: 'eua' },
  { id: 'europa', name: 'Copa Europa', country: 'Europa', flag: '🇪🇺', trackIds: ['autobahn', 'passo_alpino', 'monaco_noite'], requires: 'japao' },
];

export function cupDef(id: string): CupDef {
  const c = CUPS.find((x) => x.id === id);
  if (!c) throw new Error(`Copa desconhecida: ${id}`);
  return c;
}
