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
  public void TestSimpleBlob()
  {
    var orig = new StructBlob();
    orig.x = 1;
    orig.y = 10;
    orig.z = 100;

    var vm = new VM();

    var val = Val.New(vm);
    val.SetBlob(ref orig, null);

    ref var b = ref val.GetBlob<StructBlob>();
    AssertEqual(b.x, orig.x);
    AssertEqual(b.y, orig.y);
    AssertEqual(b.z, orig.z);

    b.x = 20;
    AssertEqual(b.x, 20);
    AssertEqual(orig.x, 1);
  }
  
  [Fact]
  public void TestBlobCopyOverEmptyValue()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    val1.GetBlob<StructBlob>().x = 20;
    
    var val2 = Val.New(vm);
    val2.ValueCopyFrom(val1);
    //let's check if it was copied
    AssertEqual(val2.GetBlob<StructBlob>().x, 20);
    
    val2.GetBlob<StructBlob>().x = 30;
    //original value is intact 
    AssertEqual(val1.GetBlob<StructBlob>().x, 20);
  }
  
  [Fact]
  public void TestBlobCopyOverAnotherBlob()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    val1.GetBlob<StructBlob>().x = 20;
    
    var val2 = Val.New(vm);
    val2.SetBlob(ref orig, null);
    
    val2.ValueCopyFrom(val1);
    //let's check if it was copied
    AssertEqual(val2.GetBlob<StructBlob>().x, 20);
  }
  
  [Fact]
  public void TestBlobCopyOverNumber()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    val1.GetBlob<StructBlob>().x = 20;
    
    var val2 = Val.NewInt(vm, 10);
    val2.SetBlob(ref orig, null);
    
    val2.ValueCopyFrom(val1);
    //let's check if it was copied
    AssertEqual(val2.GetBlob<StructBlob>().x, 20);
  }
  
  [Fact]
  public void TestBlobEquality()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    var val2 = Val.New(vm);
    val2.SetBlob(ref orig, null);
    
    AssertTrue(val1.IsValueEqual(val2));
  }
  
  [Fact]
  public void TestBlobEqualityAfterChanges()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    var val2 = Val.New(vm);
    val2.SetBlob(ref orig, null);
    
    val1.GetBlob<StructBlob>().x = 20;
    val2.GetBlob<StructBlob>().x = 20;
    
    AssertTrue(val1.IsValueEqual(val2));
  }
  
  [Fact]
  public void TestBlobInEquality()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    var val2 = Val.New(vm);
    val2.SetBlob(ref orig, null);
    
    val1.GetBlob<StructBlob>().x = 20;
    
    AssertFalse(val1.IsValueEqual(val2));
  }
  
  [Fact]
  public void TestBlobInEqualityWithNumber()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    var val2 = Val.NewInt(vm, 10);
    
    AssertFalse(val1.IsValueEqual(val2));
  }
  
  [Fact]
  public void TestBlobInEqualityWithRandomObj()
  {
    var orig = new StructBlob();
    orig.x = 1;

    var vm = new VM();

    var val1 = Val.New(vm);
    val1.SetBlob(ref orig, null);
    
    var val2 = Val.NewObj(vm, /*using orig!*/ orig, null);
    
    AssertFalse(val1.IsValueEqual(val2));
  }
}
