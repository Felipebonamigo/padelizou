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
    // O admin do Padelizou cria torneio em qualquer situação — é ele quem socorre organizador
    // travado no dia do jogo, e depender do próprio perfil que ele mesmo distribui seria um
    // nó que só se desata pelo banco.
    public static bool PodeCriarTorneio(Jogador? jogador) =>
        jogador != null && (jogador.IsOrganizadorTorneio || jogador.IsAdminRaiz || jogador.IsAdminGeral);

    // A frase que aparece pra quem ainda não tem o perfil. Diz o que fazer, não só o que
    // faltou — recusa sem caminho de saída é o jeito mais rápido de perder um cliente novo.
    public const string ComoPedirOPerfil =
        "Criar torneio no Padelizou é liberado organizador por organizador. "
        + "Fale com a gente pelo WhatsApp que a liberação é rápida — e depois disso você cria "
        + "quantos torneios quiser.";

    // Torneio aparece na listagem pública, na Home e no aviso? Só depois de aprovado.
    //
    // ⚠️ Torneio SEM aprovação registrada (`AprovadoEm == null`) é o padrão de quem nasce
    // agora. Os que já existiam entram aprovados pela migração — mudar a regra não pode apagar
    // da vitrine torneio que já estava anunciado e com gente inscrita.
    public static bool ApareceNaVitrine(Torneio torneio) =>
        torneio.AprovadoEm != null && !torneio.Oculto;
}
