namespace Zora.Modules.Filters.Utilities
{
    public static class ValidationMessage
    {
        public const string InvalidCustomerId = "Invalid Customer Id.";
		public const string InvalidCustomerCode = "Invalid Customer Code.";
        public const string ProblemDetail_CustomerId = "The provided customer ID is invalid.";
		public const string ProblemDetail_CustomerCode = "The provided customer code is invalid.";
		public const string CustomerIdInvalidMessage = "The customer ID must be a positive integer.";
		public const string CustomerCodeInvalidMessage = "The Customer Code must be 12 characters long and alphanumeric.";
	}
}