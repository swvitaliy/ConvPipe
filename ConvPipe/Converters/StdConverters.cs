using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework.Constraints;
using org.matheval;

namespace ConvPipe.Converters;

public static class StdConverters
{
  public static void InitializeLib(Pipe convLib)
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
    convLib.Converters.Add("Const", ConstValue);
    convLib.Converters.Add("Property", Property);
    convLib.Converters.Add("ItemProperty", ItemProperty);
    convLib.Converters.Add("ExprEval", ExprEval);
    convLib.Converters.Add("ExprEval_Typed", ExprEval_Typed);
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
    convLib.NAryConverters.Add("ExprEval", ExprEvalN);
    convLib.NAryConverters.Add("ExprEvalN_Typed", ExprEvalN_Typed);
    convLib.NAryConverters.Add("ExprEval_Typed", ExprEvalN_Typed);
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

  // it uses for call method by reflection
  public static T ExprEvalReflection<T>(Expression expr)
    => expr.Eval<T>();

  static object ExprEval_Typed(object val, string[] args)
  {
    if (args.Length < 2)
      throw new ArgumentException("expression expected");
    var exprStr = args[1].Trim('"').Trim('\'');
    Expression expr = new(exprStr);
    if (args.Length > 2)
      expr.Bind(args[2], val ?? 0);

    var typeName = args[0];
    var type = Pipe.ResolveType(typeName);
    if (type == null)
      throw new ArgumentException("unknown type " + typeName);

    MethodInfo method = typeof(StdConverters).GetMethod(nameof(ExprEvalReflection));
    MethodInfo generic = method.MakeGenericMethod(type);
    try
    {
      return generic.Invoke(null, new[] { expr });
    }
    catch (Exception e)
    {
      throw new Exception($"Error while evaluate expression \"{exprStr}\"", e);
    }
  }

  static object ExprEval(object val, string[] args)
  {
    if (args.Length == 0)
      throw new ArgumentException("expression expected");
    Expression expr = new(args[0].Trim('"').Trim('\''));
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

  static object ExprEvalN_Typed(object[] vals, string[] args)
  {
    var exprStr = args[1].Trim('"').Trim('\'');
    Expression expr = new(exprStr);
    for (int i = 2; i < args.Length; ++i)
    {
      if ((i - 2) >= vals.Length)
        throw new ArgumentException("expected value for " + args[i]);
      expr.Bind(args[i], vals[i - 2]);
    }

    var typeName = args[0];
    var type = Pipe.ResolveType(typeName);
    if (type == null)
      throw new ArgumentException("unknown type " + typeName);


    MethodInfo method = typeof(StdConverters).GetMethod(nameof(ExprEvalReflection));
    MethodInfo generic = method.MakeGenericMethod(type);
    try
    {
      return generic.Invoke(null, new[] { expr });
    }
    catch (Exception e)
    {
      throw new Exception($"Error while evaluate expression \"{exprStr}\"", e);
    }
  }

  static object ExprEvalN(object[] vals, string[] args)
  {
    Expression expr = new(args[0].Trim('"').Trim('\''));
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