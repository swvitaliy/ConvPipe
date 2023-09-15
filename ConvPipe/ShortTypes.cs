using System.Text.RegularExpressions;

namespace ConvPipe;

/**
 * Console.WriteLine(ShortTypes.ProcessConverter("Int64[] | Each PlusOneIfOdd | Max")); // Пример вызова функции ProcessConverter
 */
class ShortTypes
{
    static string ProcessConverter(string tail)
        => ProcessConverter(tail.Split('|'));

    static string ProcessConverter(string[] tail)
        => string.Join(" | ", tail.Select(ProcessTypes).ToArray()).Trim();

    static string ProcessTypes(string val)
    {
        val = val.Trim();
        string[] types = { "String", "Boolean", "Int32", "UInt32", "Int64", "UInt64", "DateTime" };

        foreach (var t in types)
        {
            if (val == t)
                return "Convert To" + t;

            if (Regex.IsMatch(val, "^" + t + @"\s*\[\s*\]$"))
                return "ConvertArray System." + t + " To" + t;
        }

        return val;
    }
}
