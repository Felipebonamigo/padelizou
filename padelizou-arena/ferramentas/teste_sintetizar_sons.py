#!/usr/bin/env python3
"""Confere os WAVs que o sintetizar_sons.py gera em Padel.Godot/audio/.

A tabela ESPERADO abaixo é a especificação (nomes, quantas variações, faixa de duração) e é
escrita aqui de propósito, sem importar do gerador: se o gerador mudar um som, o teste diz.
Confere em cada arquivo: taxa 44,1 kHz, 16 bits, mono, pico em -3 dBFS, sem clipping, sem DC,
começo e fim em silêncio (sem clique ao disparar) e variações que de fato diferem entre si.
No ambiente: 20 s exatos, marca de loop no próprio WAV e emenda do fim no começo sem salto.
Por fim regenera tudo numa pasta temporária e compara com o que está no repositório — o WAV
commitado tem que ser o que o script produz.

Rodar:  python3 padelizou-arena/ferramentas/teste_sintetizar_sons.py
"""
import array
import importlib.util
import math
import os
import struct
import sys
import tempfile
import unittest
import wave

# O teste importa o gerador; sem isto sobraria um __pycache__/ em ferramentas/ a cada rodada.
sys.dont_write_bytecode = True

AQUI = os.path.dirname(os.path.abspath(__file__))
PASTA_AUDIO = os.path.normpath(os.path.join(AQUI, "..", "Padel.Godot", "audio"))

TAXA = 44100
PICO_DBFS = -3.0
TOLERANCIA_PICO_DB = 0.05
DC_MAXIMO = 0.001          # |média| em fração do fundo de escala (-60 dBFS)
SEGUNDOS_DO_AMBIENTE = 20.0

# nome-base -> (variações, duração mínima em s, duração máxima em s)
ESPERADO = {
    "golpe_drive":     (4, 0.08, 0.40),
    "golpe_voleio":    (4, 0.06, 0.35),
    "golpe_smash":     (4, 0.10, 0.50),
    "golpe_bandeja":   (4, 0.08, 0.45),
    "golpe_lob":       (4, 0.08, 0.40),
    "quique":          (5, 0.08, 0.40),
    "vidro":           (5, 0.25, 0.90),
    "grade":           (4, 0.35, 1.20),
    "rede":            (3, 0.20, 0.80),
    "passo":           (5, 0.10, 0.45),
    "publico_aplauso": (3, 1.50, 4.50),
    "publico_uhh":     (3, 0.90, 3.00),
    "bip_placar":      (3, 0.06, 0.40),
}
AMBIENTE = "ambiente_clube"


def ler_wav(caminho):
    """Devolve (parâmetros, amostras int16, blocos RIFF) de um WAV PCM."""
    with wave.open(caminho, "rb") as w:
        params = w.getparams()
        bruto = w.readframes(params.nframes)
    amostras = array.array("h")
    amostras.frombytes(bruto)
    if sys.byteorder == "big":
        amostras.byteswap()
    return params, amostras, blocos_riff(caminho)


def blocos_riff(caminho):
    """Lista os blocos (id, bytes) do RIFF — o wave da biblioteca padrão não expõe o 'smpl'."""
    with open(caminho, "rb") as f:
        dados = f.read()
    if dados[:4] != b"RIFF" or dados[8:12] != b"WAVE":
        raise AssertionError(f"{caminho}: não é RIFF/WAVE")
    tamanho_riff = struct.unpack_from("<I", dados, 4)[0]
    if tamanho_riff != len(dados) - 8:
        raise AssertionError(f"{caminho}: tamanho do RIFF {tamanho_riff} != arquivo {len(dados) - 8}")
    blocos, i = {}, 12
    while i + 8 <= len(dados):
        ident, tam = dados[i:i + 4].decode("ascii"), struct.unpack_from("<I", dados, i + 4)[0]
        blocos[ident] = dados[i + 8:i + 8 + tam]
        i += 8 + tam + (tam & 1)
    return blocos


