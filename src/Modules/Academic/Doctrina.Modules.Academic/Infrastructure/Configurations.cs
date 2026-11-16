using Doctrina.Modules.Academic.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Doctrina.Modules.Academic.Infrastructure;

// Keys, ExternalId/Source and the unique (Source, ExternalId) index come from ModuleDbContext.
// All relationships use Restrict: records are never hard-deleted (status ToBeDeleted instead).

internal static class Lengths
{
    public const int Name = 200;
    public const int Short = 50;
    public const int Description = 4000;
    public const int Enum = 32;
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(o => o.Name).HasMaxLength(Lengths.Name);
        builder.Property(o => o.ShortName).HasMaxLength(Lengths.Short);
        builder.Property(o => o.OrganizationType).HasConversion<string>().HasMaxLength(Lengths.Enum);
        builder.HasOne(o => o.Parent).WithMany().HasForeignKey(o => o.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AcademicSessionConfiguration : IEntityTypeConfiguration<AcademicSession>
{
    public void Configure(EntityTypeBuilder<AcademicSession> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(Lengths.Name);
        builder.Property(s => s.AcademicSessionType).HasConversion<string>().HasMaxLength(Lengths.Enum);
        builder.HasOne(s => s.Parent).WithMany().HasForeignKey(s => s.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(Lengths.Name);
        builder.Property(c => c.Abbreviation).HasMaxLength(Lengths.Short);
        builder.Property(c => c.Description).HasMaxLength(Lengths.Description);
        builder.Property(c => c.StudyLoad).HasPrecision(5, 1);
        builder.HasOne(c => c.Organization).WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseOfferingConfiguration : IEntityTypeConfiguration<CourseOffering>
{
    public void Configure(EntityTypeBuilder<CourseOffering> builder)
    {
        builder.Property(o => o.Name).HasMaxLength(Lengths.Name);
        builder.HasOne(o => o.Course).WithMany(c => c.Offerings).HasForeignKey(o => o.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.AcademicSession).WithMany().HasForeignKey(o => o.AcademicSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.Property(p => p.GivenName).HasMaxLength(Lengths.Name);
        builder.Property(p => p.SurnamePrefix).HasMaxLength(Lengths.Short);
        builder.Property(p => p.Surname).HasMaxLength(Lengths.Name);
        builder.Property(p => p.DisplayName).HasMaxLength(Lengths.Name);
        builder.Property(p => p.Mail).HasMaxLength(Lengths.Name);
        builder.Ignore(p => p.FullName);
        builder.HasMany(p => p.Logins).WithOne().HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssociationConfiguration : IEntityTypeConfiguration<Association>
{
    public void Configure(EntityTypeBuilder<Association> builder)
    {
        builder.Property(a => a.Role).HasConversion<string>().HasMaxLength(Lengths.Enum);
        builder.HasOne(a => a.Person).WithMany(p => p.Associations).HasForeignKey(a => a.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.CourseOffering).WithMany(o => o.Associations).HasForeignKey(a => a.CourseOfferingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.PersonId, a.CourseOfferingId, a.Role }).IsUnique();
    }
}

internal sealed class PersonLoginConfiguration : IEntityTypeConfiguration<PersonLogin>
{
    public void Configure(EntityTypeBuilder<PersonLogin> builder)
    {
        builder.Property(l => l.Issuer).HasMaxLength(PersonLogin.IssuerMaxLength);
        builder.Property(l => l.Oid).HasMaxLength(PersonLogin.OidMaxLength);
        builder.HasIndex(l => new { l.Issuer, l.Oid }).IsUnique();
    }
}

internal sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(Lengths.Enum);
        builder.Property(j => j.Source).HasMaxLength(Doctrina.Core.Entities.ExchangeableEntity.SourceMaxLength);
        builder.Property(j => j.StartedBy).HasMaxLength(ImportJob.StartedByMaxLength);
        builder.Property(j => j.Message).HasMaxLength(ImportJob.MessageMaxLength);
        builder.Property(j => j.Warnings).HasMaxLength(ImportJob.MessageMaxLength);
        builder.Property(j => j.UploadName).HasMaxLength(ImportJob.NameMaxLength);
        builder.Property(j => j.UploadType).HasMaxLength(Lengths.Enum);
        builder.Ignore(j => j.IsFinished);
        builder.HasIndex(j => j.CreatedAt);
        builder.HasMany<ImportJobRow>().WithOne().HasForeignKey(r => r.ImportJobId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ImportJobRowConfiguration : IEntityTypeConfiguration<ImportJobRow>
{
    public void Configure(EntityTypeBuilder<ImportJobRow> builder)
    {
        builder.Property(r => r.File).HasMaxLength(ImportJobRow.FileMaxLength);
        builder.Property(r => r.Key).HasMaxLength(Doctrina.Core.Entities.ExchangeableEntity.ExternalIdMaxLength);
        builder.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(Lengths.Enum);
        builder.Property(r => r.Code).HasMaxLength(Lengths.Name);
        builder.Property(r => r.Message).HasMaxLength(ImportJobRow.MessageMaxLength);
        builder.HasIndex(r => new { r.ImportJobId, r.File, r.Line });
    }
}
