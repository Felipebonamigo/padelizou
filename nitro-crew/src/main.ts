import { createSession } from './game/session';

const canvas = document.getElementById('game') as HTMLCanvasElement | null;
const hud = document.getElementById('hud');
const ui = document.getElementById('ui');
if (!canvas || !hud || !ui) throw new Error('index.html precisa de #game, #hud e #ui');

const session = createSession(canvas, hud, ui);

// O AudioContext só nasce depois de um gesto do usuário.
const unlock = () => { session.audio.unlock(); window.removeEventListener('keydown', unlock); window.removeEventListener('pointerdown', unlock); };
window.addEventListener('keydown', unlock);
window.addEventListener('pointerdown', unlock);

session.start();

// API de depuração/playtest: window.nc.session, window.nc.startQuick(...)
declare global { interface Window { nc: { session: typeof session } } }
window.nc = { session };
