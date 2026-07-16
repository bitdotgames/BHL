using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using bhl.marshall;
using ThreadTask = System.Threading.Tasks.Task;

#pragma warning disable CS8981

namespace bhl.taskman;

public static partial class Tasks
{
  static void dump_usage(string msg = "")
  {
    Console.WriteLine("Usage:");
    Console.WriteLine("bhl dump <path_to_bundle> [--symbols]");
    Console.WriteLine(msg);
    Environment.Exit(1);
  }

  [Task(verbose: false)]
  public static ThreadTask dump(Taskman tm, string[] args)
  {
    bool include_symbols = false;

    var p = new OptionSet()
    {
      {
        "symbols", "also dump declared namespaces/functions/classes/interfaces/enums with their types",
        v => include_symbols = v != null
      }
    };

    List<string> extra;
    try
    {
      extra = p.Parse(args);
    }
    catch(OptionException e)
    {
      dump_usage(e.Message);
      return ThreadTask.CompletedTask;
    }

    if(extra.Count == 0 || string.IsNullOrEmpty(extra[0]))
      dump_usage("No bundle file specified");

    string path = extra[0];
    if(!File.Exists(path))
      dump_usage("File not found: " + path);

    JObject root;
    try
    {
      root = DumpBundle(path, include_symbols);
    }
    catch(Exception e)
    {
      dump_usage("Not a valid bhl bundle: " + e.Message);
      return ThreadTask.CompletedTask;
    }

    Console.WriteLine(root.ToString(Formatting.Indented));

    return ThreadTask.CompletedTask;
  }

  static JObject DumpBundle(string path, bool include_symbols)
  {
    var root = new JObject();
    root["path"] = Path.GetFullPath(path);
    root["file_size"] = new FileInfo(path).Length;

    var modules = new JArray();
    var name2mod = new Dictionary<string, JObject>();
    var module_names = new List<string>();

    using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
    {
      var reader = new MsgPackDataReader(fs);

      byte file_format = 0;
      reader.ReadU8(ref file_format);
      if(file_format != ModuleLoader.COMPILE_FMT)
        throw new Exception("bad file format: " + file_format);
      root["format"] = file_format;

      uint file_version = 0;
      reader.ReadU32(ref file_version);
      root["version"] = file_version;

      int num_entries = 0;
      reader.ReadI32(ref num_entries);
      root["modules_count"] = num_entries;

      while(num_entries-- > 0)
      {
        int format = 0;
        reader.ReadI32(ref format);

        string name = "";
        reader.ReadString(ref name);

        int blob_len = 0;
        reader.ReadRawBegin(ref blob_len);
        var blob = ArrayPool<byte>.Shared.Rent(blob_len);
        reader.ReadRawEnd(blob);

        var mod_fmt = (ModuleBinaryFormat)format;

        var mod = new JObject();
        mod["name"] = name;
        mod["format"] = mod_fmt.ToString();
        mod["size"] = blob_len;

        //NOTE: FMT_FILE_REF entries store a file path instead of the module bytes,
        //      let's resolve it to report the actual referenced file size
        if(mod_fmt == ModuleBinaryFormat.FMT_FILE_REF)
        {
          string file_ref = Encoding.UTF8.GetString(blob, 0, blob_len);
          mod["file_ref"] = file_ref;
          if(File.Exists(file_ref))
            mod["file_ref_size"] = new FileInfo(file_ref).Length;
        }

        ArrayPool<byte>.Shared.Return(blob);

        modules.Add(mod);
        name2mod[name] = mod;
        module_names.Add(name);
      }
    }

    root["modules"] = modules;

    //NOTE: walking the actual symbol table requires fully loading each module
    //      (resolving imports/types via the VM), which is a lot more expensive
    //      than just reading the bundle's container header above.
    //      Each module gets its own fresh VM/Types/loader: if loading one module
    //      throws (e.g. a missing native binding), the VM is left with internal
    //      'loading in progress' state that has no public reset API, and reusing
    //      it would make every subsequent module fail with an unrelated
    //      "Already loading modules" error instead of its own real one
    if(include_symbols)
    {
      foreach(var name in module_names)
      {
        var mod = name2mod[name];
        try
        {
          var types = new Types();
          using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
          {
            var loader = new ModuleLoader(types, fs);
            var vm = new VM(types, loader);

            if(vm.LoadModule(name, out var module))
              mod["symbols"] = DumpNamespaceMembers(module.ns);
            else
              mod["symbols_error"] = "module could not be loaded";
          }
        }
        catch(Exception e)
        {
          mod["symbols_error"] = e.Message;
        }
      }
    }

    return root;
  }

