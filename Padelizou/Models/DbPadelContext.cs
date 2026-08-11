using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using System;
using System.Collections.Generic;

namespace Padelizou.Models;

public partial class DbPadelContext : DbContext
{
    public DbPadelContext()
    {
    }

    public DbPadelContext(DbContextOptions<DbPadelContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Categoria> Categorias { get; set; }

    public virtual DbSet<Dupla> Duplas { get; set; }

    public virtual DbSet<Jogador> Jogadores { get; set; }


    public virtual DbSet<Partida> Partidas { get; set; }

    public virtual DbSet<Torneio> Torneios { get; set; }
    public virtual DbSet<Aula> Aulas { get; set; }
    public virtual DbSet<CategoriaPadrao> CategoriasPadrao { get; set; }
    public virtual DbSet<TorneioOrganizador> TorneioOrganizadores { get; set; }
    public DbSet<Clube> Clubes { get; set; }
    public DbSet<Time> Times { get; set; }
    public DbSet<TimeAdministrador> TimeAdministradores { get; set; }
    public DbSet<LeadComercial> LeadsComerciais { get; set; }
    public DbSet<RepasseAoParceiro> RepassesAoParceiro { get; set; }
    public DbSet<SolicitacaoRegistroResultados> SolicitacoesRegistroResultados { get; set; }
    public DbSet<LocalAula> LocaisAula { get; set; }
    public DbSet<PacoteDeAulas> PacotesDeAulas { get; set; }
    public DbSet<PrecoDeAluno> PrecosDeAluno { get; set; }
    public DbSet<HorarioDisponivel> HorariosDisponiveis { get; set; }
    public DbSet<Cidade> Cidades { get; set; }
    public DbSet<ProfessorCidade> ProfessorCidades { get; set; }
    public DbSet<JogadorCategoria> JogadorCategorias { get; set; }
    public DbSet<JogadorClube> JogadorClubes { get; set; }
    public DbSet<JogadorDiaHorario> JogadorDiasHorarios { get; set; }
    public DbSet<AvisoJogo> AvisosJogo { get; set; }
    public DbSet<AvisoParceiro> AvisosParceiro { get; set; }
    public DbSet<CandidaturaParceiro> CandidaturasParceiro { get; set; }
    public DbSet<JogoSemanal> JogosSemanais { get; set; }
    public DbSet<GrupoPrivado> GruposPrivados { get; set; }
    public DbSet<JogadorGrupo> JogadoresGrupo { get; set; }
    public DbSet<SessaoGrupo> SessoesGrupo { get; set; }
    public DbSet<ConfirmacaoSessao> ConfirmacoesSessao { get; set; }
    public DbSet<MensalidadeGrupo> MensalidadesGrupo { get; set; }
    public DbSet<PalpitePartida> PalpitesPartida { get; set; }
    public DbSet<Quadra> Quadras { get; set; }
    public DbSet<InscricaoAmericana> InscricoesAmericanas { get; set; }
    public DbSet<PushSubscriptionJogador> PushSubscriptionsJogador { get; set; }
    public DbSet<ClubeAdministrador> ClubeAdministradores { get; set; }
    public DbSet<AvisoRaqueteLivre> AvisosRaqueteLivre { get; set; }
    public DbSet<InscricaoRaqueteLivre> InscricoesRaqueteLivre { get; set; }
    public DbSet<JogoAula> JogosAula { get; set; }
    public DbSet<InscricaoJogoAula> InscricoesJogoAula { get; set; }
    public DbSet<SeguidorJogador> SeguidoresJogador { get; set; }
    public DbSet<SeguidorTorneio> SeguidoresTorneio { get; set; }
    public DbSet<QuadraClube> QuadrasClube { get; set; }
    public DbSet<HorarioMarcacaoDisponivel> HorariosMarcacaoDisponivel { get; set; }
    public DbSet<MarcacaoJogo> MarcacoesJogo { get; set; }

    // Bar do clube: cardápio, comandas e o caixa do dia.
    public DbSet<ProdutoBar> ProdutosBar { get; set; }
    public DbSet<Comanda> Comandas { get; set; }
    public DbSet<ItemComanda> ItensComanda { get; set; }
    public DbSet<CaixaDoDia> CaixasDoDia { get; set; }
    public DbSet<LancamentoFinanceiro> LancamentosFinanceiros { get; set; }
    public DbSet<MovimentoEstoque> MovimentosEstoque { get; set; }
    public DbSet<JogadorCidade> JogadorCidades { get; set; }
    public DbSet<Pagamento> Pagamentos { get; set; }
    public DbSet<Elogio> Elogios { get; set; }
    public DbSet<ComentarioPerfil> ComentariosPerfil { get; set; }
    public DbSet<FeedbackSite> FeedbacksSite { get; set; }

    // A caixa de entrada de avisos do jogador (a tela "Notificações"). Ver AvisoDoJogador.
    public DbSet<AvisoDoJogador> AvisosDoJogador { get; set; }

    // Inscrições que o Ranking RS reprovou e que esperam a decisão do organizador.
    public DbSet<BloqueioDoRanking> BloqueiosDoRanking { get; set; }

    // Toda consulta ao Ranking, e não só as que barraram — inclusive as que nem chegaram a
    // ser feitas. É o que permite dizer quantas pessoas passaram pelo filtro sem chutar.
    public DbSet<ConsultaAoRankingRs> ConsultasAoRankingRs { get; set; }
    public DbSet<AvaliacaoProfessor> AvaliacoesProfessor { get; set; }
    public DbSet<AnotacaoAula> AnotacoesAula { get; set; }

    // Avaliação técnica do aluno: a régua do professor, as fichas e as notas.
    public DbSet<FundamentoDoProfessor> FundamentosDoProfessor { get; set; }
    public DbSet<AvaliacaoDeAluno> AvaliacoesDeAluno { get; set; }
    public DbSet<NotaDeFundamento> NotasDeFundamento { get; set; }
    public DbSet<AlertaSistema> AlertasSistema { get; set; }

    // Erros não tratados de produção — o que o /Admin/Erros lista. Ver Services/RegistroDeErros.
    public DbSet<ErroDoSistema> ErrosDoSistema { get; set; }

    // Chave/valor que o admin muda de dentro do app e que sobrevive ao restart.
    public DbSet<ConfiguracaoDoSistema> ConfiguracoesDoSistema { get; set; }

    // Despesas do Padelizou lançadas à mão pelo admin raiz (fatura do gateway, VPS...).
    // Tabela própria de propósito — ver o comentário do modelo.
    public DbSet<DespesaRegistrada> DespesasRegistradas { get; set; }

    // O acerto de contas com o Ranking RS, torneio a torneio. É fotografia, não cálculo — ver
    // o comentário do modelo.
    public DbSet<AcertoRankingRs> AcertosRankingRs { get; set; }

    // Padelímetro: o extrato de movimentos do nível (o número atual vive no Jogador).
    public DbSet<HistoricoDePadelimetro> HistoricosDePadelimetro { get; set; }

    // Desafios: o mural de duplas abertas na semana e os confrontos que saem dele.
    // Espec em DESAFIOS.md, na raiz do repo.
    public DbSet<AnuncioDeDesafio> AnunciosDeDesafio { get; set; }
    public DbSet<AnuncioDesafioCategoria> AnuncioDesafioCategorias { get; set; }
    public DbSet<AnuncioDesafioCidade> AnuncioDesafioCidades { get; set; }
    public DbSet<AnuncioDesafioClube> AnuncioDesafioClubes { get; set; }
    public DbSet<Desafio> Desafios { get; set; }
    // O cinturão de cada categoria: o dono de hoje é a linha com TerminouEm nulo, e as demais
    // são o histórico. Ver o comentário do modelo.
    public DbSet<ReinadoNoCinturao> ReinadosNoCinturao { get; set; }
    // O voto pro melhor jogador do torneio, um por pessoa. A apuração é contagem de linhas
    // daqui — nunca um contador numa coluna, que não dá pra auditar nem pra desfazer.
    public DbSet<VotoDeMvp> VotosDeMvp { get; set; }

    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=DB_PADEL;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Jogador>()
    .HasOne(j => j.Time)
    .WithMany(t => t.Jogadores)
    .HasForeignKey(j => j.TimeId)
    .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Jogador>().ToTable("Jogador");

        // Dupla-time (categoria de times): o vínculo com o cadastro de Times é só pra
        // mostrar o escudo. SetNull — apagar um time do cadastro não pode apagar a
        // história de um torneio (a lição do Torneio.ClubeId em cascade já custou caro).
        modelBuilder.Entity<Dupla>()
            .HasOne(d => d.Time)
            .WithMany()
            .HasForeignKey(d => d.TimeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<SolicitacaoRegistroResultados>(entity =>
        {
            // Cascade: sem o torneio, o pedido de equipe não quer dizer nada.
            entity.HasOne(e => e.Torneio)
                .WithMany()
                .HasForeignKey(e => e.TorneioId)
                .OnDelete(DeleteBehavior.Cascade);

            // SolicitadoPorId e RespondidaPorId são colunas simples, sem FK pra Jogador, de
            // propósito: já existe caminho de cascade demais saindo de Jogador, e aqui o que
            // importa é o registro histórico de quem pediu e quem respondeu — não vale
            // arriscar o pedido sumir junto com uma conta excluída pela LGPD.
            entity.Property(e => e.Status).HasMaxLength(30);
        });

        modelBuilder.Entity<TimeAdministrador>(entity =>
        {
            // Chave composta: uma pessoa administra um time uma vez só. É a trava de
            // duplicata no próprio banco, não só na tela.
            entity.HasKey(e => new { e.TimeId, e.JogadorId });

            // Cascade nos dois lados: sem o time, ou sem a pessoa, a linha não quer dizer
            // mais nada. Não repete o problema que fez o DonoId nascer sem FK — aqui os
            // caminhos partem de tabelas diferentes, e o PostgreSQL não tem a restrição de
            // múltiplos caminhos de cascade que o SQL Server tinha.
            entity.HasOne(e => e.Time)
                .WithMany(t => t.Administradores)
                .HasForeignKey(e => e.TimeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadComercial>(entity =>
        {
            entity.Property(e => e.Contato).HasMaxLength(Services.LeadsComerciais.TamanhoMaximoContato);
            entity.Property(e => e.Telefone).HasMaxLength(11);
            entity.Property(e => e.Tipo).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Observacao).HasMaxLength(Services.LeadsComerciais.TamanhoMaximoObservacao);
            entity.Property(e => e.MotivoPerda).HasMaxLength(Services.LeadsComerciais.TamanhoMaximoObservacao);

            // A pergunta que a tela faz em toda gravação é "quem já registrou esse telefone?".
            // Sem índice, cada registro varre a tabela inteira.
            //
            // ⚠️ NÃO é único, e isso é decisão, não esquecimento: o mesmo contato PODE ser
            // registrado de novo depois de vencer ou de ser perdido. Quem decide se o novo
            // registro vale é LeadsComerciais.QuemSegura, que sabe ler as datas — um índice
            // único recusaria a segunda tentativa até de quem tem direito a ela.
            entity.HasIndex(e => e.Telefone);

            // Restrict nos três: um lead é registro comercial, e sumir junto com a conta de
            // quem indicou apagaria a resposta de "quem trouxe esse cliente?" bem depois de a
            // comissão já ter sido paga. Se a conta precisar sair, o lead aparece primeiro.
            entity.HasOne(e => e.Parceiro)
                .WithMany()
                .HasForeignKey(e => e.ParceiroId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cliente)
                .WithMany()
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RepasseAoParceiro>(entity =>
        {
            entity.Property(e => e.Valor).HasPrecision(10, 2);
            entity.Property(e => e.Observacao).HasMaxLength(Services.LeadsComerciais.TamanhoMaximoObservacao);

            // A tela lê "os repasses deste parceiro" o tempo todo.
            entity.HasIndex(e => e.ParceiroId);

            // Restrict pela mesma razão do lead: repasse é registro de dinheiro que saiu, e
            // sumir junto com uma conta apagaria a prova de um pagamento já feito.
            entity.HasOne(e => e.Parceiro)
                .WithMany()
                .HasForeignKey(e => e.ParceiroId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GrupoPrivado>(entity =>
        {
            // Restrict: não pode excluir um Clube/CategoriaPadrao que ainda esteja em uso por um
            // grupo (mesmo padrão de AvisoJogo — não são FKs de Jogador, então não entram no
            // conflito de múltiplos caminhos de cascade, mas ainda assim não fazem sentido cascatear).
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CategoriaPadrao)
                .WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.EnviarLembrete24h).HasDefaultValue(false);
        });
        modelBuilder.Entity<JogadorGrupo>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.GrupoId });

            entity.HasOne(e => e.GrupoPrivado)
                .WithMany()
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict aqui porque GrupoPrivado já faz cascade a partir de Jogador (Administrador) —
            // um segundo caminho direto de Jogador até JogadorGrupo causaria o mesmo conflito de
            // múltiplos caminhos de cascade já visto em JogoSemanal/CandidaturaParceiro/PalpitePartida.
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TorneioOrganizador>()
        .HasKey(to => new { to.TorneioId, to.JogadorId });
        modelBuilder.Entity<Clube>(entity =>
        {
            // Restrict: apagar um Jogador não pode apagar o Clube que ele é dono — força
            // reatribuir o dono antes. Primeiro (e único) caminho de Jogador até Clube, sem
            // risco de conflito de múltiplos caminhos de cascade.
            entity.HasOne(e => e.Dono)
                .WithMany()
                .HasForeignKey(e => e.DonoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ClubeAdministrador>(entity =>
        {
            entity.HasKey(e => new { e.ClubeId, e.JogadorId });

            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AvisoRaqueteLivre>(entity =>
        {
            // Restrict: não é FK de Jogador, só não faz sentido cascatear (mesmo padrão de
            // AvisoJogo.Clube).
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Criador)
                .WithMany()
                .HasForeignKey(e => e.CriadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Preco).HasPrecision(18, 2);
        });
        modelBuilder.Entity<InscricaoRaqueteLivre>(entity =>
        {
            entity.HasKey(e => new { e.AvisoRaqueteLivreId, e.JogadorId });

            entity.HasOne(e => e.AvisoRaqueteLivre)
                .WithMany()
                .HasForeignKey(e => e.AvisoRaqueteLivreId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: Jogador já alcança essa tabela via Criador (Cascade) -> AvisoRaqueteLivre
            // (Cascade) -> InscricaoRaqueteLivre — um segundo caminho direto causaria o mesmo
            // conflito de múltiplos caminhos de cascade já visto em ConfirmacaoSessao/
            // JogadorGrupo/MensalidadeGrupo.
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<InscricaoAmericana>(entity =>
        {
            entity.HasOne(e => e.Categoria)
                .WithMany()
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<JogoAula>(entity =>
        {
            // LocalAula.ProfessorId já cascateia de Jogador — JogoAula tem 2 caminhos
            // candidatos (direto via Professor, indireto via LocalAula). Sigo o mesmo
            // precedente de Aula.cs: Professor cascateia, LocalAula/CategoriaPadrao restrict.
            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LocalAula)
                .WithMany()
                .HasForeignKey(e => e.LocalAulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CategoriaPadrao)
                .WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.Preco).HasPrecision(18, 2);
        });
        modelBuilder.Entity<InscricaoJogoAula>(entity =>
        {
            entity.HasKey(e => new { e.JogoAulaId, e.JogadorId });

            entity.HasOne(e => e.JogoAula)
                .WithMany()
                .HasForeignKey(e => e.JogoAulaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: JogoAula já cascateia de Jogador via Professor — segundo caminho
            // direto causaria o mesmo conflito de múltiplos caminhos de cascade.
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SeguidorJogador>(entity =>
        {
            entity.HasKey(e => new { e.SeguidorId, e.SeguidoId });

            // Cascade: apagar minha conta remove minhas inscrições de "seguir".
            entity.HasOne(e => e.Seguidor)
                .WithMany()
                .HasForeignKey(e => e.SeguidorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: primeira auto-referência intencionalmente cascateando em Jogador —
            // evita o mesmo conflito de múltiplos caminhos de cascade se Seguidor e Seguido
            // fossem os dois Cascade (Dupla.Jogador1/Jogador2 é legado com ClientSetNull,
            // não serve de precedente aqui).
            entity.HasOne(e => e.Seguido)
                .WithMany()
                .HasForeignKey(e => e.SeguidoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SeguidorTorneio>(entity =>
        {
            // A chave composta é o que impede seguir duas vezes: clicar de novo não pode
            // dobrar o aviso. Aqui isso pesa mais que no SeguidorJogador — um torneio grande
            // dispara um aviso POR INSCRIÇÃO, e a linha repetida dobraria a rajada inteira.
            entity.HasKey(e => new { e.TorneioId, e.JogadorId });

            // Cascade nos dois: torneio apagado e conta apagada devem levar junto o pedido de
            // ser avisado. São FKs pra tabelas DIFERENTES, então não existe aqui o conflito de
            // múltiplos caminhos que obrigou o SeguidorJogador a usar Restrict num dos lados.
            entity.HasOne(e => e.Torneio)
                .WithMany()
                .HasForeignKey(e => e.TorneioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Elogio>(entity =>
        {
            // UM elogio por pessoa: quem já elogiou alguém pode TROCAR a escolha, não somar
            // mais uma. Antes o índice incluía o Tipo, então uma pessoa só empilhava quantos
            // elogios quisesse no mesmo perfil e o contador virava "quem clica mais, pesa
            // mais" — a mesma razão pela qual AvaliacaoProfessor é uma por aluno.
            entity.HasIndex(e => new { e.DeJogadorId, e.ParaJogadorId }).IsUnique();

            // Mesmo raciocínio do SeguidorJogador: dois FKs pra Jogador, um Cascade e outro
            // Restrict, pra evitar o conflito de múltiplos caminhos de cascade.
            entity.HasOne(e => e.DeJogador)
                .WithMany()
                .HasForeignKey(e => e.DeJogadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ParaJogador)
                .WithMany()
                .HasForeignKey(e => e.ParaJogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<VotoDeMvp>(entity =>
        {
            // UM VOTO POR PESSOA POR TORNEIO, garantido pelo BANCO.
            //
            // ⚠️ A checagem em C# ("já votou?") não basta e não é redundância: dois POSTs
            // simultâneos — o clique duplo no celular lento é o caso comum — passam os dois
            // pela consulta antes de qualquer um gravar, e a pessoa vota duas vezes sem
            // trapacear de propósito. Aqui a segunda gravação simplesmente não acontece.
            //
            // Mesma régua do Elogio: quem já votou pode TROCAR a escolha (o serviço atualiza a
            // linha), nunca somar outra.
            entity.HasIndex(v => new { v.TorneioId, v.VotanteId }).IsUnique();

            // Torneio apagado leva os votos junto: a votação não existe fora dele.
            entity.HasOne(v => v.Torneio)
                .WithMany()
                .HasForeignKey(v => v.TorneioId)
                .OnDelete(DeleteBehavior.Cascade);

            // ⚠️ Os DOIS lados de Jogador em Restrict, e não um Cascade como no Elogio: aqui já
            // existe um caminho de cascade pelo Torneio, e um segundo por Jogador criaria os
            // "múltiplos caminhos" que o Postgres recusa. Restrict é a resposta certa de
            // qualquer forma — apagar conta é bloqueado pelo banco desde sempre (ver
            // Jogador.ExcluidoEm: a exclusão raspa os dados, não remove a linha).
            entity.HasOne(v => v.Votante)
                .WithMany()
                .HasForeignKey(v => v.VotanteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.Candidato)
                .WithMany()
                .HasForeignKey(v => v.CandidatoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ConfiguracaoDoSistema>(entity =>
        {
            // A chave É a identidade da linha: duas linhas pra mesma chave fariam a leitura
            // depender de qual vem primeiro, e o valor "certo" mudaria sem ninguém mexer.
            entity.HasIndex(c => c.Chave).IsUnique();
            entity.Property(c => c.Chave).HasMaxLength(80);
            entity.Property(c => c.Valor).HasMaxLength(400);
        });
        modelBuilder.Entity<ErroDoSistema>(entity =>
        {
            // A janela de silêncio pergunta "quando foi o último AVISO deste erro?" — e essa
            // consulta roda dentro do tratamento de um erro, onde lentidão dobraria o estrago.
            entity.HasIndex(e => new { e.Tipo, e.Caminho, e.AvisoEnviado, e.QuandoEm });
            // A listagem do admin e a limpeza dos antigos varrem por data.
            entity.HasIndex(e => e.QuandoEm);
        });
        modelBuilder.Entity<HistoricoDePadelimetro>(entity =>
        {
            // O extrato de um jogador se consulta inteiro e em ordem — é o gráfico do perfil.
            entity.HasIndex(h => new { h.JogadorId, h.CriadoEm });

            // Jogador Restrict (a linha de Jogador nunca é apagada de verdade — LGPD raspa
            // os dados e mantém a linha); a partida Cascade: sumiu o jogo, sai a linha do
            // extrato, e o replay reacerta o total.
            entity.HasOne(h => h.Jogador)
                .WithMany()
                .HasForeignKey(h => h.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Partida)
                .WithMany()
                .HasForeignKey(h => h.PartidaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AnotacaoAula>(entity =>
        {
            // Apagar a aula leva as anotações junto (são sobre ela); o autor fica Restrict —
            // mesmo raciocínio do Elogio: dois caminhos de cascade pra Jogador conflitam.
            entity.HasOne(a => a.Aula)
                .WithMany()
                .HasForeignKey(a => a.AulaId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Autor)
                .WithMany()
                .HasForeignKey(a => a.AutorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<FundamentoDoProfessor>(entity =>
        {
            // A régua é lida inteira e em ordem toda vez que o professor abre uma ficha.
            entity.HasIndex(f => new { f.ProfessorId, f.Modulo, f.Ordem });

            entity.HasOne(f => f.Professor)
                .WithMany()
                .HasForeignKey(f => f.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AvaliacaoDeAluno>(entity =>
        {
            // As duas consultas que existem: "as fichas deste aluno comigo" (professor) e
            // "as fichas que me liberaram" (aluno).
            entity.HasIndex(a => new { a.ProfessorId, a.AlunoId, a.CriadoEm });
            entity.HasIndex(a => new { a.AlunoId, a.VisivelParaAluno });

            // Mesmo raciocínio do Elogio e da AvaliacaoProfessor: dois FKs pra Jogador, só um
            // pode ser Cascade, senão dá conflito de múltiplos caminhos.
            entity.HasOne(a => a.Professor)
                .WithMany()
                .HasForeignKey(a => a.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Aluno)
                .WithMany()
                .HasForeignKey(a => a.AlunoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<NotaDeFundamento>(entity =>
        {
            // Uma nota por fundamento em cada ficha — a segunda seria duas verdades sobre o
            // mesmo golpe no mesmo dia, e a evolução não saberia qual ler.
            entity.HasIndex(n => new { n.AvaliacaoDeAlunoId, n.FundamentoDoProfessorId }).IsUnique();

            entity.HasOne(n => n.Avaliacao)
                .WithMany(a => a.Notas)
                .HasForeignKey(n => n.AvaliacaoDeAlunoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict no fundamento: tirar um item da régua NÃO pode apagar a nota que o
            // aluno já levou. Por isso remover desativa (Ativo=false) em vez de excluir.
            entity.HasOne(n => n.Fundamento)
                .WithMany()
                .HasForeignKey(n => n.FundamentoDoProfessorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AvaliacaoProfessor>(entity =>
        {
            // Um aluno tem UMA avaliação por professor — reavaliar edita a mesma linha,
            // senão a média viraria "quem escreve mais, pesa mais".
            entity.HasIndex(e => new { e.AlunoId, e.ProfessorId }).IsUnique();

            // Mesmo raciocínio do Elogio: dois FKs pra Jogador, um Cascade e outro
            // Restrict, pra evitar conflito de múltiplos caminhos de cascade.
            entity.HasOne(e => e.Aluno)
                .WithMany()
                .HasForeignKey(e => e.AlunoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ComentarioPerfil>(entity =>
        {
            // UM comentário por pessoa em cada perfil — comentar de novo EDITA o mesmo texto.
            // Mesma regra do Elogio: sem isso, um perfil vira mural de quem escreve mais.
            entity.HasIndex(e => new { e.AutorId, e.PerfilId }).IsUnique();

            // Mesmo raciocínio do Elogio/SeguidorJogador: dois FKs pra Jogador, um Cascade e
            // outro Restrict, pra evitar o conflito de múltiplos caminhos de cascade.
            entity.HasOne(e => e.Autor)
                .WithMany()
                .HasForeignKey(e => e.AutorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Perfil)
                .WithMany()
                .HasForeignKey(e => e.PerfilId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BloqueioDoRanking>(entity =>
        {
            // UMA linha por pessoa em cada categoria. Tentar de novo ATUALIZA a mesma linha em
            // vez de somar outra — foi exatamente assim que a tabela de Elogios cresceu debaixo
            // da consulta em produção, e aqui seria pior: a fila do organizador encheria de
            // repetições da mesma pessoa e ele decidiria a mesma coisa cinco vezes.
            entity.HasIndex(e => new { e.CategoriaId, e.Cpf }).IsUnique();

            // Os dois em Cascade de propósito. O caminho Torneio → Categoria → Bloqueio já
            // existe, então um Restrict no TorneioId travaria a exclusão do torneio; e o
            // Postgres lida com caminho múltiplo de cascade sem reclamar (o conflito é coisa
            // do SQL Server).
            entity.HasOne(e => e.Torneio)
                .WithMany()
                .HasForeignKey(e => e.TorneioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Categoria)
                .WithMany()
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Quem decidiu pode apagar a própria conta depois (LGPD, ver Services/ExclusaoDeConta).
            // A decisão continua valendo — o que se perde é só o nome de quem a tomou.
            entity.HasOne(e => e.DecididoPor)
                .WithMany()
                .HasForeignKey(e => e.DecididoPorId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<ConsultaAoRankingRs>(entity =>
        {
            // UMA linha por pessoa em cada categoria, igual ao BloqueioDoRanking: quem tenta
            // se inscrever cinco vezes é uma pessoa consultada, não cinco.
            entity.HasIndex(e => new { e.CategoriaId, e.Cpf }).IsUnique();

            // A tela lê "as consultas deste torneio" o tempo todo — sem isto, todo carregamento
            // do relatório varre a tabela inteira.
            entity.HasIndex(e => e.TorneioId);

            // Cascade nos dois pelo mesmo motivo do BloqueioDoRanking: o caminho
            // Torneio → Categoria → Consulta já existe, e Restrict aqui travaria a exclusão
            // do torneio. O Postgres aceita caminho múltiplo de cascade sem reclamar.
            entity.HasOne(e => e.Torneio)
                .WithMany()
                .HasForeignKey(e => e.TorneioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Categoria)
                .WithMany()
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Clube>(entity =>
        {
            entity.HasOne(e => e.Cidade)
                .WithMany()
                .HasForeignKey(e => e.CidadeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<QuadraClube>(entity =>
        {
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Bar do clube ----
        //
        // Os campos *PorId (AbertaPorId, LancadoPorId, CanceladoPorId...) NÃO têm navegação e
        // por isso não viram chave estrangeira: são carimbo de auditoria, e nenhum deles pode
        // impedir que uma conta seja excluída depois (LGPD). Mesmo precedente de
        // ComentarioPerfil.DenunciadoPorId.
        modelBuilder.Entity<ProdutoBar>(entity =>
        {
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ClubeId, e.Ativo });
        });

        modelBuilder.Entity<Comanda>(entity =>
        {
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            // SetNull, não Cascade: sumir com a conta do jogador não pode levar junto a venda
            // do bar — o dinheiro entrou e o dono precisa continuar vendo isso no caixa. O
            // NomeCliente fica, e é por isso que ele é sempre preenchido mesmo quando veio de
            // um cadastro. (Precedente doloroso: o TRUNCATE CASCADE que apagou conta real.)
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Mesma razão: cancelar/apagar uma reserva não apaga o consumo do bar dela.
            entity.HasOne(e => e.MarcacaoJogo)
                .WithMany()
                .HasForeignKey(e => e.MarcacaoJogoId)
                .OnDelete(DeleteBehavior.SetNull);

            // Duas "comanda 7" no mesmo dia e no mesmo clube é confusão na hora de cobrar.
            // Com dois tablets no balcão isso acontece de verdade — o índice transforma a
            // corrida num erro que dá pra tratar (tenta o próximo número) em vez de num
            // número repetido que ninguém percebe.
            entity.HasIndex(e => new { e.ClubeId, e.DiaReferencia, e.Numero }).IsUnique();

            // A busca do balcão é sempre "as comandas abertas deste clube".
            entity.HasIndex(e => new { e.ClubeId, e.Status });
        });

        modelBuilder.Entity<ItemComanda>(entity =>
        {
            entity.HasOne(e => e.Comanda)
                .WithMany(c => c.Itens)
                .HasForeignKey(e => e.ComandaId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: produto que já foi vendido não pode ser apagado por baixo da comanda.
            // A tela nem oferece apagar — oferece INATIVAR, que é o certo (ProdutoBar.Ativo).
            entity.HasOne(e => e.ProdutoBar)
                .WithMany()
                .HasForeignKey(e => e.ProdutoBarId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CaixaDoDia>(entity =>
        {
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Um caixa por clube por dia — abrir o segundo é sempre engano.
            entity.HasIndex(e => new { e.ClubeId, e.Dia }).IsUnique();
        });

        modelBuilder.Entity<MovimentoEstoque>(entity =>
        {
            // Cascade: apagar o produto (coisa que a tela não oferece — ela INATIVA) levaria
            // junto o histórico dele, que só faz sentido junto.
            entity.HasOne(e => e.ProdutoBar)
                .WithMany()
                .HasForeignKey(e => e.ProdutoBarId)
                .OnDelete(DeleteBehavior.Cascade);

            // O saldo é sempre "todos os movimentos deste produto".
            entity.HasIndex(e => new { e.ProdutoBarId, e.CriadoEm });

            // Cancelar um item da comanda procura a baixa dele por aqui pra devolver a
            // unidade ao estoque.
            entity.HasIndex(e => e.ItemComandaId);
        });

        modelBuilder.Entity<LancamentoFinanceiro>(entity =>
        {
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            // A tela abre sempre no que está em aberto, ordenado por vencimento.
            entity.HasIndex(e => new { e.ClubeId, e.QuitadoEm, e.Vencimento });
        });
        modelBuilder.Entity<HorarioMarcacaoDisponivel>(entity =>
        {
            // Restrict em Clube: Clube alcançaria essa tabela por 2 caminhos (direto via
            // ClubeId, indireto via QuadraClube) — mesmo conflito de múltiplos caminhos de
            // cascade já visto com Jogador como raiz, aqui com Clube. QuadraClube cascateia
            // (primeiro/único caminho por ali), mesmo precedente de HorarioDisponivel
            // (ProfessorId Restrict, LocalAulaId Cascade).
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.QuadraClube)
                .WithMany()
                .HasForeignKey(e => e.QuadraClubeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MarcacaoJogo>(entity =>
        {
            // Mesmo motivo do bloco acima: Restrict em Clube, Cascade em QuadraClube.
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.QuadraClube)
                .WithMany()
                .HasForeignKey(e => e.QuadraClubeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade: único caminho a partir de Jogador (Clube.DonoId é Restrict, não
            // cascateia a partir de Jogador) — apagar a conta remove as marcações da pessoa.
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<JogadorCidade>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.CidadeId });

            entity.HasOne(e => e.Jogador)
                .WithMany(j => j.JogadorCidades)
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cidade)
                .WithMany()
                .HasForeignKey(e => e.CidadeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC079FE58FF8");

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nome)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Torneio).WithMany(p => p.Categorias)
                .HasForeignKey(d => d.TorneioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Categoria__Torne__5165187F");
        });

        modelBuilder.Entity<Dupla>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Dupla__3214EC07E192F96C");

            entity.ToTable("Dupla");

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Categoria).WithMany(p => p.Duplas)
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Dupla__Categoria__571DF1D5");

            entity.HasOne(d => d.Jogador1).WithMany(p => p.DuplaJogador1s)
                .HasForeignKey(d => d.Jogador1Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Dupla__Jogador1I__5812160E");

            entity.HasOne(d => d.Jogador2).WithMany(p => p.DuplaJogador2s)
                .HasForeignKey(d => d.Jogador2Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Dupla__Jogador2I__59063A47");
        });

        modelBuilder.Entity<Jogador>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Jogador__3214EC07E9B77CE2");

            entity.ToTable("Jogador");

            entity.HasIndex(e => e.Cpf, "UQ__Jogador__C1F897318C6002EF").IsUnique();

            entity.HasIndex(e => e.AgendaFeedToken).IsUnique();

            entity.HasIndex(e => e.Login).IsUnique();

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Cpf)
                .HasMaxLength(11)
                .IsUnicode(false)
                .HasColumnName("CPF");
            entity.Property(e => e.Login)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Nome)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.AceitaConvitesJogo).HasDefaultValue(true);
        });

        modelBuilder.Entity<Partida>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Partida__3214EC0755DC2078");

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.GamesDupla1).HasDefaultValue(0);
            entity.Property(e => e.GamesDupla2).HasDefaultValue(0);
            entity.Property(e => e.SetsDupla1).HasDefaultValue(0);
            entity.Property(e => e.SetsDupla2).HasDefaultValue(0);

            entity.HasOne(d => d.Categoria).WithMany(p => p.Partidas)
                .HasForeignKey(d => d.CategoriaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Partida__Categor__5FB337D6");

            entity.HasOne(d => d.Dupla1).WithMany(p => p.PartidasDupla1)
                .HasForeignKey(d => d.Dupla1Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Partida__Dupla1I__60A75C0F");

            entity.HasOne(d => d.Dupla2).WithMany(p => p.PartidasDupla2)
                .HasForeignKey(d => d.Dupla2Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Partida__Dupla2I__619B8048");
        });

        modelBuilder.Entity<Torneio>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Torneio__3214EC072B430D79");

            entity.ToTable("Torneio");

            entity.HasIndex(e => e.Codigo, "UQ__Torneio__06370DAC1187A52A").IsUnique();

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Nome)
                .HasMaxLength(150)
                .IsUnicode(false);

        });

        modelBuilder.Entity<Aula>(entity =>
        {
            entity.Property(e => e.Preco).HasPrecision(18, 2);
            entity.HasIndex(e => e.TokenConfirmacao).IsUnique();
            entity.Property(e => e.NomeAlunoAvulso).HasMaxLength(100);
            entity.Property(e => e.TelefoneAlunoAvulso).HasMaxLength(20);

            entity.HasOne(a => a.LocalAula)
                .WithMany()
                .HasForeignKey(a => a.LocalAulaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.Aluno)
                .WithMany(p => p.AulasRecebidas)
                .HasForeignKey(a => a.AlunoId)
                .IsRequired(false)
                .HasConstraintName("FK__Aula__AlunoId__114A936A");
        });

        modelBuilder.Entity<LocalAula>(entity =>
        {
            entity.Property(e => e.PrecoPadrao).HasPrecision(18, 2);
            entity.Property(e => e.PrecoDupla).HasPrecision(18, 2);
            entity.Property(e => e.PrecoTrio).HasPrecision(18, 2);
            entity.Property(e => e.CustoPorAula).HasPrecision(18, 2);

            entity.HasOne(l => l.Professor)
                .WithMany()
                .HasForeignKey(l => l.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PrecoDeAluno>(entity =>
        {
            entity.Property(e => e.Preco).HasPrecision(18, 2);
            entity.Property(e => e.NomeAvulso).HasMaxLength(100);
            entity.Property(e => e.Observacao).HasMaxLength(200);

            // Cascade pelo professor: o acordo é dele e não sobrevive à conta dele. O aluno
            // é Restrict porque são dois caminhos até Jogador — o mesmo conflito de sempre —
            // e porque apagar a conta do aluno não deve apagar o que o professor combinou.
            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Aluno)
                .WithMany()
                .HasForeignKey(e => e.AlunoId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // Um professor procura o preço pelos alunos dele o tempo todo (abrir o painel,
            // abrir a tela de marcar aula).
            entity.HasIndex(e => e.ProfessorId);
        });

        modelBuilder.Entity<PacoteDeAulas>(entity =>
        {
            entity.Property(e => e.Preco).HasPrecision(18, 2);

            // Cascade: pacote não existe sem o local. Apagado o local, a oferta some junto —
            // as aulas que ela gerou ficam, com o preço que já tinham.
            entity.HasOne(p => p.LocalAula)
                .WithMany(l => l.Pacotes)
                .HasForeignKey(p => p.LocalAulaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HorarioDisponivel>(entity =>
        {
            // Restrict aqui porque LocalAula já faz cascade a partir de Jogador — dois caminhos de
            // cascade até a mesma tabela não são permitidos pelo SQL Server.
            entity.HasOne(h => h.Professor)
                .WithMany()
                .HasForeignKey(h => h.ProfessorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.LocalAula)
                .WithMany(l => l.Horarios)
                .HasForeignKey(h => h.LocalAulaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JogadorCategoria>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.CategoriaPadraoId });

            entity.HasOne(e => e.Jogador)
                .WithMany(j => j.JogadorCategorias)
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CategoriaPadrao)
                .WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JogadorClube>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.ClubeId });

            entity.HasOne(e => e.Jogador)
                .WithMany(j => j.JogadorClubes)
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Desafios (espec em DESAFIOS.md) ----
        //
        // As três tabelas de escolha do anúncio morrem COM ele (Cascade): categoria, cidade e
        // clube escolhidos não têm vida própria fora do anúncio que os guardou.
        //
        // ⚠️ Já o resto é Restrict de propósito. `Desafio` aponta pra QUATRO jogadores, e deixar
        // os quatro em cascade daria quatro caminhos de exclusão pra mesma linha — além de
        // fazer o apagar de uma conta levar junto o resultado de um jogo que o adversário
        // jogou. Exclusão de conta aqui é `ExcluidoEm` (soft delete), não DELETE.
        modelBuilder.Entity<AnuncioDeDesafio>(entity =>
        {
            entity.HasOne(e => e.Jogador1)
                .WithMany()
                .HasForeignKey(e => e.Jogador1Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Jogador2)
                .WithMany()
                .HasForeignKey(e => e.Jogador2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // O mural filtra por "publicado e ainda dentro da semana" em toda visita.
            entity.HasIndex(e => new { e.Status, e.ValeAte });
        });

        modelBuilder.Entity<AnuncioDesafioCategoria>(entity =>
        {
            entity.HasKey(e => new { e.AnuncioDeDesafioId, e.CategoriaPadraoId });

            entity.HasOne(e => e.Anuncio)
                .WithMany(a => a.Categorias)
                .HasForeignKey(e => e.AnuncioDeDesafioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CategoriaPadrao)
                .WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnuncioDesafioCidade>(entity =>
        {
            entity.HasKey(e => new { e.AnuncioDeDesafioId, e.CidadeId });

            entity.HasOne(e => e.Anuncio)
                .WithMany(a => a.Cidades)
                .HasForeignKey(e => e.AnuncioDeDesafioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cidade)
                .WithMany()
                .HasForeignKey(e => e.CidadeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnuncioDesafioClube>(entity =>
        {
            entity.HasKey(e => new { e.AnuncioDeDesafioId, e.ClubeId });

            entity.HasOne(e => e.Anuncio)
                .WithMany(a => a.Clubes)
                .HasForeignKey(e => e.AnuncioDeDesafioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Desafio>(entity =>
        {
            // SetNull, nunca Cascade: o anúncio vence no domingo e o jogo pode ser na terça.
            // Apagar o anúncio levando junto o desafio aceito seria desmarcar um compromisso
            // de quatro pessoas sem ninguém pedir — a lição das 8 inscrições que sumiram.
            entity.HasOne(e => e.Anuncio)
                .WithMany()
                .HasForeignKey(e => e.AnuncioDeDesafioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DesafianteJogador1).WithMany()
                .HasForeignKey(e => e.DesafianteJogador1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DesafianteJogador2).WithMany()
                .HasForeignKey(e => e.DesafianteJogador2Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DesafiadoJogador1).WithMany()
                .HasForeignKey(e => e.DesafiadoJogador1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DesafiadoJogador2).WithMany()
                .HasForeignKey(e => e.DesafiadoJogador2Id).OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CategoriaPadrao).WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Clube).WithMany()
                .HasForeignKey(e => e.ClubeId).OnDelete(DeleteBehavior.Restrict);

            // "Meus desafios" pergunta por status e data em toda visita.
            entity.HasIndex(e => new { e.Status, e.DataHora });
        });

        modelBuilder.Entity<ReinadoNoCinturao>(entity =>
        {
            // Restrict pelo mesmo motivo do Desafio: apagar uma conta não pode levar junto o
            // reinado que o ADVERSÁRIO conquistou tomando o cinturão dela.
            entity.HasOne(e => e.CategoriaPadrao).WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Jogador1).WithMany()
                .HasForeignKey(e => e.Jogador1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Jogador2).WithMany()
                .HasForeignKey(e => e.Jogador2Id).OnDelete(DeleteBehavior.Restrict);

            // "Quem tem o cinturão desta categoria?" é a pergunta de toda tela do módulo.
            entity.HasIndex(e => new { e.CategoriaPadraoId, e.TerminouEm });
        });

        modelBuilder.Entity<ProfessorCidade>(entity =>
        {
            entity.HasKey(e => new { e.ProfessorId, e.CidadeId });

            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Cidade)
                .WithMany()
                .HasForeignKey(e => e.CidadeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JogadorDiaHorario>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.DiaSemana, e.Periodo });

            entity.HasOne(e => e.Jogador)
                .WithMany(j => j.JogadorDiasHorarios)
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AvisoJogo>(entity =>
        {
            entity.HasOne(e => e.Criador)
                .WithMany()
                .HasForeignKey(e => e.CriadorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CategoriaPadrao)
                .WithMany()
                .HasForeignKey(e => e.CategoriaPadraoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AvisoParceiro>(entity =>
        {
            entity.HasOne(e => e.Criador)
                .WithMany()
                .HasForeignKey(e => e.CriadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CandidaturaParceiro>(entity =>
        {
            entity.HasOne(e => e.AvisoParceiro)
                .WithMany(a => a.Candidaturas)
                .HasForeignKey(e => e.AvisoParceiroId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict aqui porque AvisoParceiro já faz cascade a partir de Jogador (Criador) —
            // um segundo caminho direto de Jogador até CandidaturaParceiro causaria o mesmo
            // conflito de múltiplos caminhos de cascade que já vimos em HorarioDisponivel.
            entity.HasOne(e => e.Candidato)
                .WithMany()
                .HasForeignKey(e => e.CandidatoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<JogoSemanal>(entity =>
        {
            entity.HasOne(e => e.Grupo)
                .WithMany()
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict nos 5 FKs de Jogador: GrupoPrivado já faz cascade a partir de Jogador
            // (Administrador), então um caminho direto Jogador -> JogoSemanal causaria o mesmo
            // conflito de múltiplos caminhos de cascade já visto antes.
            // Clube some do catálogo? O jogo continua no ranking do grupo, só perde o local.
            entity.HasOne(e => e.Clube)
                .WithMany()
                .HasForeignKey(e => e.ClubeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Dupla1Jogador1).WithMany().HasForeignKey(e => e.Dupla1Jogador1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Dupla1Jogador2).WithMany().HasForeignKey(e => e.Dupla1Jogador2Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Dupla2Jogador1).WithMany().HasForeignKey(e => e.Dupla2Jogador1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Dupla2Jogador2).WithMany().HasForeignKey(e => e.Dupla2Jogador2Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RegistradoPor).WithMany().HasForeignKey(e => e.RegistradoPorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessaoGrupo>(entity =>
        {
            entity.HasIndex(e => new { e.GrupoId, e.DataHora }).IsUnique();

            entity.HasOne(e => e.Grupo)
                .WithMany()
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConfirmacaoSessao>(entity =>
        {
            entity.HasKey(e => new { e.SessaoId, e.JogadorId });

            entity.HasOne(e => e.Sessao)
                .WithMany(s => s.Confirmacoes)
                .HasForeignKey(e => e.SessaoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: Jogador -> GrupoPrivado (Administrador) -> SessaoGrupo -> ConfirmacaoSessao já
            // cascateia por esse caminho; um segundo caminho direto de Jogador causaria o mesmo
            // conflito de múltiplos caminhos de cascade já visto em JogoSemanal/JogadorGrupo.
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MensalidadeGrupo>(entity =>
        {
            entity.HasKey(e => new { e.GrupoId, e.JogadorId, e.Ano, e.Mes });

            entity.HasOne(e => e.Grupo)
                .WithMany()
                .HasForeignKey(e => e.GrupoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PalpitePartida>(entity =>
        {
            entity.HasIndex(e => new { e.PartidaId, e.JogadorId }).IsUnique();

            entity.HasOne(e => e.Partida)
                .WithMany()
                .HasForeignKey(e => e.PartidaId);

            // Restrict em Jogador e DuplaEscolhida: Partida já cascadeia até Categoria/Torneio, e
            // Dupla também chega no mesmo Torneio por outro caminho — deixar as 3 FKs cascateando
            // ao mesmo tempo dispara o erro de "multiple cascade paths" do SQL Server (mesma regra
            // já usada em JogoSemanal/CandidaturaParceiro acima).
            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DuplaEscolhida)
                .WithMany()
                .HasForeignKey(e => e.DuplaEscolhidaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PushSubscriptionJogador>(entity =>
        {
            entity.HasIndex(e => e.Endpoint).IsUnique();

            entity.HasOne(e => e.Jogador)
                .WithMany()
                .HasForeignKey(e => e.JogadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
