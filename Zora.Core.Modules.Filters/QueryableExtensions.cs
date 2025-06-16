using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Zora.Modules.Filters.SearchFilters;

namespace Zora.Modules.Filters
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> ApplySearchFilters<T>(this IQueryable<T> query, List<Filter> filters, List<Sort> sorts)
        {
            if (filters != null && filters.Any())
            {
                foreach (var filter in filters)
                {
                    query = ApplySearchFilter(query, filter);
                }
            }

            if (sorts != null && sorts.Any())
            {
                query = ApplySorting(query, sorts);
            }

            return query;
        }


        private static IQueryable<T> ApplySearchFilter<T>(IQueryable<T> query, Filter filter)
        {
            if (!PropertyExists<T>(filter.PropertyName))
            {
                throw new InvalidOperationException($"The property '{filter.PropertyName}' does not exist on the entity type.");
            }

            switch (filter.Comparison.ToLower())
            {
                case "equals":
                    return ApplyEqualsNotEqualsFilter(query, filter.PropertyName, filter.PropertyValue, true);
                case "notequals":
                    return ApplyEqualsNotEqualsFilter(query, filter.PropertyName, filter.PropertyValue, false);
                case "greaterthan":
                    return ApplyComparisonFilter(query, filter.PropertyName, filter.PropertyValue, ComparisonType.GreaterThan);
                case "lessthan":
                    return ApplyComparisonFilter(query, filter.PropertyName, filter.PropertyValue, ComparisonType.LessThan);
                case "greaterthanorequal":
                    return ApplyComparisonFilter(query, filter.PropertyName, filter.PropertyValue, ComparisonType.GreaterThanOrEqual);
                case "lessthanorequal":
                    return ApplyComparisonFilter(query, filter.PropertyName, filter.PropertyValue, ComparisonType.LessThanOrEqual);
                case "contains":
                    return ApplyContainsFilter(query, filter.PropertyName, filter.PropertyValue);
                case "startswith":
                    return ApplyStartsWithFilter(query, filter.PropertyName, filter.PropertyValue);
                case "endswith":
                    return ApplyEndsWithFilter(query, filter.PropertyName, filter.PropertyValue);
                default:
                    throw new NotSupportedException($"Comparison type '{filter.Comparison}' is not supported.");
            }
        }

        private static IQueryable<T> ApplyEqualsNotEqualsFilter<T>(IQueryable<T> query, string propertyName, object value, bool isEquals)
        {
            var parameter = Expression.Parameter(typeof(T), "c");
            var propertyExpression = GetNestedPropertyExpression(parameter, propertyName);

            Expression valueExpression;
            if (value is JsonElement jsonElement)
            {
                valueExpression = GetValueExpression(jsonElement);
            }
            else
            {
                valueExpression = Expression.Constant(value);
            }

            // Convert the value to the property type if necessary
            if (propertyExpression.Type != valueExpression.Type)
            {
                if (propertyExpression.Type == typeof(Guid) && valueExpression.Type == typeof(string))
                {
                    var stringValue = valueExpression is ConstantExpression constant ? constant.Value as string : value.ToString();
                    if (Guid.TryParse(stringValue, out var guidValue))
                    {
                        valueExpression = Expression.Constant(guidValue, typeof(Guid));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot convert '{value}' to Guid.");
                    }
                }
                else if (propertyExpression.Type == typeof(bool) && valueExpression.Type == typeof(string))
                {
                    var stringValue = valueExpression is ConstantExpression constant ? constant.Value as string : value.ToString();
                    if (bool.TryParse(stringValue, out var boolValue))
                    {
                        valueExpression = Expression.Constant(boolValue, typeof(bool));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot convert '{value}' to bool.");
                    }
                }
                else if (propertyExpression.Type == typeof(long) && valueExpression.Type == typeof(string))
                {
                    var stringValue = valueExpression is ConstantExpression constant ? constant.Value as string : value.ToString();
                    if (long.TryParse(stringValue, out var longValue))
                    {
                        valueExpression = Expression.Constant(longValue, typeof(long));
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot convert '{value}' to long.");
                    }
                }
                else
                {
                    valueExpression = Expression.Convert(valueExpression, propertyExpression.Type);
                }
            }

            var comparisonExpression = isEquals
                ? Expression.Equal(propertyExpression, valueExpression)
                : Expression.NotEqual(propertyExpression, valueExpression);

            var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
            return query.Where(lambda);
        }

        private static Expression GetValueExpression(JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                var stringValue = jsonElement.GetString();
                if (Guid.TryParse(stringValue, out var guidValue))
                {
                    return Expression.Constant(guidValue, typeof(Guid));
                }
                if (DateOnly.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnlyValue))
                {
                    return Expression.Constant(dateOnlyValue, typeof(DateOnly));
                }
                if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                {
                    return Expression.Constant(ConvertToUtc(dateValue), typeof(DateTime));
                }
                return Expression.Constant(stringValue, typeof(string));
            }

            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => Expression.Constant(jsonElement.GetDecimal(), typeof(decimal)),
                JsonValueKind.True => Expression.Constant(jsonElement.GetBoolean(), typeof(bool)),
                JsonValueKind.False => Expression.Constant(jsonElement.GetBoolean(), typeof(bool)),
                JsonValueKind.Null => Expression.Constant(null, typeof(object)),
                _ => throw new NotSupportedException($"JsonValueKind '{jsonElement.ValueKind}' is not supported.")
            };
        }

        private static MemberExpression GetNestedPropertyExpression(Expression parameter, string propertyName)
        {
            var properties = propertyName.Split('.');
            Expression propertyExpression = parameter;

            foreach (var property in properties)
            {
                propertyExpression = Expression.Property(propertyExpression, property);
            }

            return (MemberExpression)propertyExpression;
        }

        private static DateTime ConvertToUtc(DateTime dateTime)
        {
            return dateTime.Kind switch
            {
                DateTimeKind.Local => dateTime.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                _ => dateTime
            };
        }

        private static IQueryable<T> ApplyComparisonFilter<T>(IQueryable<T> query, string propertyName, object value, ComparisonType comparisonType)
        {
            var parameter = Expression.Parameter(typeof(T), "c");
            var propertyExpression = GetNestedPropertyExpression(parameter, propertyName);

            Expression valueExpression;
            if (value is JsonElement jsonElement)
            {
                valueExpression = GetValueExpression(jsonElement);
            }
            else
            {
                valueExpression = Expression.Constant(value);
            }

            if (propertyExpression.Type != valueExpression.Type)
            {
                valueExpression = Expression.Convert(valueExpression, propertyExpression.Type);
            }

            Expression comparisonExpression;
            if (propertyExpression.Type == typeof(string) || propertyExpression.Type == typeof(bool))
            {
                throw new InvalidOperationException("Comparison operators are not defined for string/boolean types.");
            }
            else
            {
                comparisonExpression = comparisonType switch
                {
                    ComparisonType.GreaterThan => Expression.GreaterThan(propertyExpression, valueExpression),
                    ComparisonType.LessThan => Expression.LessThan(propertyExpression, valueExpression),
                    ComparisonType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(propertyExpression, valueExpression),
                    ComparisonType.LessThanOrEqual => Expression.LessThanOrEqual(propertyExpression, valueExpression),
                    _ => throw new NotSupportedException($"ComparisonType '{comparisonType}' is not supported.")
                };
            }

            var lambda = Expression.Lambda<Func<T, bool>>(comparisonExpression, parameter);
            return query.Where(lambda);
        }

        private static IQueryable<T> ApplyContainsFilter<T>(IQueryable<T> query, string propertyName, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "c");
            var propertyExpression = GetNestedPropertyExpression(parameter, propertyName);

            string stringValue;
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    stringValue = jsonElement.GetString();
                }
                else
                {
                    throw new InvalidOperationException("JsonElement must be of type string for Contains filter.");
                }
            }
            else
            {
                stringValue = value.ToString();
            }

            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var propertyToLower = Expression.Call(propertyExpression, toLowerMethod);
            var valueToLower = Expression.Constant(stringValue.ToLower());

            var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
            if (containsMethod == null)
            {
                throw new InvalidOperationException("Method 'Contains' not found on type 'string'.");
            }
            var containsExpression = Expression.Call(propertyToLower, containsMethod, valueToLower);

            var lambda = Expression.Lambda<Func<T, bool>>(containsExpression, parameter);
            return query.Where(lambda);
        }

        private static IQueryable<T> ApplyStartsWithFilter<T>(IQueryable<T> query, string propertyName, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "c");
            var propertyExpression = GetNestedPropertyExpression(parameter, propertyName);

            string stringValue;
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    stringValue = jsonElement.GetString();
                }
                else
                {
                    throw new InvalidOperationException("JsonElement must be of type string for StartsWith filter.");
                }
            }
            else
            {
                stringValue = value.ToString();
            }

            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var propertyToLower = Expression.Call(propertyExpression, toLowerMethod);
            var valueToLower = Expression.Constant(stringValue.ToLower());

            var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
            if (startsWithMethod == null)
            {
                throw new InvalidOperationException("Method 'StartsWith' not found on type 'string'.");
            }
            var startsWithExpression = Expression.Call(propertyToLower, startsWithMethod, valueToLower);

            var lambda = Expression.Lambda<Func<T, bool>>(startsWithExpression, parameter);
            return query.Where(lambda);
        }

        private static IQueryable<T> ApplyEndsWithFilter<T>(IQueryable<T> query, string propertyName, object value)
        {
            var parameter = Expression.Parameter(typeof(T), "c");
            var propertyExpression = GetNestedPropertyExpression(parameter, propertyName);

            // Extract string value from JsonElement if necessary
            string stringValue;
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    stringValue = jsonElement.GetString();
                }
                else
                {
                    throw new InvalidOperationException("JsonElement must be of type string for EndsWith filter.");
                }
            }
            else
            {
                stringValue = value.ToString();
            }

            var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
            var propertyToLower = Expression.Call(propertyExpression, toLowerMethod);
            var valueToLower = Expression.Constant(stringValue.ToLower());

            var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });
            if (endsWithMethod == null)
            {
                throw new InvalidOperationException("Method 'EndsWith' not found on type 'string'.");
            }
            var endsWithExpression = Expression.Call(propertyToLower, endsWithMethod, valueToLower);

            var lambda = Expression.Lambda<Func<T, bool>>(endsWithExpression, parameter);
            return query.Where(lambda);
        }

        private static bool PropertyExists<T>(string propertyName)
        {
            var properties = propertyName.Split('.');
            Type type = typeof(T);

            foreach (var property in properties)
            {
                var propertyInfo = type.GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (propertyInfo == null)
                {
                    return false;
                }
                type = propertyInfo.PropertyType;
            }

            return true;
        }

        private static IQueryable<T> ApplySorting<T>(IQueryable<T> query, List<Sort> sorts)
        {
            IOrderedQueryable<T> orderedQuery = null;

            for (int i = 0; i < sorts.Count; i++)
            {
                var sort = sorts[i];
                if (!PropertyExists<T>(sort.PropertyName))
                {
                    throw new InvalidOperationException($"The property '{sort.PropertyName}' does not exist on the entity type.");
                }
                var sortDirection = sort.Direction.ToString().ToUpper();
                if (sortDirection != "ASC" && sortDirection != "DESC")
                {
                    throw new InvalidOperationException($"The sort direction '{sort.Direction}' is not valid. Only 'ASC' and 'DESC' are allowed.");
                }
                var parameter = Expression.Parameter(typeof(T), "x");
                var property = Expression.Property(parameter, sort.PropertyName);
                var lambda = Expression.Lambda(property, parameter);

                var methodName = i == 0 ? (sortDirection == "ASC" ? "OrderBy" : "OrderByDescending") : (sortDirection == "ASC" ? "ThenBy" : "ThenByDescending");
                var method = typeof(Queryable).GetMethods().First(m => m.Name == methodName && m.GetParameters().Length == 2);
                var genericMethod = method.MakeGenericMethod(typeof(T), property.Type);

                orderedQuery = (IOrderedQueryable<T>)genericMethod.Invoke(null, new object[] { orderedQuery ?? query, lambda });
            }

            return orderedQuery ?? query;
        }
    }
}
