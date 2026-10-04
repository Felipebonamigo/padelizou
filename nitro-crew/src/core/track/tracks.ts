// As pistas, na ordem das copas (data/cups.ts): a Expedição Brasil (27 estados × 3 pistas, região por região) e
// o Mundial (7 países × 4). Comprimentos em segmentos (200 unidades cada); ~1.800 segmentos ≈ 60–70 s por volta a
// 300 km/h. Curva: 2 fácil, 4 média, 6 forte (negativa = esquerda). Altura em segmentos. `difficulty` não cai dentro
// da copa e sobe ao longo de cada etapa; tests/track.test.ts confere o rótulo contra o traçado (índice técnico).
// As pistas novas do Brasil (onda G) foram ajustadas ao índice técnico alvo da sua copa escalando curvas e morros
// de um traçado-base com a identidade do lugar; o número final é o que está aqui. Pista nova: docs/PISTAS.md.
// Pistas com desenho (uma por estado; a cuia do RS à mão, as outras 26 geradas): o minimapa desenha o símbolo do
// lugar. Os `ops` delas saem de um polígono em scripts/track-shapes.ts por `npx tsx scripts/shape-to-track.ts <pista>
// --apply` — mexer no desenho e regenerar, não nos números (docs/PISTAS.md, "Pistas com desenho").
import type { TrackDef, TrackOp } from '../types';

const st = (length: number): TrackOp => ({ op: 'straight', length });
const cv = (length: number, curve: number, hill?: number): TrackOp => ({ op: 'curve', length, curve, hill });
const hl = (length: number, height: number): TrackOp => ({ op: 'hill', length, height });
const ss = (length: number, curve: number): TrackOp => ({ op: 's', length, curve });
const pit = (length: number): TrackOp => ({ op: 'pit', length });

