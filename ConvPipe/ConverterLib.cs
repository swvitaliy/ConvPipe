using System.Collections;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using AutoMapper.Internal;
using Jint;
using Jint.Native;
using NLua;
using NUnit.Framework.Constraints;
using org.matheval;

namespace ConvPipe;

internal static class StringTokenizer
{
    // https://stackoverflow.com/a/14655145/4731483
    public static string[] Tokenize(this String str)
    {
        //var re = new Regex("(\\S+|\"[^\"]*\")");
        var re = new Regex(@"[\""].+?[\""]|[^ ]+");
        var marr = re.Matches(str);

        return marr.Select(m => m.Value)
            //.Select(v => v.Trim('"'))
            .ToArray();
    }
}

public class ConverterLib
{
    public static Dictionary<string, Type> EntityTypes { get; set; } = new();

    public static ConverterLib CreateWithDefaultsNoDbConfig(string luaScript = null, string jsScript = null,
        Dictionary<string, object> globRef = null, Action<object> log = null)
    {
        return CreateWithDefaults(luaScript, jsScript, globRef, log);
    }

    public static ConverterLib CreateWithDefaults(string luaScript = null, string jsScript = null, Dictionary<string, object> globRef = null, Action<object> log = null)
    {
        var convLib = new ConverterLib();
        DefaultConverters.InitializeLib(convLib);
        XPathConverters.InitializeLib(convLib);
        if (luaScript != null)
        {
            var lc = new LuaConverters(luaScript);
            lc.InitializeLib(convLib);
        }

        if (jsScript != null)
        {
            var jsc = new JsConverter(jsScript, null, log);
            jsc.InitializeLib(convLib);
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

    private object ConvertExpr(string[] expr, object val)
    {
        //string[] expr = convExpr.Split(' ').Select(str => str.Trim()).ToArray();
        if (expr.Length == 0)
            throw new Exception("expect converter name");
        var convName = expr[0];
        var convArgs = expr[1..];
        if (!Converters.ContainsKey(convName))
            throw new Exception($"converter \"{convName}\" not found");

        var conv = Converters[convName];
        return conv(val, convArgs);
    }

    public object ConvertPipe(string pipeExpr, object val)
    {
        pipeExpr = ShortTypes.ProcessConverter(pipeExpr);
        //var pipe = pipeExpr.Split('|').Select(str => str.Trim());
        var pipe = PipeTokenize(pipeExpr);
        return ConvertPipe(pipe, val);
    }

    private Array ConvertTypedArray(string[] args, object val, string typeName = null)
    {
        var src = (Array)val;
        var len = src.Length;
        var type = typeName == null ? src.GetType().GetElementType() : Type.GetType(typeName);
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
            }
            else if (conv[0].ToLower() == "typeof")
            {
                ans = ConvertTypedArray(args: conv.Skip(1).ToArray(), ans);
            }
        }
        else
        {
            ans = ((object[])ans).Select(item => ConvertExpr(conv, item)).ToArray();
        }
    }

    private object ConvertPipe(string[][] pipe, object val)
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
        var convName = expr[0];
        var convArgs = expr[1..];
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
                return ConvertPipe(tail, ans);
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

public class LuaConverters : IDisposable
{
    public LuaConverters(string libScript)
    {
        LibScript = libScript;
        LuaState = new Lua();
        LuaState.DoString(LibScript);
    }

    public void Dispose()
        => LuaState?.Dispose();


    public string LibScript { get; }
    private Lua LuaState { get; }

    public void InitializeLib(ConverterLib convLib)
    {
        convLib.Converters.Add("Lua", LuaRun);
        convLib.NAryConverters.Add("Lua", LuaRunN);
    }

    object LuaRun(object val, string[] args)
    {
        var fnName = args[0];
        if (LuaState[fnName] is not LuaFunction fn)
            throw new ArgumentException("Unknown lua func " + fnName);
        return fn.Call(val, args[1..]).First();
    }

