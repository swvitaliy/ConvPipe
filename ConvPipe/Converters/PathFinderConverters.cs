namespace ConvPipe.Converters;

public class PathFinderConverters
{
  public void InitializeLib(Pipe convLib)
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