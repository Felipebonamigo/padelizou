#!/usr/bin/env python3
"""Sintetiza os sons do Padelizou Arena em Padel.Godot/audio/ — nenhum arquivo de terceiro.

É a ponte até a gravação num clube prevista no REALISMO.md: cada som é montado a partir do que
fisicamente acontece (modos de vibração amortecidos, ruído filtrado, grãos de areia, palmas),
com semente fixa por arquivo, então rodar de novo dá os mesmos WAVs. Só biblioteca padrão.

Formato de todos: 44,1 kHz, 16 bits, mono, pico normalizado em -3 dBFS. Cada som curto sai em
3 a 5 variações (o SomNode sorteia entre elas e ainda varia a altura em ±5 %). O ambiente é um
loop de 20 s que emenda sem salto: tudo nele é construído como sinal periódico (ruído indexado em
módulo, eventos em linha do tempo circular, filtros aquecidos com o fim do próprio período) e o
WAV leva um bloco 'smpl' com o loop, que o importador do Godot lê.

Rodar:   python3 padelizou-arena/ferramentas/sintetizar_sons.py [--pasta DESTINO]
Testar:  python3 padelizou-arena/ferramentas/teste_sintetizar_sons.py
"""
import array
import math
import os
import random
import struct
import sys
import time
import wave

TAXA = 44100
PICO_DBFS = -3.0
SEMENTE = "padelizou-arena/som/2026"
PASTA_PADRAO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Padel.Godot", "audio"))
DOIS_PI = 2 * math.pi


# ─────────────────────────────── utilidades de sinal ───────────────────────────────

def zeros(n):
    return [0.0] * n


def amostras(segundos, taxa=TAXA):
    return int(round(segundos * taxa))


def coef(tipo, f, q, taxa=TAXA):
    """Coeficientes de biquad (receitas do Robert Bristow-Johnson), normalizados: (b0, b1, b2, a1, a2)."""
    f = min(f, 0.45 * taxa)
    w = DOIS_PI * f / taxa
    cw, sw = math.cos(w), math.sin(w)
    alfa = sw / (2 * q)
    if tipo == "pb":      # passa-baixa
        b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
    elif tipo == "pa":    # passa-alta
        b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
    elif tipo == "bp":    # passa-banda, ganho 0 dB no centro
        b0, b1, b2 = alfa, 0.0, -alfa
    else:
        raise ValueError(tipo)
    a0 = 1 + alfa
    return (b0 / a0, b1 / a0, b2 / a0, -2 * cw / a0, (1 - alfa) / a0)


def filtrar(x, c):
    """Biquad em forma direta transposta II."""
    b0, b1, b2, a1, a2 = c
    z1 = z2 = 0.0
    y = [0.0] * len(x)
    for i, v in enumerate(x):
        o = b0 * v + z1
        z1 = b1 * v - a1 * o + z2
        z2 = b2 * v - a2 * o
        y[i] = o
    return y


def filtrar_circular(x, c, aquecimento):
    """Filtra um sinal periódico: aquece o filtro com o fim do período e devolve um período inteiro.
    Assim a saída também é periódica — o fim emenda no começo com o estado do filtro contínuo."""
    aquecimento = min(aquecimento, len(x))
    y = filtrar(x[len(x) - aquecimento:] + x, c)
    return y[aquecimento:]


def somar(destino, fonte, inicio=0, ganho=1.0, circular=False):
    n = len(destino)
    if circular:
        for i, v in enumerate(fonte):
            destino[(inicio + i) % n] += ganho * v
    else:
        fim = min(n, inicio + len(fonte))
        for i in range(max(0, -inicio), fim - inicio):
            destino[inicio + i] += ganho * fonte[i]


def pico(x):
    return max((abs(v) for v in x), default=0.0)


def escalar(x, ganho):
    return [v * ganho for v in x]


def normalizar_para(x, alvo=1.0):
    p = pico(x)
    return escalar(x, alvo / p) if p > 0 else x


def modo(n, f, tau, amp=1.0, ataque=0.0005, glide=0.0, tau_glide=0.006, fase=0.0, taxa=TAXA):
    """Um modo de vibração: senoide amortecida com ataque (diferença de exponenciais) e, opcionalmente,
    um glide de altura no começo (a frequência parte de f·(1+glide) e cai pra f — o 'toc' que afunda)."""
    y = [0.0] * n
    k1 = math.exp(-1.0 / (tau * taxa))
    k2 = math.exp(-1.0 / (max(ataque, 1e-5) * taxa))
    e1, e2 = amp, amp
    if glide == 0.0:
        w = DOIS_PI * f / taxa
        cr, sr = math.cos(w), math.sin(w)
        s, c = math.sin(fase), math.cos(fase)
        for i in range(n):
            y[i] = (e1 - e2) * s
            s, c = s * cr + c * sr, c * cr - s * sr
            e1 *= k1
            e2 *= k2
    else:
        kg = math.exp(-1.0 / (tau_glide * taxa))
        g = glide
        ph = fase
        base = DOIS_PI * f / taxa
        for i in range(n):
            y[i] = (e1 - e2) * math.sin(ph)
            ph += base * (1.0 + g)
            g *= kg
            e1 *= k1
            e2 *= k2
    # Normaliza pro pico da envoltória ser 'amp' (a diferença de exponenciais não chega a 1).
    if ataque > 0:
        t_pico = math.log(tau / ataque) / (1 / ataque - 1 / tau) if tau != ataque else ataque
        env_pico = math.exp(-t_pico / tau) - math.exp(-t_pico / ataque)
        if env_pico > 1e-6:
            y = escalar(y, 1.0 / env_pico)
    return y


def envoltoria_ar(n, ataque, tau, taxa=TAXA):
    """Envoltória de ataque linear e queda exponencial."""
    na = max(1, amostras(ataque, taxa))
    k = math.exp(-1.0 / (tau * taxa))
    env = [0.0] * n
    e = 1.0
    for i in range(n):
        if i < na:
            env[i] = i / na
        else:
            env[i] = e
            e *= k
    return env


def ruido(rng, n):
    g = rng.gauss
    return [g(0.0, 1.0) for _ in range(n)]


def rajada(rng, n, ataque, tau, filtro=None, taxa=TAXA):
    """Ruído com envoltória, opcionalmente filtrado (a envoltória é aplicada antes: o filtro dá o 'corpo')."""
    env = envoltoria_ar(n, ataque, tau, taxa)
    r = ruido(rng, n)
    x = [a * b for a, b in zip(r, env)]
    if filtro:
        for c in filtro:
            x = filtrar(x, c)
    return x


