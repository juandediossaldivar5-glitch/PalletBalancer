using Microsoft.EntityFrameworkCore;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item>     Items     => Set<Item>();
    public DbSet<Fdo>      Fdos      => Set<Fdo>();
    public DbSet<FdoLinea> FdoLineas => Set<FdoLinea>();
    public DbSet<Usuario>  Usuarios  => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
