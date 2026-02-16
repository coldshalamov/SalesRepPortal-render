using LeadManagementPortal.Models;

namespace LeadManagementPortal.Services
{
    public interface IAddressService
    {
        Task<List<AddressSuggestion>> AutocompleteAsync(string search, string? city = null, string? state = null);
        Task<ValidatedAddress?> ValidateAsync(string street, string? city, string? state, string? zip);
    }

    public class AddressSuggestion
    {
        public string Text { get; set; } = string.Empty;
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
    }

    public class ValidatedAddress
    {
        public string Street1 { get; set; } = string.Empty;
        public string? Street2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Zip5 { get; set; } = string.Empty;
        public string? Zip4 { get; set; }
    }
}