export const TRACKS: TrackDef[] = [
  // ───────── Expedição Brasil · sudeste · RJ ─────────
  // Orla com o Cristo ao fundo. O traçado desenha o CRISTO REDENTOR no minimapa: de pé no pedestal (largada no lado
  // esquerdo dele), braços abertos, cabeça e pescoço. Nível 1: quinas curtas de curva ≤ 3 (nenhuma freia) e o índice
  // técnico nos dois morros da túnica.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'copacabana', name: 'Orla de Copacabana', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(31), cv(12, 1.85), st(5), cv(12, -1.8), st(58), hl(100, 15), st(59), cv(11, -1.58), st(151), cv(10, 1.74), st(25), cv(12, 1.85), st(183), cv(12, -1.85), st(3), cv(6, -3), st(7), cv(6, 2.99), st(35), cv(22, 0.98), st(30), cv(22, 0.97), st(35), cv(6, 3), st(7), cv(6, -3), st(3), cv(12, -1.85), st(183), cv(12, 1.85), st(25), cv(10, 1.74), st(151), cv(11, -1.58), st(58), hl(100, 15), st(59), cv(12, -1.8), st(5), cv(12, 1.85), st(71), cv(12, 1.85), st(146), cv(12, 1.84)],
  },
  // Centro histórico de pedra e a baía das escunas: beira-mar plano, curvas abertas.
  {
    id: 'paraty', name: 'Caminho do Ouro de Paraty', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(120), cv(110, 3.2), st(90), ss(180, 3), st(80), ss(170, 3.5), st(120), ss(170, 3.5), st(110), ss(170, 3.3), st(100), cv(110, 3.5), st(100), ss(170, 2.9), st(120), st(100)],
  },
  // Subida da serra até o Dedo de Deus: morros longos e curvas médias na mata atlântica.
  {
    id: 'serra_dos_orgaos', name: 'Serra dos Órgãos', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(100), ss(180, 3.3), st(60), st(110), ss(160, 3.5), st(100), ss(150, 3.6), st(70), hl(110, 29), cv(90, 3.7), st(100), st(110), st(80), st(110), st(100), hl(110, 29), cv(90, 3), st(100)],
  },
  // ───────── Expedição Brasil · sudeste · SP ─────────
  // Estrada da ilha ao longo do canal dos veleiros: plano, curvas abertas, o farol na ponta.
  {
    id: 'ilhabela', name: 'Canal de Ilhabela', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(90), cv(120, 2.7), st(110), ss(170, 3.3), st(90), cv(100, 3.6), st(100), cv(130, -3.1), st(160), hl(90, 9), st(90), hl(90, 9), st(80), cv(120, 3.3), st(120), st(100)],
  },
  // Serra da Mantiqueira entre chalés e araucárias: morros e curvas médias no frio.
  {
    id: 'campos_do_jordao', name: 'Campos do Jordão', country: 'Brasil', scenery: 'alpine', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(140), hl(110, 38), cv(90, 4.1), ss(150, 3.4), st(90), st(120), st(110), ss(150, 3.4), st(100), st(100), st(100), ss(180, 3.5), st(80), cv(170, 3.7), st(60), st(100)],
  },
  // São Paulo à noite. O traçado desenha o MASP de lado: a caixa suspensa nos dois pórticos (largada no pilar esquerdo)
  // e o vão livre embaixo; as oito quinas em curva 6 carregam quase todo o índice.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'sampa_noite', name: 'Noite em Sampa', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(58), hl(120, 51), st(59), cv(14, 5.7), st(34), hl(120, 51), st(34), hl(120, 51), st(34), hl(120, 51), st(35), cv(14, 5.7), st(277), cv(14, 5.7), st(37), cv(14, 5.7), st(112), cv(14, -5.7), st(390), cv(14, -5.7), st(111), cv(14, 5.7), st(37), cv(14, 5.7)],
  },
  // ───────── Expedição Brasil · sudeste · MG ─────────
  // Volta da lagoa. O traçado desenha a IGREJA DA PAMPULHA de lado: a abóbada grande da nave e quatro corcovas cada vez
  // menores, no chão reto. A largada fica no meio da perna da nave (`startAt`) para a segunda igreja do cenário cair
  // numa aproximação que a mostra por 2,5 s (tests/landmarks-enquadramento.test.ts).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'pampulha', name: 'Lagoa da Pampulha', country: 'Brasil', scenery: 'city_night', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(27), cv(4, 1.76), st(81), cv(17, 0.7), st(58), cv(16, 0.69), st(33), cv(77, 0.71), st(29), cv(24, 0.67), st(79), cv(12, 0.67), st(96), cv(4, 0.37), st(29), cv(31, -3.29), st(25), cv(11, 1.18), st(29), cv(56, 1.38), st(29), cv(11, 1.19), st(41), cv(30, -3.39), st(12), cv(7, 1.5), st(27), cv(42, 1.76), st(27), cv(8, 1.73), st(30), cv(29, -3.4), st(7), cv(5, 1.93), st(20), cv(34, 2.1), st(20), cv(6, 1.72), st(24), cv(27, -3.19), st(13), cv(9, 2.47), st(13), cv(19, 2.53), st(18), cv(6, 2.12), st(44), cv(19, 3.25), st(112), hl(120, 26), st(112), hl(120, 26), st(114), cv(17, 3.34), st(100)],
  },
  // Cidade colonial no morro: ladeiras curtas e fortes (lombadas íngremes) e esses fechados entre os casarios.
  {
    id: 'ouro_preto', name: 'Ladeiras de Ouro Preto', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(80), hl(60, 17), cv(80, 3.4), st(60), hl(50, 15), ss(140, 3.4), st(70), hl(60, 18), cv(90, -4.2), st(80), hl(50, 13), ss(160, 2.5), st(90), cv(80, 3.4, 13), hl(60, 17), st(100), ss(140, 2.5), st(80), cv(90, -3.4, -13), hl(60, 15), st(150)],
  },
  // Chapadão do São Francisco até a Casca d'Anta: estrada de terra rápida, ondulações e curvas fortes na borda.
  {
    id: 'serra_da_canastra', name: 'Serra da Canastra', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(100), cv(90, 4.8), st(140), hl(70, 24), st(120), st(210), cv(130, 4), cv(100, 6), st(110), st(170), cv(130, 3.8), hl(90, 24), st(120), cv(100, -5.5), st(110), st(100)],
  },
  // ───────── Expedição Brasil · sudeste · ES ─────────
  // Vila soterrada pelas dunas: beira-mar plano com curvas abertas e lombadas leves.
  {
    id: 'itaunas', name: 'Dunas de Itaúnas', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(80), cv(140, 3.5), st(130), cv(100, 3.6), st(110), ss(190, 3.3), st(120), ss(200, 3.2), st(100), ss(190, 2.9), st(120), cv(110, 3.6), st(100), st(100)],
  },
  // Orla de Vitória. O traçado desenha o CONVENTO DA PENHA: a encosta de pedra à esquerda (largada no pé), o bloco do
  // convento no alto e o penhasco do lado do mar.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'convento_penha', name: 'Convento da Penha', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(14), cv(4, 4.34), st(92), cv(10, 0.79), st(84), cv(10, 0.84), st(85), cv(4, 0.57), st(80), cv(11, 1.02), st(61), cv(12, 1.84), st(63), cv(16, -4.7), st(98), cv(16, 4.7), st(72), cv(16, 4.7), st(98), cv(16, -4.7), st(35), cv(15, 2.27), st(40), cv(15, 1.66), st(84), cv(4, 0.29), st(102), cv(4, -0.13), st(96), cv(4, -0.23), st(46), cv(19, 4.19), st(59), hl(120, 51), st(59), hl(120, 50), st(59), hl(120, 50), st(61), cv(16, 4.7)],
  },
  // Montanhas capixabas ao pé da Pedra Azul: subidas, grampos médios e esses na serra.
  {
    id: 'pedra_azul', name: 'Pedra Azul', country: 'Brasil', scenery: 'alpine', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(80), hl(110, 32), cv(90, 3.7), ss(180, 3.6), st(60), ss(140, 3.6), st(60), cv(100, 3.7, -16), st(70), cv(80, 3.9, -16), st(70), hl(110, 39), cv(90, 3.6), ss(150, 3.8), st(100), ss(150, 3.8), st(80), st(100)],
  },
  // ───────── Expedição Brasil · sul · PR ─────────
  // Estrada do parque até as cataratas: curvas longas na mata, retas curtas.
  {
    id: 'foz_do_iguacu', name: 'Cataratas do Iguaçu', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(120), cv(140, 3.6), st(110), cv(160, 3.3), st(100), cv(160, 3.9), st(110), hl(80, 17), st(80), cv(160, 3), st(90), ss(190, 3.1), st(80), cv(160, -3.5), st(80), st(100)],
  },
  {
    id: 'serra_do_mar', name: 'Serra do Mar', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(60), hl(120, 40), cv(100, 4, 15), st(80), cv(120, -4), hl(100, 30), ss(200, 3), st(120), cv(80, 5), st(100), cv(140, -3, -20), hl(90, 25), st(160), cv(100, 3), st(120)],
  },
  // Curitiba à noite, a Ópera de Arame entre as árvores. O traçado desenha uma ARAUCÁRIA: o tronco (largada) e a copa
  // em taça achatada com os tufos dos galhos no alto.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'curitiba', name: 'Ópera de Arame', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(5), hl(120, 48), st(5), cv(8, -5.1), st(81), cv(4, -0.35), st(3), hl(82, 32), st(3), cv(4, 1.18), st(74), cv(4, 1.05), st(60), cv(11, 4.45), st(55), cv(15, 4.43), st(30), cv(13, -4.84), st(49), cv(13, 4.89), st(41), cv(13, -4.68), st(46), cv(12, 4.94), st(38), cv(12, -4.94), st(45), cv(13, 4.92), st(45), cv(12, -4.95), st(38), cv(12, 4.95), st(46), cv(13, -4.68), st(41), cv(13, 4.89), st(49), cv(13, -4.85), st(30), cv(15, 4.43), st(55), cv(11, 4.45), st(60), cv(4, 1.06), st(74), cv(4, 1.17), st(3), hl(82, 31), st(3), cv(4, -0.34), st(81), cv(8, -5.1), st(61), hl(120, 48), st(62), cv(13, 4.92), st(33), cv(13, 4.92), st(73)],
  },
  // ───────── Expedição Brasil · sul · SC ─────────
  // Ilha de Santa Catarina. O traçado desenha a PONTE HERCÍLIO LUZ de lado: as duas torres (largada no pé da esquerda),
  // o cabo pênsil descendo até o tabuleiro no meio e os estais finos até as ancoragens, com os vãos laterais abertos.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'floripa', name: 'Ponte Hercílio Luz', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(111), cv(16, -4.4), st(14), hl(120, 43), st(15), cv(4, -2.97), st(18), cv(9, 4.32), st(23), cv(9, 4.33), st(15), cv(9, 4.32), st(8), cv(6, -3.86), st(41), hl(120, 42), st(42), cv(4, -2.93), st(31), cv(9, 4.32), st(6), cv(9, 4.32), st(31), cv(4, -1.51), st(38), cv(4, -0.3), st(46), cv(4, -0.56), st(40), cv(4, -1.19), st(30), cv(4, -3.08), st(17), cv(5, -4.07), st(17), cv(4, -3.09), st(30), cv(4, -1.19), st(40), cv(4, -0.55), st(46), cv(4, -0.31), st(38), cv(4, -1.52), st(31), cv(9, 4.33), st(6), cv(9, 4.32), st(31), cv(4, -2.93), st(203), cv(6, -3.86), st(8), cv(9, 4.32), st(15), cv(9, 4.32), st(23), cv(9, 4.33), st(18), cv(4, -2.98), st(149), cv(16, -4.4), st(151), cv(9, 4.32), st(185), cv(9, 4.33)],
  },
  // Orla dos arranha-céus à noite: avenida da praia, retornos e a roda-gigante no morro.
  {
    id: 'camboriu', name: 'Avenida Atlântica de Camboriú', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 2,
    ops: [pit(40), st(130), ss(160, 3.9), st(140), cv(150, -3.8), st(110), cv(130, 3.5), st(110), cv(130, 3.7), st(100), ss(160, 3.6), st(90), cv(120, 4.5), st(110), st(100)],
  },
  // A estrada dos zigue-zagues: grampos em sequência subindo o paredão até o mirante.
  {
    id: 'rio_do_rastro', name: 'Serra do Rio do Rastro', country: 'Brasil', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(110), st(120), cv(90, 4.1, 19), st(70), ss(140, 4.6), st(90), st(110), st(100), ss(180, 4.1), st(70), ss(150, 4.1), st(60), ss(140, 4.1), st(100), st(120), cv(170, 4.4), st(80), st(100)],
  },
  // ───────── Expedição Brasil · sul · RS ─────────
  // Porto Alegre ao pôr do sol no Guaíba: avenida da orla com a Usina do Gasômetro.
  {
    id: 'orla_guaiba', name: 'Orla do Guaíba', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(90), cv(100, 3.8), st(100), ss(170, 3.7), st(90), cv(110, 3.8), st(100), ss(170, 3.8), st(100), cv(100, -4.1), st(100), hl(100, 19), st(70), cv(110, 3.7), st(140), cv(110, 3.3), st(120), st(100)],
  },
  // Serra gaúcha com araucárias e parreirais. O traçado desenha uma CUIA DE CHIMARRÃO no minimapa (sentido horário,
  // largando no lado esquerdo do bojo): bojo redondo embaixo (cinco arcos abertos de 1,4), cintura estreita (as duas
  // curvas à esquerda de -6) e a boca larga em cima (dois cotovelos de 6 e a reta da borda). tests/track.test.ts
  // trava a forma e `npx tsx scripts/track-outline.ts cuia_gaucha` a desenha. Os morros ficam nas retas e nas
  // curvas: altura não muda o contorno, curva muda — mexer numa curva aqui é mexer no desenho.
  {
    id: 'cuia_gaucha', name: 'Cuia da Serra Gaúcha', country: 'Brasil', scenery: 'alpine', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), cv(257, 1.4, 4), cv(107, -6), hl(60, 4), cv(130, 6), hl(130, 5), cv(130, 6), hl(60, 4), cv(107, -6), cv(257, 1.4, -4), cv(237, 1.4, 3), cv(237, 1.4, -3), cv(218, 1.4)],
  },
  // Campos de cima da serra na borda do Itaimbezinho: morros, curvas fortes à beira do cânion.
  {
    id: 'aparados_da_serra', name: 'Aparados da Serra', country: 'Brasil', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(90), hl(110, 36), cv(90, 4.5), st(80), ss(180, 3.7), st(90), st(80), cv(100, 4.2, -18), st(60), ss(160, 4.2), st(60), hl(110, 27), cv(100, -4.2), cv(100, 3.9, -18), st(70), ss(140, 4.2), st(70), st(130)],
  },
  // ───────── Expedição Brasil · centro_oeste · DF ─────────
  // Margem do lago até a Ponte JK e seus arcos: curvas longas de beira d'água.
  {
    id: 'lago_paranoa', name: 'Lago Paranoá', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(90), cv(170, -3.3), st(90), hl(90, 9), st(80), ss(190, 4.2), st(90), cv(140, 4.4), st(100), cv(150, 4), st(110), hl(90, 19), st(90), hl(80, 9), st(70), st(100)],
  },
  // Centro de Brasília à noite: eixos retos e as alças em volta da Torre de TV.
  {
    id: 'torre_de_tv', name: 'Torre de TV', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(80), ss(160, 5.7), st(90), st(120), st(120), ss(140, 5.3), st(90), cv(70, 5.8), st(120), st(140), cv(60, 5.6), st(80), st(130), cv(60, 5.7), st(110), st(110)],
  },
  // Eixo Monumental. O traçado desenha o AVIÃO DO PLANO PILOTO visto de cima: a fuselagem (o eixo; largada atrás da asa
  // esquerda), o bico, as asas arqueadas para trás (o Eixo Rodoviário) e a cauda.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'brasilia', name: 'Eixo Monumental', country: 'Brasil', scenery: 'city_night', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(21), cv(13, -5.3), st(42), cv(4, -1.38), st(56), cv(4, -1.37), st(3), hl(54, 23), st(4), cv(4, -2.61), st(45), cv(15, 5.11), st(59), cv(8, 5.9), st(52), cv(4, 0.29), st(3), hl(60, 26), st(3), cv(4, 2.13), st(3), hl(56, 24), st(4), cv(4, 2.09), st(55), cv(10, -5.12), st(44), cv(4, 2.23), st(59), cv(19, 5.27), st(59), cv(4, 2.22), st(44), cv(10, -5.12), st(55), cv(4, 2.1), st(3), hl(56, 24), st(4), cv(4, 2.13), st(3), hl(60, 26), st(3), cv(4, 0.29), st(52), cv(8, 5.9), st(59), cv(15, 5.11), st(45), cv(4, -2.61), st(3), hl(54, 22), st(4), cv(4, -1.37), st(56), cv(4, -1.38), st(42), cv(13, -5.3), st(16), hl(120, 52), st(17), cv(7, -4.79), st(3), hl(68, 29), st(4), cv(7, 4.79), st(34), cv(13, 5.34), st(3), hl(56, 24), st(4), cv(4, -1.92), st(37), cv(4, -1.92), st(3), hl(56, 24), st(4), cv(13, 5.34), st(34), cv(7, 4.79), st(3), hl(68, 29), st(4), cv(7, -4.79), st(92)],
  },
  // ───────── Expedição Brasil · centro_oeste · GO ─────────
  // Cidade de pedra das cavalhadas: ruas estreitas, esses curtos e ladeiras.
  {
    id: 'pirenopolis', name: 'Ruas de Pirenópolis', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(100), hl(80, 15), st(60), hl(90, 15), st(60), ss(120, 3.8), st(80), ss(140, 3.9), st(50), cv(90, 3.8), st(60), cv(80, 3.9), st(70), hl(80, 15), st(60), hl(90, 15), st(50), ss(150, 4), st(60), cv(70, 4.3), st(60), st(100)],
  },
  // Cerrado das águas quentes. O traçado desenha uma GOTA D'ÁGUA: ponta fina em cima e o fundo redondo. A gota é quase
  // toda arco (curva forte arredondaria a ponta): o índice técnico vem dos morros.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'caldas_novas', name: 'Águas de Caldas Novas', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(16), cv(18, 0.62), st(3), hl(66, 28), st(4), cv(26, 0.6), st(3), hl(76, 33), st(4), cv(14, 0.61), st(3), hl(92, 39), st(4), cv(10, 0.55), st(3), hl(80, 35), st(4), cv(11, -0.53), st(3), hl(66, 27), st(4), cv(4, -0.57), st(50), cv(22, 6), st(50), cv(4, -0.58), st(3), hl(66, 27), st(4), cv(11, -0.53), st(3), hl(80, 34), st(4), cv(10, 0.55), st(3), hl(92, 39), st(4), cv(14, 0.61), st(3), hl(76, 32), st(4), cv(26, 0.6), st(3), hl(66, 27), st(4), cv(18, 0.62), st(38), cv(76, 0.35), st(42), cv(98, 0.35), st(40), cv(75, 0.34), st(21), cv(91, 0.35), st(22), cv(74, 0.35), st(41), cv(98, 0.35), st(3), hl(54, 22), st(3), cv(40, 0.66)],
  },
  // Cerrado do Planalto Central: retas entre chapadões, ondulações fortes e curvas fechadas nos mirantes.
  {
    id: 'chapada_veadeiros', name: 'Chapada dos Veadeiros', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(140), hl(120, 32), cv(90, 4.1), st(120), hl(100, 28), cv(80, -4.9), st(100), hl(120, 32), ss(160, 4.1), st(120), cv(100, 4.1, 20), hl(100, 24), st(100), cv(90, 4.9), hl(120, 28), cv(100, -4.1, -20), st(140)],
  },
  // ───────── Expedição Brasil · centro_oeste · MS ─────────
  // Rios de água clara. O traçado desenha um PEIXE (a piraputanga) nadando para a direita: a cauda em V (largada no
  // lobo de baixo), a dorsal e a adiposa nas costas, a anal e a pélvica na barriga, e o focinho. Quinas arredondadas:
  // pista de mata precisa de curva longa para o cenário caber no orçamento (docs/PISTAS.md, "Pistas com desenho").
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'bonito', name: 'Rios de Bonito', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(1), cv(22, 4.29), st(3), hl(98, 34), st(3), cv(44, -4.5), st(3), hl(98, 34), st(3), cv(22, 4.29), st(48), cv(50, 4.35), st(3), hl(106, 37), st(3), cv(30, -4.33), st(9), cv(9, -4.07), st(32), cv(40, 4.35), st(6), cv(33, -4.41), st(44), cv(10, -3.88), st(58), cv(45, 4.4), st(55), cv(13, -4.36), st(66), cv(5, 3.42), st(90), cv(5, 3.75), st(94), cv(15, 3.98), st(19), cv(19, 4.04), st(50), cv(9, 4.05), st(81), cv(4, 3.22), st(79), cv(15, -3.98), st(35), cv(37, 4.27), st(36), cv(11, -3.7), st(38), cv(23, -4.14), st(15), cv(43, 4.09), st(43), cv(9, -3.97), st(48), cv(30, -4.46), st(3), hl(108, 38), st(3), cv(51, 4.27), st(5)],
  },
  // Capital morena ao entardecer: avenidas largas, rotatórias e esquinas.
  {
    id: 'campo_grande', name: 'Avenidas de Campo Grande', country: 'Brasil', scenery: 'city_night', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(130), cv(70, 6), st(90), hl(90, 24), st(80), ss(150, 5.9), st(110), ss(140, 5.5), st(90), st(200), cv(70, 6), st(130), st(180), hl(90, 12), st(60), st(100)],
  },
  // Terra batida no Pantanal sul: pontes de madeira como lombadas, curvas entre as baías.
  {
    id: 'estrada_parque', name: 'Estrada Parque do Pantanal', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(120), st(220), cv(130, 6), hl(80, 15), st(140), st(180), cv(120, 6), cv(110, 6), st(100), cv(100, 6), st(130), hl(60, 15), st(110), hl(100, 15), st(150), st(100)],
  },
  // ───────── Expedição Brasil · centro_oeste · MT ─────────
  // Estrada de terra no Pantanal. O traçado desenha um JACARÉ visto de cima: focinho, olhos, as quatro patas e o rabo
  // (largada no lado esquerdo do corpo). Nível 1: curvas ≤ 3 e as pontes de madeira como lombadas.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'transpantaneira', name: 'Transpantaneira', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(45), cv(11, -2.45), st(53), cv(4, 2.08), st(32), cv(4, 2.31), st(28), cv(10, 2.5), st(16), cv(7, 2.43), st(19), cv(4, 0.5), st(64), cv(10, -2.6), st(26), cv(7, -2.37), st(34), cv(6, 2.39), st(28), cv(4, 2.57), st(25), cv(6, -2.16), st(69), cv(4, 2.93), st(12), cv(6, 2.23), st(30), cv(6, 2.22), st(12), cv(4, 2.92), st(69), cv(6, -2.16), st(25), cv(4, 2.59), st(28), cv(6, 2.39), st(34), cv(7, -2.37), st(26), cv(10, -2.6), st(64), cv(4, 0.48), st(19), cv(7, 2.43), st(16), cv(10, 2.5), st(28), cv(4, 2.32), st(32), cv(4, 2.08), st(53), cv(11, -2.45), st(3), hl(78, 7), st(4), cv(10, -2.62), st(60), cv(4, 1.69), st(31), cv(5, 2.3), st(26), cv(8, 3), st(16), cv(10, 2.5), st(28), cv(4, -1.7), st(60), cv(10, -2.7), st(31), cv(4, -0.51), st(83), cv(4, 0.65), st(64), cv(9, 2.66), st(14), cv(9, 2.66), st(64), cv(4, 0.03), st(3), hl(80, 9), st(4), cv(4, 0.1), st(31), cv(10, -2.7), st(60), cv(4, -1.69), st(28), cv(10, 2.5), st(16), cv(8, 3), st(26), cv(5, 2.3), st(31), cv(4, 1.7), st(60), cv(10, -2.62)],
  },
  // O centro da América do Sul à noite: esquinas fortes e esses no calor da cidade.
  {
    id: 'cuiaba', name: 'Centro Geodésico de Cuiabá', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(90), cv(60, 5.6), st(80), cv(70, 6), st(80), st(140), st(130), cv(60, 6), st(80), ss(140, 6), st(90), ss(160, 5.8), st(80), cv(60, 6), st(110), ss(150, 6), st(60), st(90)],
  },
  // Paredões vermelhos e o Véu de Noiva: subida da chapada com grampos e esses no alto.
  {
    id: 'chapada_guimaraes', name: 'Chapada dos Guimarães', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), hl(110, 43), cv(100, 5), st(120), hl(120, 43), cv(100, 4.5), st(90), st(90), ss(150, 5.2), st(100), cv(90, -5.4, 21), st(90), hl(120, 43), cv(100, 5.7), ss(170, 5), st(70), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · BA ─────────
  // Litoral do descobrimento. O traçado desenha a CARAVELA DE CABRAL de lado: o casco (largada na popa), a vela latina
  // atrás e as duas velas enfunadas.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'porto_seguro', name: 'Costa de Porto Seguro', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(86), cv(11, 4.19), st(55), cv(11, 4.19), st(16), cv(11, -4.19), st(3), cv(9, -4.56), st(3), hl(100, 39), st(3), cv(18, 4.61), st(3), hl(92, 35), st(3), cv(7, -3.93), st(25), cv(15, -4.36), st(18), cv(4, 1.8), st(55), cv(4, 1.25), st(53), cv(8, 4.9), st(60), cv(4, 4.69), st(56), cv(8, 4.8), st(53), cv(4, 1.26), st(55), cv(4, 1.8), st(20), cv(13, -4.71), st(41), cv(13, -4.7), st(20), cv(4, 1.48), st(36), cv(4, 1.88), st(34), cv(7, 3.93), st(30), cv(6, 3.67), st(30), cv(7, 3.94), st(34), cv(4, 1.87), st(35), cv(4, 1.5), st(13), cv(17, -4.69), st(11), cv(4, -3.36), st(22), cv(19, 4.56), st(39), cv(4, 2.84), st(67), cv(4, 2.17), st(72), cv(4, 2.82), st(3), hl(82, 30), st(4), cv(4, 2.43), st(3), hl(106, 41), st(4), cv(4, 1.25), st(85), cv(4, 2.25), st(48), cv(8, 4.56)],
  },
  // Cidade Alta e Cidade Baixa: a subida forte da ladeira com grampos, e a orla curva até o Farol da Barra.
  {
    id: 'salvador', name: 'Orla de Salvador', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(80), hl(140, 42), cv(80, 4.6, 19), st(60), cv(80, -4.6, 15), ss(160, 3.9), st(70), cv(100, -4.6, -23), st(60), cv(90, 4.6, -12), st(100), cv(160, 3.9), st(80), ss(160, 4.6), st(70), cv(90, 3.9), hl(80, 15), cv(140, 3.9), st(140)],
  },
  // Morro do Pai Inácio e os vales da chapada: subidas, curvas fortes e esses no alto.
  {
    id: 'chapada_diamantina', name: 'Chapada Diamantina', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(90), cv(100, 4.1, 19), st(70), hl(120, 46), cv(90, 4.4), hl(110, 46), cv(90, 4.7), cv(100, 4.9, -19), st(80), st(120), st(100), ss(150, 4.4), st(70), ss(160, 4.4), st(80), cv(90, 4.2, 19), st(80), hl(120, 37), cv(80, 5), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · SE ─────────
  // Orla de Atalaia à noite. O traçado desenha um CARANGUEJO visto de cima: as garras abertas (largada no lado de fora
  // da esquerda), os olhos, o corpo e três patas de cada lado.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'aracaju', name: 'Orla de Atalaia', country: 'Brasil', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(4), cv(4, 2.45), st(21), cv(10, 4.48), st(23), cv(4, 1.89), st(19), cv(12, -5.16), st(33), cv(11, 4.58), st(24), cv(4, 2.55), st(31), cv(4, -3.93), st(30), cv(4, 0.95), st(26), cv(5, -4.38), st(3), cv(7, -3.77), st(16), cv(7, 3.77), st(6), cv(7, 3.77), st(16), cv(7, -3.77), st(33), cv(7, -3.77), st(16), cv(7, 3.77), st(6), cv(7, 3.77), st(16), cv(7, -3.77), st(3), cv(5, -4.39), st(26), cv(4, 0.96), st(30), cv(4, -3.93), st(31), cv(4, 2.54), st(24), cv(11, 4.58), st(33), cv(12, -5.16), st(19), cv(4, 1.91), st(23), cv(10, 4.48), st(21), cv(4, 2.44), st(3), hl(38, 16), st(3), cv(4, 4.9), st(30), cv(4, 0.54), st(35), cv(5, -4.08), st(21), cv(7, -4.15), st(3), hl(36, 15), st(4), cv(13, 4.74), st(39), cv(11, -4.57), st(37), cv(12, 5.3), st(3), hl(36, 15), st(3), cv(10, -4.73), st(39), cv(12, 5.08), st(3), hl(44, 19), st(4), cv(5, -3.93), st(3), hl(38, 16), st(3), cv(4, 4.15), st(3), hl(58, 25), st(4), cv(4, 4.14), st(3), hl(38, 16), st(3), cv(5, -3.93), st(3), hl(44, 19), st(4), cv(12, 5.08), st(39), cv(10, -4.72), st(3), hl(36, 15), st(3), cv(12, 5.3), st(37), cv(11, -4.57), st(39), cv(13, 4.73), st(43), cv(7, -4.15), st(21), cv(5, -4.07), st(35), cv(4, 0.54), st(30), cv(4, 4.89)],
  },
  // Quarta cidade mais antiga do país: ladeiras e esses entre os conventos da praça.
  {
    id: 'sao_cristovao', name: 'Praça de São Cristóvão', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(80), ss(150, 4.4), st(60), ss(160, 4.5), st(50), cv(70, 4.5), st(60), cv(80, 4.8), st(50), cv(80, 4.2), st(80), hl(70, 16), st(70), ss(120, 4.4), st(80), cv(70, -4.1), st(70), cv(80, 4.4), st(80), hl(90, 16), st(60), st(100)],
  },
  // Estrada da caatinga sobre os cânions do São Francisco: curvas fortes na borda do paredão.
  {
    id: 'xingo', name: 'Cânions do Xingó', country: 'Brasil', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), st(90), st(100), st(100), st(120), cv(90, 4.9, -20), st(50), st(90), hl(110, 51), cv(100, -4.5), hl(100, 40), cv(100, 5), cv(90, -4.1, 20), st(60), hl(100, 51), cv(90, 5.3), st(90), hl(110, 51), cv(80, 5.5), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · AL ─────────
  // Litoral de Alagoas: beira-mar plano entre coqueirais, retas e curvas abertas. A pista fácil do estado.
  {
    id: 'maragogi', name: 'Piscinas de Maragogi', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(160), cv(140, 3.5), st(180), cv(120, -3.5), st(160), ss(200, 3.5), st(140), cv(120, 4.6), st(160), cv(140, 3.5), st(120), cv(100, -4.6), st(150)],
  },
  // Pajuçara e Ponta Verde à noite. O traçado desenha uma JANGADA: a vela triangular no mastro (largada no pé dele), o
  // casco e as ondas embaixo.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'maceio', name: 'Orla de Maceió', country: 'Brasil', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(16), hl(120, 52), st(16), hl(120, 52), st(16), cv(15, 5.35), st(9), cv(6, 5.09), st(3), hl(58, 25), st(3), cv(4, 2.54), st(3), hl(74, 31), st(3), cv(4, 1.56), st(3), hl(80, 34), st(3), cv(4, 1.36), st(3), hl(74, 31), st(4), cv(4, 1.04), st(3), hl(48, 19), st(3), cv(4, 4.8), st(21), cv(15, -5.35), st(41), cv(7, -4.94), st(10), cv(24, 5.97), st(34), cv(4, 3.59), st(32), cv(4, 4.05), st(35), cv(15, 5.35), st(29), cv(14, -5.35), st(39), cv(12, 6), st(41), cv(12, -6), st(41), cv(12, 6), st(41), cv(12, -6), st(41), cv(12, 6), st(41), cv(12, -6), st(41), cv(12, 6), st(39), cv(14, -5.34), st(27), cv(17, 5.78), st(37), cv(5, 5.29), st(27), cv(6, 4.87), st(19), cv(9, 5.39), st(14), hl(120, 51), st(14), cv(15, -5.34)],
  },
  // Dunas de Piaçabuçu até o farol na foz do rio: lombadas de areia e curvas fortes.
  {
    id: 'foz_sao_francisco', name: 'Foz do São Francisco', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(130), hl(80, 45), st(50), cv(110, 6), st(70), hl(100, 30), st(50), hl(90, 30), st(70), cv(100, 6), st(80), hl(100, 30), st(60), hl(90, 30), st(60), cv(110, 6), st(80), hl(90, 45), st(70), ss(160, 6), st(60), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · PE ─────────
  // Bairro do Recife à noite: as pontes sobre o Capibaribe e as esquinas do Marco Zero.
  {
    id: 'recife_antigo', name: 'Recife Antigo', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 3, difficulty: 3,
    ops: [pit(40), st(130), cv(70, 5.4), st(120), cv(70, 6), st(110), cv(60, 6), st(110), ss(160, 5.7), st(80), cv(60, 6), st(90), cv(70, -5.4), st(140), st(170), cv(60, -6), st(80), cv(60, 6), st(90), st(200), st(100)],
  },
  // Colinas coloniais sobre o mar. O traçado desenha uma SOMBRINHA DE FREVO: a cúpula com a ponteira, a borda em gomos
  // entre as varetas e o cabo (largada).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'olinda', name: 'Ladeiras de Olinda', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(10), hl(120, 53), st(11), cv(4, -5.38), st(38), cv(13, -5.51), st(27), cv(13, 5.51), st(31), cv(13, -5.51), st(26), cv(14, 5.11), st(35), cv(13, -5.45), st(22), cv(14, 5.24), st(35), cv(14, -5.26), st(10), cv(16, 5.8), st(3), hl(38, 16), st(4), cv(4, 4.46), st(3), hl(38, 16), st(4), cv(4, 3.29), st(3), hl(46, 19), st(4), cv(4, 1.95), st(3), hl(54, 23), st(4), cv(4, 1.25), st(3), hl(50, 22), st(3), cv(4, 1.63), st(37), cv(4, -4.83), st(20), cv(11, 4.95), st(20), cv(4, -4.82), st(37), cv(4, 1.62), st(3), hl(50, 21), st(3), cv(4, 1.25), st(3), hl(54, 23), st(4), cv(4, 1.96), st(3), hl(46, 19), st(4), cv(4, 3.28), st(45), cv(4, 4.48), st(45), cv(16, 5.8), st(10), cv(14, -5.27), st(35), cv(14, 5.25), st(22), cv(13, -5.45), st(35), cv(14, 5.11), st(26), cv(13, -5.51), st(31), cv(13, 5.51), st(27), cv(13, -5.51), st(38), cv(4, -5.39), st(30), hl(120, 52), st(31), cv(11, 4.95), st(17), cv(11, 4.96)],
  },
  // A BR mais curta do país, de praia em praia: esses e morros com o Morro do Pico ao fundo.
  {
    id: 'noronha', name: 'Fernando de Noronha', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), ss(130, 4.6), st(60), ss(130, 5.1), st(80), ss(130, 5.2), st(70), hl(80, 19), st(60), ss(140, 4.5), st(60), hl(70, 19), st(60), ss(140, 5), st(60), cv(160, 4.7), st(80), cv(120, 5), st(80), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · PB ─────────
  // Ponta do Seixas, o ponto mais oriental das Américas: curvas médias no alto das falésias, à beira-mar.
  {
    id: 'joao_pessoa', name: 'Ponta do Seixas', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(100), cv(100, 4.6), st(70), ss(160, 3.7), hl(90, 23), cv(90, -4.6), st(80), cv(110, 3.7, 14), ss(160, 3.7), st(90), cv(90, 5.5), hl(80, 18), cv(100, -4.6), st(80), ss(160, 4.6), st(90), cv(100, 3.7, -14), st(140)],
  },
  // Campina Grande em noite de São João. O traçado desenha um BALÃO JUNINO: a ponta, o corpo de papel (largada no lado
  // esquerdo) com a franja recortada, a boca e a bucha acesa pendurada.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'campina_grande', name: 'Parque do Povo', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(8), hl(120, 50), st(9), cv(9, 5.7), st(88), cv(4, 0.46), st(3), hl(90, 36), st(3), cv(4, -5.79), st(47), cv(19, 5.55), st(47), cv(4, -5.78), st(96), cv(4, 0.46), st(88), cv(9, 5.7), st(28), hl(120, 50), st(29), cv(4, 4.51), st(38), cv(21, 5.75), st(19), cv(18, -5.82), st(23), cv(18, 5.82), st(20), cv(20, -6), st(4), hl(120, 50), st(4), cv(4, -5.31), st(15), cv(15, 5.33), st(4), cv(19, -5.42), st(34), cv(6, 4.55), st(28), cv(4, 6), st(29), cv(17, 5.75), st(29), cv(4, 6), st(28), cv(6, 4.55), st(34), cv(19, -5.42), st(4), cv(15, 5.33), st(15), cv(4, -5.31), st(4), hl(120, 50), st(4), cv(20, -6), st(20), cv(18, 5.82), st(23), cv(18, -5.82), st(19), cv(21, 5.75), st(38), cv(4, 4.51)],
  },
  // Agreste dos lajedos com a Pedra da Boca: subidas curtas e curvas fortes entre as rochas.
  {
    id: 'pedra_da_boca', name: 'Pedra da Boca', country: 'Brasil', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(90), hl(120, 49), cv(100, -4), hl(100, 49), cv(90, 4.2), ss(170, 4.8), st(80), ss(150, 3.9), st(60), st(100), hl(120, 39), cv(90, 5.1), hl(130, 29), cv(90, 5.1), st(80), ss(170, 4.5), st(80), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · RN ─────────
  // Dunas de Genipabu: lombadas e descidas como num passeio de buggy, curvas entre as dunas.
  {
    id: 'natal', name: 'Dunas de Genipabu', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(100), hl(80, 25), st(60), hl(80, 28), cv(100, 4), st(80), hl(100, 32), cv(90, -5), st(80), hl(80, 25), ss(160, 4), st(80), hl(90, 30), cv(100, 4), st(100), hl(80, 25), cv(90, 5), st(80), hl(100, 30), st(140)],
  },
  // Ponta Negra ao entardecer: orla com o Morro do Careca, lombadas e curvas fortes.
  {
    id: 'ponta_negra', name: 'Morro do Careca', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), hl(90, 36), st(50), cv(100, 5.4), st(70), ss(160, 5.4), st(60), cv(100, 5.4), st(80), cv(110, 5), st(70), cv(100, 5.3), st(90), hl(90, 24), st(80), ss(160, 5.4), st(90), hl(100, 24), st(70), st(100)],
  },
  // Em volta do maior cajueiro do mundo. O traçado desenha o PRÓPRIO CAJUEIRO de lado: a copa larga cheia de tufos e os
  // galhos que descem até o chão, com os vãos em arco (largada no pé da esquerda). O caju — fruto e castanha — não se
  // leu numa linha só (docs/PISTAS.md, "Pistas com desenho").
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'cajueiro_pirangi', name: 'Cajueiro de Pirangi', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(45), cv(6, 3.83), st(3), hl(44, 19), st(3), cv(4, -0.62), st(37), cv(12, 5.1), st(14), cv(8, 5.09), st(14), cv(12, 5.1), st(11), cv(35, -4.87), st(13), cv(11, 4.41), st(18), cv(8, 5.01), st(18), cv(11, 4.42), st(24), cv(27, -4.85), st(24), cv(11, 4.42), st(18), cv(8, 5.01), st(18), cv(11, 4.42), st(27), cv(23, -4.67), st(27), cv(11, 4.43), st(18), cv(8, 5.01), st(18), cv(11, 4.42), st(24), cv(27, -4.85), st(24), cv(11, 4.41), st(18), cv(8, 5.02), st(18), cv(11, 4.41), st(13), cv(35, -4.87), st(11), cv(12, 5.1), st(14), cv(8, 5.09), st(14), cv(12, 5.1), st(37), cv(4, -0.63), st(50), cv(6, 3.84), st(3), hl(78, 35), st(4), cv(25, 4.92), cv(19, 4.73), st(23), cv(13, -4.74), st(6), cv(6, -4.37), st(8), cv(6, -4.37), st(6), cv(13, -4.73), st(23), cv(19, 4.72), st(17), cv(20, 5.04), st(3), hl(52, 23), st(3), cv(14, -4.6), st(15), cv(6, -4.2), st(17), cv(6, -4.19), st(15), cv(14, -4.6), st(3), hl(52, 23), st(3), cv(20, 5.04), st(3), hl(68, 30), st(3), cv(20, 5), st(3), hl(46, 20), st(4), cv(13, -5.04), st(14), cv(6, -4.23), st(16), cv(6, -4.23), st(14), cv(13, -5.04), st(3), hl(46, 20), st(4), cv(20, 5.01), st(17), cv(19, 4.58), st(24), cv(12, -5.02), st(7), cv(7, -3.72), st(8), cv(7, -3.71), st(7), cv(12, -5.02), st(24), cv(19, 4.58), st(20), cv(25, 4.92)],
  },
  // ───────── Expedição Brasil · nordeste · CE ─────────
  // Areia e dunas da vila: curvas largas e compridas, morros de areia entre elas.
  {
    id: 'jericoacoara', name: 'Jericoacoara', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), cv(160, 4.9), st(100), hl(100, 24), cv(140, -4.9), st(100), cv(160, 4.9), hl(90, 21), st(80), cv(150, 4.9), st(110), cv(140, -3.9), hl(100, 24), cv(160, 4.9), st(140)],
  },
  // Avenida Beira-Mar à noite até a Ponte dos Ingleses: orla com esquinas e retornos.
  {
    id: 'fortaleza_beira_mar', name: 'Beira-Mar de Fortaleza', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(100), ss(140, 6), st(60), cv(60, 6), st(90), cv(60, 6), st(100), st(140), cv(60, 6), st(70), cv(60, 6), st(70), cv(60, 6), st(90), ss(150, 6), st(90), hl(90, 12), st(60), st(90)],
  },
  // Falésias vermelhas e dunas. O traçado desenha a LUA CRESCENTE das falésias, pontas para a direita (largada no
  // dorso). A lua é toda arco — só as pontas são quina viva: o índice vem das lombadas, nas retas e dentro das curvas.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'canoa_quebrada', name: 'Falésias de Canoa Quebrada', country: 'Brasil', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(27), cv(14, 0.78), st(19), cv(4, 0.62), st(3), hl(54, 24), st(3), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.79), st(3), hl(50, 22), st(3), cv(12, 0.81), st(3), hl(50, 22), st(3), cv(12, 0.79), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(50, 22), st(4), cv(10, 0.72), st(9), cv(17, 6), st(3), cv(10, -0.72), st(3), hl(38, 17), st(4), cv(11, -0.74), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.74), st(3), hl(38, 17), st(4), cv(11, -0.75), st(3), hl(38, 17), st(4), cv(11, -0.75), st(45), cv(11, -0.75), st(45), cv(11, -0.75), st(45), cv(11, -0.75), st(45), cv(11, -0.74), st(45), cv(10, -0.72), st(3), cv(17, 6), st(9), cv(10, 0.72), st(3), hl(50, 22), st(4), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.79), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(50, 22), st(3), cv(12, 0.8), st(3), hl(54, 24), st(3), cv(4, 0.63), st(19), cv(14, 0.78)],
  },
  // ───────── Expedição Brasil · nordeste · PI ─────────
  // Entre os braços do único delta em mar aberto das Américas: curvas longas e fortes no mangue.
  {
    id: 'delta_parnaiba', name: 'Delta do Parnaíba', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), cv(150, 5.3), st(100), cv(160, 5.8), st(80), ss(190, 5.3), st(100), hl(80, 29), st(80), hl(90, 29), st(80), hl(80, 15), st(60), cv(160, -5.6), st(90), hl(80, 15), st(70), st(100)],
  },
  // Entre as formações de pedra do parque: esses e curvas fortes em volta das 'cidades'.
  {
    id: 'sete_cidades', name: 'Pedras de Sete Cidades', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(90), hl(70, 19), st(60), hl(90, 19), st(70), hl(80, 19), st(50), ss(140, 5.1), st(60), ss(150, 5), st(70), hl(80, 19), st(60), ss(160, 5.1), st(60), ss(140, 4.8), st(50), cv(160, 5.2), st(70), cv(160, 4.6), st(80), st(100)],
  },
  // Caatinga e paredões de pedra. O traçado desenha uma CAPIVARA de lado: o corpo de barril (largada no traseiro), a
  // cabeça de focinho rombudo, a orelhinha e as quatro patas.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'serra_capivara', name: 'Serra da Capivara', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(59), cv(4, 5.14), st(46), cv(6, 4.51), st(55), cv(4, 5.27), st(3), hl(82, 34), st(4), cv(4, 2.13), st(3), hl(72, 31), st(3), cv(4, -3.78), st(36), cv(8, -5.77), st(25), cv(10, 5.44), st(14), cv(11, 5.41), st(21), cv(10, -5.65), st(44), cv(4, 3.92), st(46), cv(6, 4.67), st(36), cv(5, 4.78), st(3), hl(70, 29), st(4), cv(6, 5.12), st(34), cv(6, 4.65), st(58), cv(5, -4.98), st(38), cv(6, -5.15), st(3), hl(102, 43), st(3), cv(13, 6), st(20), cv(14, 5.72), st(3), hl(60, 25), st(3), cv(14, -5.72), st(9), cv(14, -5.72), st(3), hl(58, 24), st(4), cv(14, 5.72), st(14), cv(14, 5.72), st(3), hl(58, 24), st(4), cv(15, -5.52), st(3), hl(86, 36), st(3), cv(13, -5.97), st(56), cv(14, 5.73), st(14), cv(14, 5.72), st(55), cv(14, -5.72), st(14), cv(14, -5.73), st(55), cv(14, 5.73), st(19), cv(14, 5.72), st(3), hl(64, 27), st(3), cv(7, -4.82), st(30), cv(6, 4.74), st(31), cv(4, 1.73)],
  },
  // ───────── Expedição Brasil · nordeste · MA ─────────
  // Centro histórico de azulejos à noite. O traçado desenha a CABEÇA DO BOI do bumba-meu-boi, de frente: os chifres, as
  // orelhas e a cara (largada na bochecha esquerda) até o focinho.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'sao_luis', name: 'Casarões de São Luís', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 3, difficulty: 4,
    ops: [pit(40), st(21), cv(15, -5.59), st(3), hl(70, 31), st(4), cv(5, 5.14), st(28), cv(20, 6), st(23), cv(6, 4.65), st(47), cv(26, -5.86), st(14), cv(4, 1), st(3), hl(54, 24), st(3), cv(4, 5.52), st(3), hl(56, 25), st(4), cv(4, 5.45), st(52), cv(5, 5.13), st(13), cv(23, 5.54), st(23), cv(4, 0.75), st(57), cv(5, -4.58), st(51), cv(4, -4.97), st(3), hl(52, 23), st(4), cv(4, -5.88), st(3), hl(52, 23), st(3), cv(4, 4.74), st(3), hl(52, 23), st(3), cv(4, -5.87), st(59), cv(4, -4.97), st(51), cv(5, -4.58), st(57), cv(4, 0.73), st(23), cv(23, 5.54), st(13), cv(5, 5.14), st(52), cv(4, 5.45), st(3), hl(56, 25), st(4), cv(4, 5.53), st(3), hl(54, 24), st(3), cv(4, 1.01), st(14), cv(26, -5.86), st(47), cv(6, 4.64), st(23), cv(20, 6), st(28), cv(5, 5.13), st(3), hl(70, 31), st(4), cv(15, -5.59), st(3), hl(54, 24), st(4), cv(4, 3.24), st(3), hl(80, 36), st(4), cv(5, -4.84), st(39), cv(8, 5.77), st(43), cv(10, 5.34), st(44), cv(10, 5.33), st(43), cv(8, 5.77), st(39), cv(5, -4.84), st(3), hl(80, 36), st(4), cv(4, 3.23)],
  },
  // Morros de topo plano e cachoeiras: estrada de terra com ondulações e curvas fortes.
  {
    id: 'chapada_das_mesas', name: 'Chapada das Mesas', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), st(160), cv(110, 6), cv(100, 6), st(120), cv(100, 6), st(110), st(160), cv(120, 6), st(160), cv(110, 6), cv(110, 6), st(110), st(90)],
  },
  // Dunas brancas e lagoas: ondulação constante, uma lombada atrás da outra, e curvas fortes entre as dunas.
  {
    id: 'lencois', name: 'Lençóis Maranhenses', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), hl(80, 21), hl(80, 21), cv(100, 4.8), hl(80, 19), hl(80, 21), cv(90, -4.8), hl(80, 21), ss(160, 3.9), hl(80, 19), hl(80, 21), cv(100, 4.8), hl(80, 21), cv(90, -3.9), hl(80, 19), hl(80, 21), cv(100, 4.8), st(140)],
  },
  // ───────── Expedição Brasil · norte · PA ─────────
  // O 'Caribe amazônico': praias de rio no Tapajós, curvas longas e fortes na margem.
  {
    id: 'alter_do_chao', name: 'Praias de Alter do Chão', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), ss(180, 4.8), st(90), ss(190, 5.3), st(90), cv(140, -4.5), st(90), hl(80, 13), st(60), cv(140, 5.5), st(80), cv(160, 5.2), st(90), cv(170, 4.5), st(100), st(100)],
  },
  // Cais do Ver-o-Peso. O traçado desenha o MERCADO DE FERRO de frente: o prédio comprido (largada na parede da
  // esquerda) e as quatro torres de telhado pontudo.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'belem', name: 'Ver-o-Peso', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(3), hl(68, 28), st(4), cv(17, 5.96), st(2), cv(17, -5.96), st(3), hl(64, 25), st(4), cv(4, 3.64), st(33), cv(29, 6), st(33), cv(4, 3.65), st(3), hl(64, 25), st(4), cv(17, -5.96), st(36), cv(17, -5.97), st(55), cv(4, 3.65), st(33), cv(29, 6), st(33), cv(4, 3.64), st(56), cv(17, -5.96), st(40), cv(17, -5.97), st(55), cv(4, 3.66), st(33), cv(29, 6), st(33), cv(4, 3.64), st(55), cv(17, -5.96), st(36), cv(17, -5.97), st(3), hl(64, 25), st(4), cv(4, 3.65), st(33), cv(29, 6), st(33), cv(4, 3.65), st(71), cv(17, -5.96), st(2), cv(17, 5.96), st(3), hl(108, 44), st(4), cv(17, 5.96), st(31), hl(120, 50), st(31), hl(120, 50), st(31), cv(17, 5.97)],
  },
  // A ilha dos búfalos: aterros entre os campos alagados e palafitas, esses fortes sem fim.
  {
    id: 'marajo', name: 'Campos do Marajó', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), ss(150, 5), st(50), ss(150, 5.7), st(60), cv(90, 5.5), st(50), ss(140, 5.1), st(80), hl(90, 21), st(60), ss(150, 5.5), st(70), ss(120, 5.1), st(80), cv(80, 5.7), st(70), cv(150, 5.2), st(60), st(100)],
  },
  // ───────── Expedição Brasil · norte · AM ─────────
  // Encontro das Águas. O traçado desenha a CÚPULA DO TEATRO AMAZONAS: a cúpula em sino com a agulha, sobre o tambor e
  // o prédio largo (largada na parede da esquerda).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'manaus', name: 'Encontro das Águas', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(3), hl(102, 44), st(3), cv(24, 6), st(14), hl(120, 54), st(14), cv(24, -6), st(43), cv(12, -5.94), st(6), cv(10, 5.41), st(43), cv(5, 5.25), st(53), cv(5, 5.22), st(55), cv(4, 4.63), st(51), st(21), cv(12, -5.94), st(28), cv(7, 4.74), st(39), cv(35, 5.57), st(39), cv(7, 4.75), st(28), cv(12, -5.94), st(21), st(51), cv(4, 4.63), st(55), cv(5, 5.21), st(53), cv(5, 5.26), st(43), cv(10, 5.41), st(6), cv(12, -5.94), st(43), cv(24, -6), st(14), hl(120, 53), st(14), cv(24, 6), st(14), hl(120, 53), st(14), cv(24, 6), st(47), hl(120, 53), st(47), hl(120, 53), st(47), hl(120, 53), st(50), cv(24, 6)],
  },
  // Manaus à noite e a ponte estaiada sobre o Rio Negro: esquinas e a lombada longa da ponte.
  {
    id: 'ponte_rio_negro', name: 'Ponte do Rio Negro', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), ss(150, 5.6), st(80), st(130), ss(150, 5.2), st(90), ss(150, 5.5), st(100), cv(70, 5.5), st(80), ss(160, 5.8), st(100), cv(60, 5.9), st(110), cv(60, 5.2), st(100), cv(70, 6), st(110), st(100)],
  },
  // A ilha do boi-bumbá: ruas em volta do Bumbódromo, esses fortes e esquinas.
  {
    id: 'parintins', name: 'Bumbódromo de Parintins', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(90), ss(130, 5.5), st(70), ss(140, 6), st(70), ss(160, 5.5), st(50), hl(80, 24), st(60), hl(80, 24), st(60), ss(160, 5.9), st(60), cv(140, 5.6), st(70), hl(90, 24), st(60), cv(130, 5.8), st(70), st(120)],
  },
  // ───────── Expedição Brasil · norte · AP ─────────
  // Marco Zero do Equador. O traçado desenha a planta da FORTALEZA DE SÃO JOSÉ: as muralhas (largada na de oeste) e os
  // quatro baluartes em ponta de lança nos cantos, de quina arredondada (o cenário de mata no orçamento).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'macapa', name: 'Marco Zero do Equador', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(68), cv(18, -5.11), st(21), cv(15, 4.89), st(3), hl(92, 40), st(4), cv(24, 5.4), st(99), cv(15, 4.89), st(21), cv(18, -5.11), st(3), hl(102, 45), st(3), cv(18, -5.1), st(21), cv(15, 4.88), st(99), cv(24, 5.4), st(99), cv(15, 4.89), st(21), cv(18, -5.11), st(3), hl(100, 45), st(4), cv(18, -5.1), st(21), cv(15, 4.89), st(99), cv(24, 5.4), st(99), cv(15, 4.88), st(21), cv(18, -5.1), st(3), hl(100, 44), st(4), cv(18, -5.11), st(21), cv(15, 4.89), st(99), cv(24, 5.4), st(99), cv(15, 4.88), st(21), cv(18, -5.1)],
  },
  // Na margem onde a pororoca sobe o rio: curvas longas e fortíssimas acompanhando a onda.
  {
    id: 'pororoca_araguari', name: 'Pororoca do Araguari', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(110), cv(150, 6), st(70), hl(90, 31), st(80), hl(90, 16), st(60), hl(90, 16), st(60), cv(160, 6), st(80), cv(150, -6), st(90), cv(170, 6), st(70), cv(170, 6), st(80), st(90)],
  },
  // A vila da mineração na floresta: serra fechada, grampos e esses na mata.
  {
    id: 'serra_do_navio', name: 'Serra do Navio', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(120), hl(110, 43), cv(90, 5.2), ss(180, 4.6), st(70), st(100), cv(80, -5.8, -22), st(80), hl(110, 54), cv(90, 4.8), ss(150, 5.1), st(60), ss(150, 4.9), st(60), hl(100, 43), cv(110, 4.7), st(100)],
  },
  // ───────── Expedição Brasil · norte · RR ─────────
  // A capital em leque à noite: avenidas radiais e esquinas em volta do monumento.
  {
    id: 'boa_vista', name: 'Avenidas de Boa Vista', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(60), cv(60, 6), st(60), cv(70, 6), st(70), cv(70, 6), st(60), cv(70, 5.8), st(70), cv(70, 6), st(90), st(100), ss(140, 5.6), st(50), st(130), cv(70, -6), st(60), ss(140, 5.2), st(70), st(70)],
  },
  // Lavrado com o tepui ao fundo. O traçado desenha o TEPUI: o planalto achatado com as rochas, os paredões (largada no
  // de oeste) e as encostas até a base.
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'monte_roraima', name: 'Monte Roraima', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(3), hl(44, 17), st(4), cv(18, 5.82), st(5), cv(18, -5.82), st(3), hl(92, 40), st(3), cv(18, 5.82), st(3), hl(48, 20), st(3), cv(14, 5.56), st(7), cv(26, -5.81), st(12), cv(12, 6), st(40), cv(12, 5.72), st(4), cv(21, -5.8), st(10), cv(9, 5.71), st(47), cv(14, 5.56), st(7), cv(26, -5.81), st(12), cv(12, 6), st(40), cv(12, 5.73), st(4), cv(21, -5.8), st(10), cv(9, 5.7), st(18), cv(18, 5.82), st(45), cv(18, -5.82), st(5), cv(18, 5.82), st(6), hl(120, 52), st(6), cv(5, -5.47), st(40), cv(4, 1.08), st(3), hl(76, 33), st(3), cv(4, 1.45), st(3), hl(88, 38), st(4), cv(22, 5.75), st(61), hl(120, 52), st(61), hl(120, 52), st(61), hl(120, 52), st(64), cv(22, 5.8), st(3), hl(60, 26), st(4), cv(4, 1.15), st(3), hl(76, 33), st(3), cv(4, 1.09), st(40), cv(4, 5.06), st(12), cv(9, -5.71)],
  },
  // Praias de lago no lavrado entre buritis: esses fortes e lombadas de areia.
  {
    id: 'lago_caracarana', name: 'Lago Caracaranã', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(140), hl(80, 43), st(60), cv(90, 6), st(70), cv(70, 6), st(60), ss(130, 6), st(60), hl(80, 43), st(60), cv(90, 6), st(60), ss(160, 6), st(60), ss(120, 6), st(70), hl(80, 43), st(70), hl(80, 43), st(50), st(100)],
  },
  // ───────── Expedição Brasil · norte · RO ─────────
  // Estrada de Ferro Madeira-Mamoré. O traçado desenha a LOCOMOTIVA de lado: a cabine (largada na traseira), a caldeira
  // com o domo, a chaminé, o limpa-trilhos e as rodas; quinas um pouco arredondadas (o cenário de mata no orçamento).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'porto_velho', name: 'Madeira-Mamoré', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(24), hl(120, 52), st(24), cv(13, -5.8), st(4), cv(13, 5.8), st(4), cv(13, 5.8), st(5), hl(120, 51), st(6), cv(13, 5.8), st(4), cv(13, 5.8), st(4), cv(13, -5.8), st(62), cv(13, -5.8), st(3), hl(88, 37), st(3), cv(10, -5.16), st(14), cv(8, 5.48), st(4), cv(4, 5.71), st(4), cv(8, 5.48), st(14), cv(10, -5.16), st(55), cv(13, -5.6), st(3), hl(70, 29), st(4), cv(6, -4.7), st(18), cv(6, 4.33), st(9), cv(13, 5.8), st(43), cv(13, 5.8), st(9), cv(6, 4.33), st(18), cv(6, -4.7), st(3), hl(70, 29), st(4), cv(13, -5.59), st(18), cv(7, 4.4), st(21), cv(6, 4.94), st(3), hl(86, 36), st(3), cv(4, -4.06), st(3), hl(64, 26), st(4), cv(17, 5.68), st(29), cv(13, 5.8), st(33), cv(13, -5.8), st(6), cv(10, -5.08), st(19), cv(8, 5.35), st(7), cv(4, 5.77), st(6), cv(8, 5.36), st(19), cv(10, -5.08), st(9), cv(10, -5.35), st(30), cv(7, 4.36), st(10), cv(4, 4.64), st(11), cv(4, 4.65), st(10), cv(7, 4.36), st(30), cv(10, -5.35), st(9), cv(10, -5.34), st(30), cv(7, 4.35), st(10), cv(4, 4.66), st(11), cv(4, 4.65), st(10), cv(7, 4.36), st(30), cv(10, -5.35), st(9), cv(10, -5.34), st(30), cv(7, 4.35), st(10), cv(4, 4.65), st(11), cv(4, 4.65), st(10), cv(7, 4.36), st(30), cv(10, -5.35), st(40), cv(13, 5.8)],
  },
  // O forte em estrela na margem do Guaporé: esses fortes em volta das muralhas.
  {
    id: 'forte_principe', name: 'Forte Príncipe da Beira', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), hl(90, 23), st(50), ss(140, 6), st(60), ss(140, 5.9), st(50), hl(90, 23), st(60), ss(130, 5.6), st(80), cv(80, 6), st(80), cv(80, 6), st(70), ss(120, 6), st(50), ss(150, 5.9), st(60), st(100)],
  },
  // Várzea do Guaporé entre jacarés e palafitas: curvas longas e fortíssimas no alagado.
  {
    id: 'vale_guapore', name: 'Vale do Guaporé', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(80), hl(80, 41), st(70), hl(80, 41), st(70), cv(160, 6), st(90), cv(140, 6), st(90), cv(170, 6), st(90), hl(90, 41), st(70), cv(150, 6), st(100), cv(160, -6), st(90), st(100)],
  },
  // ───────── Expedição Brasil · norte · AC ─────────
  // Margem do rio Acre. O traçado desenha a GAMELEIRA que dá nome à pista: a copa em domo cheia de tufos, o tronco
  // grosso (largada) e a base alargando nas raízes. Era a folha de seringueira, mas pontas finas e lados retos não têm
  // curva longa, e a mata em volta passava do orçamento do cenário (docs/PISTAS.md, "Pistas com desenho").
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'rio_branco', name: 'Gameleira de Rio Branco', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(3), hl(24, 10), st(3), cv(22, -4.59, 8), st(11), cv(10, -4.53), st(19), cv(10, 4.49), st(6), cv(6, 4.26), st(6), cv(10, 4.49), st(10), cv(22, -4.75, -8), st(10), cv(10, 4.25), st(7), cv(7, 3.55), st(7), cv(10, 4.25), st(14), cv(18, -4.7, 7), st(15), cv(9, 4.66), st(9), cv(6, 4.26), st(9), cv(9, 4.66), st(15), cv(17, -4.74, 6), st(17), cv(8, 4.99), st(12), cv(6, 4.16), st(12), cv(8, 5), st(18), cv(16, -4.93, 6), st(18), cv(8, 4.74), st(13), cv(6, 4.07), st(13), cv(8, 4.74), st(18), cv(16, -4.97, -6), st(19), cv(8, 4.51), st(15), cv(6, 3.99), st(15), cv(8, 4.51), st(18), cv(17, -4.87, -6), st(18), cv(8, 4.61), st(14), cv(6, 4.01), st(14), cv(8, 4.61), st(16), cv(20, -4.9, 8), st(12), cv(10, 4.26), st(6), cv(7, 3.56), st(6), cv(10, 4.27), st(12), cv(20, -4.91, -8), st(16), cv(8, 4.61), st(14), cv(6, 4.02), st(14), cv(8, 4.61), st(18), cv(17, -4.87, 6), st(18), cv(8, 4.51), st(15), cv(6, 3.98), st(15), cv(8, 4.52), st(19), cv(16, -4.97, 6), st(18), cv(8, 4.73), st(13), cv(6, 4.08), st(13), cv(8, 4.74), st(18), cv(16, -4.93, -6), st(18), cv(8, 5), st(12), cv(6, 4.15), st(12), cv(8, 5), st(17), cv(17, -4.74, -6), st(15), cv(9, 4.66), st(9), cv(6, 4.25), st(9), cv(9, 4.67), st(15), cv(18, -4.7, -7), st(14), cv(10, 4.24), st(7), cv(7, 3.56), st(7), cv(10, 4.25), st(10), cv(22, -4.75, 8), st(10), cv(10, 4.49), st(6), cv(6, 4.25), st(6), cv(10, 4.49), st(19), cv(10, -4.52), st(11), cv(22, -4.59, -8), st(3), hl(64, 28), st(3), cv(9, -4.51), st(3), hl(38, 17), st(4), cv(5, -3.96), st(22), cv(33, 4.88, 13), st(19), cv(14, 4.47, 5), st(6), cv(11, -4.08, 4), cv(6, -4.23), cv(11, -4.08, -4), st(6), cv(14, 4.48, -5), st(3), hl(46, 20), st(4), cv(14, 4.48, 5), st(6), cv(11, -4.09, 3), cv(6, -4.23), cv(11, -4.08, -3), st(6), cv(14, 4.48, -5), st(19), cv(33, 4.88, -13), st(22), cv(5, -3.98), st(3), hl(38, 17), st(4), cv(9, -4.51)],
  },
  // Entre os desenhos geométricos na terra: curvas fortes que contornam os círculos e quadrados.
  {
    id: 'geoglifos', name: 'Geoglifos do Acre', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), cv(70, 6), st(50), cv(90, 6), st(80), hl(90, 39), st(60), cv(90, 6), st(60), cv(70, -6), st(50), ss(140, 6), st(60), hl(70, 39), st(60), ss(130, 6), st(70), ss(140, 6), st(50), hl(80, 39), st(60), hl(70, 39), st(60), st(100)],
  },
  // A Interoceânica rumo ao Peru, entre castanheiras: morros, grampos e esses fortes.
  {
    id: 'estrada_pacifico', name: 'Estrada do Pacífico', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(110), st(90), hl(130, 45), cv(100, 6), cv(90, 6, -30), st(80), hl(130, 59), cv(100, 6), hl(120, 45), cv(90, -6), ss(160, 6), st(60), st(120), st(110), hl(140, 45), cv(100, 6), st(120)],
  },
  // ───────── Expedição Brasil · norte · TO ─────────
  // Areia laranja e a serra do Espírito Santo: retas com lombadas grandes e curvas fortes nas dunas.
  {
    id: 'jalapao', name: 'Dunas do Jalapão', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(120), hl(120, 49), cv(90, 6), st(100), hl(120, 56), cv(80, -6), st(90), hl(100, 43), ss(160, 6), st(90), hl(120, 49), cv(90, 6), st(80), cv(90, -6), hl(100, 43), st(80), cv(80, 6), st(140)],
  },
  // A capital mais nova à noite. O traçado desenha o SOL DA BANDEIRA DO TOCANTINS: dezesseis raios em ponta em volta do
  // disco (largada na borda de um raio).
  // Desenho em scripts/track-shapes.ts (ops gerados por scripts/shape-to-track.ts); tests/track.test.ts trava a forma.
  {
    id: 'palmas', name: 'Ponte de Palmas', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(3), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.68), st(12), cv(7, -4.48), st(3), hl(18, 8), st(4), cv(16, 6), st(3), hl(18, 8), st(4), cv(7, -4.48), st(11), cv(8, -5.68), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.68), st(11), cv(7, -4.48), st(3), hl(18, 7), st(4), cv(16, 6), st(3), hl(18, 7), st(4), cv(7, -4.48), st(11), cv(8, -5.69), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.68), st(12), cv(7, -4.48), st(3), hl(18, 7), st(4), cv(16, 6), st(25), cv(7, -4.48), st(11), cv(8, -5.68), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.68), st(11), cv(7, -4.48), st(25), cv(16, 6), st(25), cv(7, -4.48), st(11), cv(8, -5.69), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.67), st(12), cv(7, -4.49), st(25), cv(16, 6), st(25), cv(7, -4.48), st(11), cv(8, -5.68), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 16), st(4), cv(8, -5.68), st(11), cv(7, -4.48), st(25), cv(16, 6), st(25), cv(7, -4.48), st(11), cv(8, -5.68), st(3), hl(36, 16), st(4), cv(18, 5.74), st(3), hl(36, 15), st(4), cv(8, -5.68), st(12), cv(7, -4.48), st(25), cv(16, 6), st(25), cv(7, -4.49), st(11), cv(8, -5.68), st(3), hl(36, 15), st(4), cv(18, 5.74), st(3), hl(36, 15), st(4), cv(8, -5.67), st(11), cv(7, -4.49), st(25), cv(16, 6), st(25), cv(7, -4.48), st(11), cv(8, -5.68)],
  },
  // A maior ilha fluvial do mundo: praias de rio e curvas longas e fechadíssimas no Araguaia.
  {
    id: 'ilha_do_bananal', name: 'Ilha do Bananal', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(120), hl(90, 31), st(50), cv(140, -6), st(80), hl(80, 16), st(50), ss(190, 6), st(80), cv(170, 6), st(80), cv(180, 6), st(80), cv(160, 6), st(90), st(90)],
  },
  // ───────── Mundial · Estados Unidos ─────────
  {
    id: 'rota_66', name: 'Rota 66', country: 'Estados Unidos', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(210), cv(120, 2), st(300), cv(100, -2), st(200), cv(140, 3), hl(100, 15), st(250), cv(120, -3), st(220)],
  },
  // Rodovia de montanha: subidas e descidas grandes com curvas longas de média força.
  {
    id: 'rochosas', name: 'Montanhas Rochosas', country: 'Estados Unidos', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(100), hl(160, 50), cv(140, 3, 20), st(120), cv(120, -4), hl(140, 40), st(100), cv(160, 3, -25), st(140), ss(200, 3), st(100), cv(120, 4, 15), hl(120, 35), st(80), cv(120, 3), st(120)],
  },
  {
    id: 'canion', name: 'Cânion de Nevada', country: 'Estados Unidos', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(60), hl(120, 50), cv(100, 5, 20), st(80), cv(80, -5), hl(140, 40), ss(180, 4), st(100), cv(120, 4, -25), st(90), cv(100, -6), hl(100, 30), st(160), cv(90, 3), st(100)],
  },
  {
    id: 'las_vegas', name: 'Strip de Las Vegas', country: 'Estados Unidos', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(160), cv(80, -4), st(150), ss(160, 5), st(120), cv(100, 4), st(200), cv(60, -6), st(100), ss(180, 3), st(160), cv(100, 5), st(150)],
  },
  // ───────── Mundial · Japão ─────────
  {
    id: 'baia_toquio', name: 'Baía de Tóquio', country: 'Japão', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(110), cv(120, 3), st(100), cv(80, -4), hl(100, 20), st(160), ss(200, 3), st(120), cv(100, 4), st(200), cv(120, -3), st(120), cv(90, 2), st(140)],
  },
  // Estrada na mata subtropical de Okinawa: esses encadeados e curvas médias entre morrotes.
  {
    id: 'yanbaru', name: 'Floresta de Yanbaru', country: 'Japão', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(80), cv(100, 4), st(60), ss(160, 4), hl(80, 20), cv(90, 5), st(80), cv(110, 4, 15), ss(160, 3), st(120), cv(80, -5), hl(100, 25), st(70), cv(120, 4), ss(160, 4), st(100), cv(90, 5, -15), st(140)],
  },
  {
    id: 'monte_fuji', name: 'Monte Fuji', country: 'Japão', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(60), hl(150, 60), cv(120, -4, 30), st(60), cv(100, 5), hl(120, 45), ss(180, 4), cv(80, -6), st(100), hl(100, 30), cv(120, 4, -30), st(120), cv(100, -5), hl(90, 25), st(130)],
  },
  {
    id: 'osaka_neon', name: 'Neon de Osaka', country: 'Japão', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(80), ss(200, 5), st(80), cv(60, -6), st(100), cv(80, 6), ss(160, 4), st(140), cv(100, -5), st(80), ss(180, 5), st(100), cv(70, 6), st(120), cv(80, -4), st(130)],
  },
  // ───────── Mundial · Europa ─────────
  {
    id: 'autobahn', name: 'Autobahn', country: 'Europa', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(310), cv(150, 2), st(300), cv(120, -3), st(250), ss(200, 2), st(300), cv(140, 3), st(200)],
  },
  // Avenidas retas cortadas por esquinas de 90°, a Champs-Élysées subindo até a rotatória do Arco e o cais do Sena.
  {
    id: 'paris', name: 'Boulevards de Paris', country: 'Europa', scenery: 'city_night', timeOfDay: 'dusk', laps: 4, difficulty: 3,
    ops: [pit(40), st(120), cv(60, 5), st(140), cv(60, -5), st(100), cv(60, 6), st(260), hl(80, 12), cv(140, 4), st(120), ss(160, 3), st(100), cv(60, 5), st(100), ss(120, 5), st(100), cv(70, 6), st(120)],
  },
  {
    id: 'passo_alpino', name: 'Passo Alpino', country: 'Europa', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(40), hl(120, 60), cv(80, 6, 20), cv(80, -6, 20), hl(100, 40), ss(160, 5), st(60), cv(100, -5, -30), hl(120, 50), cv(90, 6), st(80), ss(200, 4), cv(80, -6, -20), hl(100, 30), st(120)],
  },
  {
    id: 'monaco_noite', name: 'Porto de Mônaco', country: 'Europa', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(60), cv(60, -6), st(80), ss(160, 5), cv(70, 6, 15), st(100), cv(60, -6), hl(80, 20), st(120), ss(200, 5), cv(80, 6), st(80), cv(60, -5), st(100), ss(160, 6), st(140), cv(90, 4), st(100)],
  },
  // ───────── Mundial · África do Sul ─────────
  // Estrada de safári: retas longas na savana, curvas médias e algumas fortes, ondulações suaves.
  {
    id: 'kruger', name: 'Savana do Kruger', country: 'África do Sul', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(150), cv(100, 4), hl(100, 15), st(140), cv(90, -5), st(130), cv(140, 4), hl(80, 12), st(150), ss(160, 4), cv(100, 5), st(120), cv(100, -5), st(120), cv(80, 5), st(100), cv(100, 4), st(110)],
  },
  // Semideserto: retas enormes com lombadas cegas, e curva forte logo depois da crista.
  {
    id: 'karoo', name: 'Deserto do Karoo', country: 'África do Sul', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), hl(140, 45), cv(80, 6), st(140), hl(140, 45), cv(120, 4, 20), st(110), cv(80, 6), hl(120, 40), st(120), ss(160, 5), hl(100, 30), cv(90, -5), st(120), cv(80, 6), st(100), cv(110, 4, -20), st(120)],
  },
  // Passo de montanha (curvas à esquerda, subindo): grampos em aclive e esses no alto.
  {
    id: 'drakensberg', name: 'Serra do Drakensberg', country: 'África do Sul', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), hl(140, 55), cv(90, -5, 25), st(60), cv(80, -6, 20), st(70), ss(160, 4), hl(120, 45), cv(100, -4), st(100), cv(80, 6, -25), st(80), hl(100, 35), ss(160, 4), cv(90, -5, -20), st(120), cv(100, -4), st(140)],
  },
  // Estrada do penhasco sobre o Atlântico, à noite: esses fortes e curvas fechadas à beira-mar.
  {
    id: 'boa_esperanca', name: 'Cabo da Boa Esperança', country: 'África do Sul', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(100), cv(70, 6), st(80), ss(160, 5), hl(90, 25), cv(80, 6), st(90), ss(140, 6), st(70), cv(90, 5, 20), cv(80, 6, -20), st(120), ss(160, 5), cv(70, 6), st(90), cv(80, 5), st(150)],
  },
  // ───────── Mundial · Austrália ─────────
  // Rodovia do deserto: retas sem fim, valas de enchente (baixadas) e curva forte no fim da reta.
  {
    id: 'outback', name: 'Poeira do Outback', country: 'Austrália', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(200), hl(60, -10), st(140), cv(80, 6), st(170), hl(60, -12), st(140), cv(140, 3), st(140), cv(90, -5), hl(60, -10), st(140), cv(100, 5), ss(160, 4), st(120), cv(120, 4), st(130)],
  },
  // Estrada costeira de falésias ao entardecer: curvas e esses médios a fortes com morros.
  {
    id: 'great_ocean', name: 'Great Ocean Road', country: 'Austrália', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), cv(110, 4, 15), st(70), ss(160, 5), hl(100, 25), cv(90, 5), st(80), cv(120, 4, -15), ss(160, 4), st(100), cv(80, 5), hl(90, 20), cv(100, -4), st(90), ss(160, 5), st(80), cv(100, 5, 10), st(140)],
  },
  // Estrada estreita na floresta tropical: esses e curvas fortes quase sem reta.
  {
    id: 'daintree', name: 'Selva de Daintree', country: 'Austrália', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(60), ss(140, 6), st(50), cv(80, 6, 15), cv(70, 5), st(60), hl(80, 25), ss(160, 5), cv(90, 6), st(70), cv(80, 6, -15), ss(140, 6), st(60), cv(90, 5), hl(80, 20), cv(70, -6), st(80), ss(140, 5), st(120)],
  },
  // Circuito de rua na baía, à noite: esquinas fortes, esses e a ponte (lombada longa) sobre a água.
  {
    id: 'sydney', name: 'Ponte de Sydney', country: 'Austrália', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(80), cv(60, 6), st(100), ss(160, 6), st(80), cv(60, 6), st(60), hl(160, 22), st(60), cv(70, 6), st(90), ss(140, 6), st(80), cv(60, 5), st(90), cv(70, 6), ss(160, 5), cv(60, 6), st(120)],
  },
  // ───────── Mundial · Escandinávia ─────────
  // Rodovia que salta de ilhota em ilhota: pontes como lombadas íngremes (a maior é a Storseisundet) e curvas entre elas.
  {
    id: 'atlantico', name: 'Estrada do Atlântico', country: 'Escandinávia', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), hl(50, 18), cv(90, 4), st(70), hl(60, 22), cv(80, 5), ss(160, 4), hl(50, 16), st(80), cv(100, 5), st(90), hl(70, 28), cv(90, -4), ss(140, 5), st(80), hl(50, 16), cv(100, 5), st(100), cv(80, 4), st(130)],
  },
  // Floresta de pinheiros sob o sol da meia-noite: retas com cristas, curvas rápidas e algumas fortes.
  {
    id: 'laponia', name: 'Meia-Noite na Lapônia', country: 'Escandinávia', scenery: 'alpine', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), hl(120, 35), cv(120, 4), st(120), cv(80, 6), hl(100, 30), st(120), ss(160, 5), st(100), cv(100, 5, 20), hl(120, 35), cv(80, 6), st(120), cv(60, -4), st(80), ss(140, 5), cv(120, 4, -15), st(110)],
  },
  // A escada dos trolls: grampos alternados subindo a encosta, depois esses e curvas fortes descendo.
  {
    id: 'trollstigen', name: 'Trollstigen', country: 'Escandinávia', scenery: 'alpine', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(60), hl(100, 50), cv(70, 6, 25), st(40), cv(70, -6, 25), st(40), cv(70, 6, 25), st(40), cv(70, -6, 25), hl(100, 40), cv(120, 5), st(60), ss(140, 5), cv(80, 6, -30), st(60), cv(80, 6, -30), hl(90, 30), cv(160, 4), st(80), cv(80, -5, -20), st(140)],
  },
  // Cidade-ilha no Ártico, à noite: a ponte sobre o fiorde, esses e curvas fortes à beira-mar.
  {
    id: 'tromso', name: 'Aurora de Tromsø', country: 'Escandinávia', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(90), cv(70, 6), st(80), hl(140, 26), st(60), ss(160, 5), cv(80, 6), st(70), cv(90, 5, 15), ss(140, 6), st(80), cv(70, 6), hl(80, 18), cv(80, -5), st(90), ss(160, 5), st(70), cv(80, 6), st(140)],
  },
  // ───────── Mundial · Mediterrâneo ─────────
  // Estrada pendurada no penhasco: esses sem fim com morros, curvas fortes entre as vilas.
  {
    id: 'amalfi', name: 'Costa Amalfitana', country: 'Mediterrâneo', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(90), ss(180, 4), hl(90, 25), cv(90, 5), st(60), ss(160, 5), cv(80, 6), st(70), hl(100, 30), ss(180, 4), cv(100, 5, -20), st(80), ss(160, 5), st(70), cv(90, 5), cv(100, 4, 15), st(140)],
  },
  // Estrada da caldeira ao pôr do sol: grampos subindo do porto e esses fortes no alto do penhasco.
  {
    id: 'santorini', name: 'Caldeira de Santorini', country: 'Mediterrâneo', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(80), cv(70, 6, 20), st(40), cv(70, -6, 20), st(40), cv(70, 6, 20), hl(90, 20), ss(180, 5), st(70), cv(90, 5), st(100), ss(160, 6), cv(80, -5, -25), st(60), cv(80, 6, -25), st(80), ss(180, 5), cv(90, 5), st(140)],
  },
  // Vulcão: subida longa com grampos no campo de lava e descida rápida com esses.
  {
    id: 'etna', name: 'Vulcão Etna', country: 'Mediterrâneo', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), hl(160, 60), cv(90, 6, 30), st(50), cv(90, 6, 30), hl(120, 50), ss(140, 6), st(60), cv(100, 5, -30), st(80), cv(80, 6, -30), ss(180, 5), st(100), cv(90, -5), hl(100, 30), cv(90, 6), st(140)],
  },
  // Ruas de pedra à noite: esquinas fortes, esses, a volta longa do Coliseu e a reta dos Fóruns.
  {
    id: 'roma', name: 'Noite em Roma', country: 'Mediterrâneo', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(80), cv(60, 6), st(70), ss(140, 6), st(60), cv(60, 6), st(80), cv(160, 5), st(200), cv(60, 6), ss(160, 6), st(60), cv(60, -6), st(70), ss(160, 5), cv(60, 6), st(90), cv(70, 5), st(130)],
  },
];

export function trackDef(id: string): TrackDef {
  const def = TRACKS.find((t) => t.id === id);
  if (!def) throw new Error(`Pista desconhecida: ${id}`);
  return def;
}
