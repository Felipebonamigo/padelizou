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

    public virtual DbSet<Organizador> Organizadores { get; set; }

    public virtual DbSet<Partida> Partidas { get; set; }

    public virtual DbSet<Torneio> Torneios { get; set; }
    public virtual DbSet<Aula> Aulas { get; set; }
    public virtual DbSet<CategoriaPadrao> CategoriasPadrao { get; set; }
    public virtual DbSet<TorneioOrganizador> TorneioOrganizadores { get; set; }
    public DbSet<Clube> Clubes { get; set; }
    public DbSet<Time> Times { get; set; }
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

        modelBuilder.Entity<Organizador>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Organiza__3214EC079F755F08");

            entity.ToTable("Organizador");

            entity.HasIndex(e => e.Codigo, "UQ__Organiza__06370DAC46EF791B").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Organiza__A9D10534817DC0DF").IsUnique();

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Nome)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.SenhaHash)
                .HasMaxLength(255)
                .IsUnicode(false);
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

            entity.HasOne(d => d.Organizador).WithMany(p => p.Torneios)
                .HasForeignKey(d => d.OrganizadorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Torneio__Organiz__4E88ABD4");
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
