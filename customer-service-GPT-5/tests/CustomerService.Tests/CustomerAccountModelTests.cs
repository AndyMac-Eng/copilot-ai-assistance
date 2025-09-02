using CustomerService.Models;
using FluentAssertions;
using Xunit;

public class CustomerAccountModelTests
{
    [Fact]
    public void Defaults_Set_AsExpected()
    {
        var acct = new CustomerAccount { Email = "x@test.com", PasswordHash = "hash" };
        acct.Preferences.ThemeMode.Should().Be("light");
    }

    [Fact]
    public void With_Update_Changes_ThemeMode()
    {
        var acct = new CustomerAccount { Email = "x@test.com", PasswordHash = "hash" };
        var updated = acct with { Preferences = acct.Preferences with { ThemeMode = "dark" } };
        updated.Preferences.ThemeMode.Should().Be("dark");
        acct.Preferences.ThemeMode.Should().Be("light"); // immutability
    }
}
