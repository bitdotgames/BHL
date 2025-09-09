using System.Collections.Generic;

namespace bhl.marshall
{

public struct SyncContext
{
  public bool is_read;
  public IReader reader;
  public IWriter writer;
  public IFactory factory;
  public TypeRefIndex type_refs;
  public IReader type_refs_reader;
  public List<int> type_refs_offsets;

  public static SyncContext NewReader(IReader reader, IFactory factory = null, TypeRefIndex refs = null)
  {
    var ctx = new SyncContext()
    {
      is_read = true,
      reader = reader,
      writer = null,
      factory = factory,
      type_refs = refs ?? new TypeRefIndex()
    };
    return ctx;
  }

  public static SyncContext NewWriter(IWriter writer, IFactory factory = null, TypeRefIndex refs = null)
  {
    var ctx = new SyncContext()
    {
      is_read = false,
      reader = null,
      writer = writer,
      factory = factory,
      type_refs = refs ?? new TypeRefIndex()
    };
    return ctx;
  }
}

}