using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using API.Model;
using Microsoft.AspNetCore.Mvc;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Works out what row type a report's grid renders, so a presentation spec's
    /// <c>dataIndex</c> values can be bound without the controller declaring anything.
    /// The answer is the <c>T</c> in the controller's bare
    /// <c>[HttpPost] Task&lt;ActionResult&lt;ApiResult&lt;T&gt;&gt;&gt; Post(request)</c>
    /// — the very payload the grid consumes.
    /// </summary>
    public static class ExcelRowTypeResolver
    {
        /// <summary>
        /// The row (or, for a composite report, the summary) type behind the grid.
        /// Null when the controller has no bare <c>Post</c> and declares no
        /// <see cref="IExcelRowTypeProvider"/>.
        /// </summary>
        public static Type? Resolve(Type controllerType)
        {
            ArgumentNullException.ThrowIfNull(controllerType);

            var post = FindBarePost(controllerType, requestType: null);
            var fromPost = post == null ? null : UnwrapPostReturnType(post.ReturnType);
            if (fromPost != null)
            {
                return fromPost;
            }

            return FromRowTypeProvider(controllerType);
        }

        /// <summary>
        /// The controller's grid action: a public instance <c>Post</c> carrying
        /// <c>[HttpPost]</c> with no route template and exactly one parameter that accepts
        /// <paramref name="requestType"/> (any single-parameter <c>Post</c> when null).
        /// </summary>
        public static MethodInfo? FindBarePost(Type controllerType, Type? requestType)
        {
            ArgumentNullException.ThrowIfNull(controllerType);

            return controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == "Post"
                    && !method.IsSpecialName
                    && method.GetParameters().Length == 1
                    && HasBareHttpPost(method)
                    && (requestType == null || method.GetParameters()[0].ParameterType.IsAssignableFrom(requestType)))
                .OrderBy(method => method.GetParameters()[0].ParameterType == requestType ? 0 : 1)
                .FirstOrDefault();
        }

        /// <summary>
        /// <c>Task&lt;ActionResult&lt;ApiResult&lt;T&gt;&gt;&gt;</c> → <c>T</c>;
        /// <c>Task&lt;ActionResult&lt;TSummary&gt;&gt;</c> → <c>TSummary</c> (the composite
        /// reports, whose "row" is a summary payload with one list per table).
        /// </summary>
        public static Type? UnwrapPostReturnType(Type returnType)
        {
            var type = returnType;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                type = type.GetGenericArguments()[0];
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
            {
                type = type.GetGenericArguments()[0];
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResult<>))
            {
                return type.GetGenericArguments()[0];
            }

            // A bare IActionResult/Task tells us nothing about the row shape.
            return type == typeof(void)
                || typeof(IActionResult).IsAssignableFrom(type)
                || type == typeof(object)
                    ? null
                    : type;
        }

        private static bool HasBareHttpPost(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<HttpPostAttribute>();
            if (attribute == null)
            {
                return false;
            }

            // "Excel" and the other sub-routes carry a template; the grid action does not.
            return string.IsNullOrEmpty(attribute.Template);
        }

        /// <summary>
        /// Reads <see cref="IExcelRowTypeProvider.ExcelRowType"/> without running the
        /// controller's constructor (which needs a DbContext): the implementations return
        /// a constant <c>typeof(...)</c>, so an uninitialized instance is enough.
        /// </summary>
        private static Type? FromRowTypeProvider(Type controllerType)
        {
            if (!typeof(IExcelRowTypeProvider).IsAssignableFrom(controllerType))
            {
                return null;
            }

            try
            {
                var instance = (IExcelRowTypeProvider)RuntimeHelpers.GetUninitializedObject(controllerType);
                return instance.ExcelRowType;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