def trilha_de_graos(rng, n, quantos, t_medio, tau_amp, inicio=0.0, taxa=TAXA):
    """Impulsos esparsos (grãos de areia, fios de malha): tempos exponenciais a partir de 'inicio'."""
    x = [0.0] * n
    for _ in range(quantos):
        t = inicio + rng.expovariate(1.0 / t_medio)
        i = amostras(t, taxa)
        if 0 <= i < n:
            x[i] += (1 if rng.random() < 0.5 else -1) * rng.uniform(0.2, 1.0) * math.exp(-(t - inicio) / tau_amp)
    return x


def passa_alta_dc(x, taxa=TAXA):
    """Tira DC e subgrave inaudível (2ª ordem em 28 Hz) — com cauda, pra não truncar o filtro."""
    return filtrar(x + zeros(amostras(0.05, taxa)), coef("pa", 28.0, 0.7071, taxa))


def aparar(x, limiar_db=-70.0, margem=0.004, fade=0.010, fade_in=0.0003, taxa=TAXA):
    """Corta o silêncio do fim (abaixo de limiar_db do pico), com fade de saída e um fade de entrada
    curtíssimo — a primeira amostra é zero, então disparar o som não dá clique."""
    p = pico(x)
    lim = p * 10 ** (limiar_db / 20)
    fim = len(x)
    while fim > 1 and abs(x[fim - 1]) < lim:
        fim -= 1
    # O fade fica todo DEPOIS da última amostra acima do limiar: ele não pode morder a cauda do som.
    nf = amostras(fade, taxa)
    fim += amostras(margem, taxa) + nf
    y = x[:fim] + zeros(max(0, fim - len(x)))
    for i in range(nf):
        y[len(y) - 1 - i] *= 0.5 - 0.5 * math.cos(math.pi * i / nf)
    ni = max(1, amostras(fade_in, taxa))
    for i in range(min(ni, len(y))):
        y[i] *= 0.5 - 0.5 * math.cos(math.pi * i / ni)
    return y


def subir_taxa(x, fator, circular=False):
    """Interpolação linear por um fator inteiro (o passa-baixa depois limpa as imagens)."""
    n = len(x)
    y = [0.0] * (n * fator)
    for i in range(n):
        a = x[i]
        b = x[(i + 1) % n] if circular else (x[i + 1] if i + 1 < n else 0.0)
        d = (b - a) / fator
        base = i * fator
        for k in range(fator):
            y[base + k] = a + d * k
    return y


def reverb(x, taxa, rt60, amortecimento=0.35, pre=0.012, tamanho=1.0, circular=False):
    """Reverberação de Schroeder/Freeverb (4 pentes com amortecimento + 2 passa-tudo). Devolve só o 'molhado'.
    Circular: aquece com os últimos segundos do período, e a cauda do fim cai no começo."""
    escala = taxa / 44100.0 * tamanho
    atrasos = [int(d * escala) for d in (1116, 1277, 1422, 1557)]
    aps = [int(d * taxa / 44100.0) for d in (556, 341)]
    pre_n = amostras(pre, taxa)
    if circular:
        aquec = min(len(x), amostras(rt60 * 2.5, taxa))
        entrada = x[len(x) - aquec:] + x
    else:
        aquec = 0
        entrada = x + zeros(amostras(rt60 * 1.2, taxa))
    n = len(entrada)
    # pré-atraso
    entrada = zeros(pre_n) + entrada[:n - pre_n] if pre_n else entrada
    ganhos = [10 ** (-3.0 * d / (taxa * rt60)) for d in atrasos]
    bufs = [zeros(d) for d in atrasos]
    abuf = [zeros(d) for d in aps]
    am, um = amortecimento, 1 - amortecimento
    b0, b1, b2, b3 = bufs
    d0, d1, d2, d3 = atrasos
    g0, g1, g2, g3 = ganhos
    i0 = i1 = i2 = i3 = 0
    f0 = f1 = f2 = f3 = 0.0
    ab0, ab1 = abuf
    ad0, ad1 = aps
    j0 = j1 = 0
    y = [0.0] * n
    for i in range(n):
        v = entrada[i] * 0.25
        o0 = b0[i0]; f0 = o0 * um + f0 * am; b0[i0] = v + f0 * g0; i0 += 1
        if i0 == d0: i0 = 0
        o1 = b1[i1]; f1 = o1 * um + f1 * am; b1[i1] = v + f1 * g1; i1 += 1
        if i1 == d1: i1 = 0
        o2 = b2[i2]; f2 = o2 * um + f2 * am; b2[i2] = v + f2 * g2; i2 += 1
        if i2 == d2: i2 = 0
        o3 = b3[i3]; f3 = o3 * um + f3 * am; b3[i3] = v + f3 * g3; i3 += 1
        if i3 == d3: i3 = 0
        s = o0 + o1 + o2 + o3
        bo = ab0[j0]; o = bo - s; ab0[j0] = s + bo * 0.5; j0 += 1
        if j0 == ad0: j0 = 0
        bo = ab1[j1]; s = bo - o; ab1[j1] = o + bo * 0.5; j1 += 1
        if j1 == ad1: j1 = 0
        y[i] = s
    return y[aquec:] if circular else y


# ─────────────────────────────── golpes de raquete ───────────────────────────────
#
# A raquete de padel não tem corda: é um núcleo de espuma EVA com faces de fibra, furado. O contato
# é um "toc" oco, mais grave e mais curto que o "ping" do tênis: um modo principal de 650–900 Hz que
# morre em ~15–30 ms, um baque grave do aro (~200 Hz), a casca da bola (~1,15 kHz) e um estalo de
# contato curtíssimo. O que muda entre golpes é a dureza do contato (ataque e brilho), quanto o corpo
# soa e o que vem junto: o corte da bandeja "escova" a face, o smash puxa ar pelos furos.

