using System.Dynamic;
using System.Text.RegularExpressions;
using AutoMapper.Internal;
using ConvPipe.Converters;
using Jint.Native;

namespace ConvPipe;

internal static class StringTokenizer
{
    // https://stackoverflow.com/a/14655145/4731483
    public static string[] Tokenize(this String str)
    {
        //var re = new Regex("(\\S+|\"[^\"]*\")");
        var re = new Regex(@"[\""].+?[\""]|[\'].+?[\']|\S+");
        var marr = re.Matches(str);

        return marr.Select(m => m.Value)
            //.Select(v => v.Trim('"'))
            .ToArray();
    }
}

public class Pipe
{
    public static Pipe CreateWithDefaults(string luaScript = null, Dictionary<string, object> globRef = null, Action<object> log = null)
    {
        var convLib = new Pipe();
        StdConverters.InitializeLib(convLib);
        XPathConverters.InitializeLib(convLib);
        if (luaScript != null)
        {
            var lc = new LuaConverters(luaScript);
            lc.InitializeLib(convLib);
        }

        if (globRef != null)
        {
            var pfc = new PathFinderConverters(globRef);
            pfc.InitializeLib(convLib);
        }

        return convLib;
    }

    public Dictionary<string, Func<object, string[], object>> Converters { get; } = new();
    public Dictionary<string, Func<object[], string[], object>> NAryConverters { get; } = new();

    public bool IsNAryConverter(string conv)
    {
        var i = conv.IndexOf(' ');
        if (i < 0)
            return false;
        var key = conv[..i];
        return NAryConverters.ContainsKey(key);
    }

    private const string ConvPattern = @"^(?<name>\S+)\[(?<type>[^\]]+)\]$";
    private Regex ConvRegex = new Regex(ConvPattern, RegexOptions.IgnoreCase);

    private object ConvertExpr(string[] expr, object val)
    {
        //string[] expr = convExpr.Split(' ').Select(str => str.Trim()).ToArray();
        if (expr.Length == 0)
            throw new Exception("expect converter name");
        var convExpr = expr[0];
        var mr = ConvRegex.Match(convExpr);
        var convName = mr.Success ? mr.Groups["name"].Value + "_Typed" : convExpr;
        var convArgs = mr.Success ? new [] { mr.Groups["type"].Value }.Concat(expr[1..]).ToArray() : expr[1..];

        if (!Converters.ContainsKey(convName))
            throw new Exception($"converter \"{convName}\" not found");

        var conv = Converters[convName];
        return conv(val, convArgs);
    }

    public object RunPipe(string pipeExpr, object val)
    {
        pipeExpr = ShortTypes.Process(pipeExpr);
        //var pipe = pipeExpr.Split('|').Select(str => str.Trim());
        var pipe = PipeTokenize(pipeExpr);
        return RunPipe(pipe, val);
    }

    private static readonly Dictionary<string, string> SystemTypes = new Dictionary<string, string>()
    {
        { "bool", typeof(bool).ToString() },
        { "byte", typeof(byte).ToString() },
        { "char", typeof(char).ToString() },
        { "decimal", typeof(decimal).ToString() },
        { "double", typeof(double).ToString() },
        { "float", typeof(float).ToString() },
        { "int", typeof(int).ToString() },
        { "long", typeof(long).ToString() },
        { "object", typeof(object).ToString() },
        { "sbyte", typeof(sbyte).ToString() },
        { "short", typeof(short).ToString() },
        { "string", typeof(string).ToString() },
        { "uint", typeof(uint).ToString() },
        { "ulong", typeof(ulong).ToString() },
        { "ushort", typeof(ushort).ToString() },
        { "uint32", typeof(uint).ToString() },
        { "uint64", typeof(ulong).ToString() },
        { "int64", typeof(long).ToString() },
        { "int32", typeof(int).ToString() },
    };

    public static Type? ResolveType(string typeName)
        => Type.GetType(SystemTypes.TryGetValue(typeName.ToLower(), out var result) ? result : typeName);

    private Array ConvertTypedArray(string[] args, object val, string typeName = null)
    {
        var src = (Array)val;
        var len = src.Length;
        var type = typeName == null ? src.GetType().GetElementType() : ResolveType(typeName);
        if (type == null)
            throw new Exception($"type \"{typeName}\" not found");
        var dest = Array.CreateInstance(type, len);
        for (int i = 0; i < len; ++i)
        {
            var destItem = ConvertExpr(args, src.GetValue(i));
            dest.SetValue(destItem, i);
        }

        return dest;
    }

