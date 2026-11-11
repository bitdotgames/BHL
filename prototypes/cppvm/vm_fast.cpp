// Minimal BHL VM prototype: just enough opcodes to run the compiled fib(x).
// Bytecode and constants are the real ones emitted by the C# compiler (dumped from
// ModuleCompiler), so this executes exactly what the C# VM executes.
//
// VAL_PAD lets us compare like-for-like: BHL's Val is 64 bytes (type, num, obj, refc and
// three spare doubles). A C++ VM would naturally use a smaller value, so measuring both
// separates "native code is faster" from "the value is smaller".
#include <cstdio>
#include <cstdint>
#include <cstring>
#include <chrono>

enum Op : uint8_t {
  OP_Constant     = 1,
  OP_Add          = 2,
  OP_Sub          = 3,
  OP_GetVarScalar = 8,
  OP_Frame        = 14,
  OP_Return       = 15,
  OP_Jump         = 16,
  OP_JumpZ        = 17,
  OP_CallLocal    = 22,
  OP_EqualScalar  = 55,
};

#ifndef VAL_PAD
#define VAL_PAD 0
#endif

//NOTE: mirrors IRefcounted - a virtual Release so the cost of the indirect call is modelled,
//      not just the null check
struct IRefcounted {
  int _refs = 1;
  virtual void Retain() { ++_refs; }
  virtual void Release() { --_refs; }
  virtual ~IRefcounted() {}
};

struct Val {
  void*        type;
  double       num;
  void*        obj;
  IRefcounted* _refc;
#if VAL_PAD
  double n2, n3, n4;
#endif
};

static const uint8_t CODE[] = {
  0x0e,0x01,0x01,                          // Frame(locals=1, rets=1)
  0x08,0x00,                               // GetVarScalar(0)
  0x01,0x00,0x00,0x00,                     // Constant(0)
  0x37,                                    // EqualScalar
  0x11,0x08,0x00,                          // JumpZ(+8)
  0x01,0x00,0x00,0x00,                     // Constant(0)
  0x0f,                                    // Return
  0x10,0x32,0x00,                          // Jump(+50)
  0x08,0x00,                               // GetVarScalar(0)
  0x01,0x01,0x00,0x00,                     // Constant(1)
  0x37,                                    // EqualScalar
  0x11,0x08,0x00,                          // JumpZ(+8)
  0x01,0x01,0x00,0x00,                     // Constant(1)
  0x0f,                                    // Return
  0x10,0x20,0x00,                          // Jump(+32)
  0x08,0x00,                               // GetVarScalar(0)
  0x01,0x01,0x00,0x00,                     // Constant(1)
  0x03,                                    // Sub
  0x16,0x00,0x00,0x00,0x01,0x00,0x00,0x00, // CallLocal(ip=0, args=1)
  0x08,0x00,                               // GetVarScalar(0)
  0x01,0x02,0x00,0x00,                     // Constant(2)
  0x03,                                    // Sub
  0x16,0x00,0x00,0x00,0x01,0x00,0x00,0x00, // CallLocal(ip=0, args=1)
  0x02,                                    // Add
  0x0f,                                    // Return
};

static const double CONSTANTS[] = { 0, 1, 2 };

struct Frame {
  int  locals_offset;
  int  ret_ip;
  int  locals_vars_num;
  int  return_vars_num;
  bool dirty;
};

static Val   stack[4096];
static Frame frames[2048];

static inline uint32_t dec8 (const uint8_t* b, int& ip) { return b[++ip]; }
static inline uint32_t dec16(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8; ip += 2; return v; }
static inline uint32_t dec24(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8 | (uint32_t)b[ip+3]<<16; ip += 3; return v; }
static inline uint32_t dec32(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8 | (uint32_t)b[ip+3]<<16 | (uint32_t)b[ip+4]<<24; ip += 4; return v; }


//NOTE: decode the bytecode ONCE into fixed-width instructions with resolved operands, so the
//      hot loop never re-parses bytes. Jump targets become absolute indices. Semantics are
//      unchanged - Val, refcounting and ReleaseLocals all stay
struct Instr {
  uint8_t  op;
  int32_t  a;
  int32_t  b;
};

static Instr prog[256];
static int   prog_len = 0;
static int   ip_to_instr[256];

