﻿using CorporateSystem.Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CorporateSystem.Auth.Infrastructure;

public class DataContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public DataContext()
    {
    }
    
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.Password).IsRequired();
            entity.Property(u => u.Role).IsRequired();
            entity.Property(u => u.FirstName).IsRequired();
            entity.Property(u => u.LastName).IsRequired();
            entity.Property(u => u.Gender).IsRequired();
            
            entity.HasIndex(u => u.Gender).HasDatabaseName("IX_Users_Gender");
        });
    }
}