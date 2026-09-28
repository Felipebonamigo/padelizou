// Textos da acessibilidade: direção assistida (lobby e opções), cores dos jogadores, tamanho do HUD,
// texto grande e reduzir efeitos, e o selo do HUD.
import { registerStrings } from '../i18n';

registerStrings('access', {
  pt: {
    'hud.assist': 'ASSIST',

    'level.none': 'Nenhuma',
    'level.brake': 'Freio automático',
    'level.steer': 'Volante assistido',
    'level.full': 'Completa',

    'short.brake': 'Freio',
    'short.steer': 'Volante',
    'short.full': 'Completa',

    'lobby.assist': 'Direção',

    'options.title': 'Acessibilidade',
    'options.open': 'Ajustar ›',
    'options.vision': 'Visão e efeitos',
    'options.driving': 'Direção assistida',
    'options.seatAssist': 'Jogador {n}',
    'options.palette': 'Cores dos jogadores',
    'options.hudScale': 'Tamanho do HUD',
    'options.largeText': 'Texto grande',
    'options.reduceEffects': 'Reduzir efeitos',

    'palette.default': 'Padrão',
    'palette.deutan': 'Vermelho/verde',
    'palette.tritan': 'Azul/amarelo',

    'desc.intro': 'Ajustes para jogar com crianças, iniciantes e daltônicos. Tudo fica salvo.',
    'desc.palette.default': 'Cores dos jogadores: as de sempre.',
    'desc.palette.deutan': 'Cores dos jogadores que não se confundem no daltonismo vermelho-verde (protanopia e deuteranopia).',
    'desc.palette.tritan': 'Cores dos jogadores que não se confundem no daltonismo azul-amarelo (tritanopia).',
    'desc.hudScale': 'Tamanho dos painéis da corrida (posição, tempo, velocímetro), de 80% a 150%.',
    'desc.largeText': 'Texto maior nos menus.',
    'desc.reduceEffects': 'Sem tremor da câmera, linhas de velocidade, piscadas e faíscas.',
    'desc.level.none': 'Sem ajuda: o jogador faz tudo.',
    'desc.level.brake': 'Freio automático: tira o pé e freia sozinho antes das curvas fortes; o volante é do jogador.',
    'desc.level.steer': 'Volante assistido: compensa as curvas e segura o carro no asfalto, sem tirar o volante do jogador.',
    'desc.level.full': 'Completa: esterça, freia, desvia e vai ao box sozinha — o jogador só acelera e usa o nitro.',
    'desc.override': 'O freio e o volante do jogador sempre valem por cima da ajuda. No HUD aparece o selo ASSIST.',
  },
  en: {
    'hud.assist': 'ASSIST',

    'level.none': 'None',
    'level.brake': 'Auto brake',
    'level.steer': 'Steering assist',
    'level.full': 'Full',

    'short.brake': 'Brake',
    'short.steer': 'Steering',
    'short.full': 'Full',

    'lobby.assist': 'Assist',

    'options.title': 'Accessibility',
    'options.open': 'Adjust ›',
    'options.vision': 'Vision and effects',
    'options.driving': 'Driving assist',
    'options.seatAssist': 'Player {n}',
    'options.palette': 'Player colors',
    'options.hudScale': 'HUD size',
    'options.largeText': 'Large text',
    'options.reduceEffects': 'Reduce effects',

    'palette.default': 'Default',
    'palette.deutan': 'Red/green',
    'palette.tritan': 'Blue/yellow',

    'desc.intro': 'Settings for playing with kids, beginners and color-blind players. Everything is saved.',
    'desc.palette.default': 'Player colors: the usual ones.',
    'desc.palette.deutan': 'Player colors that stay apart with red-green color blindness (protanopia and deuteranopia).',
    'desc.palette.tritan': 'Player colors that stay apart with blue-yellow color blindness (tritanopia).',
    'desc.hudScale': 'Size of the race panels (position, time, speedometer), from 80% to 150%.',
    'desc.largeText': 'Bigger text in the menus.',
    'desc.reduceEffects': 'No camera shake, speed lines, blinking or sparks.',
    'desc.level.none': 'No help: the player does everything.',
    'desc.level.brake': 'Auto brake: lifts and brakes by itself before sharp corners; the steering is the player\'s.',
    'desc.level.steer': 'Steering assist: counters the corners and keeps the car on the asphalt without taking the wheel away.',
    'desc.level.full': 'Full: steers, brakes, dodges and pits by itself — the player only accelerates and uses nitro.',
    'desc.override': 'The player\'s brake and steering always win over the assist. The HUD shows an ASSIST badge.',
  },
});
