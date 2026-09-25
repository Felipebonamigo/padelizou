import type { CupDef } from '../types';

// Oito copas de quatro pistas, como o original. Destravam em sequência (cada uma exige a anterior)
// e ficam mais difíceis dentro da copa e de uma copa para a outra (tests/track.test.ts confere).
// Regiões com mais de um país usam a bandeira de onde fica a maioria das pistas: Escandinávia →
// Noruega (Atlântico, Trollstigen, Tromsø; a Lapônia é de três países), Mediterrâneo → Itália
// (Amalfi, Etna, Roma; Santorini é grega).
export const CUPS: CupDef[] = [
  { id: 'brasil', name: 'Copa Brasil', country: 'Brasil', flag: '🇧🇷', trackIds: ['copacabana', 'transpantaneira', 'serra_do_mar', 'sampa_noite'], requires: null },
  { id: 'eua', name: 'Copa Estados Unidos', country: 'Estados Unidos', flag: '🇺🇸', trackIds: ['rota_66', 'rochosas', 'canion', 'las_vegas'], requires: 'brasil' },
  { id: 'japao', name: 'Copa Japão', country: 'Japão', flag: '🇯🇵', trackIds: ['baia_toquio', 'yanbaru', 'monte_fuji', 'osaka_neon'], requires: 'eua' },
  { id: 'europa', name: 'Copa Europa', country: 'Europa', flag: '🇪🇺', trackIds: ['autobahn', 'paris', 'passo_alpino', 'monaco_noite'], requires: 'japao' },
  { id: 'africa_do_sul', name: 'Copa África do Sul', country: 'África do Sul', flag: '🇿🇦', trackIds: ['kruger', 'karoo', 'drakensberg', 'boa_esperanca'], requires: 'europa' },
  { id: 'australia', name: 'Copa Austrália', country: 'Austrália', flag: '🇦🇺', trackIds: ['outback', 'great_ocean', 'daintree', 'sydney'], requires: 'africa_do_sul' },
  { id: 'escandinavia', name: 'Copa Escandinávia', country: 'Escandinávia', flag: '🇳🇴', trackIds: ['atlantico', 'laponia', 'trollstigen', 'tromso'], requires: 'australia' },
  { id: 'mediterraneo', name: 'Copa Mediterrâneo', country: 'Mediterrâneo', flag: '🇮🇹', trackIds: ['amalfi', 'santorini', 'etna', 'roma'], requires: 'escandinavia' },
];

export function cupDef(id: string): CupDef {
  const c = CUPS.find((x) => x.id === id);
  if (!c) throw new Error(`Copa desconhecida: ${id}`);
  return c;
}
