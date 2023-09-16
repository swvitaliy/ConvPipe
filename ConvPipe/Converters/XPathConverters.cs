using System.Xml.XPath;

namespace ConvPipe.Converters;

public static class XPathConverters
{
  public static void InitializeLib(Pipe convLib)
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