  //NOTE: dumps only the symbols actually declared in 'ns' itself, skipping
  //      shadow namespaces created by Namespace.Link() for imported/builtin symbols
  static JArray DumpNamespaceMembers(Namespace ns)
  {
    var arr = new JArray();
    for(int i = 0; i < ns.members.Count; ++i)
    {
      var m = ns.members[i];
      if(m is Namespace child)
      {
        if(!child.IsLinkedShadow)
          arr.Add(DumpSymbol(child));
      }
      else
        arr.Add(DumpSymbol(m));
    }
    return arr;
  }

  static JObject DumpSymbol(Symbol sym)
  {
    switch(sym)
    {
      case Namespace ns:
        return new JObject
        {
          ["kind"] = "namespace",
          ["name"] = ns.name,
          ["members"] = DumpNamespaceMembers(ns)
        };

      case EnumSymbol en:
      {
        var values = new JArray();
        foreach(var item in en)
          if(item is EnumItemSymbol eis)
            values.Add(new JObject { ["name"] = eis.name, ["value"] = eis.val });

        return new JObject
        {
          ["kind"] = "enum",
          ["name"] = en.name,
          ["native"] = en is EnumSymbolNative,
          ["values"] = values
        };
      }

      case InterfaceSymbol ifs:
      {
        var inherits = new JArray();
        for(int i = 0; i < ifs.inherits.Count; ++i)
          inherits.Add(((Symbol)ifs.inherits[i]).GetFullTypePath().ToString());

        var methods = new JArray();
        foreach(var m in ifs)
          if(m is FuncSymbol fs)
            methods.Add(DumpSymbol(fs));

        return new JObject
        {
          ["kind"] = "interface",
          ["name"] = ifs.name,
          ["native"] = ifs is InterfaceSymbolNative,
          ["inherits"] = inherits,
          ["methods"] = methods
        };
      }

      case ClassSymbol cs:
      {
        var implements = new JArray();
        for(int i = 0; i < cs.implements.Count; ++i)
          implements.Add(((Symbol)cs.implements[i]).GetFullTypePath().ToString());

        //NOTE: iterating 'cs' gives the flattened member list including inherited
        //      ones (valid only once the class went through Setup()); filtering by
        //      scope keeps only the members actually declared in this class
        var members = new JArray();
        foreach(var m in cs)
          if(m.scope == cs)
            members.Add(DumpSymbol(m));

        return new JObject
        {
          ["kind"] = "class",
          ["name"] = cs.name,
          ["native"] = cs is ClassSymbolNative,
          ["super_class"] = ((Symbol)cs.super_class)?.GetFullTypePath().ToString(),
          ["implements"] = implements,
          ["members"] = members
        };
      }

      case FuncSymbol fn:
      {
        var fargs = new JArray();
        foreach(var a in fn)
          if(a is FuncArgSymbol arg && arg.name != "this")
            fargs.Add(new JObject
            {
              ["name"] = arg.name,
              ["type"] = arg.type.ToString(),
              ["is_ref"] = arg.is_ref
            });

        return new JObject
        {
          ["kind"] = "func",
          ["name"] = fn.name,
          ["native"] = fn is FuncSymbolNative,
          ["coro"] = fn.attribs.HasFlag(FuncAttrib.Coro),
          ["static"] = fn.attribs.HasFlag(FuncAttrib.Static),
          ["virtual"] = fn.attribs.HasFlag(FuncAttrib.Virtual),
          ["override"] = fn.attribs.HasFlag(FuncAttrib.Override),
          ["return_type"] = fn.signature.return_type.ToString(),
          ["args"] = fargs
        };
      }

      case VariableSymbol vs:
        return new JObject
        {
          ["kind"] = vs is GlobalVariableSymbol ? "gvar" : vs is FieldSymbol ? "field" : "var",
          ["name"] = vs.name,
          ["type"] = vs.type.ToString()
        };

      default:
        return new JObject
        {
          ["kind"] = sym.GetType().Name,
          ["name"] = sym.name
        };
    }
  }
}
