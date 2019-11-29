using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Traktor.Core.Domain;

namespace Traktor.Core.Data
{
    public class LibraryDbContext : DbContext
    {
        public DbSet<Media> Media { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=Library.db", options =>
            {
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Movie>().HasBaseType<Media>();
            modelBuilder.Entity<Episode>().HasBaseType<Media>();

            modelBuilder.Entity<Media>().Property(x => x.Genres).HasConversion(v => string.Join(",", v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            modelBuilder.Entity<Media>().Property(x => x.Magnet).HasConversion(v => v.ToString(), v => new Uri(v));
            modelBuilder.Entity<Media>().Property(x => x.RelativePath).HasConversion(v => string.Join(",", v), v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));

            modelBuilder.Entity<Episode>().Property<int?>("ShowTrakt");
            modelBuilder.Entity<Episode>().Property<string>("ShowSlug");
            modelBuilder.Entity<Episode>().Property<int?>("ShowTVDB");
            modelBuilder.Entity<Episode>().Property<string>("ShowIMDB");
            modelBuilder.Entity<Episode>().Property<int?>("ShowTMDB");

        }
    }
}
