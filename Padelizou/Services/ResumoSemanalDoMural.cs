using Padelizou.Models;

namespace Padelizou.Services;

// O ÚNICO empurrão que os Desafios dão. Espec em DESAFIOS.md, seção 5.
//
// ⚠️ POR QUE ELE EXISTE, E POR QUE É UM SÓ: o mural é PULL — a pessoa entra e olha. Isso
// protege o número do WhatsApp e a cota de e-mail, mas tem um custo: mural que ninguém abre é
// mural vazio, e dupla que anuncia e não é desafiada não anuncia de novo. O resumo semanal é a
// única coisa que quebra esse empate, e por isso ele é UM aviso, por semana, para uma lista
// estreita.
//
// ⚠️ O QUE ELE NÃO É: "nova dupla aberta na sua cidade". Aviso por anúncio publicado é
// broadcast — exatamente o que a Meta chama de spam e o que restringiu o número em 04/08/2026.
// A diferença não é o volume: é que ali o gatilho é o que OUTRA pessoa fez.
public static class ResumoSemanalDoMural
{
    // Quinta de manhã: perto o bastante do fim de semana pra a pessoa conseguir marcar, e longe
    // o bastante pra a outra dupla ter tempo de responder as 48h antes do sábado.
    public const DayOfWeek DiaDoEnvio = DayOfWeek.Thursday;
    public const int HoraDoEnvio = 9;

    // A chave que guarda o último envio, pra ele sobreviver a restart e a deploy. Sem isso, um
    // deploy numa quinta às 10h faria a base inteira receber o mesmo aviso duas vezes — e aviso
    // repetido é o que faz a pessoa desligar a notificação (e perder junto o que importa).
    public const string ChaveDoUltimoEnvio = "Desafios.ResumoSemanal.UltimoEnvio";

    // Abaixo disso o resumo não sai. "1 dupla aberta" é um convite fraco que gasta o único
    // empurrão da semana; e zero seria um aviso que só ensina a ignorar os próximos.
    public const int MinimoDeDuplas = 2;

    public static bool EhHoraDeEnviar(DateTime agora, DateTime? ultimoEnvio) =>
        agora.DayOfWeek == DiaDoEnvio
        && agora.Hour >= HoraDoEnvio
        && ultimoEnvio?.Date != agora.Date;

    // Quantas duplas ESTA pessoa poderia desafiar hoje.
    //
    // Conta o que ela veria no mural, tirando os anúncios dela mesma. A cidade entra quando as
    // duas pontas a têm: anúncio sem cidade aceita qualquer lugar, e pessoa sem cidade no perfil
    // não tem por onde ser filtrada — nos dois casos o anúncio conta.
    //
    // ⚠️ A comparação passa por `NomeDeCidade.Chave`, e não por igualdade de texto: "Gravataí",
    // "GRAVATAI" e "Gravatai" são a mesma cidade, e comparar na lata faria o resumo dizer
    // "nenhuma dupla" pra quem digitou o nome com acento diferente do catálogo.
    public static int QuantasDuplasPara(
        IEnumerable<AnuncioDeDesafio> noMural, int jogadorId, string? cidadeDoJogador)
    {
        var chaveDaMinhaCidade = NomeDeCidade.Chave(cidadeDoJogador);

        return noMural.Count(a =>
            a.Jogador1Id != jogadorId
            && a.Jogador2Id != jogadorId
            && AceitaMinhaCidade(a, chaveDaMinhaCidade));
    }

    private static bool AceitaMinhaCidade(AnuncioDeDesafio anuncio, string chaveDaMinhaCidade)
    {
        if (anuncio.Cidades.Count == 0) return true;
        if (chaveDaMinhaCidade.Length == 0) return true;

        return anuncio.Cidades.Any(c => NomeDeCidade.Chave(c.Cidade?.Nome) == chaveDaMinhaCidade);
    }

    // Vale mandar pra esta pessoa?
    //
    // ⚠️ A régua é estreita de propósito, e cada linha tira alguém de uma lista de e-mail:
    //
    //  · JÁ USOU os Desafios. É retenção, não divulgação — mandar pra quem nunca entrou é o
    //    broadcast que a seção 5 proíbe, só que uma vez por semana.
    //  · NÃO está no mural agora. Quem já anunciou não precisa ser convidado a anunciar; pra
    //    ela o aviso é ruído, e ruído é o que ensina a ignorar o remetente.
    //  · Tem o interruptor de aviso de jogo ligado, não saiu (LGPD) e assumiu a conta —
    //    pré-cadastro nunca viu tela nenhuma e não tem login pra abrir o mural.
    public static bool VaiReceber(Jogador jogador, bool jaUsouDesafios, bool temAnuncioNoMural,
        int duplasDisponiveis) =>
        jaUsouDesafios
        && !temAnuncioNoMural
        && duplasDisponiveis >= MinimoDeDuplas
        && jogador.NotificarAvisoJogo
        && !jogador.Excluido
        && !jogador.EhPreCadastro;
}
