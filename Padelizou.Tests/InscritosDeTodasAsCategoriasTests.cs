using Padelizou.Models;
using Padelizou.Services;
using Xunit;
using padelizou.Models;

namespace Padelizou.Tests;

// A LISTA "TODOS" DA ABA DE INSCRITOS.
//
// 🗣️ Pedido do Felipe, 04/09/2026: *"na primeira vez que abrir a aba de inscritos, uma aba com
// todos, sem separar por categoria, uma lista dizendo o nome da dupla e a categoria que foi
// inscrito, do mais recente pro mais antigo — e aí se o usuário quiser, ele seleciona a
// categoria"*.
//
// 🕳️ O que ele viu: o ER PADEL TOUR tem **49 duplas**, e a aba abria na 3ª Masculina, que tem
// **2**. Quem chega na página não sabe qual categoria olhar, e a primeira da lista não é
// resposta pra pergunta nenhuma — ela é só a primeira.
//
// ⚠️ O que NÃO muda: quem está inscrito continua abrindo na PRÓPRIA categoria. Isso já existe
// no controller, é proposital, e é melhor do que "Todos" pra essa pessoa — ela abriu a página
// pra ver a categoria dela.
public class InscritosDeTodasAsCategoriasTests
{
    private static Jogador Jog(int id, string nome) => new() { Id = id, Nome = nome, Cpf = id.ToString() };

    private static Categoria Cat(int id, string nome) => new() { Id = id, Nome = nome, TorneioId = 1 };

    private static Dupla Dup(int id, Categoria cat, Jogador j1, Jogador? j2, DateTime? criado,
        bool espera = false) => new()
    {
        Id = id, Codigo = "D" + id, CategoriaId = cat.Id, Categoria = cat,
        Jogador1Id = j1.Id, Jogador1 = j1,
        Jogador2Id = j2?.Id, Jogador2 = j2,
        CriadoEm = criado, EmListaDeEspera = espera,
    };

    private static Torneio ComCategorias(params Categoria[] cats)
    {
        var t = new Torneio { Id = 1, Nome = "2ª Etapa ER PADEL TOUR (EPT)", Codigo = "EPT2" };
        foreach (var c in cats) t.Categorias.Add(c);
        return t;
    }

