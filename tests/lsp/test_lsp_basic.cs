using System.Collections.Generic;
using bhl;
using bhl.lsp;
using Xunit;

public class TestLSPBasic : TestLSPShared
{
  public class TestDocumentCalcByteIndex : BHL_TestBase
  {
    [Fact]
    public void Unix_line_endings()
    {
      string bhl = "func int test()\n{\nwhat()\n}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }

    [Fact]
    public void Windows_line_endings()
    {
      string bhl = "func int test()\r\n{\r\nwhat()\r\n}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }

    [Fact]
    public void Mixed_line_endings()
    {
      string bhl = "func int test()\n{\r\nwhat()\r}";
      var code = new CodeIndex();
      code.Update(bhl);

      AssertEqual("f", bhl[code.CalcByteIndex(0, 0)].ToString());
      AssertEqual("u", bhl[code.CalcByteIndex(0, 1)].ToString());
      AssertEqual("{", bhl[code.CalcByteIndex(1, 0)].ToString());
      AssertEqual("h", bhl[code.CalcByteIndex(2, 1)].ToString());
      AssertEqual("}", bhl[code.CalcByteIndex(3, 0)].ToString());

      Assert.Equal(-1, code.CalcByteIndex(4, 0));
      Assert.Equal(-1, code.CalcByteIndex(100, 1));
    }
  }

  [Fact]
  public void TestDocumentGetLineColumn()
  {
    string bhl = "func int test()\n{\nwhat()\n}";
    var code = new CodeIndex();
    code.Update(bhl);

    {
      var pos = code.GetIndexPosition(0);
      AssertEqual("f", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(1);
      AssertEqual("u", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(16);
      AssertEqual("{", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length - 1);
      AssertEqual("}", bhl[code.CalcByteIndex(pos.line, pos.column)].ToString());
    }

    {
      var pos = code.GetIndexPosition(bhl.Length);
      Assert.Equal(-1, pos.line);
      Assert.Equal(-1, pos.column);
    }
  }

  [Fact]
  public void TestNativeSymbolReflection()
  {
    var fn = new FuncSymbolNative(new Origin(), "test", Types.Void,
      delegate(VM.FrameOld frm, ValOldStack stack, FuncArgsInfo args_info, ref BHS status) { return null; }
    );
    Assert.EndsWith("test_lsp_basic.cs", fn.origin.source_file);
    Assert.True(fn.origin.source_range.start.line > 0);
  }
}
