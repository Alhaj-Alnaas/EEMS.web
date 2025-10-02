using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Core.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? value.ToString();
        }

        public static string ToEnumDescription<TEnum>(this string value) where TEnum : struct, Enum
        {
            if (string.IsNullOrWhiteSpace(value))
                return "غير معروف";

            // 1) Try parse by enum name (Export, Import, ...)
            if (Enum.TryParse<TEnum>(value, true, out var enumValueByName))
            {
                return enumValueByName.GetDescription();
            }

            // 2) Try match by DescriptionAttribute text (Arabic visible text stored somewhere)
            var enumType = typeof(TEnum);
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var descAttr = field.GetCustomAttribute<DescriptionAttribute>();
                var desc = descAttr?.Description;
                if (!string.IsNullOrEmpty(desc) && string.Equals(desc, value, StringComparison.OrdinalIgnoreCase))
                {
                    var parsed = (TEnum)Enum.Parse(enumType, field.Name);
                    return parsed.GetDescription();
                }
            }

            // 3) fallback: return original value
            return value;
        }
    }
}