GOLPES = {
    #            corpo  baixo  alto   brilho | tau(ms) corpo baixo alto | amp corpo baixo alto brilho bola | ataque(ms) estalo  f_estalo escova sopro glide
    "drive":   (780, 215, 1520, 2650, 17, 20, 10, 1.00, 0.32, 0.45, 0.24, 0.40, 0.40, 0.65, 3300, 0.00, 0.00, 0.06),
    "voleio":  (900, 245, 1760, 3050, 11, 14, 7, 1.00, 0.22, 0.55, 0.30, 0.30, 0.25, 0.85, 3900, 0.00, 0.00, 0.04),
    "smash":   (690, 180, 1340, 2420, 22, 28, 14, 1.00, 0.50, 0.55, 0.34, 0.45, 0.30, 1.00, 2800, 0.00, 0.20, 0.09),
    "bandeja": (820, 225, 1610, 2800, 14, 17, 8, 0.90, 0.25, 0.34, 0.16, 0.30, 0.55, 0.35, 3000, 0.55, 0.07, 0.05),
    "lob":     (640, 200, 1290, 2300, 17, 20, 8, 1.00, 0.35, 0.20, 0.05, 0.28, 1.10, 0.16, 1800, 0.12, 0.00, 0.03),
}


def golpe(tipo, rng):
    (f_corpo, f_baixo, f_alto, f_brilho, t_corpo, t_baixo, t_alto, a_corpo, a_baixo, a_alto, a_brilho, a_bola,
     ataque_ms, estalo, f_estalo, escova, sopro, glide) = GOLPES[tipo]
    v = lambda x, d=0.05: x * (1 + rng.uniform(-d, d))  # noqa: E731 — variação por arquivo
    n = amostras(0.32 if tipo == "smash" else 0.22)
    x = zeros(n)
    ataque = v(ataque_ms, 0.2) / 1000
    atraso = lambda: amostras(rng.uniform(0, 0.0004))  # noqa: E731 — as partes não soam no mesmo instante exato
    somar(x, modo(n, v(f_corpo, 0.04), v(t_corpo, 0.15) / 1000, v(a_corpo, 0.1), ataque, glide=v(glide, 0.3)))
    somar(x, modo(n, v(f_baixo, 0.06), v(t_baixo, 0.15) / 1000, v(a_baixo, 0.2), ataque * 1.5), atraso())
    somar(x, modo(n, v(f_alto, 0.05), v(t_alto, 0.2) / 1000, v(a_alto, 0.2), ataque, fase=rng.uniform(0, 6.28)), atraso())
    somar(x, modo(n, v(f_brilho, 0.05), v(t_alto * 0.6, 0.2) / 1000, v(a_brilho, 0.25), ataque, fase=rng.uniform(0, 6.28)), atraso())
    # A casca da bola: o "pock" que afunda um pouco enquanto ela se descomprime.
    somar(x, modo(n, v(1150, 0.05), v(0.009, 0.2), v(a_bola, 0.2), ataque, glide=0.08, tau_glide=0.004), atraso())
    if estalo > 0:
        ne = amostras(0.012)
        clique = rajada(rng, ne, 0.00015, v(0.0009, 0.2), [coef("bp", v(f_estalo, 0.1), 0.8)])
        somar(x, normalizar_para(clique, v(estalo, 0.15)))
    if escova > 0:
        # Corte: a bola rola na face furada por alguns milissegundos — um "shh" agudo colado no toc.
        ne = amostras(0.08)
        e = rajada(rng, ne, 0.002, v(0.013, 0.2), [coef("bp", v(3300, 0.1), 1.2), coef("pa", 1500, 0.7)])
        somar(x, normalizar_para(e, v(escova, 0.15)), amostras(0.0005))
    if sopro > 0:
        # Remate: a raquete passa rasgando o ar depois do contato (os furos assobiam de leve).
        ns = amostras(0.22)
        s = rajada(rng, ns, 0.018, v(0.055, 0.2), [coef("bp", v(650, 0.15), 0.7), coef("pb", 2200, 0.7)])
        somar(x, normalizar_para(s, v(sopro, 0.2)), amostras(0.004))
    return x


# ─────────────────────────────── bola no chão, vidro, grade, rede ───────────────────────────────

def quique(rng):
    """Grama sintética com areia: um baque abafado (a grama não ressoa), a casca da bola curta e a areia
    espirrando — grãos agudos nos primeiros 40 ms e uns poucos caindo de volta depois."""
    v = lambda x, d=0.08: x * (1 + rng.uniform(-d, d))  # noqa: E731
    n = amostras(0.26)
    x = zeros(n)
    somar(x, modo(n, v(150, 0.15), v(0.014), v(0.7, 0.1), 0.0015))
    somar(x, modo(n, v(460, 0.1), v(0.010), v(0.4, 0.2), 0.001))      # a bola amassando contra a areia
    somar(x, modo(n, v(1050, 0.06), v(0.007), v(0.7, 0.2), 0.0005, glide=0.05, tau_glide=0.003))
    somar(x, modo(n, v(2300, 0.08), 0.003, v(0.12, 0.3), 0.0003, fase=rng.uniform(0, 6.28)))
    areia = trilha_de_graos(rng, n, rng.randint(40, 70), v(0.012, 0.3), 0.02)
    somar(areia, trilha_de_graos(rng, n, rng.randint(4, 10), 0.04, 0.05, inicio=0.03), 0, 0.5)
    areia = filtrar(filtrar(areia, coef("bp", v(4500, 0.15), 0.9)), coef("pa", 1800, 0.7))
    somar(x, normalizar_para(areia, v(0.32, 0.2)))
    grama = rajada(rng, amostras(0.06), 0.001, v(0.010), [coef("bp", v(1600, 0.15), 1.0)])
    somar(x, normalizar_para(grama, v(0.2, 0.2)))
    return x


