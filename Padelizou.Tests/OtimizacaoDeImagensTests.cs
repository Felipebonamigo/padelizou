using Microsoft.AspNetCore.Hosting;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;

namespace Padelizou.Tests;

// Conserta o que ficou pra trás: as imagens que já estavam no disco antes de todo upload passar
// pelo ImagemEnviada. O caso que motivou isso foi uma capa de torneio de 8 MB em produção.
public class OtimizacaoDeImagensTests : IDisposable
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), "pdz-otim-" + Guid.NewGuid().ToString("N"));
    private readonly DbPadelContext _ctx = TestInfra.NovoContexto();

    public void Dispose()
    {
        _ctx.Dispose();
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }

    private OtimizacaoDeImagens Servico()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.WebRootPath.Returns(_webRoot);
        return new OtimizacaoDeImagens(_ctx, env);
    }

    [Fact]
    public async Task Capa_pesada_que_ja_estava_no_disco_e_refeita_e_o_banco_passa_a_apontar_pra_nova()
    {
        var caminhoAntigo = GravarImagem("capas-torneio", "capona.png", 2000, 1500);
        var tamanhoAntigo = new FileInfo(CaminhoFisico(caminhoAntigo)).Length;

        var torneio = new Torneio { Nome = "Copa Teste", Codigo = "OTIM01", ImagemCapa = caminhoAntigo };
        _ctx.Torneios.Add(torneio);
        await _ctx.SaveChangesAsync();

        var r = await Servico().RodarAsync();

        Assert.Equal(1, r.Otimizadas);
        Assert.Equal(0, r.ComProblema);

        // O banco aponta pra imagem nova...
        Assert.NotEqual(caminhoAntigo, torneio.ImagemCapa);
        Assert.EndsWith(".webp", torneio.ImagemCapa);
        Assert.True(File.Exists(CaminhoFisico(torneio.ImagemCapa!)));

        // ...que é bem mais leve, e cabe no teto de largura da capa.
        var nova = new FileInfo(CaminhoFisico(torneio.ImagemCapa!)).Length;
        Assert.True(nova < tamanhoAntigo / 2, $"esperava bem menos que a metade; antes {tamanhoAntigo}, depois {nova}");
        using (var codec = SKCodec.Create(CaminhoFisico(torneio.ImagemCapa!)))
        {
            Assert.Equal(1600, codec.Info.Width);
        }

        // E o arquivo velho não fica ocupando espaço.
        Assert.False(File.Exists(CaminhoFisico(caminhoAntigo)));
        Assert.True(r.BytesEconomizados > 0);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_mexe_no_que_ja_esta_certo()
    {
        var torneio = new Torneio { Nome = "Copa Teste", Codigo = "OTIM02", ImagemCapa = GravarImagem("capas-torneio", "capa.png", 2000, 1500) };
        _ctx.Torneios.Add(torneio);
        await _ctx.SaveChangesAsync();

        await Servico().RodarAsync();
        var depoisDaPrimeira = torneio.ImagemCapa;
        var bytesDepoisDaPrimeira = new FileInfo(CaminhoFisico(depoisDaPrimeira!)).Length;

        // Duas passadas a mais. Sem a checagem de "já está no padrão", cada uma recomprimiria a
        // imagem de novo — o arquivo iria encolhendo e a foto iria borrando a cada rodada.
        var segunda = await Servico().RodarAsync();
        await Servico().RodarAsync();

        Assert.Equal(0, segunda.Otimizadas);
        Assert.Equal(1, segunda.Puladas);
        Assert.Equal(depoisDaPrimeira, torneio.ImagemCapa);
        Assert.True(File.Exists(CaminhoFisico(torneio.ImagemCapa!)));
        Assert.Equal(bytesDepoisDaPrimeira, new FileInfo(CaminhoFisico(torneio.ImagemCapa!)).Length);
    }

    [Fact]
    public async Task Pega_foto_de_perfil_e_logo_de_time_tambem()
    {
        _ctx.Jogadores.Add(new Jogador
        {
            Nome = "Felipe", Cpf = "11111111111",
            FotoPerfil = GravarImagem("fotos-perfil", "selfie.png", 1800, 1800)
        });
        _ctx.Times.Add(new Time { Nome = "Chakra", Logo = GravarImagem("logos-time", "escudo.png", 1200, 1200) });
        await _ctx.SaveChangesAsync();

        var r = await Servico().RodarAsync();

        Assert.Equal(2, r.Otimizadas);

        // Cada um respeita o teto do seu tipo — os dois são 512.
        foreach (var caminho in new[] { _ctx.Jogadores.Single().FotoPerfil, _ctx.Times.Single().Logo })
        {
            using var codec = SKCodec.Create(CaminhoFisico(caminho!));
            Assert.Equal(512, codec.Info.Width);
        }
    }

    [Fact]
    public async Task Caminho_apontando_pra_arquivo_que_nao_existe_e_pulado_sem_estourar()
    {
        // Base antiga tem disso: upload que falhou pela metade, arquivo apagado à mão.
        _ctx.Torneios.Add(new Torneio { Nome = "Fantasma", Codigo = "OTIM03", ImagemCapa = "/uploads/capas-torneio/nao-existe.png" });
        await _ctx.SaveChangesAsync();

        var r = await Servico().RodarAsync();

        Assert.Equal(0, r.Otimizadas);
        Assert.Equal(1, r.Puladas);
        Assert.Equal(0, r.ComProblema);
    }

    [Fact]
    public async Task Caminho_adulterado_no_banco_nao_faz_o_laco_sair_da_pasta_de_uploads()
    {
        // Se uma linha do banco pudesse apontar pra fora de wwwroot/uploads, esse laço viraria
        // uma ferramenta de apagar arquivo do servidor.
        var forasteiro = Path.Combine(_webRoot, "importante.txt");
        Directory.CreateDirectory(_webRoot);
        await File.WriteAllTextAsync(forasteiro, "não me apague");

        _ctx.Torneios.Add(new Torneio { Nome = "Malandro", Codigo = "OTIM04", ImagemCapa = "/uploads/../importante.txt" });
        await _ctx.SaveChangesAsync();

        var r = await Servico().RodarAsync();

        Assert.Equal(0, r.Otimizadas);
        Assert.True(File.Exists(forasteiro));
    }

    // ── Apoio ─────────────────────────────────────────────────────────────────────────

    private string GravarImagem(string subpasta, string nome, int largura, int altura)
    {
        var pasta = Path.Combine(_webRoot, "uploads", subpasta);
        Directory.CreateDirectory(pasta);

        var info = new SKImageInfo(largura, altura, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new byte[info.BytesSize];
        new Random(7).NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;

        using var imagem = SKImage.FromPixelCopy(info, pixels);
        using var dados = imagem.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(pasta, nome), dados.ToArray());

        return "/uploads/" + subpasta + "/" + nome;
    }

    private string CaminhoFisico(string caminhoRelativo) =>
        Path.Combine(_webRoot, caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
}
