// Pintura do carro (docs/CARROS.md, "Pintura"): a paleta curada, a escolha de cada assento para cada carro
// (guardada no save) e a pintura de cada carro na pista. Só aparência: a pintura vai no HumanEntry da sessão
// e do online, mas NUNCA na RaceConfig nem no estado da corrida — `withoutPaint` a tira antes de `createRace`,
// e o renderizador a recebe pronta em `RenderFrame.paints` (racePaints). Sem DOM nem Three: os testes usam direto.
import { CARS } from '../core/data/cars';
import type { CarDef, CarState, HumanEntry } from '../core/types';
import type { CarColors, SaveData } from './contracts';

/** A cor de fábrica do carro (CarDef.color/accent): a primeira opção da paleta e o valor de quem não escolheu. */
export const ORIGINAL_PAINT = 'original';

/** Uma pintura da paleta: carroceria + detalhe (faixas, aerofólio, pinças, capacete), já combinados. */
export interface PaintDef {
  id: string;
  color: string;
  accent: string;
}

/**
 * A paleta, depois da Original, na ordem do arco-íris e terminando nos neutros. Cada opção é um par cor + detalhe
 * escolhido junto (a combinação feia não existe); nenhuma repete a de fábrica de um carro (tests/paint.test.ts).
 * Os ids são do save e do online: renomear um apaga a escolha de quem o usava (vira a Original).
 */
export const PAINTS: readonly PaintDef[] = Object.freeze([
  { id: 'rubi', color: '#d7263d', accent: '#f4f4f2' },
  { id: 'tangerina', color: '#ff7a1a', accent: '#f6f7f9' },
  { id: 'canarinho', color: '#ffc425', accent: '#0f7a45' },
  { id: 'limao', color: '#9bd61f', accent: '#24282f' },
  { id: 'esmeralda', color: '#0f9d58', accent: '#ffd23f' },
  { id: 'turquesa', color: '#18b8c4', accent: '#ff8a33' },
  { id: 'ceu', color: '#4fb3ff', accent: '#14254f' },
  { id: 'cobalto', color: '#2346d6', accent: '#ffcf26' },
  { id: 'marinho', color: '#1b2b5e', accent: '#e8434f' },
  { id: 'violeta', color: '#7a3ff0', accent: '#ffb02e' },
  { id: 'flamingo', color: '#ff4fa0', accent: '#2fd9e6' },
  { id: 'gelo', color: '#eef1f4', accent: '#d7263d' },
  { id: 'prata', color: '#98a2ae', accent: '#2346d6' },
  { id: 'grafite', color: '#3b4048', accent: '#c8ff2e' },
  { id: 'onix', color: '#17191e', accent: '#d9b25a' },
].map((p) => Object.freeze(p)));

/** Todas as opções, na ordem em que o seletor as percorre: a Original primeiro. */
export const PAINT_IDS: readonly string[] = Object.freeze([ORIGINAL_PAINT, ...PAINTS.map((p) => p.id)]);

/** Assentos locais que o save lembra (P1..P4), como `seatNames`/`seatCars`. */
const SEATS = 4;

export function isPaintId(v: unknown): v is string {
  return typeof v === 'string' && PAINT_IDS.includes(v);
}

/** Cores da pintura; null = a de fábrica (Original, ausente ou id que não existe mais). */
export function paintColors(id: string | null | undefined): CarColors | null {
  const p = PAINTS.find((x) => x.id === id);
  return p ? { color: p.color, accent: p.accent } : null;
}

/** O carro com a pintura aplicada, para os desenhos dos menus (silhuetas). A Original devolve o próprio carro. */
export function paintedCar<T extends Pick<CarDef, 'color' | 'accent'>>(car: T, id: string | null | undefined): T {
  const c = paintColors(id);
  return c ? { ...car, color: c.color, accent: c.accent } : car;
}

/** A opção seguinte (ou a anterior) da paleta, em círculo; um id desconhecido conta como a Original. */
export function nextPaint(id: string, dir: -1 | 1): string {
  const i = Math.max(0, PAINT_IDS.indexOf(id));
  return PAINT_IDS[(i + dir + PAINT_IDS.length) % PAINT_IDS.length];
}

/**
 * `base` com a pintura, só quando ela é uma da paleta: a Original (e o que não existe) não entra no objeto — quem
 * não pintou manda a mesma mensagem/save de antes (como `withAssist` em net/protocol.ts).
 */
export function withPaint<T extends object>(base: T, paint: unknown): T & { paint?: string } {
  const { paint: _drop, ...rest } = base as T & { paint?: string };
  return isPaintId(paint) && paint !== ORIGINAL_PAINT ? { ...(rest as T), paint } : (rest as T);
}