    object LuaRunN(object[] vals, string[] args)
    {
        var fnName = args[0];
        if (LuaState[fnName] is not LuaFunction fn)
            throw new ArgumentException("Unknown lua func " + fnName);
        return fn.Call(vals, args[1..]).First();
    }
}

public class JsConverter : IDisposable
{
    public JsConverter(string js, string modulesDir = null, Action<object> log = null)
    {
        LibScript = js;

        JsEngine = new Jint.Engine(options => {
            options.LimitMemory(5_000_000);
            options.TimeoutInterval(TimeSpan.FromSeconds(4));
            options.MaxStatements(1000);
            if (!string.IsNullOrEmpty(modulesDir))
                options.EnableModules(modulesDir);
        });

        if (log != null)
        {
            JsEngine.SetValue("log", log);
            void Dump(object obj) => log(ObjectDumper.Dump(obj));
            JsEngine.SetValue("dump", Dump);
        }

        object ConvertDelegate(string typeName, object val)
        {
            var methodName = "To" + typeName;
            var method = typeof(Convert).GetMethod(methodName, new Type[] { val.GetType() });

            if (method == null)
                throw new ArgumentException("Unknown method " + methodName);

            return method.Invoke(null, new [] {val});
        }

        JsEngine.SetValue("Type", ConvertDelegate);

        JsEngine.Execute(LibScript);
    }

    public void Dispose()
        => JsEngine?.Dispose();

    public string LibScript { get; }
    public Jint.Engine JsEngine { get; }

    public void InitializeLib(ConverterLib convLib)
    {
        convLib.Converters.Add("Js", JsRun);
        convLib.NAryConverters.Add("Js", JsRunN);
    }

    object JsRun(object val, string[] args)
    {
        var fnName = args[0];
        return JsEngine.Invoke(fnName, val).ToObject();
    }

    object JsRunN(object[] vals, string[] args)
    {
        var fnName = args[0];
        return JsEngine.Invoke(fnName, (object) vals).ToObject();
    }
}

public class PathFinderConverters
{
    public void InitializeLib(ConverterLib convLib)
    {
        convLib.Converters.Add("ByPath", ByPath);
        convLib.NAryConverters.Add("ByPath", ByPathN);
    }

    public PathFinderConverters(Dictionary<string, object> globRef)
    {
        _pFinder = new PathFinder(globRef.Keys.ToArray(), globRef);
    }

    private readonly PathFinder _pFinder;

    object FindPath(object val, string path, bool multi)
        => _pFinder.GetValue(val, path, multi, out _, out _, out _, out _);

    object ByPath(object val, string[] args)
    {
        var path = args[0].Trim('"');
        return FindPath(val, path, multi: true);
    }

    object ByPathN(object[] vals, string[] args)
    {
        var path = args[0].Trim('"');
        return FindPath(vals, path, multi: true);
    }
}

public static class XPathConverters
{
    public static void InitializeLib(ConverterLib convLib)
    {
        convLib.Converters.Add("XPathNav", XPathNav);
        convLib.Converters.Add("XPathNavigator", XPathNav);
        convLib.Converters.Add("XPathDoc", XPathDoc);
        convLib.Converters.Add("XPathDocument", XPathDoc);
        convLib.Converters.Add("XPath", XPath);

        convLib.NAryConverters.Add("XPathNav", XPathNavN);
        convLib.NAryConverters.Add("XPathNavigator", XPathNavN);
        convLib.NAryConverters.Add("XPathDoc", XPathDocN);
        convLib.NAryConverters.Add("XPathDocument", XPathDocN);
        convLib.NAryConverters.Add("XPath", XPathN);
    }

    private static object XPathDoc(object val, string[] args)
    {
        var str = (string)val;
        str = str.Replace(" xmlns=\"", " whocares=\"");
        return new XPathDocument(new StringReader(str));
    }

    private static object XPathNav(object val, string[] args)
    {
        if (args.Length > 0)
            return _XPathNav(val, args);

        var str = (string)val;
        str = str.Replace(" xmlns=\"", " whocares=\"");
        var doc = new XPathDocument(new StringReader(str));
        return doc.CreateNavigator();
    }

    private static object XPathDocN(object[] val, string[] args)
        => val.Select(item => XPathDoc(item, args)).ToArray();

    private static object XPathNavN(object[] val, string[] args)
        => val.Select(item => XPathNav(item, args)).ToArray();

    private static object XPath(object val, string[] args)
    {
        var list = new List<string>();
        XPathAccum(list, val, args);
        return list.ToArray();
    }

    private static void XPathAccum(ICollection<string> list, object val, string[] args)
    {
        if (val == null)
            throw new ArgumentException(nameof(val) + " should not be null");

        if (val is string)
            val = XPathNav(val, Array.Empty<string>());

        var navigator = val is XPathDocument xpd ? xpd.CreateNavigator() : (XPathNavigator)val;

        if (args is not { Length: 1 })
            throw new ArgumentException("expect exactly one argument");

        string xpath = args[0].Trim('\'').Trim('"');

        XPathExpression expression = navigator.Compile(xpath);
        XPathNodeIterator iterator = navigator.Select(expression);

        while (iterator.MoveNext())
        {
            XPathNavigator item = iterator.Current;
            list.Add(item?.Value ?? string.Empty);
        }
    }

