using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace API.Service.ExcelExport
{
    /// <summary>
    /// Resolves a UI <c>dataIndex</c> (a camelCase JSON key) to a compiled accessor on
    /// the report's row type — the same lookup the grid does with
    /// <c>row[dataIndex]</c>. No row DTO in this codebase carries
    /// <c>[JsonPropertyName]</c>, so the JSON key is exactly
    /// <see cref="JsonNamingPolicy.CamelCase"/> over the property name
    /// ("NRCNo" → "nrcNo", "TotalUSDValue" → "totalUSDValue", "HSCode" → "hsCode").
    ///
    /// Cached per row type; the compiled delegates make the per-cell cost a plain call.
    /// </summary>
    public sealed class ExcelRowPropertyMap
    {
        private static readonly ConcurrentDictionary<Type, ExcelRowPropertyMap> Cache = new();

        private readonly Dictionary<string, Func<object, object?>> _exact;
        private readonly Dictionary<string, Func<object, object?>> _ignoreCase;
        private readonly Dictionary<string, Type> _propertyTypes;

        private ExcelRowPropertyMap(Type rowType)
        {
            RowType = rowType;
            _exact = new Dictionary<string, Func<object, object?>>(StringComparer.Ordinal);
            _ignoreCase = new Dictionary<string, Func<object, object?>>(StringComparer.OrdinalIgnoreCase);
            _propertyTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in rowType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
            {
                var jsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                var accessor = Compile(rowType, property);

                _exact.TryAdd(jsonName, accessor);
                _ignoreCase.TryAdd(jsonName, accessor);
                // The raw C# name is also accepted, so a spec written against the
                // PascalCase property still resolves instead of exporting a blank column.
                _ignoreCase.TryAdd(property.Name, accessor);
                _propertyTypes.TryAdd(jsonName, property.PropertyType);
                _propertyTypes.TryAdd(property.Name, property.PropertyType);
            }

            JsonNames = _exact.Keys.ToArray();
        }

        public Type RowType { get; }

        /// <summary>Every camelCase JSON key the row type exposes.</summary>
        public IReadOnlyCollection<string> JsonNames { get; }

        public static ExcelRowPropertyMap For(Type rowType)
        {
            ArgumentNullException.ThrowIfNull(rowType);
            return Cache.GetOrAdd(rowType, static type => new ExcelRowPropertyMap(type));
        }

        /// <summary>Exact camelCase match first, then case-insensitive. Null when unknown.</summary>
        public Func<object, object?>? Find(string? dataIndex)
        {
            if (string.IsNullOrEmpty(dataIndex))
            {
                return null;
            }

            if (_exact.TryGetValue(dataIndex, out var exact))
            {
                return exact;
            }

            return _ignoreCase.TryGetValue(dataIndex, out var loose) ? loose : null;
        }

        public bool TryGet(string? dataIndex, out Func<object, object?> accessor)
        {
            var found = Find(dataIndex);
            accessor = found ?? (static _ => null);
            return found != null;
        }

        /// <summary>The declared CLR type behind a dataIndex, or null when unknown.</summary>
        public Type? PropertyType(string? dataIndex)
            => dataIndex != null && _propertyTypes.TryGetValue(dataIndex, out var type) ? type : null;

        /// <summary>Reads a value, returning null when the property does not exist.</summary>
        public object? GetValue(object row, string? dataIndex)
        {
            var accessor = Find(dataIndex);
            return accessor?.Invoke(row);
        }

        private static Func<object, object?> Compile(Type rowType, PropertyInfo property)
        {
            var parameter = Expression.Parameter(typeof(object), "row");
            var typed = Expression.Convert(parameter, rowType);
            var access = Expression.Property(typed, property);
            var boxed = Expression.Convert(access, typeof(object));

            return Expression.Lambda<Func<object, object?>>(boxed, parameter).Compile();
        }
    }
}
