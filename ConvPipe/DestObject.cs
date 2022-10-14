using System.Collections;
using System.Reflection;

namespace ConvPipe;

public interface IDestObject
{
    public object this[string key] { get; set; }
    public object GetProperty(string name);
    public void SetProperty(string name, object val);
    public bool ContainsKey(string name);
    public object Origin { get; }
}

public static class DestObject
{
    public static IDestObject Create(object obj)
    {
        if (obj is IDestObject d)
            return d;

        if (obj is IDictionary<string, object> dynObj)
            return new DynamicDestObject(dynObj);

        Type genericType = typeof(TypedDestObject<>);
        Type type = genericType.MakeGenericType(obj.GetType());
        ConstructorInfo ci = type.GetConstructor(new Type[] { obj.GetType() });

        if (ci == null)
            throw new Exception("TypedDestObject.ctor not found for type " + obj.GetType());

        return (IDestObject)ci.Invoke(new object[] { obj });
    }
}

public class DynamicDestObject : IDestObject
{
    public object this[string key]
    {
        get => GetProperty(key);
        set => SetProperty(key, value);
    }

    public DynamicDestObject(IDictionary<string, object> obj)
    {
        Obj = obj;
    }

    private IDictionary<string, object> Obj { get; set; }
    public object Origin => Obj;

    public object GetProperty(string name)
    {
        return Obj[name];
    }

    public void SetProperty(string name, object val)
    {
        Obj[name] = val;
    }

    public bool ContainsKey(string name)
    {
        return Obj.ContainsKey(name);
    }
}

public interface ITypedDestObject : IDestObject
{
    public bool IsArray(string propName);
    Type GetPropertyType(string propName);
    public Type GetElementType(string propName);
}

public class TypedDestObject<T> : ITypedDestObject
{
    public object this[string key]
    {
        get => GetProperty(key);
        set => SetProperty(key, value);
    }

    public TypedDestObject(T obj)
    {
        if (obj == null)
            throw new ArgumentException("object cannot be null");

        Obj = obj;
    }

    public IEnumerable AsEnumerable() => (IEnumerable)Obj;

    private T Obj { get; set; }
    public object Origin => Obj;

    public bool IsArray(string propName)
    {
        var prop = GetProp(propName);
        return prop.PropertyType.IsArray;
    }

    public Type GetPropertyType(string propName)
    {
        var prop = GetProp(propName);
        return prop.PropertyType;
    }

    public Type GetElementType(string propName)
    {
        var prop = GetProp(propName);
        return prop.PropertyType.GetElementType();
    }

    private PropertyInfo GetProp(string name, bool raiseException = true)
    {
        var prop = Obj.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty);

        if (!(prop != null && prop.CanWrite))
        {
            if (raiseException)
                throw new Exception("cannot access property " + name);

            return null;
        }

        return prop;
    }

    public object GetProperty(string name)
    {
        var prop = GetProp(name);
        return prop.GetValue(Obj);
    }

    public void SetProperty(string name, object val)
    {
        var prop = GetProp(name);
        prop.SetValue(Obj, val);
    }

    public bool ContainsKey(string name)
    {
        return GetProp(name, raiseException: false) != null;
    }
}