using Microsoft.AspNetCore.Authentication.JwtBearer;
using Zora.Core.Modules.Filters;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
namespace Zora.Modules.Filters.Authentication
{
    public static class AuthenticationHandler
    {
        public static IHostApplicationBuilder InitializeJwtAuthentication(this IHostApplicationBuilder builder, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            string audiences = builder.Configuration[Utilities.Constants.AuthorizedParty] ?? "";
            if (string.IsNullOrEmpty(audiences))
            {
                throw new InvalidOperationException("AuthorizedParty is not configured properly.");
            }
            // Split into dictionary
            Dictionary<string, string> audienceDictionary = audiences.Split(',').ToDictionary(x => x.Trim(), x => x.Trim());

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(x =>
            {
                x.Authority = builder.Configuration[Utilities.Constants.Authority];
                x.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateAudience = false,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    ValidateIssuer = true
                };
                x.Events = new JwtBearerEvents()
                {
                    OnTokenValidated = async context =>
                    {
                        var azp = context.Principal?.FindFirstValue("azp");
                        if (string.IsNullOrEmpty(azp) || !audienceDictionary.ContainsKey(azp))
                        {
                            context.Fail("AZP Claim is invalid or missing");
                            return;
                        }

                        await SetClaims(context, builder, httpClientFactory, memoryCache).ConfigureAwait(false);
                    },
                };
            });
            return builder;
        }
        private static async Task SetClaims(TokenValidatedContext context, IHostApplicationBuilder builder, IHttpClientFactory httpClientFactory, IMemoryCache memoryCache)
        {
            try
            {
                string userId = ClaimsHelper.GetClerkUserIdFromClaims(context.Principal);

                if (!memoryCache.TryGetValue(userId, out List<Claim> claims))
                {
                    var clerkApiKey = builder.Configuration[Utilities.Constants.SecretKey];
                    if (string.IsNullOrEmpty(clerkApiKey))
                    {
                        throw new InvalidOperationException("Clerk API key is not configured properly.");
                    }

                    string clerkApiUrl = $"{Utilities.Constants.ClerkUrl}{userId}";

                    using var client = httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clerkApiKey);

                    var response = await client.GetAsync(clerkApiUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        // handle error as needed
                        Console.WriteLine("Failed to retrieve user details from Clerk.");
                        return;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseBody);

                    var user = jsonDoc.RootElement;

                    var id = user.GetProperty("id").GetString();
                    var emailAddress = user.GetProperty("email_addresses")[0].GetProperty("email_address").GetString();
                    var firstName = user.GetProperty("first_name").GetString();
                    var lastName = user.GetProperty("last_name").GetString();
                    var userName = user.GetProperty("username").GetString();
                    var databaseUserId = user.GetProperty("external_id").GetString();
                    var customerCode = user.GetProperty("public_metadata").GetProperty("customerCode").GetString();
                    var customerId = user.GetProperty("private_metadata").GetProperty("customerId").GetInt64();

                    claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, id),
                        new Claim(ClaimTypes.Name, userName),
                        new Claim("firstName", firstName),
                        new Claim("lastName", lastName),
                        new Claim(ClaimTypes.Email, emailAddress),
                        new Claim("userId", databaseUserId),
                        new Claim("customerId", customerId.ToString()),
                        new Claim("CustomerCode", customerCode),
                    };

                    // Cache the claims for future requests
                    memoryCache.Set(userId, claims, TimeSpan.FromMinutes(Utilities.Constants.UserClaimCacheDurationInMinutes));
                }

                if (context.Principal.Identity is ClaimsIdentity claimsIdentity)
                {
                    claimsIdentity.AddClaims(claims);
                }
            }
            catch (Exception ex)
            {
                // handle error as needed
                Console.WriteLine($"Error in SetClaims: {ex.Message}");
            }
        }
    }
}
