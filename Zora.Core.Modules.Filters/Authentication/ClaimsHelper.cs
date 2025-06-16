using System.Security.Claims;

namespace Zora.Modules.Filters.Authentication
{
    public static class ClaimsHelper
    {
        public static string GetClerkUserIdFromClaims(ClaimsPrincipal user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == "user_id" || c.Type == ClaimTypes.NameIdentifier);
            if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
            {
                throw new InvalidOperationException("Clerk User ID claim is not available.");
            }

            return userIdClaim.Value;
        }

        public static string GetUserIdFromClaims(ClaimsPrincipal user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var externalIdClaim = user.Claims.FirstOrDefault(c => c.Type == "userId");
            if (externalIdClaim == null || string.IsNullOrEmpty(externalIdClaim.Value))
            {
                throw new InvalidOperationException("User ID claim is not available.");
            }

            return externalIdClaim.Value;
        }
    }
}
