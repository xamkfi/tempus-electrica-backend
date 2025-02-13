using Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace Infrastructure.Data
{
    public class ElectricityDbContext : DbContext
    {
        public DbSet<ElectricityPriceData> ElectricityPriceDatas { get; set; }

        public ElectricityDbContext(DbContextOptions<ElectricityDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ElectricityPriceData>(entity =>
            {
                entity.Property(e => e.Price)
                      .HasPrecision(18, 2); // Set precision and scale
            });

            // Define indexes on StartDate and EndDate
            modelBuilder.Entity<ElectricityPriceData>()
                        .HasIndex(e => e.StartDate)
                        .HasDatabaseName("IX_ElectricityPriceData_StartDate");

            modelBuilder.Entity<ElectricityPriceData>()
                        .HasIndex(e => e.EndDate)
                        .HasDatabaseName("IX_ElectricityPriceData_EndDate");

            // Optional: Create a composite index if queries often filter by both StartDate and EndDate
            modelBuilder.Entity<ElectricityPriceData>()
                        .HasIndex(e => new { e.StartDate, e.EndDate })
                        .HasDatabaseName("IX_ElectricityPriceData_StartEndDate");
        }


        public override int SaveChanges()
        {
            AddTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void AddTimestamps()
        {
            var entities = ChangeTracker.Entries()
                .Where(x => x.Entity is BaseEntity
                && (x.State == EntityState.Modified));

            var now = DateTime.Now;

            foreach (var entity in entities)
            {
                var baseEntity = (BaseEntity)entity.Entity;

                baseEntity.UpdatedAt = now;
            }
        }

    }
}