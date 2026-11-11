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
  int locals_offset;
  int ret_ip;
  int locals_vars_num;
  int return_vars_num;
};

static Val   stack[4096];
static Frame frames[2048];

static inline uint32_t dec8 (const uint8_t* b, int& ip) { return b[++ip]; }
static inline uint32_t dec16(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8; ip += 2; return v; }
static inline uint32_t dec24(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8 | (uint32_t)b[ip+3]<<16; ip += 3; return v; }
static inline uint32_t dec32(const uint8_t* b, int& ip) { uint32_t v = b[ip+1] | (uint32_t)b[ip+2]<<8 | (uint32_t)b[ip+3]<<16 | (uint32_t)b[ip+4]<<24; ip += 4; return v; }


static double run_fib(double arg)
{
  //NOTE: computed goto - a dense label table indexed by opcode, with the dispatch repeated at
  //      the end of each handler so the branch predictor sees a separate site per opcode
  static void* labels[256];
  static bool inited = false;
  if(!inited) {
    for(int i = 0; i < 256; ++i) labels[i] = &&L_bad;
    labels[OP_Frame]        = &&L_frame;
    labels[OP_GetVarScalar] = &&L_getvar;
    labels[OP_Constant]     = &&L_const;
    labels[OP_EqualScalar]  = &&L_eq;
    labels[OP_Add]          = &&L_add;
    labels[OP_Sub]          = &&L_sub;
    labels[OP_JumpZ]        = &&L_jumpz;
    labels[OP_Jump]         = &&L_jump;
    labels[OP_CallLocal]    = &&L_call;
    labels[OP_Return]       = &&L_ret;
    inited = true;
  }

  const uint8_t* b = CODE;
  int sp = 0, fc = 0, ip = 0;

  stack[sp].num = arg; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp;
  frames[fc].locals_offset = sp - 1;
  frames[fc].ret_ip = -1;
  ++fc;

#define DISPATCH() do { ++ip; goto *labels[b[ip]]; } while(0)
#define DISPATCH_NOINC() do { goto *labels[b[ip]]; } while(0)

  goto *labels[b[ip]];

L_frame: {
    int locals = (int)dec8(b, ip);
    int rets   = (int)dec8(b, ip);
    Frame& f = frames[fc-1];
    f.locals_vars_num = locals;
    f.return_vars_num = rets;
    int need = f.locals_offset + locals;
    while(sp < need) { stack[sp].num = 0; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp; }
    DISPATCH();
  }
L_getvar: {
    int idx = (int)dec8(b, ip);
    Val& src = stack[frames[fc-1].locals_offset + idx];
    stack[sp].type = src.type; stack[sp].num = src.num; ++sp;
    DISPATCH();
  }
L_const: {
    int ci = (int)dec24(b, ip);
    stack[sp].num = CONSTANTS[ci]; stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp;
    DISPATCH();
  }
L_eq: {
    double r = stack[--sp].num;
    stack[sp-1].num = (r == stack[sp-1].num) ? 1 : 0;
    DISPATCH();
  }
L_add: { double r = stack[--sp].num; stack[sp-1].num += r; DISPATCH(); }
L_sub: { double r = stack[--sp].num; stack[sp-1].num -= r; DISPATCH(); }
L_jumpz: {
    int off = (int16_t)dec16(b, ip);
    if(stack[--sp].num == 0) ip += off;
    DISPATCH();
  }
L_jump: { int off = (int16_t)dec16(b, ip); ip += off; DISPATCH(); }
L_call: {
    int func_ip = (int)dec24(b, ip);
    uint32_t args_bits = dec32(b, ip);
    int args_num = (int)(args_bits & 0x7F);
    Frame& nf = frames[fc];
    nf.locals_offset = sp - args_num;
    nf.ret_ip = ip;
    ++fc;
    ip = func_ip;
    DISPATCH_NOINC();
  }
L_ret: {
    Frame& f = frames[fc-1];
    int rets = f.return_vars_num;
    for(int i = f.locals_offset; i < f.locals_offset + f.locals_vars_num; ++i) {
      Val& lv = stack[i];
      if(lv._refc != nullptr) { lv._refc->Release(); lv._refc = nullptr; }
      lv.obj = nullptr;
    }
    for(int i = 0; i < rets; ++i)
      stack[f.locals_offset + i] = stack[sp - rets + i];
    sp = f.locals_offset + rets;
    --fc;
    if(fc == 0) return stack[sp-1].num;
    ip = f.ret_ip;
    DISPATCH();
  }
L_bad:
  std::printf("unknown opcode %d at %d\n", b[ip], ip);
  return -1;
}

int main(int argc, char** argv)
{
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
