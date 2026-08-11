using Padelizou.Models;

namespace Padelizou.Services;

// O link que fecha a dupla do anúncio de desafio.
//
// POR QUE O ANÚNCIO PRECISA DOS DOIS: sem isto eu publico "eu e o Lucas jogamos sábado às 8h
// em qualquer clube de Gravataí", e o Lucas descobre por um push de desafio JÁ ACEITO — com
// hora e lugar que ele nunca combinou. É o mesmo furo que o convite de parceiro fechou na
// inscrição de torneio (lá, dava pra inscrever alguém digitando um CPF qualquer), com um
// agravante: aqui o anúncio é público e diz onde a pessoa vai estar.
//
// Por isso o anúncio nasce `Rascunho`: ele existe, é editável por quem criou, e NÃO aparece no
// mural até o parceiro entrar com a própria conta e aceitar.
//
// O token e a comparação em tempo fixo vêm de ConviteDeParceiro — a criptografia é a mesma e
// não pode ter duas cópias. O que muda é a régua de validade, que é o que este arquivo guarda.
public static class ConviteDoAnuncio
{
    public static string NovoToken() => ConviteDeParceiro.NovoToken();

    // O convite vale enquanto: existe token, o anúncio ainda não tem parceiro, não foi
    // cancelado e a semana anunciada não acabou.
    //
    // Não há prazo próprio de propósito: `ValeAte` JÁ é o prazo, e um prazo menor faria o link
    // morrer com o anúncio ainda de pé — a pessoa leria "convite expirado" olhando pro
    // anúncio vivo de quem a convidou. Aceitar limpa o token, então link usado não fecha uma
    // segunda dupla.
    public static bool Valido(AnuncioDeDesafio? anuncio, string? token, DateTime agora)
    {
        if (anuncio == null) return false;
        if (anuncio.Jogador2Id != null) return false;
        if (anuncio.Status == AnuncioDeDesafio.Cancelado) return false;
        if (SemanaDoDesafio.Vencido(anuncio, agora)) return false;

        return ConviteDeParceiro.TokenConfere(anuncio.ConviteToken, token);
    }

    // Por que o convite não serve mais. Distinguir os motivos importa: "já tem parceiro" e
    // "a semana acabou" levam a ações diferentes (falar com quem convidou × pedir um anúncio
    // novo).
    public static string MotivoDeNaoValer(AnuncioDeDesafio? anuncio, DateTime agora)
    {
        if (anuncio == null) return "Esse convite não existe mais.";
        if (anuncio.Jogador2Id != null) return "Essa dupla já está fechada — alguém aceitou antes.";
        if (anuncio.Status == AnuncioDeDesafio.Cancelado) return "Quem te convidou cancelou o anúncio.";
        if (SemanaDoDesafio.Vencido(anuncio, agora)) return "A semana desse anúncio já passou. Peça um link novo.";
        return "Esse convite não vale mais. Peça um link novo pra quem te chamou.";
    }

    // Quem convida não pode ser o convidado: dupla de um jogador só não existe, e o formulário
    // que permitisse isso encheria o mural de anúncios que ninguém consegue desafiar.
    public static bool PodeAceitar(AnuncioDeDesafio anuncio, int quemAceitaId) =>
        anuncio.Jogador1Id != quemAceitaId;
}
