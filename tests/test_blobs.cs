using bhl;
using Xunit;

public class TestBlobs : BHL_TestBase
{
  public struct StructBlob
  {
    public int x;
    public int y;
    public int z;
  }

  [Fact]
  public void TestBlob()
  {
    var val = new StructBlob();
    val.x = 1;
    val.y = 10;
    val.z = 100;
    
    var blob = Blob<StructBlob>.New(ref val);
    
    AssertEqual(1, blob.Value.x);
    AssertEqual(10, blob.Value.y);
    AssertEqual(100, blob.Value.z);

    blob.Value.y = 30;
    AssertEqual(30, blob.Value.y);

    blob.Release();
  }
}
