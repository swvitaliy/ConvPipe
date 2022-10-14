using System;
using ConvPipe;
using NLua;
using NUnit.Framework;

namespace ConvPipe.Tests;

internal class SimpleRecord
{
    public long Id { get; set; }
    public string Name { get; set; }
}

internal class SimpleRecord2
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public enum Gender
{
    Male = 0,
    Female,
    Other,
    Unknown,
}

public class ConverterTests
{
    [Test]
    public void ToInt32Test()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("Convert ToInt32", (long)123);
        Assert.AreEqual(r, 123);
    }

    [Test]
    public void AsArrayWithOneItemTest()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("Convert ToInt32 | AsArrayWithOneItem", (long)123);
        Assert.NotNull(r);
        Assert.AreEqual(r.GetType(), typeof(int[]));
        Assert.AreEqual((r as int[]).Length, 1);
        Assert.AreEqual((r as int[])[0], 123);
    }

    [Test]
    public void AsArrayWithOneItemTest2()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("AsArrayWithOneItem", new SimpleRecord() { Id = 1, Name = "A" });
        Assert.NotNull(r);
        Assert.AreEqual(r.GetType(), typeof(SimpleRecord[]));
        Assert.AreEqual((r as SimpleRecord[]).Length, 1);
        Assert.AreEqual((r as SimpleRecord[])[0].Id, 1);
        Assert.AreEqual((r as SimpleRecord[])[0].Name, "A");
    }

    [Test]
    public void ExprEvalTest0()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("ExprEval \"4 - 1\"", null);
        Assert.NotNull(r);
        Assert.AreEqual(r, 3);
    }


    [Test]
    public void ExprEvalTest1()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("ExprEval \"a - 1\" a", 7);
        Assert.NotNull(r);
        Assert.AreEqual(r, 6);
    }

    [Test]
    public void ExprEvalNTest()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipeArray("ExprEvalN \"a - b\" a b", new object[] {7, 3});
        Assert.NotNull(r);
        Assert.AreEqual(r, 4);
    }

    class A
    {
        public Gender g { get; set; }
    }

    [Test]
    public void GenderTest()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("ExprEval \"a - 1\" a | Convert ToInt32", 2);
        var a = new A();
        var d = new TypedDestObject<A>(a);
        d.SetProperty("g", r);
        Assert.AreEqual(a.g, Gender.Female);
    }

    [Test]
    public void ConvertTest0()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("Convert ToUInt64", null);
        Assert.AreEqual(r, null);
    }

    [Test]
    public void ConvertTest1()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("Convert ToInt32", "17");
        Assert.AreEqual(r, 17);
    }

    [Test]
    public void ConvertTest2()
    {
        var cl = ConverterLib.CreateWithDefaults();
        var r = cl.ConvertPipe("Convert ToDateTime", "2022-02-01");
        Assert.AreEqual(r, DateTime.Parse("2022-02-01"));
    }

    [Test]
    public void Lua1Test()
    {
        const string luaLib = @"
function fn(a)
    local b = ""world!"";
    return a .. "" "" .. b;
end
";

        var cl = new ConverterLib();
        var luaConv = new LuaConverters(luaLib);
        luaConv.InitializeLib(cl);
        var ans = cl.ConvertPipe("Lua fn", "hello");
        Assert.AreEqual("hello world!", ans);
    }

    [Test]
    public void Lua2Test()
    {
        const string luaLib = @"
function first(a)
    return a[0];
end

function last(a)
    return a[a.Length-1];
end

function count(a)
    return a.Length;
end
";

        var cl = new ConverterLib();
        var luaConv = new LuaConverters(luaLib);
        luaConv.InitializeLib(cl);
        var arr = new string[] { "hello", ",", "world", "!" };
        var f = cl.ConvertPipeArray("Lua first", arr);
        var l = cl.ConvertPipeArray("Lua last", arr);
        var c = cl.ConvertPipeArray("Lua count", arr);
        Assert.AreEqual("hello", f);
        Assert.AreEqual("!", l);
        Assert.AreEqual(4, c);
    }

    [Test]
    public void Js1Test()
    {
        const string jsLib = @"
function fn(a) {
    let b = ""world!"";
    return a + ' ' + b;
}
";

        var cl = new ConverterLib();
        var luaConv = new JsConverter(jsLib);
        luaConv.InitializeLib(cl);
        var ans = cl.ConvertPipe("Js fn", "hello");
        Assert.AreEqual("hello world!", ans);
    }

    [Test]
    public void Js2Test()
    {
        const string jsLib = @"
function first(a) {
    return a[0];
}

function last(a) {
    return a[a.length-1];
}

function count(a) {
    return a.length;
}
";

        var cl = new ConverterLib();
        var jsConv = new JsConverter(jsLib, null);
        jsConv.InitializeLib(cl);
        var arr = new string[] { "hello", ",", "world", "!" };
        var f = cl.ConvertPipeArray("Js first", arr);
        var l = cl.ConvertPipeArray("Js last", arr);
        var c = cl.ConvertPipeArray("Js count", arr);
        Assert.AreEqual("hello", f);
        Assert.AreEqual("!", l);
        Assert.AreEqual(4, c);
    }
}