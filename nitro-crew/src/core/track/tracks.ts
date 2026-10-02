// As pistas, na ordem das copas (data/cups.ts): a Expedição Brasil (27 estados × 3 pistas, região por região) e
// o Mundial (7 países × 4). Comprimentos em segmentos (200 unidades cada); ~1.800 segmentos ≈ 60–70 s por volta a
// 300 km/h. Curva: 2 fácil, 4 média, 6 forte (negativa = esquerda). Altura em segmentos. `difficulty` não cai dentro
// da copa e sobe ao longo de cada etapa; tests/track.test.ts confere o rótulo contra o traçado (índice técnico).
// As pistas novas do Brasil (onda G) foram ajustadas ao índice técnico alvo da sua copa escalando curvas e morros
// de um traçado-base com a identidade do lugar; o número final é o que está aqui. Pista nova: docs/PISTAS.md.
import type { TrackDef, TrackOp } from '../types';

const st = (length: number): TrackOp => ({ op: 'straight', length });
const cv = (length: number, curve: number, hill?: number): TrackOp => ({ op: 'curve', length, curve, hill });
const hl = (length: number, height: number): TrackOp => ({ op: 'hill', length, height });
const ss = (length: number, curve: number): TrackOp => ({ op: 's', length, curve });
const pit = (length: number): TrackOp => ({ op: 'pit', length });

