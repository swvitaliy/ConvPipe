using NLua;

namespace ConvPipe.Converters;

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

  public void InitializeLib(Pipe convLib)
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