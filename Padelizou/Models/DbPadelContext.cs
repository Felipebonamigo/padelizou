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
    public DbSet<SolicitacaoRegistroResultados> SolicitacoesRegistroResultados { get; set; }
    public DbSet<LocalAula> LocaisAula { get; set; }
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
    public DbSet<QuadraClube> QuadrasClube { get; set; }
    public DbSet<HorarioMarcacaoDisponivel> HorariosMarcacaoDisponivel { get; set; }
    public DbSet<MarcacaoJogo> MarcacoesJogo { get; set; }

    // Bar do clube: cardápio, comandas e o caixa do dia.
    public DbSet<ProdutoBar> ProdutosBar { get; set; }
    public DbSet<Comanda> Comandas { get; set; }
    public DbSet<ItemComanda> ItensComanda { get; set; }
    public DbSet<CaixaDoDia> CaixasDoDia { get; set; }
    public DbSet<JogadorCidade> JogadorCidades { get; set; }
    public DbSet<Pagamento> Pagamentos { get; set; }
    public DbSet<Elogio> Elogios { get; set; }
    public DbSet<ComentarioPerfil> ComentariosPerfil { get; set; }
    public DbSet<FeedbackSite> FeedbacksSite { get; set; }
    public DbSet<AvaliacaoProfessor> AvaliacoesProfessor { get; set; }
    public DbSet<AnotacaoAula> AnotacoesAula { get; set; }
    public DbSet<AlertaSistema> AlertasSistema { get; set; }

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
        modelBuilder.Entity<Elogio>(entity =>
        {
            // Impede o mesmo jogador dar o mesmo tipo de elogio 2x pra mesma pessoa.
            entity.HasIndex(e => new { e.DeJogadorId, e.ParaJogadorId, e.Tipo }).IsUnique();

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
            entity.Property(e => e.CustoPorAula).HasPrecision(18, 2);
            entity.Property(e => e.PacotePreco).HasPrecision(18, 2);

            entity.HasOne(l => l.Professor)
                .WithMany()
                .HasForeignKey(l => l.ProfessorId)
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