    private static object _XPathNav(object val, string[] args)
    {
        if (val == null)
            throw new ArgumentException(nameof(val) + " should not be null");

        if (val is string)
            val = XPathNav(val, Array.Empty<string>());

        var navigator = val is XPathDocument xpd ? xpd.CreateNavigator() : (XPathNavigator)val;

        if (args is not { Length: 1 })
            throw new ArgumentException("expect exactly one argument");

        string xpath = args[0].Trim('\'').Trim('"');

        XPathExpression expression = navigator.Compile(xpath);
        XPathNodeIterator iterator = navigator.Select(expression);

        var list = new List<XPathNavigator>();
        while (iterator.MoveNext())
            list.Add(iterator.Current?.CreateNavigator());

        return list.ToArray();
    }

    private static object XPathN(object[] val, string[] args)
    {
        // return val.Select(item => XPath(item, args)).ToArray();
        var list = new List<string>();
        foreach (var item in val)
            XPathAccum(list, item, args);
        return list.ToArray();
    }
}

public static class DefaultConverters
{
    public static void InitializeLib(ConverterLib convLib)
    {
        // Deprecated section. Use "Convert" instead.
        convLib.Converters.Add("ToDate", ToDate);
        convLib.Converters.Add("ToDecimal", ToDecimal);
        convLib.Converters.Add("ToInt32", ToInt32);
        convLib.Converters.Add("ToUInt32", ToUInt32);
        // End of deprecated section

        convLib.Converters.Add("Convert", ConvertFn);
        convLib.Converters.Add("ToString", ToString);
        convLib.Converters.Add("ToUniversalTime", ToUniversalTime);
        convLib.Converters.Add("ToLower", ToLower);
        convLib.Converters.Add("ToUpper", ToUpper);
        convLib.Converters.Add("AsFirstItemOfArray", AsFirstItemOfArray);
        convLib.Converters.Add("AsArrayWithOneItem", AsArrayWithOneItem);
        convLib.Converters.Add("Split", Split);
        convLib.Converters.Add("ConstValue", ConstValue);
        convLib.Converters.Add("Property", Property);
        convLib.Converters.Add("ItemProperty", ItemProperty);
        convLib.Converters.Add("ExprEval", ExprEval);
        convLib.Converters.Add("ParseDateTime", ParseDateTime);
        convLib.Converters.Add("First", First);
        convLib.Converters.Add("Last", Last);
        convLib.Converters.Add("ConvertArray", ConvertArray);
        convLib.Converters.Add("Check", CheckFn);
        convLib.Converters.Add("Filter", Filter);
        convLib.NAryConverters.Add("OneOf", OneOf);
        convLib.NAryConverters.Add("Join", Join);
        convLib.NAryConverters.Add("IfThenElse", IfThenElse);
        convLib.NAryConverters.Add("ExprEvalN", ExprEvalN);
        convLib.NAryConverters.Add("First", FirstN);
        convLib.NAryConverters.Add("Last", LastN);
        convLib.NAryConverters.Add("ConvertArray", ConvertArrayN);
        convLib.NAryConverters.Add("Filter", FilterN);
    }

    static object First(object vals, string[] args)
    {
        return ((object[])vals)?.FirstOrDefault();
    }

    static object Last(object vals, string[] args)
    {
        return ((object[])vals)?.LastOrDefault();
    }
    static object FirstN(object[] vals, string[] args)
    {
        return vals?.FirstOrDefault();
    }

    static object LastN(object[] vals, string[] args)
    {
        return vals?.LastOrDefault();
    }
    public static object ConvertFn(object val, string[] args)
    {
        if (val == null)
            return null;

        var methodName = args[0];
        var method = typeof(Convert).GetMethod(methodName, new Type[] { val.GetType() });

        if (method == null)
            throw new ArgumentException("Unknown method " + methodName);

        return method.Invoke(null, new [] {val});
    }

    static object ToDate(object val, string[] args)
    {
        return val == null ? null : Convert.ToDateTime(val);
    }

    static object ToDecimal(object val, string[] args)
    {
        return val == null ? null : Convert.ToDecimal(val);
    }

