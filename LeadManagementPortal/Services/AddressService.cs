using System.Net.Http.Json;
using LeadManagementPortal.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace LeadManagementPortal.Services
{
    public class AddressService : IAddressService
    {
        private readonly HttpClient _http;
        private readonly SmartyStreetsOptions _options;
        private readonly ILogger<AddressService> _logger;

        public AddressService(IHttpClientFactory httpFactory, IOptions<SmartyStreetsOptions> options, ILogger<AddressService> logger)
        {
            _http = httpFactory.CreateClient();
            _options = options.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_options.Referer))
            {
                _http.DefaultRequestHeaders.Add("Referer", _options.Referer);
            }
        }

        public async Task<List<AddressSuggestion>> AutocompleteAsync(string search, string? city = null, string? state = null)
        {
            var list = new List<AddressSuggestion>();
            
            if (string.IsNullOrWhiteSpace(_options.SmartyStreetUrl) || string.IsNullOrWhiteSpace(_options.SmartyStreetKey) || string.IsNullOrWhiteSpace(search))
                return list;

             var url = string.Format(_options.SmartyStreetUrl, Uri.EscapeDataString(_options.SmartyStreetKey), Uri.EscapeDataString(search));
             if (!string.IsNullOrWhiteSpace(city)) url += $"&city={Uri.EscapeDataString(city)}";
             if (!string.IsNullOrWhiteSpace(state)) url += $"&state={Uri.EscapeDataString(state)}";
             
             try
             {
                 var resp = await _http.GetFromJsonAsync<AutocompleteResponse>(url);
                 if (resp?.suggestions != null)
                 {
                     list = resp.suggestions.Select(s => new AddressSuggestion
                     {
                         Text = $"{s.street_line} {s.secondary}".Trim(),
                         City = s.city,
                         State = s.state,
                         Zip = s.zipcode
                     }).ToList();
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error calling SmartyStreets Autocomplete API");
             }
             return list;
        }

        public async Task<ValidatedAddress?> ValidateAsync(string street, string? city, string? state, string? zip)
        {
            if (string.IsNullOrWhiteSpace(_options.SmartyStreetKey))
                return null;

            var url = $"https://us-street.api.smartystreets.com/street-address?key={Uri.EscapeDataString(_options.SmartyStreetKey)}&street={Uri.EscapeDataString(street)}";
            if (!string.IsNullOrWhiteSpace(city)) url += $"&city={Uri.EscapeDataString(city)}";
            if (!string.IsNullOrWhiteSpace(state)) url += $"&state={Uri.EscapeDataString(state)}";
            if (!string.IsNullOrWhiteSpace(zip)) url += $"&zipcode={Uri.EscapeDataString(zip)}";

            try
            {
                var resp = await _http.GetFromJsonAsync<List<UsStreetCandidate>>(url);
                var c = resp?.FirstOrDefault();
                if (c == null || c.components == null) return null;
                return new ValidatedAddress
                {
                    Street1 = c.delivery_line_1 ?? string.Empty,
                    Street2 = c.delivery_line_2,
                    City = c.components.city_name ?? string.Empty,
                    State = c.components.state_abbreviation ?? string.Empty,
                    Zip5 = c.components.zipcode ?? string.Empty,
                    Zip4 = c.components.plus4_code
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SmartyStreets Validate API");
                return null;
            }
        }

        private class AutocompleteResponse
        {
            public List<Suggestion>? suggestions { get; set; }
        }

        private class Suggestion
        {
            public string? street_line { get; set; }
            public string? secondary { get; set; }
            public string? city { get; set; }
            public string? state { get; set; }
            public string? zipcode { get; set; }
        }

        private class UsStreetCandidate
        {
            public string? delivery_line_1 { get; set; }
            public string? delivery_line_2 { get; set; }
            public Components? components { get; set; }
        }

        private class Components
        {
            public string? city_name { get; set; }
            public string? state_abbreviation { get; set; }
            public string? zipcode { get; set; }
            public string? plus4_code { get; set; }
        }
    }
}
