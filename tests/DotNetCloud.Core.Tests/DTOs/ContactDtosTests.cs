namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Contact DTOs.
/// </summary>
[TestClass]
public class ContactDtosTests
{
    [TestMethod]
    public void ContactDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var contact = new ContactDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            ContactType = ContactType.Person,
            DisplayName = "Jane Doe",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, contact.Id);
        Assert.AreEqual("Jane Doe", contact.DisplayName);
        Assert.AreEqual(ContactType.Person, contact.ContactType);
        Assert.IsFalse(contact.IsDeleted);
    }

    [TestMethod]
    public void ContactDto_OptionalFields_DefaultToNull()
    {
        // Arrange & Act
        var contact = new ContactDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            ContactType = ContactType.Person,
            DisplayName = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNull(contact.FirstName);
        Assert.IsNull(contact.LastName);
        Assert.IsNull(contact.Organization);
        Assert.IsNull(contact.JobTitle);
        Assert.IsNull(contact.AvatarUrl);
        Assert.IsNull(contact.Birthday);
        Assert.IsNull(contact.ETag);
    }

    [TestMethod]
    public void ContactDto_Collections_DefaultToEmpty()
    {
        // Arrange & Act
        var contact = new ContactDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            ContactType = ContactType.Person,
            DisplayName = "Test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual(0, contact.Emails.Count);
        Assert.AreEqual(0, contact.PhoneNumbers.Count);
        Assert.AreEqual(0, contact.Addresses.Count);
        Assert.AreEqual(0, contact.GroupIds.Count);
        Assert.AreEqual(0, contact.CustomFields.Count);
    }

    [TestMethod]
    public void ContactType_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(ContactType));

        // Assert
        Assert.AreEqual(3, values.Length);
    }

    [TestMethod]
    public void ContactEmailDto_HasRequiredProperties()
    {
        // Arrange & Act
        var email = new ContactEmailDto
        {
            Address = "jane@example.com",
            Label = "work",
            IsPrimary = true
        };

        // Assert
        Assert.AreEqual("jane@example.com", email.Address);
        Assert.AreEqual("work", email.Label);
        Assert.IsTrue(email.IsPrimary);
    }

    [TestMethod]
    public void ContactEmailDto_Label_DefaultsToOther()
    {
        // Arrange & Act
        var email = new ContactEmailDto { Address = "test@example.com" };

        // Assert
        Assert.AreEqual("other", email.Label);
    }

    [TestMethod]
    public void ContactPhoneDto_HasRequiredProperties()
    {
        // Arrange & Act
        var phone = new ContactPhoneDto
        {
            Number = "+1-555-0100",
            Label = "mobile",
            IsPrimary = true
        };

        // Assert
        Assert.AreEqual("+1-555-0100", phone.Number);
        Assert.AreEqual("mobile", phone.Label);
    }

    [TestMethod]
    public void ContactAddressDto_HasAllFields()
    {
        // Arrange & Act
        var address = new ContactAddressDto
        {
            Label = "home",
            Street = "123 Main St",
            City = "Springfield",
            Region = "IL",
            PostalCode = "62701",
            Country = "US",
            IsPrimary = true
        };

        // Assert
        Assert.AreEqual("home", address.Label);
        Assert.AreEqual("123 Main St", address.Street);
        Assert.AreEqual("Springfield", address.City);
        Assert.AreEqual("US", address.Country);
    }

    [TestMethod]
    public void ContactGroupDto_HasRequiredProperties()
    {
        // Arrange & Act
        var group = new ContactGroupDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Name = "Family",
            MemberCount = 5,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Family", group.Name);
        Assert.AreEqual(5, group.MemberCount);
    }

    [TestMethod]
    public void CreateContactDto_HasRequiredProperties()
    {
        // Arrange & Act
        var dto = new CreateContactDto
        {
            ContactType = ContactType.Organization,
            DisplayName = "Acme Corp"
        };

        // Assert
        Assert.AreEqual(ContactType.Organization, dto.ContactType);
        Assert.AreEqual("Acme Corp", dto.DisplayName);
        Assert.AreEqual(0, dto.Emails.Count);
    }

    [TestMethod]
    public void UpdateContactDto_AllFields_AreNullable()
    {
        // Arrange & Act
        var dto = new UpdateContactDto();

        // Assert
        Assert.IsNull(dto.DisplayName);
        Assert.IsNull(dto.FirstName);
        Assert.IsNull(dto.LastName);
        Assert.IsNull(dto.Organization);
        Assert.IsNull(dto.Emails);
        Assert.IsNull(dto.PhoneNumbers);
        Assert.IsNull(dto.Addresses);
    }

    [TestMethod]
    public void ContactDto_IsImmutableRecord()
    {
        // Arrange
        var contact = new ContactDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            ContactType = ContactType.Person,
            DisplayName = "Original",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act — with-expression creates a new instance
        var updated = contact with { DisplayName = "Updated" };

        // Assert
        Assert.AreEqual("Original", contact.DisplayName);
        Assert.AreEqual("Updated", updated.DisplayName);
        Assert.AreEqual(contact.Id, updated.Id);
    }
}
