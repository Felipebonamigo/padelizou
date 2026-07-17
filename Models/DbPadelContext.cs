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
        modelBuilder.Entity<JogadorGrupo>(entity =>
        {
            entity.HasKey(e => new { e.JogadorId, e.GrupoId });
        });
        modelBuilder.Entity<TorneioOrganizador>()
        .HasKey(to => new { to.TorneioId, to.JogadorId });
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

            entity.Property(e => e.Codigo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Cpf)
                .HasMaxLength(11)
                .IsUnicode(false)
                .HasColumnName("CPF");
            entity.Property(e => e.Nome)
                .HasMaxLength(100)
                .IsUnicode(false);
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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