    /**
     * Convert each element of input array.
     * When passes a TypeOf, it will be converted to array with type of input array.
     * When passes, for instance, Type[Int64], it will be converted to Int64[] array.
     */
    private void ConvertEach(string[] conv, ref object ans)
    {
        if (conv is { Length: > 0 })
        {
            var pattern = @"^type\[(?<type>\S+)\]$";
            var mr = Regex.Match(conv[0], pattern, RegexOptions.IgnoreCase);
            if (mr.Success)
            {
                ans = ConvertTypedArray(args: conv.Skip(1).ToArray(), ans, typeName: mr.Groups["type"].Value);
                return;
            }

            if (conv[0].ToLower() == "typeof")
            {
                ans = ConvertTypedArray(args: conv.Skip(1).ToArray(), ans);
                return;
            }

        }

        ans = ((object[])ans).Select(item => ConvertExpr(conv, item)).ToArray();
    }

    public static object GetDefault(Type type) =>
        type.IsValueType ? Activator.CreateInstance(type) : null;

    private object ConvertReduceTypedArray(string[] args, object val, string? typeName = null)
    {
        var src = (Array)val;
        var len = src.Length;
        var type = typeName == null ? src.GetType().GetElementType() : ResolveType(typeName);
        if (type == null)
            throw new Exception($"type \"{typeName}\" not found");

        var acc = GetDefault(type);
        for (int i = 0; i < len; ++i)
            acc = ConvertExprArray(args, new[] { acc, src.GetValue(i) });

        return acc;
    }

    private void ConvertReduce(string[] conv, ref object ans)
    {
        if (conv is { Length: 0 })
            throw new Exception("expect converter name");

        var pattern = @"^type\[(?<type>\S+)\]$";
        var mr = Regex.Match(conv[0], pattern, RegexOptions.IgnoreCase);
        if (mr.Success)
        {
            ans = ConvertReduceTypedArray(args: conv.Skip(1).ToArray(), ans, typeName: mr.Groups["type"].Value);
            return;
        }

        if (conv[0].ToLower() == "typeof")
        {
            ans = ConvertReduceTypedArray(args: conv.Skip(1).ToArray(), ans);
            return;
        }

        var arr = (object[])ans;
        ans = arr.Aggregate<object?, object>(null, (current, t) => ConvertExprArray(conv, new[] { current, t }));
    }

    private object RunPipe(string[][] pipe, object val)
    {
        // foreach (var conv in pipe)
        //     val = ConvertExpr(conv, val);
        // return val;

        object ans = val;
        bool eachMode = false;
        foreach (var conv in pipe)
        {
            if (conv.First().ToLower() == "each")
            {
                if (conv is { Length: 1 })
                    eachMode = true;
                else
                    ConvertEach(conv[1..], ref ans);

                continue;
            }

            if (conv.First().ToLower() == "reduce")
            {
                ConvertReduce(conv[1..], ref ans);
                continue;
            }

            if (eachMode)
                ConvertEach(conv, ref ans);
            else if (ans is IEnumerable<object>)
                ans = ConvertExprArray(conv, (object[])ans);
            else
                ans = ConvertExpr(conv, ans);
        }

        if (ans is IDestObject d)
            ans = d.Origin;

        return ans;
    }

    private object ConvertExprArray(string[] expr, object[] val)
    {
        //string[] expr = convExpr.Split(' ').Select(str => str.Trim()).ToArray();
        if (expr.Length == 0)
            throw new Exception("expect converter name");
        var convExpr = expr[0];
        var mr = ConvRegex.Match(convExpr);
        var convName = mr.Success ? mr.Groups["name"].Value + "_Typed" : convExpr;
        var convArgs = mr.Success ? new [] { mr.Groups["type"].Value }.Concat(expr[1..]).ToArray() : expr[1..];
        // var convName = expr[0];
        // var convArgs = expr[1..];
        if (!NAryConverters.ContainsKey(convName))
            throw new Exception($"nary converter \"{convName}\" not found");

        var conv = NAryConverters[convName];
        return conv(val, convArgs);
    }

    public object ConvertPipeArray(string pipeExpr, object[] val)
    {
        var pipe = PipeTokenize(pipeExpr);
        object ans = val;
        int index = 0;
        foreach (var conv in pipe)
        {
            if (conv.First().ToLower() == "each")
            {
                var tail = pipe[index..];
                return RunPipe(tail, ans);
            }

            if (ans is IEnumerable<object>)
                ans = ConvertExprArray(conv, (object[])ans);
            else
                ans = ConvertExpr(conv, ans);

            ++index;
        }

        if (ans is IDestObject d)
            ans = d.Origin;

        return ans;
    }

    private static string[][] PipeTokenize(string pipeExpr)
        => pipeExpr.Tokenize()
            .Aggregate(
                new List<List<string>> { new() },
                (acc, tok) =>
                {
                    if (tok == "|")
                        acc.Add(new List<string>());
                    else
                        acc.Last().Add(tok);

                    return acc;
                })
            .Select(item =>
                item.Where(s => !string.IsNullOrEmpty(s)).ToArray())
            .Where(arr => arr.Length > 0)
            .ToArray();
}