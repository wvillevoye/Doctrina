using Doctrina.Infrastructure.Persistence;
using Doctrina.Modules.Academic.Domain;
using Microsoft.EntityFrameworkCore;

namespace Doctrina.Modules.Academic.Infrastructure;

/// <summary>Database context of the Academic module (schema <c>academic</c>).</summary>
public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options) : ModuleDbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Association> Associations => Set<Association>();
    public DbSet<PersonLogin> PersonLogins => Set<PersonLogin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>Design-time factory for <c>dotnet ef</c>.</summary>
public sealed class AcademicDbContextFactory() : ModuleDesignTimeDbContextFactory<AcademicDbContext>(AcademicModule.ModuleName);