def dbfs(valor):
    return 20 * math.log10(max(valor, 1e-12) / 32767)


def rms(xs):
    return math.sqrt(sum(v * v for v in xs) / max(len(xs), 1))


def arquivos_de(nome, n):
    return [os.path.join(PASTA_AUDIO, f"{nome}_{i}.wav") for i in range(1, n + 1)]


class TesteDosArquivos(unittest.TestCase):
    """Formato e sinal de cada WAV curto."""

    def test_existem_todas_as_variacoes_e_nada_sobra(self):
        esperados = {f"{AMBIENTE}.wav"}
        for nome, (n, _, _) in ESPERADO.items():
            self.assertTrue(3 <= n <= 5, f"{nome}: 3 a 5 variações")
            esperados.update(os.path.basename(c) for c in arquivos_de(nome, n))
        self.assertTrue(os.path.isdir(PASTA_AUDIO), f"falta a pasta {PASTA_AUDIO} — rode sintetizar_sons.py")
        presentes = {a for a in os.listdir(PASTA_AUDIO) if a.endswith(".wav")}
        self.assertEqual(sorted(esperados - presentes), [], "faltam arquivos")
        self.assertEqual(sorted(presentes - esperados), [], "sobram arquivos (som velho que ninguém gera?)")

    def test_formato_duracao_pico_clipping_dc_e_silencio_nas_pontas(self):
        for nome, (n, dmin, dmax) in ESPERADO.items():
            for caminho in arquivos_de(nome, n):
                with self.subTest(arquivo=os.path.basename(caminho)):
                    p, x, _ = ler_wav(caminho)
                    self.conferir_formato(p)
                    duracao = p.nframes / p.framerate
                    self.assertGreaterEqual(duracao, dmin, "curto demais")
                    self.assertLessEqual(duracao, dmax, "longo demais")
                    self.conferir_sinal(x)
                    # Começa e termina em silêncio: sem clique ao disparar nem ao cortar.
                    self.assertLessEqual(abs(x[0]), 0.01 * 32767, "primeira amostra alta: clique no disparo")
                    self.assertLessEqual(max(abs(v) for v in x[-int(0.005 * TAXA):]), 0.001 * 32767,
                                         "os últimos 5 ms não estão em silêncio: corte audível")

    def test_variacoes_diferem_entre_si(self):
        for nome, (n, _, _) in ESPERADO.items():
            sinais = [ler_wav(c)[1] for c in arquivos_de(nome, n)]
            for i in range(n):
                for j in range(i + 1, n):
                    with self.subTest(som=nome, par=(i + 1, j + 1)):
                        self.assertLess(correlacao_maxima(sinais[i], sinais[j]), 0.98,
                                        "duas variações praticamente iguais: a repetição mecânica volta")

    def conferir_formato(self, p):
        self.assertEqual(p.nchannels, 1, "mono")
        self.assertEqual(p.sampwidth, 2, "16 bits")
        self.assertEqual(p.framerate, TAXA, "44,1 kHz")
        self.assertEqual(p.comptype, "NONE", "PCM sem compressão")

    def conferir_sinal(self, x):
        pico = max(abs(min(x)), abs(max(x)))
        self.assertAlmostEqual(dbfs(pico), PICO_DBFS, delta=TOLERANCIA_PICO_DB, msg="pico fora de -3 dBFS")
        self.assertLess(pico, 32767, "encostou no fundo de escala")
        # Clipping aparece como platô: várias amostras seguidas cravadas no valor máximo.
        self.assertLess(maior_plato_no_pico(x, pico), 4, "platô no pico: clipping")
        media = sum(x) / len(x) / 32767
        self.assertLess(abs(media), DC_MAXIMO, f"DC de {media:+.5f} do fundo de escala")


