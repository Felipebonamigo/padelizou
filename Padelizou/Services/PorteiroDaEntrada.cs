using Microsoft.Extensions.Options;
using Padelizou.Models;

namespace Padelizou.Services;

// O porteiro da ENTRADA: decide se uma conta pode usar este ambiente.
//
// Irmão do PorteiroDaSaida, e pela mesma razão — o dev passou a rodar com dado de produção.
// Lá o risco era o sistema ESCREVER pra gente de verdade; aqui é gente de verdade ENTRAR e
// tomar um ensaio por um torneio.
//
// ⚠️ Ele NÃO é o portão de acesso antecipado, e não substitui autorização nenhuma. O portão
// pergunta "você tem a senha compartilhada?" e some no dia em que o site abrir pro público;
// `IsAdminGeral`, organizador e dono de clube continuam decidindo o que cada um FAZ. Este
// responde uma pergunta que nenhum dos dois responde: *este ambiente é seu?*
//
// ⚠️ Casa por e-mail, login OU CPF porque a lista é escrita por gente no meio de um deploy,
// com o que estiver à mão. Os três já são identificadores únicos de uma pessoa só — é o que o
// IdentidadeJogador garante, com índice no banco por baixo.
public class PorteiroDaEntrada
{
    private readonly HashSet<string> _permitidas;

    public PorteiroDaEntrada(IOptions<EntradaSettings> settings, IOptions<BetaSettings> beta,
        ILogger<PorteiroDaEntrada> logger)
    {
        _permitidas = (settings.Value.SoEstas ?? [])
            .Select(IdentidadeJogador.Normalizar)
            .Where(identificador => identificador.Length > 0)
            .ToHashSet();

        // No start, uma vez, e em Warning — mesma regra do porteiro da saída: ambiente que
        // recusa gente tem que dizer isso na primeira tela do log, não na hora em que alguém
        // liga perguntando por que não entra.
        if (Restringindo)
        {
            logger.LogWarning(
                "ENTRADA RESTRITA neste ambiente: só as {Quantas} conta(s) de Entrada:SoEstas conseguem entrar. " +
                "Qualquer outra é recusada no login e desconectada na porta.", _permitidas.Count);
        }
        // O gêmeo do aviso do PorteiroDaSaida, e pelo mesmo motivo: config perdida não produz
        // erro, só ausência — e ausência aqui quer dizer que qualquer conta do banco copiado da
        // produção entra no ambiente de teste. Produção roda sem a chave de propósito, então o
        // aviso é só pra quem se declarou cópia.
        else if (beta.Value.AmbienteDeTeste)
        {
            logger.LogWarning(
                "ENTRADA LIBERADA num ambiente de TESTE: `Entrada:SoEstas` está vazia, então qualquer " +
                "conta do banco entra aqui — e este banco é cópia da produção. Confira o drop-in do " +
                "systemd: variável malformada some sem erro nenhum.");
        }
    }

    // Produção roda com a lista vazia, e aqui isso quer dizer "porteiro sem trabalho": o
    // middleware nem chega a olhar o cookie, e o login segue o caminho de sempre.
    public bool Restringindo => _permitidas.Count > 0;

    public bool PodeEntrar(Jogador? jogador)
    {
        if (!Restringindo) return true;

        // Cookie válido de uma conta que não existe mais (apagada, ou banco trocado por baixo)
        // NÃO passa. Com o ambiente restrito, "não sei quem é" se resolve pro lado de fora.
        if (jogador == null) return false;

        return Bate(jogador.Email) || Bate(jogador.Login) || Bate(jogador.Cpf);
    }

    private bool Bate(string? valor)
    {
        var normalizado = IdentidadeJogador.Normalizar(valor);
        return normalizado.Length > 0 && _permitidas.Contains(normalizado);
    }
}
