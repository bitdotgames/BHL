using bhl.lsp.proto;

namespace bhl.lsp
{

public interface IService
{
  void GetCapabilities(ClientCapabilities cc, ref ServerCapabilities sc);
}

}