class TesteDoAmbiente(unittest.TestCase):
    """O loop de 20 s: formato, marca de loop no WAV e emenda sem salto."""

    @classmethod
    def setUpClass(cls):
        cls.params, cls.x, cls.blocos = ler_wav(os.path.join(PASTA_AUDIO, f"{AMBIENTE}.wav"))

    def test_formato_duracao_pico_e_dc(self):
        TesteDosArquivos.conferir_formato(self, self.params)
        self.assertEqual(self.params.nframes, int(SEGUNDOS_DO_AMBIENTE * TAXA), "20 s exatos")
        TesteDosArquivos.conferir_sinal(self, self.x)

    def test_wav_declara_o_loop_inteiro(self):
        # Bloco 'smpl' com um loop pra frente do começo ao fim: o importador do Godot lê e liga o loop sozinho.
        self.assertIn("smpl", self.blocos, "sem bloco smpl: o Godot importa sem loop")
        smpl = self.blocos["smpl"]
        (n_loops,) = struct.unpack_from("<I", smpl, 28)
        self.assertEqual(n_loops, 1)
        _, tipo, inicio, fim, _, vezes = struct.unpack_from("<6I", smpl, 36)
        self.assertEqual(tipo, 0, "loop pra frente")
        self.assertEqual(inicio, 0)
        self.assertEqual(fim, len(self.x) - 1, "o fim do loop é a última amostra (inclusiva, como manda o RIFF)")
        self.assertEqual(vezes, 0, "loop infinito")

    def test_emenda_sem_salto(self):
        # Tocando em loop, depois da última amostra vem a primeira. Um clique é uma amostra cujo salto
        # (1ª diferença) ou curvatura (2ª diferença) sai da faixa que o próprio sinal percorre por dentro.
        # Corte que cai dentro dessa faixa não se ouve como clique — num sinal ruidoso como murmúrio, é o
        # limite do que dá pra distinguir (medido: pega ~88 % dos cortes aleatórios e ~90 % dos filtros
        # aplicados sem aquecer, e os que escapam são indistinguíveis de uma amostra comum).
        x = self.x
        n = len(x)
        d1 = sorted(abs(x[i + 1] - x[i]) for i in range(n - 1))
        d2 = sorted(abs(x[i + 1] - 2 * x[i] + x[i - 1]) for i in range(1, n - 1))
        p999_d1 = d1[int(0.999 * (len(d1) - 1))]
        p999_d2 = d2[int(0.999 * (len(d2) - 1))]
        salto = abs(x[0] - x[-1])
        curvatura = max(abs(x[0] - 2 * x[-1] + x[-2]), abs(x[1] - 2 * x[0] + x[-1]))
        self.assertLessEqual(salto, max(p999_d1, 1), f"salto de {salto} na emenda (p99,9 interno: {p999_d1})")
        self.assertLessEqual(curvatura, max(p999_d2, 1), f"curvatura de {curvatura} na emenda (p99,9 interno: {p999_d2})")

        # E o nível não pula: a diferença de RMS entre os 250 ms antes e depois da emenda não pode ser a maior do loop.
        # (Não um percentil: num loop periódico a emenda é um ponto como outro qualquer, e com p90 um arquivo correto
        # reprovava 1 vez em 10 — aconteceu, com uma frase começando 0,1 s depois da emenda. Um fade no fim dá 10,6 dB,
        # e o maior salto interno do murmúrio fica em ~6,6.)
        q = int(0.25 * TAXA)
        def db(seg):
            return 20 * math.log10(max(rms(seg), 1e-9))
        salto_de_nivel = abs(db(x[-q:]) - db(x[:q]))
        vizinhos = [abs(db(x[i - q:i]) - db(x[i:i + q])) for i in range(q, n - q + 1, q // 10)]
        self.assertLessEqual(salto_de_nivel, max(max(vizinhos), 1.0),
                             f"o nível pula {salto_de_nivel:.1f} dB na emenda — mais que em qualquer ponto de dentro do loop")

    def test_tem_movimento_e_nao_e_silencio(self):
        # Um "ambiente" chapado ou mudo passaria nos testes acima; o murmúrio precisa existir e variar.
        seg = int(1.0 * TAXA)
        niveis = [dbfs(rms(self.x[i:i + seg])) for i in range(0, len(self.x) - seg + 1, seg)]
        self.assertGreater(min(niveis), -45, "algum segundo praticamente mudo")
        self.assertGreater(max(niveis) - min(niveis), 0.5, "nível chapado: não soa como gente")


def carregar_gerador():
    spec = importlib.util.spec_from_file_location("sintetizar_sons", os.path.join(AQUI, "sintetizar_sons.py"))
    modulo = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(modulo)
    return modulo


class TesteDaConstrucaoDoAmbiente(unittest.TestCase):
    """Peças do loop por dentro: o que a emenda do WAV mistura e esconde, aqui aparece sozinho."""

    def test_falante_cobre_o_periodo_inteiro(self):
        # Regressão: o falante andava em blocos de 32 amostras e 220.500 não é múltiplo de 32 — as últimas 20
        # amostras de cada voz nunca eram escritas (zero exato), um buraco de 1,8 ms na fala bem na emenda.
        # Numa pausa a saída dos formantes decai (~1e-40), mas não vira zero exato: zero exato é amostra não escrita.
        g = carregar_gerador()
        import random
        taxa = g.TAXA_AMBIENTE
        n = g.amostras(g.SEGUNDOS_AMBIENTE, taxa)
        for semente in range(3):
            with self.subTest(semente=semente):
                y = g._falante(random.Random(semente), n, taxa, masc=semente != 1, ganho=1.0)
                self.assertEqual(len(y), n)
                buraco = 0
                for v in reversed(y):
                    if v != 0.0:
                        break
                    buraco += 1
                self.assertEqual(buraco, 0, f"{buraco} amostras nunca escritas no fim do período")


class TesteDaReproducao(unittest.TestCase):
    """O que está no repositório é o que o script gera (semente fixa)."""

    def test_regenerar_da_os_mesmos_arquivos(self):
        modulo = carregar_gerador()
        with tempfile.TemporaryDirectory() as tmp:
            modulo.gerar_tudo(tmp, silencioso=True)
            gerados = sorted(a for a in os.listdir(tmp) if a.endswith(".wav"))
            commitados = sorted(a for a in os.listdir(PASTA_AUDIO) if a.endswith(".wav"))
            self.assertEqual(gerados, commitados)
            for nome in gerados:
                with self.subTest(arquivo=nome):
                    _, a, ba = ler_wav(os.path.join(tmp, nome))
                    _, b, bb = ler_wav(os.path.join(PASTA_AUDIO, nome))
                    self.assertEqual(len(a), len(b))
                    # ±2 LSB: math.sin/exp podem diferir no último bit entre plataformas.
                    pior = max((abs(p - q) for p, q in zip(a, b)), default=0)
                    self.assertLessEqual(pior, 2, f"regenerado difere em até {pior} LSB")
                    self.assertEqual({k: v for k, v in ba.items() if k != "data"},
                                     {k: v for k, v in bb.items() if k != "data"}, "blocos RIFF diferentes")


def maior_plato_no_pico(x, pico):
    maior = atual = 0
    for v in x:
        if abs(v) == pico:
            atual += 1
            maior = max(maior, atual)
        else:
            atual = 0
    return maior


def correlacao_maxima(a, b, janela=64):
    """Correlação normalizada máxima entre dois sinais, com deslocamento de até ±janela amostras."""
    n = min(len(a), len(b))
    ea = math.sqrt(sum(v * v for v in a[:n])) or 1
    eb = math.sqrt(sum(v * v for v in b[:n])) or 1
    melhor = 0.0
    for d in range(-janela, janela + 1, 2):
        if d >= 0:
            s = sum(a[i + d] * b[i] for i in range(0, n - d, 3))
        else:
            s = sum(a[i] * b[i - d] for i in range(0, n + d, 3))
        melhor = max(melhor, abs(3 * s) / (ea * eb))
    return melhor


if __name__ == "__main__":
    unittest.main(verbosity=2)
