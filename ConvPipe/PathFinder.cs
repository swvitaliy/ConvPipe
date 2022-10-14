using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ConvPipe;

public class PathFinder
{
    public class ItemFinder
    {
        public string Container;
        public string Key;
        public string Value;
    }

    private static ItemFinder NewItemFinder(string container, string key, string value)
        => new ()
        {
            Container = container,
            Key = key,
            Value = value,
        };

    private static bool IsItemFinder(string str)
        => str is { Length: > 0 } && str.IndexOf('[') >= 0 && str.Last() == ']';

    private static ItemFinder ParseItemFinder(string str)
    {
        var i = str.IndexOf('[');
        var j = str.IndexOf('=');
        return NewItemFinder(str[..i].Trim(), str[(i+1)..j].Trim(), str[(j+1)..^1].Trim());
    }

    public PathFinder(string[] varNames, Dictionary<string, object> globRef)
    {
        _varNames = varNames;
        _vars = globRef;
    }

    private readonly string[] _varNames = null;
    private readonly Dictionary<string, object> _vars = null;

    static object ConvertFn(object val, string methodName)
    {
        if (val == null)
            return null;

        var method = typeof(Convert).GetMethod(methodName, new Type[] { val.GetType() });

        if (method == null)
            throw new ArgumentException("Unknown method " + methodName);

        return method.Invoke(null, new [] {val});
    }

    private object ConvertValue(string str)
    {
        if (str is { Length: > 0 } && str.IndexOf('(') > 0 && str.Last() == ')')
        {
            var i = str.IndexOf('(');
            var c = str[..i];
            var v = str[(i + 1)..^1];
            return ConvertFn(v, "To" + c);
        }

        return str;
    }

    private object ResolveFinderValue(string str)
    {
        if (_varNames is { Length: > 0 })
        {
            if (str is { Length: > 0 } && str[0] == '@')
            {
                string varName = str[1..];
                if (_varNames.Contains(varName) && !_vars.ContainsKey(varName))
                    throw new Exception("unknown global " + varName);

                var obj = _vars[varName];
                return obj is string str2 ? ConvertValue(str2) : obj;
            }
        }

        return ConvertValue(str);
    }

    private Func<object, bool> ResolveFinder(IDestObject tmp, string str, out bool hasContainer, out ItemFinder itemFinder,
        out IEnumerable<object> list)
    {
        var t = itemFinder = ParseItemFinder(str);
        if (string.IsNullOrEmpty(t.Container))
        {
            hasContainer = true;
            list = (IEnumerable<object>)tmp.Origin;
        }
        else if (!tmp.ContainsKey(t.Container))
        {
            hasContainer = false;
            list = null;
            return null;
        }
        else
        {
            hasContainer = true;
            list = (IEnumerable<object>)tmp[t.Container];
        }

        var v = ResolveFinderValue(t.Value);
        Func<object, bool> fn;
        if (v is IList l)
            fn = (obj) =>
            {
                var d = DestObject.Create(obj);
                return d.ContainsKey(t.Key) && l.IndexOf(d[t.Key]) >= 0;
            };
        else
            fn = (obj) =>
            {
                var d = DestObject.Create(obj);
                return d.ContainsKey(t.Key) && d[t.Key].Equals(v);
            };
        return fn;
    }

    public object GetValue(object tmp, string pathStr, bool multi, out string errMiddleElem,
        out bool hasElem, out string[] path)
        => GetValue(DestObject.Create(tmp), pathStr, multi, out errMiddleElem, out hasElem, out _, out path);
    public object GetValue(object tmp, string pathStr, bool multi, out string errMiddleElem,
        out bool hasElem, out ItemFinder itemFinder, out string[] path)
        => GetValue(DestObject.Create(tmp), pathStr, multi, out errMiddleElem, out hasElem, out itemFinder, out path);

    public object GetValue(IDestObject tmp, string pathStr, bool multi, out string errMiddleElem,
        out bool hasElem, out ItemFinder itemFinder, out string[] path)
    {
        path = pathStr.Split('.');
        errMiddleElem = null;
        hasElem = false;
        itemFinder = null;
        for (int i = 0; i < path.Length; ++i)
        {
            if (IsItemFinder(path[i]))
            {
                var fn = ResolveFinder(tmp, path[i], out var hasContainer, out itemFinder, out var list);
                if (!hasContainer)
                {
                    errMiddleElem = path[i];
                    break;
                }

                if (multi)
                {
                    var subList = list.Where(fn).ToArray();
                    if (i == path.Length - 1)
                        return subList;

                    var ans = new List<object>();
                    foreach (var subItem in subList)
                    {
                        var r = GetValue(subItem, string.Join('.', path[(i+1)..]), multi: false, out errMiddleElem, out hasElem, out _);
                        ans.Add(r);
                    }

                    return ans.ToArray();
                }

                var item = list.FirstOrDefault(fn);
                if (item == null)
                {
                    errMiddleElem = path[i];
                    break;
                }

                if (i == path.Length - 1)
                {
                    hasElem = true;
                    if (item is IDestObject d)
                        return d.Origin;
                    return item;
                }

                tmp = DestObject.Create(item);
                continue;
            }

            if (i == path.Length - 1)
            {
                hasElem = tmp.ContainsKey(path[i]);
                return !hasElem ? null : tmp[path.Last()];
            }

            if (!tmp.ContainsKey(path[i]))
            {
                errMiddleElem = path[i];
                break;
            }

            tmp = DestObject.Create(tmp[path[i]]);
        }

        return null;
    }
}

