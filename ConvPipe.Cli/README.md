# ConvPipe.Cli

Консольная утилита для преобразования данных и библиотека на C#.

## Build 

```bash
dotnet publish -c release -r linux-x64 \
  -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:SelfContained=true
```

## Install

В домашней директории создайте директорию `Bin` (или другое имя), если у вас ее нет и пропишите ее в `$PATH`.

В директории `Bin` создайте директорию `ConvPipe`. Это будет базовая директория.

Скопируйте туда бинарник, файл `config.json` и при желании файлы js, lua библиотек.

Создайте симлинк на бинарник в директории `~/Bin`. Убедитесь, что у него есть права на исполнение.

```bin
mkdir -p ~/Bin/ConvPipe
cp -pr bin/release/net6.0/linux-x64/publish/* ~/Bin/ConvPipe
chmod ug+x ~/Bin/ConvPipe/ConvPipe.Cli
ln -s ~/Bin/ConvPipe/ConvPipe.Cli ~/Bin/convpipe
echo 'export PATH=$PATH:~/Bin' >> ~/.bashrc && source ~/.bashrc
```

## Converters

Один из самых мощных конвертеров является `Convert`. Он соответствует одноименному классу в C#,
вызывая через рефлексию его методы.

Пример:

```bash
convpipe "Convert ToInt32" 7 -n
```

Output:

```text
7
```

Конвертер `ByPath` возвращает по переданному пути подъобъект из `json`.

Конвертер `ConstValue` возвращает константное значение, переданное в качестве аргумента.

// TODO Converter list

## Extension

Для добавления своих конвертеров есть 2 способа сделать это:
1. Написать js или lua функцию в соответствующей библиотеке и подключить ее (см. примеры ниже)
2. Написать расширение на C# и пересобрать проект

Для добавление расширения вы должны добавить свои конвертеры в программу.

Это можно сделать в файле `Program.cs` рядом с комментарием:

```text
// Adding a new converter extensions here!
```

Чтобы не захламлять код основной программы я предлагаю у вашего расширения сделать статический метод,
который будет добавлять конвернеры, описанные в классе.

Пример:

```csharp
public class PathFinderConverters
{
    public void InitializeLib(ConverterLib convLib)
    {
        convLib.Converters.Add("ByPath", ByPath);
        convLib.NAryConverters.Add("ByPath", ByPathN);
    }
    
    ...
```

Тогда регистрация расширения будет выглядеть так:

```csharp
// cl ConverterLib instance
PathFinderConverters.InitializeLib(cl);
```

// TODO Dynamic extension adding

## Usage

### Simple usage

```bash
convpipe "Convert ToInt32" "7" -n
```

Show help:
```bash
convpipe --help
```

### Libraries

Для подключения необходимо указать в конфигурационном файле путь до библиотек. 
Путь к самому конфигурационному файлу можно указать с помощью опции `-c`, `--config` (по умолчанию - `config.json`).

Поддерживается 2 языка для библиотек:
* js
* lua

Структуру конфигурационного файла наглядно отражает класс Config:

```csharp
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
```

Пути до файлов и директорий могут быть относительными. 

#### Call a js function from single file

Set config parameter `JslibFile` value `lib.js`

Run:

```bash
convpipe "Js Hello" "World" -n
```

Output:

```text
Hello World!
```

#### Call a js function from directory

Set config parameter "JsLibDirectory" value "jsLib"

Run:

```bash
convpipe "Convert ToInt32 | Js inc" 15 -n
```

Output:

```text
16
```

Обратите внимание, что предварительно необходимо преобразовать входную строку в число.

##### Call a lua function from file

Set config parameter "LuaLibFile" value "lib.lua".

Run:

```bash
conpipe "Lua Hello" "World" -n
```

Output:

```text
Hello World!
```

#### Call a lua function from directory

Set config parameter "LuaLibDirectory" value "luaLib"

Run:

```bash
convpipe "Convert ToInt32 | Lua sqr" 7 -n
```

Output:

```text
49
```

### ByPath with Json

Content of `fixtures/a.json`:

```json
{
  "a": {
    "b": {
      "c": "Propellerheads"
    }
  }
}
```

#### Read from json file and return json object by path:
```bash
convpipe "ByPath a.b" _ -n -j --file "fixtures/a.json"
```

Output:
```json
{"c":"Propellerheads"}
```

#### Read from json file and return property value by path:
```bash
convpipe "ByPath a.b.c" _ -n -j --file "fixtures/a.json"
```

Output:
```text
Propellerheads
```

When there is not nothing by the path just return empty string.