def vidro(rng):
    """Bola no vidro temperado de 12 mm: o estalo seco do contato, o 'bum' grave do painel empurrando o ar
    e a ressonância curta dos modos de placa (painel ~2×3 m preso na estrutura) — é o som que mais
    identifica o padel. Os modos saem da equação da placa apoiada, com o ponto de impacto sorteado:
    cada impacto excita um conjunto diferente, como no clube."""
    v = lambda x, d=0.08: x * (1 + rng.uniform(-d, d))  # noqa: E731
    n = amostras(0.75)
    x = zeros(n)
    clique = rajada(rng, amostras(0.015), 0.0001, v(0.0014, 0.2), [coef("bp", v(2000, 0.1), 0.5)])
    somar(x, normalizar_para(clique, v(1.0, 0.1)))
    somar(x, modo(n, v(1100, 0.05), 0.008, v(0.5, 0.2), 0.0004, glide=0.06, tau_glide=0.003))
    somar(x, modo(n, v(95, 0.12), v(0.035, 0.15), 0.6, 0.002))
    # Modos de placa: f_mn = f11 · (m²/a² + n²/b²) / (1/a² + 1/b²).
    a, b = v(2.0, 0.05), v(3.0, 0.04)
    f11 = v(10.7, 0.06)
    px, py = rng.uniform(0.2, 0.8) * a, rng.uniform(0.15, 0.6) * b   # a bola bate da metade pra baixo
    norma = 1 / a ** 2 + 1 / b ** 2
    candidatos = []
    for m in range(1, 26):
        for k in range(1, 30):
            f = f11 * (m * m / a ** 2 + k * k / b ** 2) / norma
            if 90 <= f <= 2500:
                forma = abs(math.sin(m * math.pi * px / a) * math.sin(k * math.pi * py / b))
                candidatos.append((forma / (f / 150) ** 0.45, f))
    candidatos.sort(reverse=True)
    painel = zeros(n)
    for peso, f in candidatos[:32]:
        tau = 0.11 * (150 / f) ** 0.5 * rng.uniform(0.8, 1.2)
        somar(painel, modo(n, f, tau, peso, 0.0006, fase=rng.uniform(0, 6.28)))
    somar(x, normalizar_para(painel, v(0.9, 0.12)))
    # O "tim" de vidro: poucos modos agudos, curtos.
    for _ in range(5):
        somar(x, modo(n, rng.uniform(2200, 5200), rng.uniform(0.012, 0.035), rng.uniform(0.08, 0.2), 0.0003,
                      fase=rng.uniform(0, 6.28)))
    return x


def _tinido(rng, taxa=TAXA):
    """Um microimpacto metálico: estalo + três modos de fio de aço."""
    n = amostras(0.03, taxa)
    t = zeros(n)
    for _ in range(3):
        somar(t, modo(n, rng.uniform(1500, 6000), rng.uniform(0.005, 0.02), rng.uniform(0.4, 1.0), 0.0002,
                      fase=rng.uniform(0, 6.28), taxa=taxa))
    somar(t, rajada(rng, amostras(0.004, taxa), 0.0001, 0.0004, [coef("pa", 2500, 0.7, taxa)], taxa), 0, 0.5)
    return normalizar_para(t)


def grade(rng):
    """Bola na grade (malha eletrossoldada em painel de aço): baque morto da bola, fios que tinem e o
    chocalho — o painel balança (~15 Hz) e bate na estrutura em rajadas que vão morrendo."""
    v = lambda x, d=0.08: x * (1 + rng.uniform(-d, d))  # noqa: E731
    n = amostras(1.0)
    x = zeros(n)
    somar(x, modo(n, v(250, 0.1), 0.015, 0.6, 0.001))
    somar(x, modo(n, v(900, 0.08), 0.006, 0.3, 0.0005))
    somar(x, normalizar_para(rajada(rng, amostras(0.02), 0.0001, 0.003, [coef("pa", 3000, 0.7)]), 0.5))
    fios = zeros(n)
    for _ in range(18):
        f = math.exp(rng.uniform(math.log(1200), math.log(7000)))
        somar(fios, modo(n, f, rng.uniform(0.03, 0.18), rng.uniform(0.3, 1.0) * (1200 / f) ** 0.5, 0.0003,
                         fase=rng.uniform(0, 6.28)))
    somar(x, normalizar_para(fios, v(0.3, 0.15)))
    tinidos = [_tinido(rng) for _ in range(6)]
    chocalho = zeros(n)
    f_balanco = v(15.0, 0.15)
    tau = v(0.14, 0.2)
    ciclo = 0
    while True:
        t0 = 0.006 + ciclo / f_balanco
        if t0 > 0.7:
            break
        for _ in range(rng.choice((1, 1, 2, 3))):
            t = t0 + rng.gauss(0, 0.004)
            if t > 0:
                somar(chocalho, rng.choice(tinidos), amostras(t), rng.uniform(0.3, 1.0) * math.exp(-t / tau))
        ciclo += 1
    somar(x, normalizar_para(chocalho, v(0.55, 0.15)))
    somar(x, modo(n, v(72, 0.1), 0.10, 0.12, 0.004))
    somar(x, modo(n, v(145, 0.1), 0.07, 0.10, 0.003))
    return x


def rede(rng, com_poste=False):
    """Bola na rede: um 'fump' abafado (a malha freia a bola em ~30 ms), o farfalhar dos fios e o cabo de
    cima balançando grave. Numa das variações a bola pega perto do poste e dá um tique metálico."""
    v = lambda x, d=0.08: x * (1 + rng.uniform(-d, d))  # noqa: E731
    n = amostras(0.55)
    x = zeros(n)
    abafado = rajada(rng, amostras(0.3), v(0.004), v(0.032), [coef("pb", v(750, 0.15), 0.7), coef("pb", 1000, 0.7)])
    somar(x, normalizar_para(abafado, 1.0))
    somar(x, modo(n, v(900, 0.1), 0.004, 0.25, 0.0005))
    farfalho = trilha_de_graos(rng, n, rng.randint(20, 40), 0.06, 0.12, inicio=0.015)
    farfalho = filtrar(farfalho, coef("bp", v(2000, 0.15), 1.5))
    somar(x, normalizar_para(farfalho, v(0.25, 0.2)))
    # Cabo: ~110 Hz com o balanço da rede modulando de leve.
    cabo = modo(n, v(110, 0.1), v(0.07, 0.2), 1.0, 0.008)
    f_vib = v(5.0, 0.2)
    cabo = [c * (1 + 0.25 * math.sin(DOIS_PI * f_vib * i / TAXA)) for i, c in enumerate(cabo)]
    somar(x, cabo, 0, v(0.12, 0.2))
    if com_poste:
        somar(x, modo(n, v(2800, 0.05), 0.012, 0.15, 0.0003), amostras(0.015))
    return x


# ─────────────────────────────── passos na grama com areia ───────────────────────────────

def _pisada(rng, n, t0, forca, x):
    """Um contato do tênis no chão: baque grave, a areia estalando sob a sola e o roçar da fibra."""
    v = lambda y, d=0.1: y * (1 + rng.uniform(-d, d))  # noqa: E731
    i0 = amostras(t0)
    m = n - i0
    somar(x, modo(m, v(90, 0.2), v(0.015, 0.2), 0.28 * forca, 0.003), i0)
    areia = trilha_de_graos(rng, m, int(rng.randint(60, 120) * forca), v(0.018, 0.3), 0.04)
    areia = filtrar(filtrar(areia, coef("bp", v(3500, 0.2), 0.8)), coef("pa", 1500, 0.7))
    somar(x, normalizar_para(areia, 0.5 * forca), i0)
    fibra = rajada(rng, amostras(0.1), 0.005, v(0.025, 0.2), [coef("bp", v(1200, 0.2), 0.9)])
    somar(x, normalizar_para(fibra, 0.25 * forca), i0)


