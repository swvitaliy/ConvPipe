// See https://aka.ms/new-console-template for more information

using System;
using System.Dynamic;
using CommandLine;
using Newtonsoft.Json;
using ConvPipe;

CommandLine.Parser.Default.ParseArguments<Options>(args)
    .WithParsed(Handler.Run);

class Options
{
    [Value(0, MetaName = "Pipeline", HelpText = "Pipe string. Each command is separated by '|' from each other.")]
    public string Pipeline { get; set; } = string.Empty;

    [Value(1, MetaName = "Input value",
        HelpText = "Input value. If it is equals to '-' then need to read from stdin.")]
    public string Input { get; set; } = string.Empty;

    [Option('f', "file", HelpText = "Read from file.")]
    public string InputFile { get; set; } = string.Empty;

    [Option('j', "json", HelpText = "Detect that input is json.")]
    public bool Json { get; set; }

    [Option('c', "config", HelpText = "Path to the config file.", Default = "config.json")]
    public string Config { get; set; }

    [Option('n', "newline", HelpText = "Added newline symbol at the end of the output.")]
    public bool Newline { get; set; }
}

class Config
{
    // Путь до файла lua библиотеки
    public string LuaLibFile { get; set; }

    // Путь до директории с файлами lua библиотек
    public string LuaLibDirectory { get; set; }

    // Путь до файла js библиотеки
    public string JsLibFile { get; set; }

    // Путь до директории с файлами js библиотек
    public string JsLibDirectory { get; set; }

    private string ReadAllDirectory(string dirPath)
    {
        var acc = "";
        foreach (var filePath in Directory.GetFiles(dirPath))
            acc += File.ReadAllText(filePath);

        return acc;
    }

    public string GetJsScript()
    {
        if (!string.IsNullOrEmpty(JsLibFile) && !string.IsNullOrEmpty(JsLibDirectory))
            throw new ArgumentOutOfRangeException("Both mutually exclusive options (JsLibFile and JsLibDirectory) are setting up.");

        if (!string.IsNullOrEmpty(JsLibFile))
            return File.ReadAllText(ResolvePath(JsLibFile));

        if (!string.IsNullOrEmpty(JsLibDirectory))
            return ReadAllDirectory(ResolvePath(JsLibDirectory));

        if (!string.IsNullOrEmpty(JsLibFile))
            return File.ReadAllText(ResolvePath(JsLibFile));

        return string.Empty;
    }

    public string GetLuaScript()
    {
        if (!string.IsNullOrEmpty(LuaLibFile) && !string.IsNullOrEmpty(LuaLibDirectory))
            throw new ArgumentOutOfRangeException("Both mutually exclusive options (LuaLibFile and LuaLibDirectory) are setting up.");

        if (!string.IsNullOrEmpty(LuaLibFile))
            return File.ReadAllText(ResolvePath(LuaLibFile));

        if (!string.IsNullOrEmpty(LuaLibDirectory))
            return ReadAllDirectory(ResolvePath(LuaLibDirectory));

        if (!string.IsNullOrEmpty(LuaLibFile))
            return File.ReadAllText(ResolvePath(LuaLibFile));

        return string.Empty;
    }

    public static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
    }
}

static class Handler
{
    public static void Run(Options opts)
    {
        if (!string.IsNullOrEmpty(opts.InputFile))
            opts.Input = File.ReadAllText(Config.ResolvePath(opts.InputFile));

        else if (opts.Input == "-")
            opts.Input = Console.ReadLine() ?? string.Empty;

        object input = opts.Json ? (JsonConvert.DeserializeObject<ExpandoObject>(opts.Input) ?? new ExpandoObject()) : opts.Input;

        string luaScript = null;
        string jsScript = null;
        Dictionary<string, object> globVars = new();

        if (!string.IsNullOrEmpty(opts.Config) && opts.Config != "-")
        {
            var json = File.ReadAllText(Config.ResolvePath(opts.Config));
            var config = JsonConvert.DeserializeObject<Config>(json);
            if (config != null)
            {
                luaScript = config.GetLuaScript();
                jsScript = config.GetJsScript();
            }
        }

        var cl = ConverterLib.CreateWithDefaults(luaScript, jsScript, globVars);
        // Adding a new converter extensions here!

        var r = cl.RunPipe(opts.Pipeline, input);

        if (opts.Json && r is ExpandoObject)
            r = JsonConvert.SerializeObject(r);

        Console.Write(r + (opts.Newline ? "\n" : ""));
    }
}