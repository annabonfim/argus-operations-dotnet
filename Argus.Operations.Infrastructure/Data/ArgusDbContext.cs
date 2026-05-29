using Argus.Operations.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.Infrastructure.Data;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options)
        : base(options)
    {
    }

    // ===== DbSets (cada um vira uma tabela) =====
    public DbSet<Brigada> Brigadas { get; set; }
    public DbSet<Brigadista> Brigadistas { get; set; }
    public DbSet<Recurso> Recursos { get; set; }
    public DbSet<Ocorrencia> Ocorrencias { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<RegistroCampo> RegistrosCampo { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===================== Compatibilidade Oracle pre-23ai =====================
        // Mapeia bool C# como NUMBER(1) no Oracle (0 = false, 1 = true)
        // O tipo BOOLEAN SQL só existe no Oracle 23ai; o FIAP usa versão anterior
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(bool) || property.ClrType == typeof(bool?))
                {
                    property.SetColumnType("NUMBER(1)");
                }
            }
        }

        // ===================== BRIGADA =====================
        modelBuilder.Entity<Brigada>(entity =>
        {
            entity.ToTable("BRIGADA");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).HasColumnName("ID_BRIGADA").ValueGeneratedOnAdd();
            entity.Property(b => b.Nome).HasColumnName("NOME").IsRequired().HasMaxLength(150);
            entity.Property(b => b.BaseOperacional).HasColumnName("BASE_OPERACIONAL").IsRequired().HasMaxLength(150);
            entity.Property(b => b.Telefone).HasColumnName("TELEFONE").HasMaxLength(20);
            entity.Property(b => b.Ativa).HasColumnName("ATIVA").IsRequired();
        });

        // ===================== BRIGADISTA =====================
        modelBuilder.Entity<Brigadista>(entity =>
        {
            entity.ToTable("BRIGADISTA");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).HasColumnName("ID_BRIGADISTA").ValueGeneratedOnAdd();
            entity.Property(b => b.Nome).HasColumnName("NOME").IsRequired().HasMaxLength(150);
            entity.Property(b => b.Matricula).HasColumnName("MATRICULA").IsRequired().HasMaxLength(20);
            entity.Property(b => b.Email).HasColumnName("EMAIL").IsRequired().HasMaxLength(150);
            entity.Property(b => b.Telefone).HasColumnName("TELEFONE").HasMaxLength(20);
            entity.Property(b => b.Funcao).HasColumnName("FUNCAO").HasMaxLength(50);
            entity.Property(b => b.Ativo).HasColumnName("ATIVO").IsRequired();
            entity.Property(b => b.DataAdmissao).HasColumnName("DATA_ADMISSAO").IsRequired();
            entity.Property(b => b.BrigadaId).HasColumnName("ID_BRIGADA").IsRequired();

            entity.HasOne(b => b.Brigada)
                  .WithMany()
                  .HasForeignKey(b => b.BrigadaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ===================== RECURSO =====================
        modelBuilder.Entity<Recurso>(entity =>
        {
            entity.ToTable("RECURSO");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("ID_RECURSO").ValueGeneratedOnAdd();
            entity.Property(r => r.Nome).HasColumnName("NOME").IsRequired().HasMaxLength(150);
            entity.Property(r => r.Tipo).HasColumnName("TIPO").IsRequired().HasConversion<int>();
            entity.Property(r => r.Disponivel).HasColumnName("DISPONIVEL").IsRequired();
            entity.Property(r => r.BrigadaId).HasColumnName("ID_BRIGADA").IsRequired();

            entity.HasOne(r => r.Brigada)
                  .WithMany()
                  .HasForeignKey(r => r.BrigadaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ===================== OCORRENCIA =====================
        modelBuilder.Entity<Ocorrencia>(entity =>
        {
            entity.ToTable("OCORRENCIA");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).HasColumnName("ID_OCORRENCIA").ValueGeneratedOnAdd();
            entity.Property(o => o.Descricao).HasColumnName("DESCRICAO").IsRequired().HasMaxLength(500);
            entity.Property(o => o.Latitude).HasColumnName("LATITUDE").IsRequired();
            entity.Property(o => o.Longitude).HasColumnName("LONGITUDE").IsRequired();
            entity.Property(o => o.Status).HasColumnName("STATUS").IsRequired().HasConversion<int>();
            entity.Property(o => o.DataAbertura).HasColumnName("DATA_ABERTURA").IsRequired();
            entity.Property(o => o.DataFinalizacao).HasColumnName("DATA_FINALIZACAO");
            entity.Property(o => o.BrigadistaId).HasColumnName("ID_BRIGADISTA").IsRequired();
            entity.Property(o => o.BrigadaId).HasColumnName("ID_BRIGADA").IsRequired();

            // FK cross-domain (Java) — só a coluna, SEM constraint formal
            // Constraint será adicionada via ALTER TABLE no script consolidado
            entity.Property(o => o.AlertaId).HasColumnName("ID_ALERTA");

            entity.HasOne(o => o.Brigadista)
                  .WithMany()
                  .HasForeignKey(o => o.BrigadistaId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.Brigada)
                  .WithMany()
                  .HasForeignKey(o => o.BrigadaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ===================== USUARIO =====================
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("USUARIO");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("ID_USUARIO").ValueGeneratedOnAdd();
            entity.Property(u => u.Nome).HasColumnName("NOME").IsRequired().HasMaxLength(150);
            entity.Property(u => u.Email).HasColumnName("EMAIL").IsRequired().HasMaxLength(150);
            entity.Property(u => u.SenhaHash).HasColumnName("SENHA_HASH").IsRequired().HasMaxLength(255);
            entity.Property(u => u.Perfil).HasColumnName("PERFIL").IsRequired().HasConversion<int>();
            entity.Property(u => u.Ativo).HasColumnName("ATIVO").IsRequired();
            entity.Property(u => u.DataCriacao).HasColumnName("DATA_CRIACAO").IsRequired();
            entity.Property(u => u.UltimoLogin).HasColumnName("ULTIMO_LOGIN");

            // Email único (não pode ter dois usuários com o mesmo email)
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // ===================== REGISTRO_CAMPO =====================
        modelBuilder.Entity<RegistroCampo>(entity =>
        {
            entity.ToTable("REGISTRO_CAMPO");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("ID_REGISTRO_CAMPO").ValueGeneratedOnAdd();
            entity.Property(r => r.Observacao).HasColumnName("OBSERVACAO").HasMaxLength(1000);
            entity.Property(r => r.UrlFoto).HasColumnName("URL_FOTO").HasMaxLength(500);
            entity.Property(r => r.Latitude).HasColumnName("LATITUDE").IsRequired();
            entity.Property(r => r.Longitude).HasColumnName("LONGITUDE").IsRequired();
            entity.Property(r => r.DataRegistro).HasColumnName("DATA_REGISTRO").IsRequired();
            entity.Property(r => r.OcorrenciaId).HasColumnName("ID_OCORRENCIA").IsRequired();

            entity.HasOne(r => r.Ocorrencia)
                  .WithMany()
                  .HasForeignKey(r => r.OcorrenciaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}