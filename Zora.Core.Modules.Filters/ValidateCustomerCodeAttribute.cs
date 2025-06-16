using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Zora.Modules.Filters.Utilities;
using System.Text.RegularExpressions;

namespace Zora.Modules.Filters
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Delegate)]
	public class ValidateCustomerCodeAttribute : ActionFilterAttribute
	{
		public override void OnActionExecuting(ActionExecutingContext context)
		{
			var customerCode = context.HttpContext.Request.RouteValues["customerCode"]?.ToString();
			if (string.IsNullOrWhiteSpace(customerCode) || customerCode.Length != 12 || !Regex.IsMatch(customerCode, "^[a-zA-Z0-9]*$"))
			{
				context.Result = new BadRequestObjectResult(GetInvalidCustomerCodeResponse());
				return;
			}

			base.OnActionExecuting(context);
		}

		private static ValidationProblemDetails GetInvalidCustomerCodeResponse()
		{
			var response = new ValidationProblemDetails
			{
				Title = ValidationMessage.InvalidCustomerCode,
				Detail = ValidationMessage.ProblemDetail_CustomerCode,
				Status = StatusCodes.Status400BadRequest
			};

			response.Errors.Add("CustomerCode", new[] { ValidationMessage.CustomerCodeInvalidMessage });
			return response;
		}
	}
}
