namespace Padelizou.Tests;

// O BOTÃO PROMETIA MENOS DO QUE ENTREGA (Felipe, 24/09/2026): *"aqui esta escrito 'definir por
// cpf' mas tambem permite por nome, temos q trocar o nome desse botão"*.
//
// ⚠️ MENTIRA DE RÓTULO AO CONTRÁRIO — e por isso ela é fácil de não ver. O caso do `build-373`
// era um selo prometendo o que o sistema não fazia; aqui o botão ESCONDE o que ele faz. Quem não
// tem o CPF do parceiro em mãos lê "Definir por CPF" e não clica — e a busca por nome, que é o
// caminho fácil, fica atrás de uma porta com a placa errada.
//
// ⚠️ SÃO DOIS BOTÕES, NÃO UM. O do jogador na própria inscrição (o do print) e o do ORGANIZADOR
// na lista de duplas. Os dois abrem painel com `Procure pelo nome ou apelido`, e consertar só o
// que apareceu no print deixaria a mesma mentira de pé na outra tela.
public class RotuloDoBotaoDeParceiroTests
{
    private const string RotuloVelho = "Definir por CPF";
    private const string RotuloNovo = "Definir parceiro";

    [Fact]
    public void Nenhum_dos_dois_botoes_promete_so_CPF()
    {
        var tela = Arquivo(Path.Combine("Views", "Torneios", "Details.cshtml"));

        Assert.DoesNotContain(RotuloVelho, tela);

        // ⚠️ AS DUAS EXPRESSÕES, uma por uma, e não a contagem do texto solto: contar
        // ocorrências pegava os comentários junto (eram 6, não 2) e o teste reprovava dizendo a
        // coisa errada. Nomear os dois botões é o que impede o conserto pela metade — a falha de
        // 20/09, quando a vitrine foi consertada e a Home ficou para trás.
        Assert.Contains($"sozinho ? \"{RotuloNovo}\"", tela);                    // o do jogador
        Assert.Contains($"dupla.Completa ? \"Trocar parceiro\" : \"{RotuloNovo}\"", tela);  // o do organizador
    }

    [Fact]
    public void O_rotulo_novo_continua_fazendo_par_com_Trocar_parceiro()
    {
        var tela = Arquivo(Path.Combine("Views", "Torneios", "Details.cshtml"));

        // Os dois botões são o MESMO botão em dois estados — dupla vazia e dupla fechada. O par
        // "Definir parceiro" / "Trocar parceiro" diz isso; um nome de família diferente em cada
        // estado faria parecerem duas funções distintas.
        Assert.Contains("\"Trocar parceiro\"", tela);
        Assert.Contains($"\"{RotuloNovo}\"", tela);
    }

    [Fact]
    public void O_painel_continua_aceitando_nome_E_CPF()
    {
        var tela = Arquivo(Path.Combine("Views", "Torneios", "Details.cshtml"));

        // ⚠️ É ISTO QUE TORNA O RÓTULO NOVO HONESTO, e é a única parte que pode apodrecer
        // sozinha: se um dia a busca por nome sumir do painel, "Definir parceiro" vira vago e o
        // rótulo velho volta a ser o certo. O teste falha e obriga a decisão.
        Assert.Contains("Procure pelo nome ou apelido", tela);
        Assert.Contains("CPF do parceiro", tela);
    }

    [Fact]
    public void A_explicacao_do_organizador_nao_cita_mais_o_rotulo_velho()
    {
        // O texto do `_EntramSemParceiro` manda o organizador procurar um botão pelo nome. Nome
        // antigo ali é a pessoa procurando na tela um botão que não existe mais.
        var aviso = Arquivo(Path.Combine("Views", "Torneios", "_EntramSemParceiro.cshtml"));

        Assert.DoesNotContain(RotuloVelho, aviso);
        Assert.Contains(RotuloNovo, aviso);
    }

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