/** Cópias sem a pintura: os humanos que entram na RaceConfig (o estado da corrida não carrega cosmético). */
export function withoutPaint<T extends { paint?: string }>(list: readonly T[]): T[] {
  return list.map((h) => {
    const { paint: _drop, ...rest } = h;
    return rest as T;
  });
}

// ───────────────────────────── Save: por assento e por carro ─────────────────────────────

/** A pintura que o assento escolheu para o carro (a Original quando nunca escolheu). */
export function seatPaint(save: Pick<SaveData, 'seatPaints'>, seat: number, carId: string): string {
  const id = save.seatPaints[seat]?.[carId];
  return isPaintId(id) ? id : ORIGINAL_PAINT;
}

/**
 * Grava a escolha do assento para o carro. Troca os objetos em vez de mexer neles (o save padrão é compartilhado);
 * a Original sai do mapa, então só quem pintou ocupa o save.
 */
export function setSeatPaint(save: Pick<SaveData, 'seatPaints'>, seat: number, carId: string, id: string): void {
  if (seat < 0 || seat >= SEATS) return;
  const lists = Array.from({ length: SEATS }, (_, i) => save.seatPaints[i] ?? {});
  const { [carId]: _old, ...rest } = lists[seat];
  lists[seat] = isPaintId(id) && id !== ORIGINAL_PAINT ? { ...rest, [carId]: id } : rest;
  save.seatPaints = lists;
}

/** ←/→ no seletor: a próxima (ou a anterior) da paleta, já gravada para o assento e o carro. Devolve a nova. */
export function stepSeatPaint(save: Pick<SaveData, 'seatPaints'>, seat: number, carId: string, dir: -1 | 1): string {
  const id = nextPaint(seatPaint(save, seat, carId), dir);
  setSeatPaint(save, seat, carId, id);
  return id;
}

/** Saneamento da leitura: 4 mapas carro → pintura, só com carro e pintura que existem (a Original não é guardada). */
export function sanitizeSeatPaints(v: unknown): Array<Record<string, string>> {
  const arr = Array.isArray(v) ? v : [];
  return Array.from({ length: SEATS }, (_, i) => {
    const out: Record<string, string> = {};
    const m: unknown = arr[i];
    if (typeof m !== 'object' || m === null || Array.isArray(m)) return out;
    for (const [carId, id] of Object.entries(m as Record<string, unknown>)) {
      if (CARS.some((c) => c.id === carId) && isPaintId(id) && id !== ORIGINAL_PAINT) out[carId] = id;
    }
    return out;
  });
}

/** Humanos montados do save (carreira, tutorial): cada um com a pintura que o assento dele guardou para o carro. */
export function paintFromSave(humans: readonly HumanEntry[], save: Pick<SaveData, 'seatPaints'>): HumanEntry[] {
  return humans.map((h) => withPaint(h, seatPaint(save, h.seat, h.carId)));
}

// ───────────────────────────── Na pista ─────────────────────────────

/**
 * A pintura de cada carro da corrida (índice = `cars`): a IA e o VIP da escolta de fábrica (null); cada humano
 * com a dele (a do HumanEntry do assento que larga com o carro — no revezamento, o primeiro da dupla).
 * Dois humanos com o mesmo carro e a mesma pintura sairiam iguais: o de assento maior ganha, só nesta corrida,
 * a próxima opção da paleta que ninguém com aquele carro escolheu. Pura e só da largada: no online todo
 * computador chega à mesma lista.
 */
export function racePaints(cars: ReadonlyArray<Pick<CarState, 'seat' | 'carId'>>, humans: readonly HumanEntry[]): Array<CarColors | null> {
  const out: Array<CarColors | null> = cars.map(() => null);
  const drivers = cars
    .map((c, index) => ({ index, seat: c.seat, carId: c.carId }))
    .filter((d) => d.seat >= 0)
    .sort((a, b) => a.seat - b.seat);
  const chosen = drivers.map((d) => {
    const id = humans.find((h) => h.seat === d.seat)?.paint;
    return isPaintId(id) ? id : ORIGINAL_PAINT;
  });
  const given = new Map<string, Set<string>>(); // carro → pinturas já na pista (só render; o núcleo não vê)
  drivers.forEach((d, k) => {
    const taken = given.get(d.carId) ?? new Set<string>();
    let id = chosen[k];
    if (taken.has(id)) {
      // Reserva as escolhas de propósito dos outros com o mesmo carro: a troca não toma a pintura de ninguém.
      const reserved = new Set(drivers.flatMap((o, j) => (o.carId === d.carId ? [chosen[j]] : [])));
      let next = nextPaint(id, 1);
      while ((taken.has(next) || reserved.has(next)) && next !== id) next = nextPaint(next, 1);
      id = next;
    }
    taken.add(id);
    given.set(d.carId, taken);
    out[d.index] = paintColors(id);
  });
  return out;
}
