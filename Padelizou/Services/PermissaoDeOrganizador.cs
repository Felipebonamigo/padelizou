using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode CRIAR torneio, e quando um torneio APARECE.
//
// Nasceu do medo certo do Felipe (07/08/2026): *"tenho medo que qualquer pessoa chegue, crie
// torneio e lote de torneios"*. E o estrago não seria uma lista suja — toda criação de torneio
// dispara push e e-mail pra base inteira, então 20 torneios falsos são milhares de avisos, o
// tipo de coisa que faz gente desinstalar o app.
//
// São DUAS travas, e elas respondem perguntas diferentes:
//   1. PERFIL — quem tem permissão de criar. Dado pelo admin, uma vez, por pessoa.
//   2. APROVAÇÃO — cada torneio, sempre (decisão do Felipe). Nasce invisível e só entra na
//      vitrine depois do OK.
//
// ⚠️ A aprovação NÃO impede de criar nem de trabalhar no torneio. O organizador monta tudo,
// vê a página e compartilha o link com quem quiser desde o primeiro minuto — o que ele não tem
// antes do OK é a VITRINE: a listagem pública, a Home e o aviso pra base. Segurar a criação
// faria o organizador ficar esperando com a quadra reservada; segurar só a vitrine não custa
// nada a ele e resolve inteiro o problema do Felipe.
public static class PermissaoDeOrganizador
{
    // ⚠️ O AMERICANO É LIVRE: qualquer pessoa cadastrada cria (decisão do Felipe, 07/08/2026).
    //
    // Ele é o rodízio de sábado — gente conhecida, parceiro trocando a cada rodada, criado na
    // sexta à noite. Exigir liberação pra isso poria o Felipe no meio do combinado de um grupo
    // de amigos, e mataria justamente a porta de entrada do app.
    //
    // E é seguro exatamente porque a APROVAÇÃO existe: criar não avisa mais ninguém (o "novo
    // torneio aberto" saiu da criação), então um Americano inventado não alcança a base. Ele
    // fica no link de quem criou até alguém aprovar. Sem a trava da vitrine, esta liberdade
    // seria imprudente; com ela, é só conveniência.
    public static bool PodeCriarTorneio(Jogador? jogador, string? formato)
    {
        if (jogador == null) return false;
        if (FormatoDoTorneio.EhAmericano(formato)) return true;

        // O torneio Oficial é o que publica chave, cobra inscrição e vale ranking — nele a
        // credibilidade do organizador importa, e por isso a porta é estreita.
        //
        // O admin do Padelizou passa em qualquer caso: é ele quem socorre organizador travado
        // no dia do jogo, e depender do perfil que ele mesmo distribui seria um nó que só se
        // desata pelo banco.
        return jogador.IsOrganizadorTorneio || jogador.IsAdminRaiz || jogador.IsAdminGeral;
    }

    // A tela de criação abre pra todo mundo (por causa do Americano), mas o formato Oficial
    // só é oferecido a quem pode. É esta pergunta que a tela faz.
    public static bool PodeCriarOficial(Jogador? jogador) =>
        PodeCriarTorneio(jogador, FormatoDoTorneio.Padrao);

    // A frase que aparece pra quem ainda não tem o perfil. Diz o que fazer, não só o que
    // faltou — recusa sem caminho de saída é o jeito mais rápido de perder um cliente novo.
    // E aponta a saída que existe HOJE: o Americano, que não depende de liberação nenhuma.
    public const string ComoPedirOPerfil =
        "O torneio Oficial (duplas fixas, com chave) é liberado organizador por organizador. "
        + "Peça a liberação por aqui mesmo — a gente responde rápido. "
        + "Enquanto isso, o Americano você já pode criar agora mesmo.";

    // ── O PEDIDO ───────────────────────────────────────────────────────────────────────
    //
    // Três estados, e a tela precisa dos três porque cada um pede uma frase diferente:
    // quem nunca pediu vê um botão, quem está esperando vê "seu pedido está na fila" (e NÃO
    // um botão que ele apertaria de novo achando que não foi), e quem foi recusado vê que
    // pode tentar de novo.
    public enum EstadoDoPedido { PodeCriar, NuncaPediu, Esperando, Recusado }

