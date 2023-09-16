using Jint;

namespace ConvPipe.Converters;

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

  public void InitializeLib(Pipe convLib)
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