    [Fact]
    public void Junta_as_categorias_numa_lista_so()
    {
        var a = Cat(10, "3ª Masculina");
        var b = Cat(20, "4ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Bruno Piccoli"), Jog(2, "Joao Bugs"), new DateTime(2026, 8, 20)));
        b.Duplas.Add(Dup(2, b, Jog(3, "Arthur Guex"), Jog(4, "Lucas Biehl"), new DateTime(2026, 8, 21)));

        var lista = InscritosDeTodasAsCategorias.Montar(ComCategorias(a, b), new List<InscricaoAmericana>());

        Assert.Equal(2, lista.Count);
    }

    [Fact]
    public void Do_MAIS_RECENTE_pro_mais_antigo()
    {
        // É o pedido literal, e faz sentido na página de um torneio com inscrição aberta:
        // quem abre quer ver quem acabou de entrar.
        var a = Cat(10, "3ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Antiga"), null, new DateTime(2026, 8, 1)));
        a.Duplas.Add(Dup(2, a, Jog(2, "Recente"), null, new DateTime(2026, 8, 30)));

        var lista = InscritosDeTodasAsCategorias.Montar(ComCategorias(a), new List<InscricaoAmericana>());

        // `StartsWith` e não `Equal`: quem está sem parceiro ganha " (procura parceiro)" no fim,
        // e o `ComoChamar` normaliza a caixa do nome. O que este teste prende é a ORDEM.
        Assert.StartsWith("Recente", lista[0].Dupla);
        Assert.StartsWith("Antiga", lista[1].Dupla);
    }

    [Fact]
    public void Inscricao_SEM_data_vai_pro_fim_e_nao_estoura()
    {
        // `CriadoEm` é anulável — inscrição anterior à coluna existe em produção, e ela é das
        // mais velhas: tem que ficar no fim.
        //
        // ⚠️ Este é um teste de CARACTERIZAÇÃO, e vale dizer por quê: o comportamento vem de
        // graça do LINQ (`null` ordena como o menor valor, e em `OrderByDescending` cai no
        // fim). Eu tinha "protegido" isso com um `?? DateTime.MinValue` e um comentário
        // dizendo que sem ele as linhas subiriam pro topo — quebrei o coalesce de propósito e
        // este teste continuou VERDE, o que provou a afirmação falsa. O coalesce saiu; o teste
        // fica, porque a ordem é promessa da tela e alguém pode trocar por um `OrderBy` que a
        // quebre de verdade.
        var a = Cat(10, "3ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Sem data"), null, null));
        a.Duplas.Add(Dup(2, a, Jog(2, "Com data"), null, new DateTime(2026, 8, 1)));

        var lista = InscritosDeTodasAsCategorias.Montar(ComCategorias(a), new List<InscricaoAmericana>());

        Assert.StartsWith("Com Data", lista[0].Dupla);
        Assert.StartsWith("Sem Data", lista[1].Dupla);
    }

    [Fact]
    public void Cada_linha_diz_a_CATEGORIA()
    {
        // O ponto da lista: sem a categoria ao lado, "Bruno e João" não diz em que o cara se
        // inscreveu — e é exatamente essa a pergunta que a lista veio responder.
        var a = Cat(10, "3ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Bruno Piccoli"), Jog(2, "Joao Bugs"), new DateTime(2026, 8, 20)));

        var linha = Assert.Single(InscritosDeTodasAsCategorias.Montar(ComCategorias(a), new List<InscricaoAmericana>()));

        Assert.Equal("3ª Masculina", linha.Categoria);
        Assert.Contains("Bruno", linha.Dupla);
        Assert.Contains("Joao", linha.Dupla);
    }

    [Fact]
    public void Dupla_sem_parceiro_aparece_assim_mesmo()
    {
        // Quem se inscreveu sozinho está inscrito e ocupa vaga — some da lista seria mentir
        // sobre o tamanho do torneio.
        var a = Cat(10, "3ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Sozinho Silva"), null, new DateTime(2026, 8, 20)));

        var linha = Assert.Single(InscritosDeTodasAsCategorias.Montar(ComCategorias(a), new List<InscricaoAmericana>()));

        Assert.Contains("Sozinho", linha.Dupla);
        Assert.Contains("procura", linha.Dupla, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Quem_esta_na_LISTA_DE_ESPERA_e_marcado()
    {
        // Sem a marca, a lista "Todos" faria a espera parecer vaga confirmada — e o número de
        // inscritos do topo da página não bateria com o que ela mostra.
        var a = Cat(10, "3ª Masculina");
        a.Duplas.Add(Dup(1, a, Jog(1, "Dentro"), null, new DateTime(2026, 8, 20)));
        a.Duplas.Add(Dup(2, a, Jog(2, "Esperando"), null, new DateTime(2026, 8, 21), espera: true));

        var lista = InscritosDeTodasAsCategorias.Montar(ComCategorias(a), new List<InscricaoAmericana>());

        Assert.True(lista.Single(l => l.Dupla.Contains("Esperando")).EmListaDeEspera);
        Assert.False(lista.Single(l => l.Dupla.Contains("Dentro")).EmListaDeEspera);
    }

    [Fact]
    public void O_Americano_tambem_entra()
    {
        // O torneio pode ser de inscrição individual — a lista precisa dizer "todos" de
        // verdade, não "todas as duplas".
        var a = Cat(10, "Mista");
        var americanas = new List<InscricaoAmericana>
        {
            new() { Id = 1, CategoriaId = 10, JogadorId = 9,
                    Jogador = Jog(9, "Carol Souza"), CriadoEm = new DateTime(2026, 8, 25) },
        };

        var linha = Assert.Single(InscritosDeTodasAsCategorias.Montar(ComCategorias(a), americanas));

        Assert.Contains("Carol", linha.Dupla);
        Assert.Equal("Mista", linha.Categoria);
    }

    [Fact]
    public void Torneio_sem_inscrito_devolve_lista_vazia()
    {
        Assert.Empty(InscritosDeTodasAsCategorias.Montar(ComCategorias(Cat(10, "3ª Masculina")), new List<InscricaoAmericana>()));
    }
}