def passo(rng, estilo):
    n = amostras(0.4)
    x = zeros(n)
    if estilo == "calcanhar_ponta":
        _pisada(rng, n, 0.0, 1.0, x)
        _pisada(rng, n, rng.uniform(0.05, 0.075), rng.uniform(0.55, 0.8), x)
    elif estilo == "apoio":        # split-step: os dois pés quase juntos, mais pesado
        _pisada(rng, n, 0.0, 1.0, x)
        _pisada(rng, n, rng.uniform(0.012, 0.022), 0.9, x)
        somar(x, modo(n, rng.uniform(65, 80), 0.025, 0.3, 0.004))
    elif estilo == "curto":        # arranque: um contato só, seco
        _pisada(rng, n, 0.0, 1.0, x)
    elif estilo == "arrasto":      # deslize lateral, típico do padel
        _pisada(rng, n, 0.0, 0.7, x)
        na = amostras(0.2)
        env = [min(1.0, i / amostras(0.02)) * (1.0 if i < amostras(0.13) else math.exp(-(i - amostras(0.13)) / amostras(0.025)))
               * (0.75 + 0.25 * math.sin(DOIS_PI * 23 * i / TAXA + rng.uniform(0, 6.28))) for i in range(na)]
        r = [a * b for a, b in zip(ruido(rng, na), env)]
        r = filtrar(filtrar(r, coef("bp", rng.uniform(2200, 2800), 0.7)), coef("pa", 900, 0.7))
        graos = trilha_de_graos(rng, na, 90, 0.07, 0.2)
        graos = filtrar(graos, coef("bp", 4200, 0.9))
        somar(x, normalizar_para(r, 0.45), amostras(0.01))
        somar(x, normalizar_para(graos, 0.3), amostras(0.01))
    else:
        raise ValueError(estilo)
    return x


# ─────────────────────────────── público (a 22,05 kHz, depois sobe) ───────────────────────────────

TAXA_PUBLICO = TAXA // 2

VOGAIS = {  # F1, F2, F3 (Hz) — média entre vozes masculinas e femininas
    "a": (750, 1250, 2600), "e": (480, 1900, 2600), "é": (600, 1750, 2600), "i": (320, 2250, 3000),
    "o": (460, 900, 2500), "ó": (580, 1000, 2500), "u": (340, 780, 2400),
}


def _vozes(rng, n, taxa, vozes, trajetoria, respiracao=0.12):
    """Coro sem palavras: cada voz é um dente de serra (PolyBLEP) com a altura própria, entradas
    desencontradas e vibrato; os formantes (três passa-bandas em paralelo) seguem a trajetória de vogal
    [(t, F1, F2, F3)] — são compartilhados pelo grupo, o que custa pouco e soa como gente junta."""
    fonte = zeros(n)
    for (f_ini, f_fim, t_ini, dur, amp) in vozes:
        i0, i1 = amostras(t_ini, taxa), min(n, amostras(t_ini + dur, taxa))
        if i1 <= i0:
            continue
        ph = rng.random()
        vib_f, vib_ph = rng.uniform(4.5, 6.5), rng.uniform(0, 6.28)
        na = amostras(rng.uniform(0.05, 0.11), taxa)
        tot = i1 - i0
        nr = max(1, amostras(min(0.35, dur * 0.4), taxa))
        a_lp = 0.0
        for j in range(tot):
            p = j / tot
            f = f_ini + (f_fim - f_ini) * p
            f *= 1 + 0.012 * math.sin(DOIS_PI * vib_f * j / taxa + vib_ph)
            dt = f / taxa
            ph += dt
            if ph >= 1.0:
                ph -= 1.0
            s = 2 * ph - 1
            if ph < dt:
                t = ph / dt
                s -= t + t - t * t - 1
            elif ph > 1 - dt:
                t = (ph - 1) / dt
                s -= t * t + t + t + 1
            env = min(1.0, j / na) * min(1.0, (tot - j) / nr)
            a_lp += 0.35 * (s - a_lp)   # inclinação espectral da glote
            fonte[i0 + j] += amp * env * a_lp
    # Respiração: ruído com a envoltória do coro.
    env_coro = [abs(v) for v in fonte]
    env_coro = filtrar(env_coro, coef("pb", 20, 0.7, taxa))
    r = ruido(rng, n)
    fonte = [f + respiracao * e * g for f, e, g in zip(fonte, env_coro, r)]
    # Formantes variando no tempo, recalculados a cada bloco.
    bloco = 64
    saida = zeros(n)
    for k_form, ganho in ((0, 1.0), (1, 0.55), (2, 0.18)):
        z1 = z2 = 0.0
        for inicio in range(0, n, bloco):
            t = inicio / taxa
            fq = _interp_trajetoria(trajetoria, t, k_form + 1)
            b0, b1, b2, a1, a2 = coef("bp", fq, 6.0 if k_form < 2 else 8.0, taxa)
            for i in range(inicio, min(n, inicio + bloco)):
                v = fonte[i]
                o = b0 * v + z1
                z1 = b1 * v - a1 * o + z2
                z2 = b2 * v - a2 * o
                saida[i] += ganho * o
    return saida


def _interp_trajetoria(traj, t, k):
    if t <= traj[0][0]:
        return traj[0][k]
    for a, b in zip(traj, traj[1:]):
        if t <= b[0]:
            p = (t - a[0]) / (b[0] - a[0])
            return a[k] + (b[k] - a[k]) * p
    return traj[-1][k]


def _para_44k(x, taxa_de):
    fator = TAXA // taxa_de
    y = subir_taxa(x, fator)
    corte = 0.42 * taxa_de
    return filtrar(filtrar(y, coef("pb", corte, 0.54)), coef("pb", corte, 1.31))


def _palma(rng, taxa):
    n = amostras(0.02, taxa)
    x = rajada(rng, n, 0.0003, rng.uniform(0.0015, 0.0035), [coef("bp", rng.uniform(800, 2600), rng.uniform(1.2, 2.5), taxa)], taxa)
    if rng.random() < 0.5:   # mão em concha: ressonância mais grave
        somar(x, rajada(rng, n, 0.0003, 0.003, [coef("bp", rng.uniform(450, 900), 3.0, taxa)], taxa), 0, 0.6)
    return normalizar_para(x)


