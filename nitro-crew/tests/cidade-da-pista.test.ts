// Pedido do dono (09/10/2026): "como cada pista é numa cidade, colocar o nome da corrida como o nome da cidade". Todo lugar
// tem cidade (`cityOf`), e o título mostrado é "Cidade · trecho" (`trackTitle`), sem repetir a cidade quando o nome do trecho já a diz.
import { describe, expect, it } from 'vitest';
import { TRACKS } from '../src/core/track/tracks';
import { cityOf, trackTitle } from '../src/core/data/places';

describe('cidade da pista', () => {
  it.each(TRACKS.map((d) => [d.id, d.name]))('%s tem cidade e título', (id, name) => {
    const city = cityOf(id, 'pt');
    expect(city, `${id} sem cidade`).toBeTruthy();
    const title = trackTitle(id, name, 'pt');
    expect(title.includes(city!), 'o título leva a cidade').toBe(true);
    expect(title.endsWith(name) || title === name, 'e o nome do trecho').toBe(true);
  });

  it('exemplos', () => {
    expect(trackTitle('copacabana', 'Orla de Copacabana', 'pt')).toBe('Rio de Janeiro · Orla de Copacabana');
    expect(trackTitle('noronha', 'Fernando de Noronha', 'pt')).toBe('Fernando de Noronha');
    expect(trackTitle('baia_toquio', 'Baía de Tóquio', 'en')).toBe('Tokyo · Baía de Tóquio');
  });

  it('pista desconhecida cai no nome recebido', () => {
    expect(cityOf('inexistente', 'pt')).toBeUndefined();
    expect(trackTitle('inexistente', 'Pista X', 'pt')).toBe('Pista X');
  });
});
