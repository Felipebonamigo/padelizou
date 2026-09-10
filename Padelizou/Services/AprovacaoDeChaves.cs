namespace Padelizou.Services;

// A aprovação das chaves: entre o sorteio e a chave virar pública, alguém de confiança confere
// antes de soltar pros jogadores. Pedido do Felipe, 22/08/2026: "colocar uma tela antes de...
// dizer como liberar das chaves".
//
// O sorteio (GerarChaves) continua rodando e gravando tudo igual — grupos, jogos, horários.
// O que muda é o status que ele grava no fim: em vez de ir direto pra "Fase de Grupos" (que
// já é público), ele para aqui. Só quando alguém aprova (AprovarChaves) é que o torneio vira
// "Fase de Grupos" de verdade e o aviso "as chaves saíram" sai pros jogadores.
//
// Quem pode ver e aprovar é a MESMA régua de sempre pra gerir o torneio — organizador desta
// categoria, admin raiz ou admin nomeado (TorneiosController.EhOrganizadorAsync). Não é papel
// de acesso novo, só mais um portão na régua que já existe.
public static class AprovacaoDeChaves
{
    public const string Pendente = "Chaves em Aprovação";

    // O JOGO JÁ É PÚBLICO? — o predicado ÚNICO de todo leitor da grade que fala com jogador
    // (10/09/2026).
    //
    // 🗣️ Felipe: *"chegou a notificação de horarios para as pessoas, e nao poderia chegar, lembra
    // que pedi para nao chegar as notificações e nem nada até publicar?"* — e o print: a Home
    // dizendo "Seu próximo jogo: sáb. 12/09 às 08:00" com o torneio em "Chaves em Aprovação".
    //
    // 🕳️ O `AprovarChaves` era o único que AVISAVA, e por isso parecia fechado. Mas três leitores
    // entregavam o horário sem olhar a aprovação: a agenda/ICS (o calendário do celular notifica
    // evento novo — foi a notificação que chegou), o "próximo jogo" da Home e o push de quadra
    // atrasada. A mesma lição da aba Jogos, de horas antes: régua de visibilidade escrita num
    // lugar protege aquele lugar, não o dado. Daí uma expressão só, traduzível pro SQL, que cada
    // leitor põe no `Where`.
    //
    // Pelo caminho `Categoria.Torneio`, e não por `Partida.TorneioId`: este é nulo em partida
    // fora de torneio (jogo semanal), e a categoria é obrigatória.
    public static readonly System.Linq.Expressions.Expression<Func<Models.Partida, bool>> Publicada =
        p => p.Categoria.Torneio.Status != Pendente;
}