def aplauso(rng, pessoas, duracao):
    """Aplauso curto: cada pessoa bate palmas no próprio ritmo (3,5–5,5 Hz), entra desencontrada e vai
    parando; o grupo ainda solta um 'ê!' baixinho por baixo. Com o eco leve do clube coberto."""
    taxa = TAXA_PUBLICO
    n = amostras(duracao + 0.3, taxa)
    x = zeros(n)
    palmas = [_palma(rng, taxa) for _ in range(40)]
    for _ in range(pessoas):
        ritmo = rng.uniform(3.5, 5.5)
        t = max(0.0, rng.gauss(0.08, 0.07))
        fim = t + rng.uniform(0.55, 1.0) * duracao
        forca = rng.uniform(0.3, 1.0)
        while t < fim:
            resta = (fim - t) / max(fim, 1e-3)
            somar(x, rng.choice(palmas), amostras(t, taxa), forca * (0.35 + 0.65 * min(1.0, resta * 2.5)) * rng.uniform(0.7, 1.0))
            t += rng.gauss(1 / ritmo, 0.08 / ritmo)
    vozes = []
    for _ in range(8):
        masc = rng.random() < 0.6
        f0 = rng.uniform(150, 200) if masc else rng.uniform(260, 330)   # grito: mais agudo que a fala
        vozes.append((f0, f0 * rng.uniform(0.85, 0.95), max(0.0, rng.gauss(0.1, 0.06)), rng.uniform(0.35, 0.7), rng.uniform(0.4, 1.0)))
    grito = _vozes(rng, n, taxa, vozes, [(0.0,) + VOGAIS["é"], (0.4,) + VOGAIS["é"], (0.8,) + VOGAIS["e"]])
    somar(x, normalizar_para(grito, rng.uniform(0.12, 0.2)))
    x = normalizar_para(x)
    eco = reverb(x, taxa, rt60=1.1, amortecimento=0.45, pre=0.018)
    y = [a + 0.35 * b for a, b in zip(x + zeros(len(eco) - len(x)), eco)]
    return _para_44k(y, taxa)


def uhh(rng, vozes_n, duracao):
    """O 'uhh' do ponto perdido: o público inteiro solta um 'ô→u' que cai de altura e morre."""
    taxa = TAXA_PUBLICO
    n = amostras(duracao + 0.2, taxa)
    vozes = []
    for _ in range(vozes_n):
        masc = rng.random() < 0.65
        f0 = rng.uniform(115, 165) if masc else rng.uniform(210, 290)
        vozes.append((f0, f0 * rng.uniform(0.7, 0.82), max(0.0, rng.gauss(0.07, 0.05)), duracao * rng.uniform(0.65, 0.95),
                      rng.uniform(0.35, 1.0)))
    traj = [(0.0,) + VOGAIS["ó"], (0.25 * duracao,) + VOGAIS["o"], (0.8 * duracao,) + VOGAIS["u"]]
    x = _vozes(rng, n, taxa, vozes, traj, respiracao=0.25)
    x = normalizar_para(x)
    eco = reverb(x, taxa, rt60=1.1, amortecimento=0.45, pre=0.018)
    y = [a + 0.4 * b for a, b in zip(x + zeros(len(eco) - len(x)), eco)]
    return _para_44k(y, taxa)


# ─────────────────────────────── bip do placar ───────────────────────────────

def bip(rng, f, dur):
    """Bip de placar LED: um piezo (senoide com um pouco de 3º harmônico), ataque e saída de 3–8 ms."""
    n = amostras(dur)
    na, nr = amostras(0.003), amostras(0.008)
    fase3 = rng.uniform(0, 0.3)
    x = zeros(n)
    for i in range(n):
        env = min(1.0, i / na, (n - i) / nr)
        t = i / TAXA
        x[i] = env * (math.sin(DOIS_PI * f * t) + 0.22 * math.sin(DOIS_PI * 3 * f * t + fase3))
    return x + zeros(amostras(0.02))


# ─────────────────────────────── ambiente de clube (loop de 20 s) ───────────────────────────────

TAXA_AMBIENTE = TAXA // 4
SEGUNDOS_AMBIENTE = 20.0


def _linha_do_tempo_circular(rng, total, gerar_evento):
    """Enche [0, total) com eventos e devolve-os deslocados por um giro aleatório (módulo total)."""
    eventos, t = [], 0.0
    while True:
        ev, dur = gerar_evento(t)
        if t + dur > total:
            break
        eventos.extend(ev)
        t += dur
    giro = rng.uniform(0, total)
    return [((ev[0] + giro) % total,) + tuple(ev[1:]) for ev in eventos]


