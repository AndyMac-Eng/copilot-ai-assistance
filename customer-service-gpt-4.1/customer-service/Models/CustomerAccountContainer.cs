using System.Collections.Generic;

namespace CustomerService.Models
{
    public class CustomerAccountContainer
    {
        public string Id { get; set; } = "CustomerAccounts";
        public List<CustomerAccount> Accounts { get; set; } = new List<CustomerAccount>();
    }
}
