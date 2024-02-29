using System;
using System.IO;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace bhl {

public class ANTLR_Preprocessor : bhlPreprocParserBaseVisitor<object>
{
  Module module;

  //NOTE: passed from above
  CompileErrors errors;
  ErrorHandlers err_handlers;

  Stream src;
  CustomInputStream input;
  CommonTokenStream tokens;

  public ANTLR_Parsed parsed { get; private set; }

  HashSet<string> defines;

  MemoryStream dst;
  StreamWriter writer;

  Dictionary<IParseTree, Annotated> annotated_nodes = new Dictionary<IParseTree, Annotated>();

  internal class Annotated
  {
    public IParseTree tree;
    public bool strip_condition;
  }

  internal struct IfBlock
  {
    internal IParseTree if_node;
    internal bool else_found;
    internal Annotated expression; 
  }

  Stack<IfBlock> ifs = new Stack<IfBlock>();

  const int SHARP_CODE = 35;

  public static Stream ProcessStream(
    Module module, 
    CompileErrors errors,
    ErrorHandlers err_handlers,
    Stream src, 
    HashSet<string> defines,
    out ANTLR_Parsed preproc_parsed
  )
  {
    preproc_parsed = null;

    var pos = src.Position;
    bool need_preproc = HasPossiblePreprocDirectives(src);
    //let's restore the original position
    src.Position = pos;
    if(!need_preproc)
      return src;
    
    var preproc = new ANTLR_Preprocessor(
      module, 
      errors, 
      err_handlers, 
      src, 
      defines
    );

    var dst = preproc.Process();

    preproc_parsed = preproc.parsed;

    return dst;
  }

  static bool HasPossiblePreprocDirectives(Stream src)
  {
    var buf = new byte[1024];

    int total_bytes = (int)src.Length;
    do
    {
      int n = src.Read(buf, 0, 1024); 

      //check if there's any # character
      for(int i=0;i<n;++i)
        if(buf[i] == SHARP_CODE)
          return true;

      total_bytes -= n;
    } while(total_bytes > 0);

    return false;
  }

  public ANTLR_Preprocessor(
    Module module, 
    CompileErrors errors, 
    ErrorHandlers err_handlers,
    Stream src, 
    HashSet<string> defines
  )
  {
    this.module = module;
    this.errors = errors;
    this.err_handlers = err_handlers;
    this.src = src;
    this.defines = defines;
  }

  public class CustomInputStream : AntlrInputStream
  {
    public char[] Data => data;

    public CustomInputStream(Stream src)
      : base(src)
    {}
  }

  public Stream Process()
  {                          
    input = new CustomInputStream(src);
    var lex = new bhlPreprocLexer(input);
    tokens = new CommonTokenStream(lex);
    var parser = new bhlPreprocParser(tokens);

    err_handlers?.AttachToParser(parser);

    dst = new MemoryStream();
    dst.Capacity = (int)src.Length;
    writer = new StreamWriter(dst);

    //NOTE: parsing happens here
    var parsed_tree = parser.program();
    parsed = new ANTLR_Parsed(parser, parsed_tree);

    VisitProgram(parsed_tree);

    CheckValidity();

    writer.Flush();
    dst.Position = 0;

    if(src.Length != dst.Length)
      throw new Exception("Stream lengths don't match, source: " + src.Length + ", dest: " + dst.Length);

    //for debug
    //Console.WriteLine(">>>> src len: " + src.Length + " VS dst len: " + dst.Length);
    //Console.WriteLine(System.Text.Encoding.UTF8.GetString(dst.GetBuffer(), 0 , (int)dst.Length));
    //Console.WriteLine("<<<<");

    return dst;
  }

  void CheckValidity()
  {
    foreach(var if_block in ifs)
    {
      AddError(if_block.if_node, "invalid usage");
    }
  }

  bool IsStripped()
  {
    if(ifs.Count == 0)
      return false;
    return ifs.Peek().expression.strip_condition == false;
  }

  public override object VisitProgram(bhlPreprocParser.ProgramContext ctx)
  {
    foreach(var item in ctx.text())
    {
      if(item.code() != null)
      {
        if(IsStripped())
          ConvertToWhiteSpace(item);
        else
          CopyBytes(item);
      }
      else
      {
        ConvertToWhiteSpace(item);
        Visit(item.directive());
      }
    }

    return null;
  }

  void CopyBytes(ParserRuleContext ctx)
  {
    var t1 = tokens.Get(ctx.Start.TokenIndex); 
    var t2 = tokens.Get(ctx.Stop.TokenIndex); 

    writer.Write(input.Data, t1.StartIndex, t2.StopIndex - t1.StartIndex + 1);
  }
  
  void ConvertToWhiteSpace(ParserRuleContext ctx)
  {
    var t1 = tokens.Get(ctx.Start.TokenIndex); 
    var t2 = tokens.Get(ctx.Stop.TokenIndex); 

    for(int i = t1.StartIndex; i < (t2.StopIndex + 1); ++i)
    {
      char c = input.Data[i];
      if(c != '\n')
      {
        int bytes_len = System.Text.Encoding.UTF8.GetByteCount(input.Data, i, 1);
        writer.Write(new string(' ', bytes_len));
      }
      else
        writer.Write(c);
    }
  }

  public override object VisitPreprocConditional(bhlPreprocParser.PreprocConditionalContext ctx)
  {
    if(ctx.IF() != null)
    {
      if(ctx.preprocessor_expression() == null)
      {
        AddError(ctx, "invalid usage");
      }
      else
      {
        Visit(ctx.preprocessor_expression());
         
        ifs.Push(new IfBlock() { 
            if_node = ctx,
            expression = Annotate(ctx.preprocessor_expression())
          }
        );
      }
    }
    else if(ctx.ELSE() != null)
    {
      if(!ifs.Peek().else_found)
      {
        var tmp = ifs.Pop();
        tmp.else_found = true;
        tmp.expression.strip_condition = !tmp.expression.strip_condition;
        ifs.Push(tmp);
      }
      else
        AddError(ctx, "invalid usage");
    }
    else if(ctx.ENDIF() != null)
    {
      if(ifs.Count == 0)
        AddError(ctx, "invalid usage");
      else
        ifs.Pop();
    }
    return null;
  }

  public override object VisitPreprocConditionalSymbol(bhlPreprocParser.PreprocConditionalSymbolContext ctx)
  {
    var symbol = ctx.CONDITIONAL_SYMBOL();
    Annotate(ctx).strip_condition = defines?.Contains(symbol.GetText()) ?? false;

    return null;
  }

  public override object VisitPreprocNot(bhlPreprocParser.PreprocNotContext ctx)
  {
    Visit(ctx.preprocessor_expression());
    Annotate(ctx).strip_condition = !Annotate(ctx.preprocessor_expression()).strip_condition;

    return null;
  }

  Annotated Annotate(IParseTree t)
  {
    Annotated at;
    if(!annotated_nodes.TryGetValue(t, out at))
    {
      at = new Annotated();
      at.tree = t;
      at.strip_condition = true;

      annotated_nodes.Add(t, at);
    }
    return at;
  }

  void AddError(IParseTree place, string msg) 
  {
    errors.Add(new ParseError(module, place, tokens, msg));
  }
}

} //namespace bhl
