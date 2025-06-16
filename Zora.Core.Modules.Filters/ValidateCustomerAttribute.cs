using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Zora.Modules.Filters.Utilities;

namespace Zora.Modules.Filters
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Delegate)]
	public class ValidateCustomerAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.Request.RouteValues.TryGetValue("customerId", out var id) ||
               !int.TryParse(id?.ToString(), out int parsedCustomerId) || parsedCustomerId < 1)
            {
                context.Result = new BadRequestObjectResult(GetInvalidCustomerIdResponse());
            }

            base.OnActionExecuting(context);
        }

        private static ValidationProblemDetails GetInvalidCustomerIdResponse()
        {
            var response = new ValidationProblemDetails
            {
                Title = ValidationMessage.InvalidCustomerId,
                Detail = ValidationMessage.ProblemDetail_CustomerId,
                Status = StatusCodes.Status400BadRequest
            };

            response.Errors.Add("CustomerId", new[] { ValidationMessage.CustomerIdInvalidMessage });
            return response;
        }
    }
}
