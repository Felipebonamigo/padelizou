using Padelizou.Models;

namespace Padelizou.Services;

// O GRUPO DO TORNEIO NO WHATSAPP.
//
// Todo torneio de padel tem um, e até aqui ele vivia no boca a boca: o organizador colava o
// convite no recado, ou mandava um por um no particular. Quem se inscrevia depois ficava de
// fora sem ninguém perceber — e é no grupo que sai mudança de horário e chamada de jogo.
// Agora o link é campo do torneio e vira botão na página, sozinho.
//
// ⚠️ O BOTÃO SÓ EXISTE PRA QUEM ESTÁ NO TORNEIO. Convite de grupo do WhatsApp é chave de
// porta: quem tem, entra — e o organizador só descobre o estranho depois de ele já estar lá
// dentro. A página do torneio é PÚBLICA (abre sem login e aparece na listagem), então o
// `href` nem chega a ser escrito no HTML de quem não está inscrito. Esconder com CSS seria
// entregar o link a qualquer um que abrisse o "ver código-fonte".
//
// ⚠️ E o link vira `href` DENTRO da nossa página, o que é a segunda razão de isto ser um
// serviço e não um campo de texto solto: sem conferir o endereço, o organizador poderia
// apontar o botão do torneio pra qualquer lugar — inclusive pra um "javascript:" rodando na
// sessão de quem clicasse.
public static class GrupoDoTorneioNoWhatsApp
{
    // O host de todo convite de grupo (e de comunidade) do WhatsApp: é o que o app gera em
    // "Convidar via link". Comparado por IGUALDADE, nunca por "termina com" — `whatsapp.com`
    // no fim de `chat.whatsapp.com.outracoisa.br` é o disfarce mais barato que existe.
    public const string HostDoConvite = "chat.whatsapp.com";

    // O que o organizador digitou, pronto pra guardar. Não julga se serve — quem responde
    // isso é EhConviteDeGrupo.
    public static string? Normalizar(string? link)
    {
        var texto = link?.Trim();
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Colado do WhatsApp o link vem completo, mas digitado à mão vem "chat.whatsapp.com/XX".
        // Sem esquema ele não é um endereço absoluto, e a conferência abaixo recusaria um
        // convite bom.
        if (!texto.Contains("://", StringComparison.Ordinal))
        {
            texto = "https://" + texto;
        }

        // http vira https: o WhatsApp só atende em https, e um botão nosso apontando pra http
        // seria um salto a mais pela rede antes do redirecionamento.
        if (texto.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            texto = "https://" + texto["http://".Length..];
        }

        return texto;
    }

    // É mesmo um convite de grupo do WhatsApp?
    public static bool EhConviteDeGrupo(string? link)
    {
        var endereco = Normalizar(link);
        if (endereco == null) return false;

        return Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            // `uri.Host` é o host de verdade: em "https://chat.whatsapp.com@outrolugar.com/x"
            // ele devolve `outrolugar.com`, que é justamente pra onde o clique iria.
            && string.Equals(uri.Host, HostDoConvite, StringComparison.OrdinalIgnoreCase)
            // Só o endereço do site não convida pra grupo nenhum: falta o código que diz QUAL
            // grupo. Um botão desses abriria a página institucional do WhatsApp.
            && uri.AbsolutePath.Trim('/').Length > 0;
    }

    // A recusa que a tela mostra, ou null quando está tudo bem. Campo em branco não é
    // problema: torneio sem grupo é o caso normal, e aí simplesmente não há botão.
    public static string? ProblemaCom(string? link)
    {
        if (Normalizar(link) == null) return null;
        if (EhConviteDeGrupo(link)) return null;

        return "O link do grupo precisa ser o convite do WhatsApp — o que começa com "
             + $"https://{HostDoConvite}/. No WhatsApp: abra o grupo, toque no nome dele e "
             + "escolha \"Convidar via link\".";
    }

    // A ÚNICA porta por onde o link chega a uma tela. Duas trancas na mesma linha, de
    // propósito:
    //
    //   1. quem não está no torneio não recebe nada — nem o href;
    //   2. o que está guardado é conferido DE NOVO na hora de mostrar, e não só na hora de
    //      salvar. Linha antiga, importação, correção na mão no banco: nada disso passou pela
    //      validação da tela, e um endereço estranho aqui vira um botão do Padelizou levando
    //      pra fora.
    public static string? LinkPara(Torneio torneio, bool estaNoTorneio)
    {
        if (!estaNoTorneio) return null;

        return EhConviteDeGrupo(torneio.LinkGrupoWhatsApp)
            ? Normalizar(torneio.LinkGrupoWhatsApp)
            : null;
    }

    // Este torneio tem grupo configurado? Pergunta do organizador, não do jogador — por isso
    // não devolve o link.
    public static bool Configurado(Torneio torneio) => EhConviteDeGrupo(torneio.LinkGrupoWhatsApp);
}