def _falante(rng, n, taxa, masc, ganho):
    """Uma pessoa conversando longe: frases de sílabas (vogal sorteada, 4–5,5 sílabas/s), pausas, entonação
    que cai no fim da frase. Tudo em tempo circular: a fonte tem um número inteiro de ciclos no período."""
    # Controle por blocos de ~3 ms. O bloco tem que dividir o período: com resto, as últimas amostras nunca eram
    # escritas e ficava um buraco de fala bem na emenda (220.500 = 20 s a 11.025 Hz; 35 divide).
    blocos = next((b for b in range(32, 65) if n % b == 0), 0)
    if blocos == 0:
        raise ValueError(f"nenhum bloco de 32 a 64 amostras divide o período de {n} amostras")
    nb = n // blocos
    total = n / taxa
    f_base = rng.uniform(95, 130) if masc else rng.uniform(175, 235)
    amp_b = zeros(nb)
    f0_b = [f_base] * nb
    f1_b = zeros(nb)
    f2_b = zeros(nb)
    ruido_b = zeros(nb)
    vogais = list(VOGAIS.values())
    escala_formante = 1.0 if masc else 1.15

    def frase(t):
        dur = rng.uniform(1.0, 3.2)
        pausa = rng.uniform(0.35, 2.2)
        silabas = []
        ts = t
        taxa_sil = rng.uniform(4.0, 5.5)
        while ts < t + dur:
            d = rng.uniform(0.7, 1.3) / taxa_sil
            prog = (ts - t) / dur
            silabas.append((ts, d, rng.choice(vogais), 1.12 - 0.25 * prog + rng.uniform(-0.07, 0.07), rng.uniform(0.5, 1.0)))
            ts += d
        return silabas, dur + pausa

    for (t, d, (fa, fb, _), alt, amp) in _linha_do_tempo_circular(rng, total, frase):
        b0, b1 = int(t * taxa / blocos), int((t + d) * taxa / blocos)
        tam = max(1, b1 - b0)
        for k in range(tam):
            j = (b0 + k) % nb
            e = math.sin(math.pi * (k + 0.5) / tam) ** 0.7 * amp
            if e > amp_b[j]:
                amp_b[j] = e
                f0_b[j] = f_base * alt
                f1_b[j] = fa * escala_formante
                f2_b[j] = fb * escala_formante
            if k < max(1, tam // 5):   # consoante: um chiado curto no começo da sílaba
                ruido_b[j] = max(ruido_b[j], 0.3 * amp * rng.random())
    # Formantes onde não há sílaba: mantém o último valor (o filtro não salta quando a voz volta).
    ultimo = (500.0, 1500.0)
    for j in list(range(nb)) + list(range(nb)):
        if f1_b[j] > 0:
            ultimo = (f1_b[j], f2_b[j])
        else:
            f1_b[j], f2_b[j] = ultimo
    # Suaviza a altura entre blocos e acerta o total de ciclos pra um inteiro (fonte periódica).
    f0_s = filtrar_circular(f0_b, coef("pb", 8, 0.7, taxa / blocos), nb // 4)
    ciclos = sum(f0_s) * blocos / taxa
    ajuste = round(ciclos) / ciclos
    f0_s = [f * ajuste for f in f0_s]
    amp_s = filtrar_circular(amp_b, coef("pb", 25, 0.7, taxa / blocos), nb // 4)
    # Fonte: dente de serra (PolyBLEP) + chiado das consoantes, amostra a amostra.
    fonte = zeros(n)
    ph = 0.0
    rnd = rng.gauss
    a_lp = 0.0
    for j in range(nb):
        f = f0_s[j]
        dt = f / taxa
        a0, a1 = amp_s[j], amp_s[(j + 1) % nb]
        rz = ruido_b[j]
        for k in range(blocos):
            ph += dt
            if ph >= 1.0:
                ph -= 1.0
            s = 2 * ph - 1
            if ph < dt:
                t = ph / dt
                s -= t + t - t * t - 1
            elif ph > 1 - dt:
                t = (ph - 1) / dt
                s -= t * t + t + t + 1
            a_lp += 0.35 * (s - a_lp)
            fonte[j * blocos + k] = (a0 + (a1 - a0) * k / blocos) * a_lp + rz * rnd(0, 0.5)
    # Formantes variando por bloco, filtragem circular (aquecida com o fim do período).
    saida = zeros(n)
    aquec = 64
    for fb_list, g_form, q in ((f1_b, 1.0, 5.0), (f2_b, 0.5, 6.0)):
        z1 = z2 = 0.0
        for jj in range(nb - aquec, nb + nb):
            j = jj % nb
            b0, b1, b2, a1, a2 = coef("bp", fb_list[j], q, taxa)
            base = j * blocos
            grava = jj >= nb
            for i in range(base, base + blocos):
                v = fonte[i]
                o = b0 * v + z1
                z1 = b1 * v - a1 * o + z2
                z2 = b2 * v - a2 * o
                if grava:
                    saida[i] += g_form * o
    return escalar(saida, ganho)


def _toc_distante(rng, taxa, tipo):
    n = amostras(0.25, taxa)
    x = zeros(n)
    if tipo == "raquete":
        somar(x, modo(n, rng.uniform(700, 880), rng.uniform(0.015, 0.025), 1.0, 0.0006, taxa=taxa))
        somar(x, modo(n, rng.uniform(190, 240), 0.03, 0.5, 0.001, taxa=taxa))
        somar(x, modo(n, rng.uniform(1100, 1250), 0.008, 0.35, 0.0005, taxa=taxa))
    elif tipo == "vidro":
        somar(x, modo(n, rng.uniform(85, 110), 0.05, 1.0, 0.002, taxa=taxa))
        for _ in range(8):
            somar(x, modo(n, rng.uniform(120, 900), rng.uniform(0.05, 0.12), rng.uniform(0.1, 0.35), 0.0006,
                          fase=rng.uniform(0, 6.28), taxa=taxa))
    else:  # quique
        somar(x, modo(n, rng.uniform(130, 160), 0.018, 0.8, 0.0015, taxa=taxa))
        somar(x, modo(n, rng.uniform(1000, 1100), 0.006, 0.3, 0.0005, taxa=taxa))
    return x


def ambiente(rng):
    """Clube coberto num fim de tarde: gente conversando longe (murmúrio), duas quadras vizinhas jogando,
    o ar do galpão e um eco leve. Construído como sinal periódico de 20 s — emenda sem salto."""
    taxa = TAXA_AMBIENTE
    n = amostras(SEGUNDOS_AMBIENTE, taxa)
    total = SEGUNDOS_AMBIENTE

    # 1. Falantes (a mistura de vozes que não se entende).
    fala = zeros(n)
    for k in range(7):
        g = rng.uniform(0.45, 1.0)
        somar(fala, _falante(rng, n, taxa, masc=(k % 3 != 2), ganho=g))
    fala = normalizar_para(fala)

    # 2. Burburinho difuso: muita gente longe vira ruído na faixa da fala, que sobe e desce devagar
    #    (senoides com número inteiro de ciclos em 20 s — periódico).
    base = ruido(rng, n)
    difuso = filtrar_circular(base, coef("bp", 480, 0.6, taxa), 4000)
    difuso2 = filtrar_circular(base, coef("bp", 1400, 0.9, taxa), 4000)
    fases = [rng.uniform(0, 6.28) for _ in range(4)]
    difuso = [(a + 0.5 * b) * (1 + 0.18 * math.sin(DOIS_PI * 1 * i / n + fases[0]) + 0.1 * math.sin(DOIS_PI * 3 * i / n + fases[1])
                              + 0.06 * math.sin(DOIS_PI * 7 * i / n + fases[2]))
              for i, (a, b) in enumerate(zip(difuso, difuso2))]
    difuso = normalizar_para(difuso)

    # 3. Quadras vizinhas: ralis de 4–12 golpes (~1,3 s entre golpes), com quique e às vezes vidro.
    quadras = zeros(n)
    for distancia in (0.55, 0.3):
        def rali(t, _d=distancia):
            evs = []
            golpes = rng.randint(4, 12)
            tt = t
            for g_i in range(golpes):
                evs.append((tt, "raquete", _d * rng.uniform(0.6, 1.0)))
                if rng.random() < 0.8:
                    evs.append((tt + rng.uniform(0.55, 0.8), "quique", _d * rng.uniform(0.3, 0.5)))
                if rng.random() < 0.18:
                    evs.append((tt + rng.uniform(0.85, 1.05), "vidro", _d * rng.uniform(0.5, 0.8)))
                tt += rng.uniform(1.1, 1.55)
            return evs, (tt - t) + rng.uniform(3.0, 7.0)
        for (t, tipo, amp) in _linha_do_tempo_circular(rng, total, rali):
            somar(quadras, _toc_distante(rng, taxa, tipo), amostras(t, taxa), amp, circular=True)
    quadras = filtrar_circular(quadras, coef("pb", 2400, 0.7, taxa), 2000)

    # 4. Ar do galpão: ruído grave, quase marrom.
    ar = filtrar_circular(ruido(rng, n), coef("pb", 180, 0.7, taxa), 8000)
    ar = normalizar_para(ar)

    seco = [0.55 * f + 0.28 * d + 0.9 * q + 0.1 * a for f, d, q, a in zip(fala, difuso, quadras, ar)]
    molhado = reverb([0.8 * f + 0.4 * d + 1.2 * q for f, d, q in zip(fala, difuso, quadras)], taxa,
                     rt60=1.4, amortecimento=0.5, pre=0.025, tamanho=1.2, circular=True)
    mix = [s + 0.5 * m for s, m in zip(seco, molhado)]

    # Sobe pra 44,1 kHz (interpolação circular + passa-baixa aquecido) e soma o "ar" agudo do galpão.
    y = subir_taxa(mix, TAXA // taxa, circular=True)
    y = filtrar_circular(y, coef("pb", 4600, 0.54), 20000)
    y = filtrar_circular(y, coef("pb", 4600, 1.31), 20000)
    agudo = filtrar_circular(ruido(rng, len(y)), coef("pa", 5000, 0.7), 2000)
    p_mix = pico(y)
    y = [a + 0.004 * p_mix * b for a, b in zip(y, agudo)]
    y = filtrar_circular(y, coef("pa", 28.0, 0.7071), TAXA * 2)
    return y


# ─────────────────────────────── gravação ───────────────────────────────

def para_int16(x):
    alvo = 10 ** (PICO_DBFS / 20) * 32767
    p = pico(x)
    if p <= 0:
        raise ValueError("sinal mudo")
    k = alvo / p
    return array.array("h", (max(-32767, min(32767, int(round(v * k)))) for v in x))


def gravar_wav(caminho, x, loop=False):
    dados = para_int16(x)
    if sys.byteorder == "big":
        dados.byteswap()
    with wave.open(caminho, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(TAXA)
        w.writeframes(dados.tobytes())
    if loop:
        # Bloco 'smpl' (RIFF): um loop pra frente de 0 até a última amostra (inclusiva), infinito.
        # O importador de WAV do Godot lê este bloco e liga o loop quando o modo é "detectar do WAV".
        periodo_ns = int(round(1e9 / TAXA))
        smpl = struct.pack("<9I", 0, 0, periodo_ns, 60, 0, 0, 0, 1, 0)
        smpl += struct.pack("<6I", 0, 0, 0, len(dados) - 1, 0, 0)
        with open(caminho, "r+b") as f:
            f.seek(0, 2)
            f.write(b"smpl" + struct.pack("<I", len(smpl)) + smpl)
            tamanho = f.tell()
            f.seek(4)
            f.write(struct.pack("<I", tamanho - 8))
    return len(dados)


def rng_de(nome):
    return random.Random(f"{SEMENTE}/{nome}")


def catalogo():
    """(arquivo sem extensão, função que sintetiza, é loop?) — a lista inteira de sons."""
    itens = []
    for tipo in GOLPES:
        for i in range(1, 5):
            itens.append((f"golpe_{tipo}_{i}", lambda r, t=tipo: golpe(t, r), False))
    for i in range(1, 6):
        itens.append((f"quique_{i}", quique, False))
    for i in range(1, 6):
        itens.append((f"vidro_{i}", vidro, False))
    for i in range(1, 5):
        itens.append((f"grade_{i}", grade, False))
    for i in range(1, 4):
        itens.append((f"rede_{i}", lambda r, p=(i == 3): rede(r, com_poste=p), False))
    for i, estilo in enumerate(("calcanhar_ponta", "curto", "arrasto", "calcanhar_ponta", "apoio"), 1):
        itens.append((f"passo_{i}", lambda r, e=estilo: passo(r, e), False))
    for i, (pessoas, dur) in enumerate(((30, 2.2), (45, 2.8), (22, 1.8)), 1):
        itens.append((f"publico_aplauso_{i}", lambda r, p=pessoas, d=dur: aplauso(r, p, d), False))
    for i, (vozes_n, dur) in enumerate(((16, 1.4), (22, 1.7), (12, 1.2)), 1):
        itens.append((f"publico_uhh_{i}", lambda r, v=vozes_n, d=dur: uhh(r, v, d), False))
    for i, (f, dur) in enumerate(((1975.0, 0.110), (2000.0, 0.120), (2030.0, 0.100)), 1):
        itens.append((f"bip_placar_{i}", lambda r, f=f, d=dur: bip(r, f, d), False))
    itens.append(("ambiente_clube", ambiente, True))
    return itens


def gerar_tudo(pasta=PASTA_PADRAO, silencioso=False, so=None):
    os.makedirs(pasta, exist_ok=True)
    gerados = []
    for nome, sintetizar, loop in catalogo():
        if so and not nome.startswith(so):
            continue
        inicio = time.time()
        x = sintetizar(rng_de(nome))
        if not loop:
            x = aparar(passa_alta_dc(x))
        caminho = os.path.join(pasta, nome + ".wav")
        quadros = gravar_wav(caminho, x, loop=loop)
        gerados.append(caminho)
        if not silencioso:
            print(f"{nome + '.wav':28s} {quadros / TAXA:6.3f} s   {time.time() - inicio:5.1f} s de síntese")
    return gerados


if __name__ == "__main__":
    args = sys.argv[1:]
    pasta = PASTA_PADRAO
    so = None
    if "--pasta" in args:
        pasta = args[args.index("--pasta") + 1]
    if "--so" in args:
        so = args[args.index("--so") + 1]
    t0 = time.time()
    arquivos = gerar_tudo(pasta, so=so)
    print(f"{len(arquivos)} arquivos em {pasta} ({time.time() - t0:.1f} s)")
