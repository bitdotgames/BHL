using System.IO;

namespace bhl.marshall
{

public interface IFactory
{
  IMarshallableGeneric CreateById(uint id);
}

public interface IMarshallable
{
  void Sync(SyncContext ctx);
}

public interface IMarshallableGeneric : IMarshallable
{
  uint ClassId();
}

public interface IReader
{
  Stream Stream { get; }

  void ReadI8(ref sbyte v);
  void ReadU8(ref byte v);
  void ReadI16(ref short v);
  void ReadU16(ref ushort v);
  void ReadI32(ref int v);
  void ReadU32(ref uint v);
  void ReadU64(ref ulong v);
  void ReadI64(ref long v);
  void ReadFloat(ref float v);
  void ReadBool(ref bool v);
  void ReadDouble(ref double v);
  void ReadString(ref string v);
  void ReadRawBegin(ref int vlen);
  void ReadRawEnd(byte[] v);
  int BeginContainer();
  void EndContainer();
}

public interface IWriter
{
  Stream Stream { get; }

  void WriteI8(sbyte v);
  void WriteU8(byte v);
  void WriteI16(short v);
  void WriteU16(ushort v);
  void WriteI32(int v);
  void WriteU32(uint v);
  void WriteI64(long v);
  void WriteU64(ulong v);
  void WriteFloat(float v);
  void WriteBool(bool v);
  void WriteDouble(double v);

  void WriteString(string v);

  //NOTE: -1 means 'unspecified'
  void BeginContainer(int len);
  void EndContainer();
}

}