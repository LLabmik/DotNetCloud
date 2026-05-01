using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Contacts.Data.Configuration;
using DotNetCloud.Modules.Contacts.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Contacts.Data;

/// <summary>
/// Database context for the Contacts module.
/// Manages all contact entities: contacts, groups, emails, phones, addresses, custom fields, and shares.
/// </summary>
public class ContactsDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsDbContext"/> class.
    /// </summary>
    public ContactsDbContext(DbContextOptions<ContactsDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactsDbContext"/> class with a specific naming strategy.
    /// </summary>
    public ContactsDbContext(DbContextOptions<ContactsDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Contact records.</summary>
    public DbSet<Contact> Contacts => Set<Contact>();

    /// <summary>Contact email addresses.</summary>
    public DbSet<ContactEmail> ContactEmails => Set<ContactEmail>();

    /// <summary>Contact phone numbers.</summary>
    public DbSet<ContactPhone> ContactPhones => Set<ContactPhone>();

    /// <summary>Contact postal addresses.</summary>
    public DbSet<ContactAddress> ContactAddresses => Set<ContactAddress>();

    /// <summary>Contact custom key-value fields.</summary>
    public DbSet<ContactCustomField> ContactCustomFields => Set<ContactCustomField>();

    /// <summary>Contact groups.</summary>
    public DbSet<ContactGroup> ContactGroups => Set<ContactGroup>();

    /// <summary>Contact-group memberships (join table).</summary>
    public DbSet<ContactGroupMember> ContactGroupMembers => Set<ContactGroupMember>();

    /// <summary>Contact sharing grants.</summary>
    public DbSet<ContactShare> ContactShares => Set<ContactShare>();

    /// <summary>Contact file attachments (including avatars).</summary>
    public DbSet<ContactAttachment> ContactAttachments => Set<ContactAttachment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("contacts"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ContactConfiguration());
        modelBuilder.ApplyConfiguration(new ContactEmailConfiguration());
        modelBuilder.ApplyConfiguration(new ContactPhoneConfiguration());
        modelBuilder.ApplyConfiguration(new ContactAddressConfiguration());
        modelBuilder.ApplyConfiguration(new ContactCustomFieldConfiguration());
        modelBuilder.ApplyConfiguration(new ContactGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ContactGroupMemberConfiguration());
        modelBuilder.ApplyConfiguration(new ContactShareConfiguration());
        modelBuilder.ApplyConfiguration(new ContactAttachmentConfiguration());
    }
}