    static object ToString(object val, string[] args)
    {
        if (val is DateTime dt && args.Length > 0)
            return dt.ToString(args[0]);
        return val?.ToString();
    }

    static object ToUniversalTime(object val, string[] args)
    {
        if (val is string str)
        {
            if (!DateTime.TryParse(str, out var d))
                throw new ArgumentException("Invalid date string: " + val);
            val = d;
        }

        if (val is not DateTime dt)
            throw new ArgumentException("Invalid date: " + val);

        return dt.ToUniversalTime();
    }

    static object ToLower(object val, string[] args)
    {
        if (val == null)
            return null;

        return ((string)val).ToLower();
    }

    static object ToUpper(object val, string[] args)
    {
        if (val == null)
            return null;

        return ((string)val).ToUpper();
    }

    static object ToUInt32(object val, string[] args)
    {
        return Convert.ToUInt32(val);
    }

    static object ToInt32(object val, string[] args)
    {
        if (val is string s)
            return int.Parse(s);

        if (val is long longVal)
            return unchecked((int)longVal);

        return Convert.ToInt32(val);
    }

    static object AsArrayWithOneItem(object val, string[] args)
    {
        if (val == null)
            return null;

        var t = val.GetType();
        var a = Array.CreateInstance(t, 1);
        a.SetValue(val, 0);

        return a;
    }

    static object AsFirstItemOfArray(object val, string[] args)
    {
        return new string[] { (string)val };
    }

    static object Split(object val, string[] args)
    {
        if (val == null)
            return null;

        if (args.Length < 1)
            throw new Exception("expected delimiter");

        var delim = args[0];
        delim = Regex.Unescape(delim.Trim().Trim('"').Trim('"'));
        // Console.WriteLine("Delim is [" + delim + "]");

        return ((string)val).Split(delim);
    }

    static object ConstValue(object val, string[] args)
    {
        if (args.Length < 1)
            throw new Exception("expected value");

        return args[0];
    }

    static object Property(object val, string[] args)
    {
        if (args.Length < 1)
            throw new Exception("expected property");

        var propName = args[0];
        var dest = DestObject.Create(val);

        return dest.GetProperty(propName);
    }

    static object ItemProperty(object val, string[] args)
    {
        if (args.Length < 1)
            throw new Exception("expected property");

        if (val is not IEnumerable)
            throw new Exception("expected enumerable property");

        var propName = args[0];
        var dest = DestObject.Create(val);

        if (dest is DynamicDestObject)
        {
            var ret = dest.GetProperty(propName);
            // ret = (KeyValuePair<string, object>)ret;
            return ret;
        }

        return val;
    }

    static object OneOf(object[] vals, string[] args)
    {
        var order = vals.Select((_, i) => i)
            .ToArray();
        if (args.Length != 0 && args.Length != vals.Length)
            throw new Exception("expect 0 or "+vals.Length+" arguments");

        if (args.Length > 0)
        {
            for (uint i = 0; i < args.Length; ++i)
                order[i] = int.Parse(args[i]);
        }

        for (uint i = 0; i < order.Length; ++i)
        {
            if (vals[order[i]] != null)
                return vals[order[i]];
        }

        return null;
    }

    static object IfThenElse(object[] vals, string[] args)
    {
        var order = vals.Select((_, i) => i)
            .ToArray();
        if (args.Length != 0 && args.Length != vals.Length)
            throw new Exception("expect 0 or "+vals.Length+" arguments");

        if (args.Length > 0)
        {
            for (uint i = 0; i < args.Length; ++i)
                order[i] = int.Parse(args[i]);
        }

        for (uint i = 0; i < order.Length; ++i)
        {
            if (vals[order[i]] != null)
                return vals[order[i]];
        }

        return null;
    }

    static object Join(object[] vals, string[] args)
    {
        if (args.Length != 0 && args.Length != 1 && args.Length != vals.Length + 1)
            throw new Exception("expect 1 or "+(vals.Length+1)+" arguments");

        var delim = args.Length == 0 ? string.Empty : args[0].Trim('"');
        delim = System.Text.RegularExpressions.Regex.Unescape(delim);
        args = args.Length > 1 ? args[1..] : Array.Empty<string>();

        var order = vals.Select((_, i) => i)
            .ToArray();
        if (args.Length > 0)
            for (uint i = 0; i < args.Length; ++i)
                order[i] = int.Parse(args[i]);

