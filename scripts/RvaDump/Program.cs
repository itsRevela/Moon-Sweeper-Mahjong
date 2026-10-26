using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using LibCpp2IL;

// Usage:
//   RvaDump <GameAssembly.dll> <global-metadata.dat> <unity version, e.g. 6000.6.0f1> <out.tsv>
//     Writes one line per distinct code address: "0x<RVA>\t<Namespace.Type>$$<Method>". When several methods share an
//     address (identical code folding) the first in metadata order wins and the rest are listed after " | ".
//   RvaDump annotate <GameAssembly.dll> <global-metadata.dat> <unity version> <dir with .c files>
//     Decodes every DAT_<address> in the decompiled files that is an IL2CPP metadata-usage slot (string literal,
//     type, field or method; v27+ slots hold an encoded token until first use) and appends it as a comment.
if (args.Length == 5 && args[0] == "annotate")
    return Annotate(args[1], args[2], args[3], args[4]);
if (args.Length != 4)
{
    Console.Error.WriteLine("usage: RvaDump <GameAssembly.dll> <global-metadata.dat> <unity version> <out.tsv>");
    Console.Error.WriteLine("       RvaDump annotate <GameAssembly.dll> <global-metadata.dat> <unity version> <dir>");
    return 2;
}

var context = LibCpp2IlMain.LoadFromFileAsContext(args[0], args[1], UnityVersion.Parse(args[2]));
var byRva = new SortedDictionary<ulong, List<string>>();
foreach (var method in context.Metadata.methodDefs)
{
    if (method.MethodPointer == 0)
        continue;
    var type = method.DeclaringType;
    var name = (type?.FullName ?? "?") + "$$" + method.Name;
    if (!byRva.TryGetValue(method.Rva, out var names))
        byRva[method.Rva] = names = new List<string>();
    names.Add(name);
}

using (var writer = new StreamWriter(args[3]))
    foreach (var (rva, names) in byRva)
        writer.WriteLine($"0x{rva:X}\t{string.Join(" | ", names.Distinct())}");

Console.WriteLine($"RVADUMP OK: {byRva.Count} addresses, {byRva.Values.Sum(n => n.Count)} methods -> {args[3]}");
return 0;

static int Annotate(string binaryPath, string metadataPath, string unityVersion, string directory)
{
    var context = LibCpp2IlMain.LoadFromFileAsContext(binaryPath, metadataPath, UnityVersion.Parse(unityVersion));
    var dataRef = new Regex(@"DAT_(18[0-9a-f]{7})\b");
    var cache = new Dictionary<ulong, string?>();
    var files = 0;
    var resolved = 0;
    foreach (var path in Directory.GetFiles(directory, "*.c"))
    {
        var text = File.ReadAllText(path);
        var marker = text.IndexOf("\n// --- metadata usages", StringComparison.Ordinal);
        if (marker >= 0)
            text = text[..marker];
        var notes = new SortedDictionary<ulong, string>();
        foreach (Match match in dataRef.Matches(text))
        {
            var va = Convert.ToUInt64(match.Groups[1].Value, 16);
            if (!cache.TryGetValue(va, out var description))
                cache[va] = description = Describe(context, va);
            if (description != null)
                notes[va] = description;
        }
        if (notes.Count > 0)
        {
            var lines = notes.Select(n => $"//   DAT_{n.Key:x} = {n.Value}");
            File.WriteAllText(path, text.TrimEnd() + "\n\n// --- metadata usages (RvaDump annotate)\n" + string.Join("\n", lines) + "\n");
            resolved += notes.Count;
            files++;
        }
    }
    Console.WriteLine($"ANNOTATE OK: {resolved} usages in {files} files");
    return 0;
}

static string? Describe(LibCpp2IlContext context, ulong va)
{
    ulong encoded;
    try
    {
        encoded = context.Binary.ReadPointerAtVirtualAddress(va);
    }
    catch (Exception)
    {
        return null; // not in a mapped data section
    }
    if ((encoded & 1) == 0 || encoded > uint.MaxValue)
        return null; // already a pointer, or not a usage slot
    try
    {
        var usage = MetadataUsage.DecodeMetadataUsage(encoded, va, context);
        if (usage == null)
            return null;
        return usage.Type switch
        {
            MetadataUsageType.StringLiteral => "\"" + usage.AsLiteral().Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"",
            MetadataUsageType.TypeInfo or MetadataUsageType.Type => "typeof " + usage.AsType(),
            MetadataUsageType.FieldInfo => "field " + usage.AsField().DeclaringType?.FullName + "." + usage.AsField().Name,
            MetadataUsageType.MethodDef => "method " + usage.AsMethod().DeclaringType?.FullName + "$$" + usage.AsMethod().Name,
            _ => usage.ToString(),
        };
    }
    catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException)
    {
        return null; // an odd value that only looked like a token
    }
}
