using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Passa nas imagens que JÁ estão no disco e refaz cada uma pelo padrão novo.
//
// O ImagemEnviada resolve daqui pra frente; isto resolve o que ficou pra trás — inclusive a capa
// de torneio de 8 MB que apareceu na medição de backup e sozinha era 60% de todo o
// armazenamento de produção.
//
// Roda pelo painel do admin, quantas vezes quiser: imagem que já está no tamanho certo é pulada,
// então a segunda passada não faz nada e não custa nada.
public class OtimizacaoDeImagens
{
    private readonly DbPadelContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<OtimizacaoDeImagens>? _logger;

    public OtimizacaoDeImagens(DbPadelContext context, IWebHostEnvironment env,
        ILogger<OtimizacaoDeImagens>? logger = null)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    public record Resultado(int Otimizadas, int Puladas, int ComProblema, long BytesAntes, long BytesDepois)
    {
        public long BytesEconomizados => Math.Max(0, BytesAntes - BytesDepois);

        public static string EmMegas(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
    }

    public async Task<Resultado> RodarAsync()
    {
        int otimizadas = 0, puladas = 0, problemas = 0;
        long antes = 0, depois = 0;

        // Cada tipo de imagem tem o seu teto: capa é banner de largura inteira, foto e logo são
        // vistos pequenos na maior parte do tempo.
        var alvos = new List<(string? Caminho, FormatoDeImagem Formato, Action<string> Atualizar)>();

        foreach (var jogador in await _context.Jogadores.Where(j => j.FotoPerfil != null && j.FotoPerfil != "").ToListAsync())
        {
            alvos.Add((jogador.FotoPerfil, FormatoDeImagem.FotoPerfil, novo => jogador.FotoPerfil = novo));
        }

        foreach (var time in await _context.Times.Where(t => t.Logo != null && t.Logo != "").ToListAsync())
        {
            alvos.Add((time.Logo, FormatoDeImagem.LogoTime, novo => time.Logo = novo));
        }

        foreach (var torneio in await _context.Torneios.Where(t => t.ImagemCapa != null && t.ImagemCapa != "").ToListAsync())
        {
            alvos.Add((torneio.ImagemCapa, FormatoDeImagem.CapaTorneio, novo => torneio.ImagemCapa = novo));
        }

        foreach (var (caminho, formato, atualizar) in alvos)
        {
            var fisico = CaminhoFisico(caminho);

            // Caminho apontando pra arquivo que não existe é comum em base antiga (upload que
            // falhou, arquivo apagado à mão). Não é problema nosso pra resolver aqui.
            if (fisico == null || !File.Exists(fisico))
            {
                puladas++;
                continue;
            }

            try
            {
                var original = await File.ReadAllBytesAsync(fisico);
                if (original.Length > ImagemEnviada.BytesMaximos)
                {
                    problemas++;
                    continue;
                }

                // Imagem que já está no padrão sai daqui intocada. Recodificar de novo não
                // adiantaria nada e ainda tiraria qualidade — WebP com perdas reprocessado
                // perde um pouco a cada passada.
                if (ImagemEnviada.JaEstaNoPadrao(original, formato))
                {
                    puladas++;
                    continue;
                }

                var nova = ImagemEnviada.Recodificar(original, formato, _logger);
                if (nova == null)
                {
                    problemas++;
                    continue;
                }

                // Rede de segurança: se mesmo assim o arquivo cresceria, deixa como está.
                if (nova.Length >= original.Length)
                {
                    puladas++;
                    continue;
                }

                var pasta = Path.GetDirectoryName(fisico)!;
                var nomeNovo = ImagemEnviada.NomeDeArquivo();
                await File.WriteAllBytesAsync(Path.Combine(pasta, nomeNovo), nova);

                var subpasta = Path.GetFileName(pasta);
                atualizar("/uploads/" + subpasta + "/" + nomeNovo);

                antes += original.Length;
                depois += nova.Length;
                otimizadas++;

                // O arquivo antigo só sai depois que o banco já aponta pro novo. Se apagasse
                // antes e o SaveChanges falhasse, a imagem sumiria da tela.
                await _context.SaveChangesAsync();
                TentarApagar(fisico);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Não deu pra otimizar {Caminho}.", caminho);
                problemas++;
            }
        }

        return new Resultado(otimizadas, puladas, problemas, antes, depois);
    }

    // O caminho vem do banco no formato "/uploads/pasta/arquivo". Confere que ele continua
    // dentro de wwwroot/uploads antes de tocar em qualquer coisa — uma linha adulterada no banco
    // não vai fazer esse laço apagar arquivo de sistema.
    private string? CaminhoFisico(string? caminhoRelativo)
    {
        if (string.IsNullOrWhiteSpace(caminhoRelativo)) return null;

        var raizUploads = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
        var completo = Path.GetFullPath(Path.Combine(_env.WebRootPath,
            caminhoRelativo.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar)));

        return completo.StartsWith(raizUploads + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? completo
            : null;
    }

    private void TentarApagar(string caminho)
    {
        try
        {
            File.Delete(caminho);
        }
        catch (Exception ex)
        {
            // Espaço não liberado é chato; erro na tela do admin depois do trabalho já feito é pior.
            _logger?.LogWarning(ex, "Imagem otimizada, mas o arquivo antigo {Caminho} continuou lá.", caminho);
        }
    }
}
