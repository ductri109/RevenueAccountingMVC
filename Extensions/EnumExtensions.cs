using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RevenueAccountingMVC.Extensions
{
    public static class EnumExtensions
    {
        public static string ToDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field.GetCustomAttribute<DisplayAttribute>();
            return attr?.Name ?? value.ToString();
        }
    }
}

