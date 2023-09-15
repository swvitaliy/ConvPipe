using System.Dynamic;

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
    public void Int32OddPlusOneSumTest()
    {
        var arr = Enumerable.Range(1, 100).ToArray();
        var cl = ConverterLib.CreateWithDefaults();
        cl.Converters.Add("Int64OddPlusOne", object (object val, string[] args) => (long)val % 2 == 1 ? (long)val + 1 : (long)val);
        cl.Converters.Add("SumInt64", object (object val, string[] args) => ((long[])val).Aggregate((a, b) => a + b));
        var ans1 = cl.ConvertPipe("Int64[] | Each Type[System.Int64] Int64OddPlusOne | SumInt64", arr);
        var ans2 = cl.ConvertPipe("Int64[] | Each TypeOf Int64OddPlusOne | SumInt64", arr);
        Assert.AreEqual(ans1, 5100);
        Assert.AreEqual(ans2, 5100);
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

    [Test]
    public void Js3Test()
    {
        const string jsLib = @"
function fn(a) {
    return a.TestList.Count;
}
function g(a) {
   a.TestList.forEach((item) => {
        log('I')
        item.Name = 'B';
    });
}
";

        ExpandoObject record = new();
        var dict = (IDictionary<string, object>)record;
        dict["Name"] = "A";
        dict["Value"] = "L";
        var obj = new ListOwner();
        obj.TestList.Add(record);

        var cl = new ConverterLib();
        var luaConv = new JsConverter(jsLib, null, Console.WriteLine);
        luaConv.InitializeLib(cl);
        cl.ConvertPipe("Js g", obj);
        var ans = cl.ConvertPipe("Js fn", obj);
        Assert.AreEqual(1, ans);
        Assert.AreEqual("B", obj.TestList[0].Name);
    }

    [Test]
    public void Xml1Test()
    {
        const string xml = @"
<root>
    <books>
        <book><title>Three Musketeers</title><author>A. Duma</author><published_at>1844</published_at></book>
        <book><title>Mysterious Island</title><author>Jules Verne</author><published_at>1875</published_at></book>
        <book><title>War and Peace</title><author>Leo Tolstoy</author><published_at>1869</published_at></book>
    </books>
</root>
";

        string[] xmlArr =
        {
            @"<root><books>
<book><title>Three Musketeers</title><author>A. Duma</author><published_at>1844</published_at></book>
</books></root>",
            @"<root><books>
<book><title>Mysterious Island</title><author>Jules Verne</author><published_at>1875</published_at></book>
</books></root>",
            @"<root><books>
<book><title>War and Peace</title><author>Leo Tolstoy</author><published_at>1869</published_at></book>
</books></root>",
        };

        var cl = new ConverterLib();
        XPathConverters.InitializeLib(cl);
        var ans = cl.ConvertPipe(@"XPathDoc | XPath '/root/books/book/title'", xml);

        Assert.IsTrue(ans is string[]);
        var arr = (string[])ans;
        Assert.AreEqual("Three Musketeers", arr[0]);
        Assert.AreEqual("War and Peace", arr[2]);

        DefaultConverters.InitializeLib(cl);

        var first = cl.ConvertPipe(@"XPath '/root/books/book/author' | First", xml);
        var last = cl.ConvertPipe(@"XPathNav '/root/books' | XPath 'book/author' | Last", xml);

        Assert.IsTrue(first is string);
        Assert.IsTrue(last is string);
        Assert.AreEqual("A. Duma", first);
        Assert.AreEqual("Leo Tolstoy", last);

        var ret = cl.ConvertPipe(@"XPathDoc | XPath '/root/books/book/published_at' | Each ToInt32", xmlArr);

        Assert.IsTrue(ret is object[]);
        var a = (object[])ret;
        Assert.AreEqual(3, a.Length);
        Assert.AreEqual(1844, a[0]);
        Assert.AreEqual(1875, a[1]);
        Assert.AreEqual(1869, a[2]);

    }

    private class Itm
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    private class ListOwner {
        public List<dynamic> TestList { get; set; } = new();
    }
}