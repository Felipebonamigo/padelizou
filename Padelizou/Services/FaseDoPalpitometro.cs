using System.Linq.Expressions;
using Padelizou.Models;

namespace Padelizou.Services;

// O PALPITÔMETRO POR FASE (12/09/2026).
//
// 🗣️ Felipe: *"também no palpitometro, colocar um filtro 'por chaves' 'Por mata mata' 'Apenas
// finais'"*. Perguntado: "por chaves" é a FASE DE GRUPOS (não por categoria), e cada recorte é
// um RANKING PRÓPRIO — 1º, 2º, 3º e pódio dele.
//
// ⚠️ ESTE FILTRO MUDA OS NÚMEROS, e é o que o separa do "jogando / de fora" que ele pediu
// junto: os pontos de "só o mata-mata" são outra apuração, não um subconjunto de linhas. Por
// isso ele mora no SERVIDOR (a página recarrega com `fasePalpiteiros=`) em vez de esconder
// linha no navegador — esconder mostraria o ponto do torneio inteiro debaixo do rótulo de uma
// fase, que é pior que não ter filtro.
//
// ⚠️ A RÉGUA DO GRUPO NÃO É NOVA: é o `FasesTorneio.EhFaseDeGrupos`, o único lugar que conhece
// as DUAS formas gravadas no banco — "Fase de Grupos" (seeds antigos) e "Grupo A"/"Grupo B"
// (o `GerarChaves` de hoje). Como aqui ela precisa virar SQL, vai escrita inline, do jeito que
// o próprio FasesTorneio manda no comentário dele. Uma régua nova aqui deixaria o torneio
// antigo fora do recorte sem nada na tela dizer por quê.
//
// ⚠️ A FINAL ESTÁ DENTRO DO MATA-MATA de propósito: ela é a última fase da chave. "Apenas
// finais" é um recorte MAIS ESTREITO, não um irmão que a exclui de lá.
public static class FaseDoPalpitometro
{
    public const string Tudo = "tudo";
    public const string Grupos = "grupos";
    public const string MataMata = "matamata";
    public const string Finais = "finais";

    // A fase da decisão, como o `ChaveamentoMataMata.NomeFase` a batiza.
    public const string Final = "Final";

    // O prefixo que o GerarChaves grava: "Grupo A", "Grupo B"...
    private const string PrefixoDeGrupo = "Grupo ";

    // ⚠️ O RECORTE VEM DA QUERY STRING, isto é, de FORA: `?fasePalpiteiros=xpto` não pode virar
    // tabela vazia nem exceção. Cai no padrão, que é o torneio inteiro.
    public static string Normalizar(string? recorte) => recorte switch
    {
        Grupos => Grupos,
        MataMata => MataMata,
        Finais => Finais,
        _ => Tudo,
    };

    // O filtro de partidas de um recorte. Vira SQL — ver
    // FiltroPorFaseNoPalpitometroTests.O_filtro_de_cada_recorte_vira_SQL, porque o InMemory da
    // suíte não traduz `StartsWith` nem nada mais.
    public static Expression<Func<Partida, bool>> Filtro(int torneioId, string recorte) =>
        Normalizar(recorte) switch
        {
            Grupos => p => p.TorneioId == torneioId
                           && (p.Fase == FasesTorneio.FaseDeGrupos || p.Fase.StartsWith(PrefixoDeGrupo)),

            MataMata => p => p.TorneioId == torneioId
                             && p.Fase != FasesTorneio.FaseDeGrupos
                             && !p.Fase.StartsWith(PrefixoDeGrupo),

            Finais => p => p.TorneioId == torneioId && p.Fase == Final,

            _ => p => p.TorneioId == torneioId,
        };

    // A que recortes UMA fase pertence — a mesma régua de cima, respondida em memória. É daqui
    // que sai quais botões a tela oferece.
    public static bool Contem(string recorte, string fase) => Normalizar(recorte) switch
    {
        Grupos => FasesTorneio.EhFaseDeGrupos(fase),
        MataMata => !FasesTorneio.EhFaseDeGrupos(fase),
        Finais => fase == Final,
        _ => true,
    };

    // ⚠️ A ORDEM É A DA TELA, e é a do pedido: o geral, os grupos, o mata-mata e a decisão.
    public static readonly string[] Todos = [Tudo, Grupos, MataMata, Finais];

    // O RÓTULO DO BOTÃO. O "Apenas" do terceiro não é enfeite: é o que o distingue do
    // "Mata-mata" ao lado, já que a final está dentro do mata-mata.
    public static string Rotulo(string recorte) => Normalizar(recorte) switch
    {
        Grupos => "Chaves e grupos",
        MataMata => "Mata-mata",
        Finais => "Apenas finais",
        _ => "Todas as fases",
    };

    // ⚠️ O MESMO RECORTE, ESCRITO PRA DENTRO DE UMA FRASE — e isto é conserto de um texto visto
    // na tela antes de publicar (12/09/2026): a frase que avisa do recorte usava o rótulo do
    // botão e saía *"Só apenas finais"*. Rótulo e texto corrido são duas escritas da mesma
    // coisa, e tentar servir as duas com uma string estraga uma delas.
    public static string NaFrase(string recorte) => Normalizar(recorte) switch
    {
        Grupos => "os jogos de grupo",
        MataMata => "os jogos do mata-mata",
        Finais => "as finais",
        _ => "o torneio inteiro",
    };
}
