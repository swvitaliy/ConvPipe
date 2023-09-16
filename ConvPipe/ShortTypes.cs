using System.Text.RegularExpressions;

namespace ConvPipe;

internal static class ShortTypes
{
    public static string Process(string tail)
        => Process(tail.Split('|'));

    private static Func<string, string>[] Processors =
    {
        ProcessEachEval,
        ProcessReduceEval,
        ProcessTypes,
    };

    private static string ApplyEachProcessor(string val)
        => Processors.Aggregate(val, (current, proc) => proc(current.Trim()));

    public static string Process(string[] tail)
        => string.Join(" | ", tail.Select(ApplyEachProcessor).ToArray()).Trim();

    private const string ReduceEvalPattern = @"^ReduceEval\[(?<type>[^\]]+)\]";
    private static readonly Regex ReduceEvalRegex = new(ReduceEvalPattern, RegexOptions.IgnoreCase);

    static string ProcessReduceEval(string val)
        => ReduceEvalRegex.Replace(val, "Reduce Type[$1] ExprEval[$1]");

    private const string EachEvalPattern = @"^EachEval\[(?<type>[^\]]+)\]";
    private static readonly Regex EachEvalRegex = new(EachEvalPattern, RegexOptions.IgnoreCase);

    static string ProcessEachEval(string val)
        => EachEvalRegex.Replace(val, "Each Type[$1] ExprEval[$1]");

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
