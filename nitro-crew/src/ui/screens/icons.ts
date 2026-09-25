// Ícones em SVG inline — sem fonte de ícone nem imagem externa. A marcação é constante nossa
// (nunca dado do usuário), por isso o innerHTML de um <template> é seguro aqui.
export type IconName =
  | 'keyboard' | 'gamepad' | 'lock' | 'sun' | 'dusk' | 'moon' | 'check' | 'trophy'
  | 'chevron-left' | 'chevron-right' | 'clock' | 'flag' | 'users' | 'timer';

const PATHS: Readonly<Record<IconName, string>> = {
  keyboard: '<rect x="2" y="6" width="20" height="12" rx="2.5"/><path d="M6.5 10h.01M10.5 10h.01M14.5 10h.01M18.5 10h.01M6.5 14h.01M18.5 14h.01M9.5 14h5"/>',
  gamepad: '<path d="M7 7h10a5 5 0 0 1 5 5v3.5a2.5 2.5 0 0 1-4.6 1.4L15.8 15H8.2l-1.6 1.9A2.5 2.5 0 0 1 2 15.5V12a5 5 0 0 1 5-5z"/><path d="M7.5 10.5v3M6 12h3"/><circle cx="16" cy="11" r=".7"/><circle cx="18.5" cy="13" r=".7"/>',
  lock: '<rect x="5" y="11" width="14" height="10" rx="2.5"/><path d="M8 11V7.5a4 4 0 0 1 8 0V11"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4"/>',
  dusk: '<path d="M3 16h18M6 20h12"/><path d="M7 16a5 5 0 0 1 10 0"/><path d="M12 5.5v2.5M5.5 9.5l1.8 1.2M18.5 9.5l-1.8 1.2"/>',
  moon: '<path d="M20 14.6A8.2 8.2 0 0 1 9.4 4a8.2 8.2 0 1 0 10.6 10.6z"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7"/>',
  trophy: '<path d="M7 4h10v5a5 5 0 0 1-10 0z"/><path d="M7 6H4v2a3 3 0 0 0 3 3M17 6h3v2a3 3 0 0 1-3 3M12 14v3M8 20h8M9.5 17h5"/>',
  'chevron-left': '<path d="M15 5l-7 7 7 7"/>',
  'chevron-right': '<path d="M9 5l7 7-7 7"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  flag: '<path d="M5 21V4M5 4h11l-2 4 2 4H5"/>',
  users: '<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0"/><circle cx="17" cy="9" r="2.5"/><path d="M15.5 14.5A5 5 0 0 1 21.5 20"/>',
  timer: '<circle cx="12" cy="13" r="8"/><path d="M12 9v4l2.5 1.5M9 2.5h6M12 2.5v2.5"/>',
};

function fromMarkup(markup: string): SVGSVGElement {
  const tpl = document.createElement('template');
  tpl.innerHTML = markup;
  return tpl.content.firstElementChild as SVGSVGElement;
}

export function icon(name: IconName, cls = ''): SVGSVGElement {
  return fromMarkup(`<svg class="ico ${cls}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${PATHS[name]}</svg>`);
}

const MEDAL_COLORS = ['#ffc53d', '#cfd6e0', '#d08b5b'];

/** Medalha para 1º, 2º e 3º; nulo para o resto. */
export function medal(position: number): SVGSVGElement | null {
  const color = MEDAL_COLORS[position - 1];
  if (!color) return null;
  return fromMarkup(`<svg class="ico medal" viewBox="0 0 24 24" aria-hidden="true"><path d="M8.5 2.5l2.2 5.5M15.5 2.5l-2.2 5.5" stroke="${color}" stroke-width="2.4" stroke-linecap="round"/><circle cx="12" cy="14.5" r="7.5" fill="${color}"/><text x="12" y="18" text-anchor="middle" font-size="9.5" font-weight="800" font-family="Inter, Segoe UI, Roboto, Arial, sans-serif" fill="#1a1c22">${position}</text></svg>`);
}

/** Silhueta de carro (vista lateral, low-poly) na cor do carro. */
export function carSilhouette(color: string): SVGSVGElement {
  return fromMarkup(`<svg class="car-svg" viewBox="0 0 200 72" aria-hidden="true">
<defs><linearGradient id="carShine" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".35"/><stop offset=".55" stop-color="#fff" stop-opacity="0"/></linearGradient></defs>
<ellipse cx="100" cy="64" rx="86" ry="5" fill="rgba(0,0,0,.45)"/>
<path d="M10 46l16-3 18-19q6-7 16-7h56q12 0 22 8l20 15 20 3q8 1 8 8v4q0 5-5 5H16q-6 0-6-6z" fill="${color}"/>
<path d="M10 46l16-3 18-19q6-7 16-7h56q12 0 22 8l20 15 20 3q8 1 8 8v4q0 5-5 5H16q-6 0-6-6z" fill="url(#carShine)"/>
<path d="M50 41l14-16q3-3 8-3h44q7 0 12 4l16 15z" fill="#0f1320" opacity=".85"/>
<path d="M94 22h4v19h-4z" fill="${color}"/>
<path d="M10 50h180" stroke="rgba(0,0,0,.25)" stroke-width="2"/>
<circle cx="52" cy="56" r="12" fill="#12161f"/><circle cx="52" cy="56" r="6" fill="#9aa3b2"/><circle cx="52" cy="56" r="2.5" fill="#12161f"/>
<circle cx="150" cy="56" r="12" fill="#12161f"/><circle cx="150" cy="56" r="6" fill="#9aa3b2"/><circle cx="150" cy="56" r="2.5" fill="#12161f"/>
<path d="M182 45h8v6h-8z" fill="#ff5a36"/><path d="M10 44h8v6h-8z" fill="#ffe9a8"/>
</svg>`);
}