    public static EstadoDoPedido EstadoDe(Jogador? jogador)
    {
        if (jogador == null) return EstadoDoPedido.NuncaPediu;
        if (PodeCriarOficial(jogador)) return EstadoDoPedido.PodeCriar;
        if (jogador.SolicitouOrganizadorEm != null) return EstadoDoPedido.Esperando;
        return jogador.PedidoDeOrganizadorRecusadoEm != null
            ? EstadoDoPedido.Recusado
            : EstadoDoPedido.NuncaPediu;
    }

    // Null = pode pedir. Texto = por que não, na língua de quem lê.
    //
    // ⚠️ Quem JÁ PODE criar não pode pedir: sem esta trava, o admin veria na fila um pedido
    // de alguém que já tem o perfil — e gastaria o tempo dele pra descobrir que não havia
    // nada a fazer.
    public static string? MotivoParaNaoPedir(Jogador? jogador)
    {
        if (jogador == null) return "Entre na sua conta pra pedir a liberação.";

        return EstadoDe(jogador) switch
        {
            EstadoDoPedido.PodeCriar => "Você já pode criar torneio Oficial.",
            EstadoDoPedido.Esperando => "Seu pedido já está na fila — a gente avisa assim que olhar.",
            _ => null,
        };
    }

    // O que o admin lê na fila. Pedido sem motivo escrito é comum (o campo é opcional) e não
    // pode virar uma linha vazia: a frase padrão diz isso com todas as letras.
    public const string SemMotivoEscrito = "Não escreveu o motivo.";

    // Torneio aparece na listagem pública, na Home e no aviso? Só depois de aprovado.
    //
    // ⚠️ Torneio SEM aprovação registrada (`AprovadoEm == null`) é o padrão de quem nasce
    // agora. Os que já existiam entram aprovados pela migração — mudar a regra não pode apagar
    // da vitrine torneio que já estava anunciado e com gente inscrita.
    public static bool ApareceNaVitrine(Torneio torneio) =>
        torneio.AprovadoEm != null && !torneio.Oculto;

    // "Um visitante deslogado, sem link direto, chega a este torneio?" — vitrine E não
    // cancelado, que é o par que as telas públicas sempre pedem junto.
    //
    // Existe porque a condição já estava escrita em dois lugares (sitemap e páginas de cidade)
    // e ia pro terceiro. Regra de visibilidade copiada é como o torneio oculto reaparece: a
    // pessoa esconde o torneio, uma das cópias é atualizada, a outra não, e ele segue saindo
    // no Google — sem erro em lugar nenhum pra ligar uma coisa à outra.
    //
    // O Index do controller NÃO usa este método de propósito: lá a mesma régua tem escapes
    // (o organizador e o admin continuam vendo o que escondeu), e é justamente essa diferença
    // que ele precisa expressar.
    public static bool ApareceParaOPublico(Torneio torneio) =>
        ApareceNaVitrine(torneio) && !CancelamentoDoTorneio.EstaCancelado(torneio.Status);
}

// Separado da regra de propósito: isto aqui é TEXTO DE TELA, e muda por razão diferente da
// permissão. Misturar os dois faz mexer na regra pra ajustar uma vírgula.
public static class TextoDoPedidoDeOrganizador
{
    public static string Frase(PermissaoDeOrganizador.EstadoDoPedido estado) => estado switch
    {
        PermissaoDeOrganizador.EstadoDoPedido.PodeCriar =>
            "Você pode criar torneio Oficial.",
        PermissaoDeOrganizador.EstadoDoPedido.Esperando =>
            "Pedido enviado! Assim que a gente liberar, você recebe um aviso — e o Americano "
            + "você já pode criar agora, sem esperar nada.",
        PermissaoDeOrganizador.EstadoDoPedido.Recusado =>
            "Seu pedido anterior não foi liberado. Dá pra pedir de novo contando um pouco mais "
            + "sobre os torneios que você organiza.",
        _ =>
            "Quer criar torneio Oficial (duplas fixas, com chave e ranking)? Peça a liberação.",
    };
}