        List<string> ans = new();
        for (uint i = 0; i < order.Length; ++i)
        {
            var val = vals[order[i]];
            if (val != null)
                ans.Add(val.ToString());
        }

        return string.Join(delim, ans);
    }

    static object ExprEval(object val, string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("expression expected");
        Expression expr = new(args[0].Trim('"'));
        if (args.Length > 1)
            expr.Bind(args[1], val ?? 0);
        return expr.Eval();
    }

    static object ParseDateTime(object val, string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("format expected");
        if (val is DateTime)
            return val;
        string format = args[0].Trim('"').Trim('\'');
        if (DateTime.TryParseExact((string)val, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            return dt;
        return val;
    }

    static object ExprEvalN(object[] vals, string[] args)
    {
        Expression expr = new(args[0].Trim('"'));
        for (int i = 1; i < args.Length; ++i)
        {
            if ((i - 1) >= vals.Length)
                throw new ArgumentException("expected value for " + args[i]);
            expr.Bind(args[i], vals[i - 1]);
        }

        return expr.Eval();
    }

    public static bool NUnitDynamicCheck(object val, string expr)
    {
        // Parse expression
        var arr = expr.Split(".");
        var path = new (string constraint, object[] args)[arr.Length];
        var re = new Regex(@"\(([^\)]+)\)");
        for (int i = 0; i < arr.Length; ++i)
        {
            var s = arr[i];
            path[i].constraint = re.Replace(s, "");
            var a = re.Match(s);
            path[i].args = a.Groups[1].ToString().Split(",").Select(x => x.Trim()).ToArray();
        }

        return NUnitDynamicCheck(val, path);
    }

    public static bool NUnitDynamicCheck(object val, (string constraint, object[] args)[] path)
    {
        object ret = null;
        Type prevType = typeof(NUnit.Framework.Is);
        foreach (var item in path)
        {
            var prop = prevType.GetProperty(item.constraint);
            if (prop != null)
                ret = prop.GetValue(ret);
            else
            {
                MethodInfo method = prevType.GetMethod(item.constraint);
                if (method == null)
                    throw new Exception("property or method not found " + item + ": " + string.Join(".", path));
                ret = method.Invoke(ret, item.args);
            }

            prevType = ret.GetType();
        }

        if (ret == null)
            throw new Exception("property not found " + path.Last() + ": " + string.Join(".", path));

        if (ret is not IResolveConstraint expr)
            throw new Exception("property " + path.Last() + " is not resolve constraint: " + string.Join(".", path));

        var constraint = expr.Resolve();
        var result = constraint.ApplyTo(val);
        return result.IsSuccess;
    }

    static object CheckFn(object val, string[] args)
    {
        return NUnitDynamicCheck(val, args[0]);
    }

    static object FilterN(object[] vals, string[] args)
        => Filter(vals, args);

    static object Filter(object val, string[] args)
    {
        if (val == null)
            return null;

        if (!val.GetType().IsArray)
            return null;

        var src = (Array)val;
        if (src.Length == 0)
            return src;

        var type = src.GetType().GetElementType();
        if (type == null)
            throw new Exception("not array");

        var expr = args[0];

        var listType = typeof(List<>);
        var constructedListType = listType.MakeGenericType(type);
        var dst = (IList)Activator.CreateInstance(constructedListType);

        var len = src.Length;
        int j = 0;
        for (int i = 0; i < len; ++i)
        {
            if (NUnitDynamicCheck(src.GetValue(i), expr))
                dst.Add(src.GetValue(i));

            j++;
        }

        MethodInfo toArray = constructedListType.GetMethod("ToArray");
        return toArray.Invoke(dst, Array.Empty<object>());
    }

    static object ConvertArray(object val, string[] args)
    {
        if (val == null)
            return null;

        if (!val.GetType().IsArray)
            return null;

        var typeStr = args[0];
        var type = Type.GetType(typeStr);
        if (type == null)
            throw new ArgumentException("wrong type " + typeStr);

        var src = (Array)val;
        var len = src.Length;
        var dst = Array.CreateInstance(type, len);
        if (args.Length > 0)
        {
            for (int i = 0; i < len; ++i)
                dst.SetValue(ConvertFn(src.GetValue(i), args[1..]), i);
        }
        else
        {
            for (int i = 0; i < len; ++i)
                dst.SetValue(src.GetValue(i), i);
        }

        return dst;
    }

    static object ConvertArrayN(object[] vals, string[] args)
        => ConvertArray(vals, args);
}
