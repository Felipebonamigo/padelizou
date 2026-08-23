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
}