export const TRACKS: TrackDef[] = [
  // ───────── Expedição Brasil · sudeste · RJ ─────────
  {
    id: 'copacabana', name: 'Orla de Copacabana', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 1,
    ops: [pit(40), st(80), cv(80, 2), st(100), cv(100, -2), hl(80, 20), st(150), cv(120, 3), st(200), ss(160, 2), cv(90, -3), st(140), cv(100, 2, 10), st(120), cv(80, -2), st(160)],
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
  {
    id: 'sampa_noite', name: 'Noite em Sampa', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(60), ss(160, 4), st(80), cv(60, -5), st(120), cv(100, 4), ss(200, 3), st(150), cv(80, -6), st(100), cv(120, 5), st(90), ss(180, 4), st(100), cv(70, -4), st(140)],
  },
  // ───────── Expedição Brasil · sudeste · MG ─────────
  // Volta da lagoa com a igrejinha de Niemeyer: avenida plana de curvas abertas.
  {
    id: 'pampulha', name: 'Lagoa da Pampulha', country: 'Brasil', scenery: 'city_night', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(130), cv(120, 3.4), st(90), ss(190, 3.4), st(120), cv(120, 3.3), st(90), cv(120, 3.5), st(100), ss(170, 3.1), st(130), ss(190, 3.2), st(100), cv(130, 3.5), st(110), st(100)],
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
  // Orla de Vitória com a pedra do Convento da Penha: curvas médias à beira-mar e uma subida longa até o convento.
  {
    id: 'convento_penha', name: 'Convento da Penha', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(140), cv(120, 3.4), st(120), cv(100, -4.5), st(150), hl(220, 50), cv(140, 3.4, 22), st(120), ss(180, 3.4), st(140), cv(100, 4.5, -22), st(160), cv(120, -3.4), st(130)],
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
  // Curitiba à noite: avenidas, esquinas e parques, a Ópera de Arame entre as árvores.
  {
    id: 'curitiba', name: 'Ópera de Arame', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(100), cv(70, 4.6), st(100), hl(90, 8), st(80), cv(70, 4.4), st(90), hl(100, 8), st(70), cv(70, 4.4), st(100), ss(140, 4), st(90), cv(70, 5), st(120), ss(160, 4.3), st(100), ss(150, 4.2), st(80), st(100)],
  },
  // ───────── Expedição Brasil · sul · SC ─────────
  // Ilha de Santa Catarina: beira-mar com curvas abertas, morrotes e a ponte pênsil.
  {
    id: 'floripa', name: 'Ponte Hercílio Luz', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(120), cv(140, 3.1), hl(100, 20), st(120), cv(120, -3.1), st(100), hl(90, 18), cv(160, 4.1), st(140), ss(200, 3.1), hl(100, 23), st(120), cv(120, 3.1, 15), st(100), cv(100, -4.1), st(140)],
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
  // Eixo Monumental: avenidas retas enormes e as tesourinhas — alças longas e fechadas (curvas compridas).
  {
    id: 'brasilia', name: 'Eixo Monumental', country: 'Brasil', scenery: 'city_night', timeOfDay: 'dusk', laps: 3, difficulty: 3,
    ops: [pit(40), st(150), cv(180, 4.6), st(110), cv(60, -3.8), st(180), cv(180, -4.6), st(100), hl(80, 9), cv(180, 4.6), st(150), cv(70, 3.8), st(90), cv(180, 4.6), st(110), cv(170, -4.6), st(90)],
  },
  // ───────── Expedição Brasil · centro_oeste · GO ─────────
  // Cidade de pedra das cavalhadas: ruas estreitas, esses curtos e ladeiras.
  {
    id: 'pirenopolis', name: 'Ruas de Pirenópolis', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 2,
    ops: [pit(40), st(100), hl(80, 15), st(60), hl(90, 15), st(60), ss(120, 3.8), st(80), ss(140, 3.9), st(50), cv(90, 3.8), st(60), cv(80, 3.9), st(70), hl(80, 15), st(60), hl(90, 15), st(50), ss(150, 4), st(60), cv(70, 4.3), st(60), st(100)],
  },
  // Cerrado das águas quentes: estrada rápida com ondulações e curvas fortes.
  {
    id: 'caldas_novas', name: 'Águas de Caldas Novas', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(110), cv(90, 6), st(130), st(150), cv(130, 5.4), st(180), cv(140, 5.7), hl(90, 13), st(130), cv(100, 6), st(150), hl(60, 13), st(120), st(150), cv(120, 4.2), st(100)],
  },
  // Cerrado do Planalto Central: retas entre chapadões, ondulações fortes e curvas fechadas nos mirantes.
  {
    id: 'chapada_veadeiros', name: 'Chapada dos Veadeiros', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(140), hl(120, 32), cv(90, 4.1), st(120), hl(100, 28), cv(80, -4.9), st(100), hl(120, 32), ss(160, 4.1), st(120), cv(100, 4.1, 20), hl(100, 24), st(100), cv(90, 4.9), hl(120, 28), cv(100, -4.1, -20), st(140)],
  },
  // ───────── Expedição Brasil · centro_oeste · MS ─────────
  // Estradas de terra entre a mata e os rios de água clara: curvas médias encadeadas, quase sem reta.
  {
    id: 'bonito', name: 'Rios de Bonito', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(100), cv(110, 3.6), st(50), cv(100, -4.4), ss(160, 3.6), st(60), cv(120, 4.4), hl(80, 13), cv(100, -3.6), st(70), ss(180, 4.4), st(60), cv(110, 4.4), cv(100, -3.6), st(80), ss(160, 3.6), hl(80, 13), cv(120, 4.4), st(140)],
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
  // Estrada de terra reta no Pantanal alagado: retas longas, curvas abertas e as pontes de madeira como lombadas curtas.
  {
    id: 'transpantaneira', name: 'Transpantaneira', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 1,
    ops: [pit(40), st(160), hl(30, 4), st(140), cv(140, 2), st(120), hl(30, 4), st(100), cv(160, 3), st(200), hl(30, 4), st(100), cv(120, -2), st(150), cv(140, 3), st(120), hl(30, 4), st(80), cv(120, 2), st(120)],
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
  // Litoral do descobrimento: beira-mar entre coqueiros e a subida do Quadrado.
  {
    id: 'porto_seguro', name: 'Costa de Porto Seguro', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 2,
    ops: [pit(40), st(140), hl(80, 11), st(80), hl(90, 22), st(100), ss(180, 3.4), st(100), cv(110, 4.2), st(100), cv(130, 4.2), st(120), cv(140, 4.2), st(120), ss(160, 4.5), st(80), st(100)],
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
  // Orla de Atalaia à noite: retas iluminadas, os arcos da orla e curvas médias.
  {
    id: 'aracaju', name: 'Orla de Atalaia', country: 'Brasil', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(100), cv(100, 4), st(100), cv(90, -5), st(120), ss(180, 4), st(100), cv(100, 5), st(100), cv(120, 4), hl(80, 12), st(100), ss(160, 4), st(100), cv(90, -5), st(110)],
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
  // Pajuçara e Ponta Verde à noite: orla com retornos fechados e esquinas.
  {
    id: 'maceio', name: 'Orla de Maceió', country: 'Brasil', scenery: 'coast', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(130), hl(80, 23), st(60), cv(80, 6), st(80), cv(80, 6), st(60), ss(130, 5.5), st(80), hl(90, 23), st(60), ss(130, 5.7), st(60), hl(80, 23), st(60), cv(70, 6), st(70), ss(140, 5.3), st(60), st(120)],
  },
  // Dunas de Piaçabuçu até o farol na foz do rio: lombadas de areia e curvas fortes.
  {
    id: 'foz_sao_francisco', name: 'Foz do São Francisco', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(130), hl(80, 45), st(50), cv(110, 6), st(70), hl(100, 30), st(50), hl(90, 30), st(70), cv(100, 6), st(80), hl(100, 30), st(60), hl(90, 30), st(60), cv(110, 6), st(80), hl(90, 45), st(70), ss(160, 6), st(60), st(100)],
  },
  // ───────── Expedição Brasil · nordeste · PE ─────────
  // Bairro do Recife à noite: as pontes sobre o Capibaribe e as esquinas do Marco Zero.
  {
    id: 'recife_antigo', name: 'Recife Antigo', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 3,
    ops: [pit(40), st(130), cv(70, 5.4), st(120), cv(70, 6), st(110), cv(60, 6), st(110), ss(160, 5.7), st(80), cv(60, 6), st(90), cv(70, -5.4), st(140), st(170), cv(60, -6), st(80), cv(60, 6), st(90), st(200), st(100)],
  },
  // Colinas coloniais sobre o mar: esses curtos e fechados entre as igrejas, ladeiras no meio.
  {
    id: 'olinda', name: 'Ladeiras de Olinda', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(70), ss(120, 4.9), st(50), hl(70, 18), ss(120, 4.1), st(60), cv(80, 4.9), hl(70, 16), ss(140, 4.1), st(60), cv(80, -4.9), ss(120, 4.1), st(70), hl(80, 19), ss(140, 4.1), st(60), cv(80, 4.9), ss(120, 4.9), st(140)],
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
  // Campina Grande em noite de São João: ruas cheias de bandeirolas, esquinas e esses.
  {
    id: 'campina_grande', name: 'Parque do Povo', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(100), cv(60, 6), st(90), ss(150, 5), st(80), cv(60, -5.9), st(120), ss(150, 4.9), st(90), cv(70, 5.7), st(100), ss(150, 5.6), st(70), cv(60, 5.9), st(100), cv(70, 5.5), st(140), st(120)],
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
  // Em volta do maior cajueiro do mundo: esses encadeados na sombra e praias de falésia.
  {
    id: 'cajueiro_pirangi', name: 'Cajueiro de Pirangi', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(80), cv(80, 5.5), st(70), ss(120, 5.1), st(70), ss(150, 5.1), st(80), ss(130, 5.1), st(70), hl(80, 19), st(60), ss(130, 4.7), st(70), hl(80, 19), st(60), ss(120, 4.8), st(60), ss(130, 4.9), st(50), cv(120, 4.7), st(80), st(100)],
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
  // Falésias vermelhas e dunas: lombadas fortes e curvas no alto do barranco.
  {
    id: 'canoa_quebrada', name: 'Falésias de Canoa Quebrada', country: 'Brasil', scenery: 'desert', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(120), cv(100, 5.4), st(60), hl(100, 25), st(70), hl(90, 25), st(70), cv(110, 5.1), st(60), hl(100, 25), st(70), ss(150, 5.5), st(60), ss(150, 6), st(60), hl(100, 38), st(40), hl(90, 25), st(60), hl(100, 38), st(50), st(100)],
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
  // Caatinga e paredões de pedra: retas no plano e curvas fortes no pé da serra.
  {
    id: 'serra_capivara', name: 'Serra da Capivara', country: 'Brasil', scenery: 'desert', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(140), cv(90, 5.3), st(120), cv(80, -5.3), st(90), hl(100, 22), cv(100, 5.3), st(100), ss(160, 5.3), st(90), cv(90, 5.3), st(120), cv(90, -5.3), hl(90, 18), cv(100, 5.3), st(70), cv(80, 5.3), st(140)],
  },
  // ───────── Expedição Brasil · nordeste · MA ─────────
  // Centro histórico de azulejos à noite: ladeiras de pedra e esquinas fechadas.
  {
    id: 'sao_luis', name: 'Casarões de São Luís', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(90), ss(130, 5.1), st(60), hl(80, 19), st(50), cv(70, 5.5), st(60), cv(70, 5.1), st(60), cv(80, 5.1), st(70), ss(120, 4.7), st(60), ss(160, 4.3), st(70), ss(140, 4.7), st(50), hl(80, 19), st(60), cv(80, 5.1), st(60), ss(160, 5.2), st(70), st(100)],
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
  // Cais do Ver-o-Peso e a baía do Guajará: retas do porto cortadas por esquinas fortes.
  {
    id: 'belem', name: 'Ver-o-Peso', country: 'Brasil', scenery: 'coast', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), cv(60, 5.6), st(80), cv(60, 5.6), st(100), cv(60, -5.6), st(70), ss(140, 5.6), st(80), cv(60, 5.6), st(120), cv(70, 5.6), st(70), cv(60, -5.6), st(80), ss(140, 5.6), st(80), cv(60, 5.6), st(100), cv(60, 5.6), st(110)],
  },
  // A ilha dos búfalos: aterros entre os campos alagados e palafitas, esses fortes sem fim.
  {
    id: 'marajo', name: 'Campos do Marajó', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(100), ss(150, 5), st(50), ss(150, 5.7), st(60), cv(90, 5.5), st(50), ss(140, 5.1), st(80), hl(90, 21), st(60), ss(150, 5.5), st(70), ss(120, 5.1), st(80), cv(80, 5.7), st(70), cv(150, 5.2), st(60), st(100)],
  },
  // ───────── Expedição Brasil · norte · AM ─────────
  // Encontro das Águas: margem do rio com curvas longas e fortes que acompanham as águas.
  {
    id: 'manaus', name: 'Encontro das Águas', country: 'Brasil', scenery: 'coast', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(110), cv(180, 5.2), st(90), cv(160, -4.1), st(80), hl(90, 21), cv(180, 5.2), st(90), cv(140, 6), st(90), cv(180, -4.1), st(80), cv(160, 5.2), hl(90, 21), st(80), cv(140, 4.1), st(130)],
  },
  // Manaus à noite e a ponte estaiada sobre o Rio Negro: esquinas e a lombada longa da ponte.
  {
    id: 'ponte_rio_negro', name: 'Ponte do Rio Negro', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 4,
    ops: [pit(40), st(80), ss(150, 5.6), st(80), st(130), ss(150, 5.2), st(90), ss(150, 5.5), st(100), cv(70, 5.5), st(80), ss(160, 5.8), st(100), cv(60, 5.9), st(110), cv(60, 5.2), st(100), cv(70, 6), st(110), st(100)],
  },
  // A ilha do boi-bumbá: ruas em volta do Bumbódromo, esses fortes e esquinas.
  {
    id: 'parintins', name: 'Bumbódromo de Parintins', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(90), ss(130, 5.5), st(70), ss(140, 6), st(70), ss(160, 5.5), st(50), hl(80, 24), st(60), hl(80, 24), st(60), ss(160, 5.9), st(60), cv(140, 5.6), st(70), hl(90, 24), st(60), cv(130, 5.8), st(70), st(120)],
  },
  // ───────── Expedição Brasil · norte · AP ─────────
  // Marco Zero do Equador: retas planas e curvas abertas mas longas na beira do Amazonas.
  {
    id: 'macapa', name: 'Marco Zero do Equador', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 3,
    ops: [pit(40), st(150), cv(120, 4.8), st(140), cv(100, -5.9), st(150), cv(140, 4.8), st(130), cv(100, 5.9), st(140), ss(160, 4.8), st(130), cv(120, 4.8), st(130)],
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
  // Lavrado (a savana de Roraima) com o tepui ao fundo: rápido no começo, um nó de curvas fortes no fim.
  {
    id: 'monte_roraima', name: 'Monte Roraima', country: 'Brasil', scenery: 'savanna', timeOfDay: 'day', laps: 3, difficulty: 5,
    ops: [pit(40), st(200), cv(140, 3.4), st(180), cv(120, -3.4), st(160), hl(80, 17), cv(90, 6), ss(140, 6), cv(80, -6), st(50), ss(160, 6), cv(90, 6), hl(80, 29), cv(80, -6), ss(140, 6), cv(90, 6), st(120)],
  },
  // Praias de lago no lavrado entre buritis: esses fortes e lombadas de areia.
  {
    id: 'lago_caracarana', name: 'Lago Caracaranã', country: 'Brasil', scenery: 'savanna', timeOfDay: 'dusk', laps: 3, difficulty: 5,
    ops: [pit(40), st(140), hl(80, 43), st(60), cv(90, 6), st(70), cv(70, 6), st(60), ss(130, 6), st(60), hl(80, 43), st(60), cv(90, 6), st(60), ss(160, 6), st(60), ss(120, 6), st(70), hl(80, 43), st(70), hl(80, 43), st(50), st(100)],
  },
  // ───────── Expedição Brasil · norte · RO ─────────
  // Ao longo da Estrada de Ferro Madeira-Mamoré: retas da ferrovia e curvas médias a fortes.
  {
    id: 'porto_velho', name: 'Madeira-Mamoré', country: 'Brasil', scenery: 'tropical', timeOfDay: 'dusk', laps: 3, difficulty: 4,
    ops: [pit(40), st(160), cv(100, 5.8), st(120), ss(160, 5.8), st(120), cv(90, -6), st(140), cv(110, 5.8), hl(80, 17), st(100), ss(160, 5.8), st(120), cv(100, 6), st(110), cv(100, -5.8), st(150)],
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
  // Margem do rio Acre: curvas longas acompanhando o rio, sob a gameleira.
  {
    id: 'rio_branco', name: 'Gameleira de Rio Branco', country: 'Brasil', scenery: 'tropical', timeOfDay: 'day', laps: 3, difficulty: 4,
    ops: [pit(40), st(100), cv(160, 4.9), st(100), cv(140, -4.9), st(90), hl(80, 18), cv(160, 4.9), st(100), ss(200, 4.9), st(90), cv(140, 6), st(100), cv(160, -4.9), hl(80, 18), st(90), cv(140, 4.9), st(110)],
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
  // A capital mais nova à noite: a ponte longa sobre o lago e esquinas fortes na praça.
  {
    id: 'palmas', name: 'Ponte de Palmas', country: 'Brasil', scenery: 'city_night', timeOfDay: 'night', laps: 4, difficulty: 5,
    ops: [pit(40), st(70), cv(70, 6), st(60), cv(60, 6), st(60), ss(160, 5.6), st(60), cv(70, 6), st(90), ss(150, 5.7), st(60), st(90), ss(150, 5.7), st(50), cv(60, 6), st(60), cv(60, 5.9), st(80), ss(150, 6), st(60), st(70)],
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
