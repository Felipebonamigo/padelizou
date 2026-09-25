// Tela de controles: grade ações × dispositivos para remapear (teclado 1, teclado 2 e um mapeamento
// para todos os gamepads), dispositivos conectados ao vivo e um teste de entrada com barras.
// Escolher uma célula (Enter, A ou clique) abre a captura: a próxima tecla/botão vira o comando,
// Esc ou Start cancelam e, sem nada em 5 s, ela desiste. A navegação dos menus é fixa (menus.ts);
// durante a captura as teclas nem chegam a ela, porque o ouvinte daqui roda antes (fase de captura).
import type { DeviceId, DeviceInfo, DevicePeek } from '../../game/contracts';
import { t } from '../../i18n';
import { isKeyboard } from '../input';
import { assignBinding, BIND_ACTIONS, BIND_DEVICES, isDefaultDevice, keyboardConflicts, restoreDefaults, type BindAction, type BindDevice } from '../remap/bindings';
import { captureButtons, captureKey, captureTick, startCapture, type Capture, type CaptureOutcome } from '../remap/capture';
import { actionLabel, codeLabel, codesLabel, deviceLabel, deviceTitle, padStyleOf, rejectedText, type LayoutMap, type PadStyle } from '../remap/labels';
import '../remap/strings';
import { button, createFocusList, h, listNav, screenFrame, type FocusItem, type ScreenApi, type ScreenInstance } from './common';
import { icon } from './icons';
import { commitSettings } from './options';
import './controls.css';

type StatusKind = 'info' | 'ok' | 'warn';
type PillKey = 'nitro' | 'gearUp' | 'gearDown' | 'pause';
const PILLS: readonly PillKey[] = ['nitro', 'gearUp', 'gearDown', 'pause'];
/** Maior passo do relógio da captura por quadro (o mesmo limite do `dt` da sessão). */
const MAX_CAPTURE_DT = 0.25;

interface Cell { el: HTMLElement; keys: HTMLElement }

interface DeviceRow {
  id: DeviceId;
  el: HTMLElement;
  steer: HTMLElement;
  throttle: HTMLElement;
  brake: HTMLElement;
  pills: Record<PillKey, HTMLElement>;
  /** Último estado desenhado (evita mexer no DOM a cada quadro sem mudança). */
  drawn: string;
}

let layoutMapPromise: Promise<LayoutMap | null> | null = null;

/** Mapa do layout do teclado do sistema (Chromium/Electron: AZERTY, ABNT2…); null sem suporte. */
function layoutMapOnce(): Promise<LayoutMap | null> {
  if (!layoutMapPromise) {
    const kb = typeof navigator === 'undefined' ? undefined : (navigator as Navigator & { keyboard?: { getLayoutMap?: () => Promise<LayoutMap> } }).keyboard;
    layoutMapPromise = kb && typeof kb.getLayoutMap === 'function' ? kb.getLayoutMap().catch(() => null) : Promise.resolve(null);
  }
  return layoutMapPromise;
}

const cellKey = (action: BindAction, device: BindDevice) => `${action}:${device}`;