static void predecode()
{
  int ip = 0;
  while(ip < (int)sizeof(CODE))
  {
    ip_to_instr[ip] = prog_len;
    Instr& in = prog[prog_len++];
    in.op = CODE[ip];
    int p = ip;
    switch(in.op) {
      case OP_Frame:        in.a = (int)dec8(CODE, p); in.b = (int)dec8(CODE, p); break;
      case OP_GetVarScalar: in.a = (int)dec8(CODE, p); break;
      case OP_Constant:     in.a = (int)dec24(CODE, p); break;
      case OP_JumpZ:
      case OP_Jump:         in.a = (int16_t)dec16(CODE, p) + p + 1; break; //NOTE: absolute byte target
      case OP_CallLocal:    in.a = (int)dec24(CODE, p); in.b = (int)(dec32(CODE, p) & 0x7F); break;
      default: break;
    }
    in.b = (in.op == OP_Frame || in.op == OP_CallLocal) ? in.b : p + 1; //NOTE: next ip for returns
    ip = p + 1;
  }
  //NOTE: rewrite byte targets into instruction indices
  for(int i = 0; i < prog_len; ++i)
    if(prog[i].op == OP_Jump || prog[i].op == OP_JumpZ)
      prog[i].a = (prog[i].a < (int)sizeof(CODE)) ? ip_to_instr[prog[i].a] : prog_len;
    else if(prog[i].op == OP_CallLocal)
      prog[i].a = ip_to_instr[prog[i].a];
}

static double run_fib(double arg)
{
  int sp = 0, fc = 0, pc = 0;
  //NOTE: the current frame's fields live in registers; frames[] is only touched on call/return
  int loc = 0, nlocals = 0, nrets = 0;
  bool dirty = false; //NOTE: did anything refcounted ever land in this frame's locals

  stack[sp].num = arg; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp;
  frames[fc].locals_offset = sp - 1;
  frames[fc].ret_ip = -1;
  ++fc;
  loc = sp - 1;

  while(pc < prog_len)
  {
    const Instr& in = prog[pc];
    switch(in.op)
    {
      case OP_Frame: {
        nlocals = in.a;
        nrets   = in.b;
        int need = loc + in.a;
        while(sp < need) { stack[sp].num = 0; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp; }
        break;
      }
      case OP_GetVarScalar: {
        Val& src = stack[loc + in.a];
        stack[sp].type = src.type; stack[sp].num = src.num; ++sp;
        break;
      }
      case OP_Constant:
        stack[sp].num = CONSTANTS[in.a]; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp;
        break;
      case OP_EqualScalar: { double r = stack[--sp].num; stack[sp-1].num = (r == stack[sp-1].num) ? 1 : 0; break; }
      case OP_Add: { double r = stack[--sp].num; stack[sp-1].num += r; break; }
      case OP_Sub: { double r = stack[--sp].num; stack[sp-1].num -= r; break; }
      case OP_JumpZ: if(stack[--sp].num == 0) { pc = in.a; continue; } break;
      case OP_Jump:  pc = in.a; continue;
      case OP_CallLocal: {
        //NOTE: spill the caller's register-held frame, then switch
        Frame& cf = frames[fc-1];
        cf.locals_offset = loc; cf.locals_vars_num = nlocals;
        cf.return_vars_num = nrets; cf.dirty = dirty;

        Frame& nf = frames[fc];
        nf.ret_ip = pc + 1;
        ++fc;
        loc = sp - in.b;
        dirty = false;
        pc = in.a;
        continue;
      }
      case OP_Return: {
        //NOTE: ReleaseLocals only has work to do if something refcounted was ever stored here
        if(dirty) {
          for(int i = loc; i < loc + nlocals; ++i) {
            Val& lv = stack[i];
            if(lv._refc != nullptr) { lv._refc->Release(); lv._refc = nullptr; }
            lv.obj = nullptr;
          }
        }
        for(int i = 0; i < nrets; ++i)
          stack[loc + i] = stack[sp - nrets + i];
        sp = loc + nrets;

        int ret_pc = frames[fc-1].ret_ip;
        --fc;
        if(fc == 0) return stack[sp-1].num;

        //NOTE: restore the caller's frame into registers
        Frame& cf = frames[fc-1];
        loc = cf.locals_offset; nlocals = cf.locals_vars_num;
        nrets = cf.return_vars_num; dirty = cf.dirty;
        pc = ret_pc;
        continue;
      }
    }
    ++pc;
  }
  return stack[sp-1].num;
}

int main(int argc, char** argv)
{
  predecode();
  int iters = argc > 1 ? atoi(argv[1]) : 2000;
  int n = argc > 2 ? atoi(argv[2]) : 15;

  double check = run_fib(n);

  auto t0 = std::chrono::high_resolution_clock::now();
  double sink = 0;
  for(int i = 0; i < iters; ++i)
    sink += run_fib(n);
  auto t1 = std::chrono::high_resolution_clock::now();

  double us = std::chrono::duration<double, std::micro>(t1 - t0).count();
  std::printf("Val=%zu bytes  fib(%d)=%.0f  %.2f us/op\n", sizeof(Val), n, sink / iters, us / iters);
  return 0;
}
