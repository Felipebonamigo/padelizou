// Strings do conteúdo do núcleo (países, dificuldades, carros). Nomes de pista e de piloto
// são nomes próprios e não mudam de idioma.
import { registerStrings } from './index';

registerStrings('core', {
  pt: {
    'country.Brasil': 'Brasil', 'country.Estados Unidos': 'Estados Unidos', 'country.Japão': 'Japão', 'country.Europa': 'Europa',
    'cup.brasil': 'Copa Brasil', 'cup.eua': 'Copa Estados Unidos', 'cup.japao': 'Copa Japão', 'cup.europa': 'Copa Europa',
    'difficulty.amador': 'Amador', 'difficulty.profissional': 'Profissional', 'difficulty.campeao': 'Campeão',
    'car.falcao.blurb': 'Equilibrado. Bom em tudo, excelente em nada.',
    'car.trovao.blurb': 'O mais rápido na reta. Bebe muito e sofre nas curvas.',
    'car.tornado.blurb': 'Arranca e faz curva como ninguém. Falta fôlego no fim da reta.',
    'car.camelo.blurb': 'Econômico: quase nunca precisa de box. Modesto no resto.',
    'team.human': 'Sua equipe',
    'time.day': 'Dia', 'time.dusk': 'Entardecer', 'time.night': 'Noite',
  },
  en: {
    'country.Brasil': 'Brazil', 'country.Estados Unidos': 'United States', 'country.Japão': 'Japan', 'country.Europa': 'Europe',
    'cup.brasil': 'Brazil Cup', 'cup.eua': 'United States Cup', 'cup.japao': 'Japan Cup', 'cup.europa': 'Europe Cup',
    'difficulty.amador': 'Amateur', 'difficulty.profissional': 'Professional', 'difficulty.campeao': 'Champion',
    'car.falcao.blurb': 'Balanced. Good at everything, great at nothing.',
    'car.trovao.blurb': 'Fastest on the straights. Drinks fuel and struggles in corners.',
    'car.tornado.blurb': 'Launches and corners like no other. Runs out of breath at the end of the straight.',
    'car.camelo.blurb': 'Frugal: almost never needs the pits. Modest otherwise.',
    'team.human': 'Your team',
    'time.day': 'Day', 'time.dusk': 'Dusk', 'time.night': 'Night',
  },
});