export function controlsScreen(api: ScreenApi): ScreenInstance {
  const { input, settings } = api.ctx;
  let capture: Capture | null = null;
  let destroyed = false;
  /** Tecla recém-capturada: a repetição automática dela é engolida até soltar (senão viraria navegação). */
  let swallowCode: string | null = null;
  /** Gamepad que acabou de capturar/cancelar: a navegação dele espera os botões serem soltos. */
  let navGuard: DeviceId | null = null;
  let padStyle: PadStyle = 'xbox';
  let layoutMap: LayoutMap | null = null;

  const label = (code: string | number) => codeLabel(code, padStyle, layoutMap);
  const labels = (codes: ReadonlyArray<string | number>) => codesLabel(codes, padStyle, layoutMap);
  const rejected = (code: string | number) => rejectedText(code, padStyle, layoutMap);

  // ───────────── Grade ─────────────

  const cells: Record<string, Cell> = {};
  const items: FocusItem[] = [];
  const gridChildren: HTMLElement[] = [
    h('div', { class: 'remap-h remap-h-action', text: t('ui.controls.action') }),
    ...BIND_DEVICES.map((d) => h('div', { class: 'remap-h', title: deviceTitle(d) },
      icon(d === 'gamepad' ? 'gamepad' : 'keyboard'), h('span', { text: deviceLabel(d) }))),
  ];
  for (const action of BIND_ACTIONS) {
    gridChildren.push(h('div', { class: 'remap-action', text: actionLabel(action) }));
    for (const device of BIND_DEVICES) {
      const keys = h('span', { class: 'bind-keys' });
      const el = h('div', {
        class: `bind-cell bind-${device}`,
        title: t('remap.cellTitle', { action: actionLabel(action), device: deviceLabel(device) }),
        attrs: { role: 'button', 'data-action': action, 'data-device': device },
      }, keys);
      cells[cellKey(action, device)] = { el, keys };
      gridChildren.push(el);
      items.push({ el, activate: () => begin(action, device) });
    }
  }
  gridChildren.push(h('div', { class: 'remap-action remap-restore-label' }));
  const restoreButtons = BIND_DEVICES.map((device) => {
    const b = button(t('remap.restore'), () => restore(device), 'btn-restore');
    b.el.title = t('remap.restoreTitle', { device: deviceLabel(device) });
    b.el.setAttribute('data-restore', device);
    gridChildren.push(b.el);
    items.push(b);
    return { device, el: b.el };
  });
  const back = button(t('ui.common.back'), () => api.back());
  items.push(back);
  const list = createFocusList(items, { cols: BIND_DEVICES.length, sfx: api.sfx });

  const status = h('p', { class: 'remap-status', attrs: { role: 'status', 'aria-live': 'polite' } });
  const conflictsEl = h('div', { class: 'remap-conflicts' });

  function setStatus(text: string, kind: StatusKind): void {
    status.textContent = text;
    status.className = `remap-status ${kind}`;
  }

  function renderCell(action: BindAction, device: BindDevice): void {
    const cell = cells[cellKey(action, device)];
    const capturing = capture !== null && capture.action === action && capture.device === device;
    cell.el.classList.toggle('capturing', capturing);
    if (capture && capturing) {
      cell.keys.replaceChildren(
        h('span', { class: 'bind-wait', text: t('remap.waiting') }),
        h('span', { class: 'bind-count mono', text: String(Math.max(1, Math.ceil(capture.left))) }),
      );
      return;
    }
    const codes: ReadonlyArray<string | number> = settings.controls[device][action];
    cell.keys.replaceChildren(...codes.flatMap((code, i) => [
      i > 0 ? h('span', { class: 'key-sep', text: '/' }) : null,
      h('kbd', { text: label(code) }),
    ].filter((n): n is HTMLElement => n !== null)));
  }

  function renderConflicts(): void {
    const found = keyboardConflicts(settings.controls);
    for (const cell of Object.values(cells)) cell.el.classList.remove('conflict');
    for (const c of found) {
      cells[cellKey(c.kb1, 'kb1')].el.classList.add('conflict');
      cells[cellKey(c.kb2, 'kb2')].el.classList.add('conflict');
    }
    conflictsEl.replaceChildren(...(found.length === 0 ? [] : [
      h('p', { class: 'remap-conflict-title', text: t('remap.conflictTitle') }),
      ...found.map((c) => h('p', { class: 'remap-conflict', text: t('remap.conflict', { key: label(c.code), kb1: actionLabel(c.kb1), kb2: actionLabel(c.kb2) }) })),
    ]));
  }

  function renderAll(): void {
    for (const action of BIND_ACTIONS) for (const device of BIND_DEVICES) renderCell(action, device);
    for (const r of restoreButtons) r.el.classList.toggle('is-default', isDefaultDevice(settings.controls, r.device));
    renderConflicts();
  }

  // ───────────── Captura ─────────────

  function promptText(c: Capture): string {
    const what = c.device === 'gamepad'
      ? t('remap.pressButton', { action: actionLabel(c.action) })
      : t('remap.pressKey', { action: actionLabel(c.action), device: deviceLabel(c.device) });
    return `${what} · ${t('remap.cancelHint', { s: Math.max(1, Math.ceil(c.left)) })}`;
  }

  function begin(action: BindAction, device: BindDevice): void {
    const previous = capture;
    // Botões já segurados agora (o A que abriu a captura) só contam depois de soltos.
    const held: Record<string, number[]> = {};
    for (const d of input.devices()) if (!isKeyboard(d.id)) held[d.id] = input.peek(d.id)?.buttons ?? [];
    capture = startCapture(action, device, held);
    if (previous) renderCell(previous.action, previous.device);
    renderCell(action, device);
    setStatus(promptText(capture), 'info');
  }

  function end(): void {
    const c = capture;
    capture = null;
    if (c) renderCell(c.action, c.device);
  }

  function resolve(outcome: CaptureOutcome): void {
    const c = capture;
    if (!c || outcome.kind === 'wait') return;
    if (outcome.kind === 'reject') {
      // Continua esperando outra tecla, com o mesmo relógio.
      setStatus(`${rejected(outcome.code)} ${t('remap.cancelHint', { s: Math.max(1, Math.ceil(c.left)) })}`, 'warn');
      api.sfx('back');
      return;
    }
    end();
    if (outcome.kind === 'cancel') {
      setStatus(t(outcome.reason === 'timeout' ? 'remap.timeout' : 'remap.canceled'), 'info');
      api.sfx('back');
      return;
    }
    const r = assignBinding(settings.controls, c.device, c.action, outcome.code);
    const what = { action: actionLabel(c.action), device: deviceLabel(c.device), keys: label(outcome.code) };
    if (r.rejected) {
      setStatus(rejected(outcome.code), 'warn');
    } else if (!r.changed) {
      setStatus(t('remap.unchanged', what), 'info');
    } else {
      settings.controls = r.bindings;
      commitSettings(api);
      const saved = t('remap.saved', what);
      const d = r.displaced;
      if (d) setStatus(`${saved} ${t(d.swapped ? 'remap.swapped' : 'remap.moved', { key: label(outcome.code), other: actionLabel(d.action), keys: labels(d.codes) })}`, 'warn');
      else setStatus(saved, 'ok');
      api.sfx('confirm');
    }
    renderAll();
  }

  function restore(device: BindDevice): void {
    end();
    if (isDefaultDevice(settings.controls, device)) {
      setStatus(t('remap.alreadyDefault', { device: deviceLabel(device) }), 'info');
      return;
    }
    settings.controls = restoreDefaults(settings.controls, device);
    commitSettings(api);
    setStatus(t('remap.restored', { device: deviceLabel(device) }), 'ok');
    renderAll();
  }

  // Fase de captura na janela: roda antes do teclado dos menus (document) e do jogo (window, bolha).
  const onKeyDown = (e: KeyboardEvent) => {
    if (!capture) {
      if (e.repeat && swallowCode !== null && e.code === swallowCode) { e.preventDefault(); e.stopImmediatePropagation(); }
      return;
    }
    e.preventDefault();
    e.stopImmediatePropagation();
    if (e.repeat) return;
    const outcome = captureKey(capture, e.code);
    if (outcome.kind === 'accept' || outcome.kind === 'cancel') swallowCode = e.code;
    resolve(outcome);
  };
  const onKeyUp = (e: KeyboardEvent) => {
    if (e.code === swallowCode) swallowCode = null;
  };
  window.addEventListener('keydown', onKeyDown, true);
  window.addEventListener('keyup', onKeyUp, true);

  /**
   * O relógio da captura anda com o `dt` da sessão, que já vem limitado a 0,25 s por quadro: a 60 Hz
   * são 5 s de verdade, e um travamento (carregamento, máquina ocupada) não come a janela antes de
   * o jogador ver o aviso — ela dura pelo menos 20 quadros.
   */
  function pollCapture(dt: number): void {
    if (!capture) return;
    for (const d of input.devices()) {
      if (isKeyboard(d.id) || !d.connected) continue;
      const outcome = captureButtons(capture, d.id, input.peek(d.id)?.buttons ?? []);
      if (outcome.kind === 'accept' || outcome.kind === 'cancel') navGuard = d.id;
      resolve(outcome);
      if (!capture) return;
    }
    const tick = captureTick(capture, Math.min(MAX_CAPTURE_DT, dt));
    if (tick.kind !== 'wait') { resolve(tick); return; }
    setStatusIfPrompt();
    renderCell(capture.action, capture.device);
  }

  /** Atualiza a contagem do aviso sem apagar uma recusa que acabou de aparecer. */
  function setStatusIfPrompt(): void {
    if (capture && status.classList.contains('info')) status.textContent = promptText(capture);
  }

  // ───────────── Dispositivos e teste de entrada ─────────────

  const deviceList = h('div', { class: 'remap-devices' });
  let signature = '';
  let rows: DeviceRow[] = [];

  function seatText(d: DeviceInfo): string {
    if (!d.connected) return t('ui.controls.disconnected');
    return d.boundSeat === null ? t('ui.controls.free') : t('ui.controls.seat', { n: d.boundSeat + 1 });
  }

  function deviceRow(d: DeviceInfo): DeviceRow {
    const steer = h('span', { class: 'steer-fill' });
    const throttle = h('span', { class: 'pedal-fill' });
    const brake = h('span', { class: 'pedal-fill brake' });
    const pills = {} as Record<PillKey, HTMLElement>;
    for (const k of PILLS) pills[k] = h('span', { class: 'pill', text: t(`remap.test.${k}`) });
    const el = h('div', { class: `remap-device ${d.connected ? 'on' : 'off'}`, attrs: { 'data-device': d.id } },
      h('div', { class: 'remap-device-head' },
        icon(isKeyboard(d.id) ? 'keyboard' : 'gamepad'),
        // Teclados com o nome da coluna da grade ("Teclado 1"); o nome completo fica na dica.
        h('span', { class: 'device-label', title: d.label, text: isKeyboard(d.id) ? deviceLabel(d.id) : d.label }),
        h('span', { class: `device-seat${d.boundSeat === null ? '' : ' bound'}`, text: seatText(d) }),
      ),
      h('div', { class: 'remap-test' },
        h('span', { class: 'test-label', text: t('remap.test.steer') }),
        h('span', { class: 'steer-track' }, h('span', { class: 'steer-center' }), steer),
        h('span', { class: 'test-label', text: t('remap.test.throttle') }),
        h('span', { class: 'pedal-track' }, throttle),
        h('span', { class: 'test-label', text: t('remap.test.brake') }),
        h('span', { class: 'pedal-track' }, brake),
      ),
      h('div', { class: 'remap-pills' }, PILLS.map((k) => pills[k])),
    );
    return { id: d.id, el, steer, throttle, brake, pills, drawn: '' };
  }

  function refreshDevices(): void {
    const devices = input.devices();
    const sig = devices.map((d) => `${d.id}|${d.label}|${d.connected}|${d.boundSeat}`).join(';');
    if (sig === signature) return;
    signature = sig;
    // Nomes dos botões no estilo do primeiro controle conectado (✕ ○ □ △ num PlayStation).
    const pad = devices.find((d) => !isKeyboard(d.id) && d.connected);
    const style = pad ? padStyleOf(pad.label) : 'xbox';
    if (style !== padStyle) { padStyle = style; renderAll(); }
    rows = devices.map(deviceRow);
    deviceList.replaceChildren(...rows.map((r) => r.el));
  }

  function drawTest(r: DeviceRow, p: DevicePeek | null): void {
    const steer = p ? Math.max(-1, Math.min(1, p.steer)) : 0;
    const key = p ? `${steer.toFixed(2)}|${+p.throttle}${+p.brake}${+p.nitro}${+p.gearUp}${+p.gearDown}${+p.pause}` : '-';
    if (key === r.drawn) return;
    r.drawn = key;
    r.steer.style.left = `${50 + Math.min(0, steer) * 50}%`;
    r.steer.style.width = `${Math.abs(steer) * 50}%`;
    r.throttle.style.width = p?.throttle ? '100%' : '0%';
    r.brake.style.width = p?.brake ? '100%' : '0%';
    for (const k of PILLS) r.pills[k].classList.toggle('on', p?.[k] === true);
    const active = !!p && (p.throttle || p.brake || steer !== 0 || PILLS.some((k) => p[k]));
    r.el.classList.toggle('active', active);
  }

  function updateTest(): void {
    for (const r of rows) drawTest(r, input.peek(r.id));
  }

  refreshDevices();
  renderAll();
  void layoutMapOnce().then((m) => {
    if (destroyed || !m) return;
    layoutMap = m;
    renderAll();
  });

  const el = screenFrame('controls', t('ui.controls.title'),
    h('div', { class: 'remap-layout' },
      h('div', { class: 'remap-main glass' },
        h('div', { class: 'remap-grid' }, gridChildren),
        status,
        conflictsEl,
      ),
      h('div', { class: 'remap-side glass' },
        h('h2', { class: 'sub-title', title: t('ui.controls.detected'), text: t('remap.test.title') }),
        deviceList,
        h('p', { class: 'hint', text: t('ui.controls.gamepadHint') }),
      ),
    ),
    h('p', { class: 'hint remap-help', text: t('remap.help') }),
    h('div', { class: 'actions' }, back.el),
  );
  // Clique fora da célula que está capturando cancela (o clique numa célula abre a captura dela).
  el.addEventListener('pointerdown', () => { if (capture) resolve({ kind: 'cancel', reason: 'escape' }); }, true);

  return {
    el,
    nav(nav) {
      if (capture) return; // tudo vai para a captura
      if (navGuard !== null && nav.device === navGuard) return;
      listNav(list, nav, api.sfx, () => api.back());
    },
    update(dt) {
      pollCapture(dt);
      if (navGuard !== null && (input.peek(navGuard)?.buttons.length ?? 0) === 0) navGuard = null;
      refreshDevices();
      updateTest();
    },
    destroy() {
      destroyed = true;
      window.removeEventListener('keydown', onKeyDown, true);
      window.removeEventListener('keyup', onKeyUp, true);
    },
  };
}
