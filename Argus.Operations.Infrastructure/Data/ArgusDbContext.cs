using Argus.Operations.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Argus.Operations.Infrastructure.Data;

public class ArgusDbContext : DbContext
{
    public ArgusDbContext(DbContextOptions<ArgusDbContext> options)
        : base(options)
    {
    }

    public DbSet<Brigadista> Brigadistas { get; set; }
    public DbSet<Ocorrencia> Ocorrencias { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ===== Configuração da entidade Brigadista =====
        modelBuilder.Entity<Brigadista>(entity =>
        {
            entity.ToTable("BRIGADISTA");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).ValueGeneratedOnAdd();
            entity.Property(b => b.Nome).IsRequired().HasMaxLength(150);
            entity.Property(b => b.Matricula).IsRequired().HasMaxLength(20);
            entity.Property(b => b.Email).IsRequired().HasMaxLength(150);
            entity.Property(b => b.Telefone).HasMaxLength(20);
            entity.Property(b => b.Funcao).HasMaxLength(50);
            entity.Property(b => b.Ativo).IsRequired();
            entity.Property(b => b.DataAdmissao).IsRequired();
        });

        // ===== Configuração da entidade Ocorrencia =====
        modelBuilder.Entity<Ocorrencia>(entity =>
        {
            entity.ToTable("OCORRENCIA");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();
            entity.Property(o => o.Descricao).IsRequired().HasMaxLength(500);
            entity.Property(o => o.Latitude).IsRequired();
            entity.Property(o => o.Longitude).IsRequired();
            entity.Property(o => o.Status).IsRequired().HasConversion<int>();
            entity.Property(o => o.DataAbertura).IsRequired();

            // Relacionamento: uma Ocorrência pertence a um Brigadista
            entity.HasOne(o => o.Brigadista)
                  .WithMany()
                  .HasForeignKey(o => o.BrigadistaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}