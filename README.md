# ConvPipe

A C# library for data conversion.

This is useful when you need to arbitrary transform your data in the application 
depending on the passed configuration.

```csharp
var arr = Enumerable.Range(1, 100).ToArray();
var cl = ConverterLib.CreateWithDefaults();
var ans = cl.RunPipe("Int64[] | EachEval[Int64] 'IF(a%2==1, a+1, a)' a | ReduceEval[Int64] 'acc + v' acc v", arr);
// ans == 5100
```

```csharp
var emails = new[] { "", "pushkin@mail.com", null, "a@b.c", "d@e.f" };
var cl = ConverterLib.CreateWithDefaults();
var ans = cl.RunPipe("Filter Not.Null.And.Not.Empty", emails);
// ans == new[] {"pushkin@mail.com", "a@b.c", "d@e.f"}
```