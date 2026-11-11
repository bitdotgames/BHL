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


struct Exec {
  int sp;
  int fc;
  int ip;
  bool done;
  double result;
};

static Exec  E;

typedef void (*OpFn)(const uint8_t* b);

static OpFn op_handlers[256];

static void op_frame(const uint8_t* b) {
  int locals = (int)dec8(b, E.ip);
  int rets   = (int)dec8(b, E.ip);
  Frame& f = frames[E.fc-1];
  f.locals_vars_num = locals;
  f.return_vars_num = rets;
  int need = f.locals_offset + locals;
  while(E.sp < need) { stack[E.sp].num = 0; stack[E.sp]._refc = nullptr; stack[E.sp].obj = nullptr; ++E.sp; }
}
static void op_getvarscalar(const uint8_t* b) {
  int idx = (int)dec8(b, E.ip);
  Val& src = stack[frames[E.fc-1].locals_offset + idx];
  stack[E.sp].type = src.type;
  stack[E.sp].num  = src.num;
  ++E.sp;
}
static void op_constant(const uint8_t* b) {
  int ci = (int)dec24(b, E.ip);
  stack[E.sp].num = CONSTANTS[ci]; stack[E.sp]._refc = nullptr; stack[E.sp].obj = nullptr; ++E.sp;
}
static void op_equalscalar(const uint8_t* b) {
  double r = stack[--E.sp].num;
  stack[E.sp-1].num = (r == stack[E.sp-1].num) ? 1 : 0;
}
static void op_add(const uint8_t* b) { double r = stack[--E.sp].num; stack[E.sp-1].num += r; }
static void op_sub(const uint8_t* b) { double r = stack[--E.sp].num; stack[E.sp-1].num -= r; }
static void op_jumpz(const uint8_t* b) {
  int off = (int16_t)dec16(b, E.ip);
  if(stack[--E.sp].num == 0) E.ip += off;
}
static void op_jump(const uint8_t* b) { int off = (int16_t)dec16(b, E.ip); E.ip += off; }
static void op_calllocal(const uint8_t* b) {
  int func_ip = (int)dec24(b, E.ip);
  uint32_t args_bits = dec32(b, E.ip);
  int args_num = (int)(args_bits & 0x7F);
  Frame& nf = frames[E.fc];
  nf.locals_offset = E.sp - args_num;
  nf.ret_ip = E.ip;
  ++E.fc;
  E.ip = func_ip - 1;
}
static void op_return(const uint8_t* b) {
  Frame& f = frames[E.fc-1];
  int rets = f.return_vars_num;
  for(int i = f.locals_offset; i < f.locals_offset + f.locals_vars_num; ++i) {
    Val& lv = stack[i];
    if(lv._refc != nullptr) { lv._refc->Release(); lv._refc = nullptr; }
    lv.obj = nullptr;
  }
  for(int i = 0; i < rets; ++i)
    stack[f.locals_offset + i] = stack[E.sp - rets + i];
  E.sp = f.locals_offset + rets;
  --E.fc;
  if(E.fc == 0) { E.done = true; E.result = stack[E.sp-1].num; return; }
  E.ip = f.ret_ip;
}

static void init_handlers() {
  op_handlers[OP_Frame]        = op_frame;
  op_handlers[OP_GetVarScalar] = op_getvarscalar;
  op_handlers[OP_Constant]     = op_constant;
  op_handlers[OP_EqualScalar]  = op_equalscalar;
  op_handlers[OP_Add]          = op_add;
  op_handlers[OP_Sub]          = op_sub;
  op_handlers[OP_JumpZ]        = op_jumpz;
  op_handlers[OP_Jump]         = op_jump;
  op_handlers[OP_CallLocal]    = op_calllocal;
  op_handlers[OP_Return]       = op_return;
}

static double run_fib(double arg)
{
  const uint8_t* b = CODE;
  E.sp = 0; E.fc = 0; E.ip = 0; E.done = false;

  stack[E.sp].num = arg; stack[E.sp]._refc = nullptr; stack[E.sp].obj = nullptr; ++E.sp;
  frames[E.fc].locals_offset = E.sp - 1;
  frames[E.fc].ret_ip = -1;
  ++E.fc;

  while(!E.done && E.ip < (int)sizeof(CODE))
  {
    op_handlers[b[E.ip]](b);
    ++E.ip;
  }
  return E.result;
}

int main(int argc, char** argv)
{
  init_handlers();
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
