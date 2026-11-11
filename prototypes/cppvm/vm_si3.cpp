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
#include <cmath>
#include <chrono>

enum Op : uint8_t {
  OP_Constant     = 1,
  OP_Add          = 2,
  OP_Sub          = 3,
  OP_Div          = 4,
  OP_Mul          = 5,
  OP_GetVarScalar = 8,
  OP_Frame        = 14,
  OP_Return       = 15,
  OP_Jump         = 16,
  OP_JumpZ        = 17,
  OP_CallLocal    = 22,
  OP_Mod          = 52,
  OP_EqualScalar  = 55,
  OP_LT           = 58,
  OP_LTE          = 59,
  OP_GT           = 60,
  OP_GTE          = 61,
  //NOTE: fused in the pre-decode pass. Matched by shape, not by operator - the operator is
  //      kept in Instr::sub and switched on, so one opcode covers all five forms
  //NOTE: matched by shape but emitted one per operator, so the hot loop never branches on
  //      the operator - a wider dispatch switch is free, an inner switch is not
  SI_LocalConstAdd      = 200,
  SI_LocalConstSub      = 201,
  SI_LocalConstMul      = 202,
  SI_LocalConstDiv      = 203,
  SI_LocalConstMod      = 204,
  SI_LocalConstEqJump   = 205,
  SI_LocalConstLtJump   = 206,
  SI_LocalConstLteJump  = 207,
  SI_LocalConstGtJump   = 208,
  SI_LocalConstGteJump  = 209,
  SI_ConstReturn        = 210,
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
  uint8_t  sub;   //NOTE: fits in op's padding, so Instr stays 16 bytes
  int32_t  a;
  int32_t  b;
  int32_t  jt;
};

static inline int arith_si(uint8_t op) {
  switch(op) {
    case OP_Add: return SI_LocalConstAdd; case OP_Sub: return SI_LocalConstSub;
    case OP_Mul: return SI_LocalConstMul; case OP_Div: return SI_LocalConstDiv;
    case OP_Mod: return SI_LocalConstMod; default: return -1;
  }
}

static inline int cmp_si(uint8_t op) {
  switch(op) {
    case OP_EqualScalar: return SI_LocalConstEqJump; case OP_LT:  return SI_LocalConstLtJump;
    case OP_LTE:         return SI_LocalConstLteJump; case OP_GT:  return SI_LocalConstGtJump;
    case OP_GTE:         return SI_LocalConstGteJump; default: return -1;
  }
}

static void fuse();
static void compact();
#define OP_Nop 13

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

  fuse();
  compact();
}

//NOTE: the Nops left by fusing would still cost a dispatch each, so squeeze them out and
//      renumber every jump target through an old->new index map
static void compact()
{
  int map[256];
  int n = 0;
  for(int i = 0; i < prog_len; ++i)
  {
    map[i] = n;
    if(prog[i].op != OP_Nop)
      prog[n++] = prog[i];
  }
  int old_len = prog_len;
  prog_len = n;

  for(int i = 0; i < prog_len; ++i)
  {
    Instr& in = prog[i];
    if(in.op == OP_Jump || in.op == OP_JumpZ || in.op == OP_CallLocal)
      in.a = (in.a < old_len) ? map[in.a] : prog_len;
    if(in.op >= SI_LocalConstEqJump && in.op <= SI_LocalConstGteJump)
      in.jt = (in.jt < old_len) ? map[in.jt] : prog_len;
  }
}

//NOTE: rewrite fused runs in place, padding with Nop so every existing jump target stays
//      valid - no need to renumber anything
static void fuse()
{
  for(int i = 0; i + 3 < prog_len; ++i)
  {
    if(prog[i].op == OP_GetVarScalar && prog[i+1].op == OP_Constant &&
       cmp_si(prog[i+2].op) >= 0 && prog[i+3].op == OP_JumpZ)
    {
      int local = prog[i].a, cst = prog[i+1].a, tgt = prog[i+3].a;
      int si = cmp_si(prog[i+2].op);
      prog[i].op = (uint8_t)si; prog[i].a = local; prog[i].b = cst;
      prog[i+1].op = OP_Nop; prog[i+2].op = OP_Nop;
      prog[i+3].op = OP_Nop;
      prog[i].jt = tgt;
      i += 3;
    }
  }
  for(int i = 0; i + 2 < prog_len; ++i)
  {
    if(prog[i].op == OP_GetVarScalar && prog[i+1].op == OP_Constant && arith_si(prog[i+2].op) >= 0)
    {
      prog[i].op = (uint8_t)arith_si(prog[i+2].op); prog[i].b = prog[i+1].a;
      prog[i+1].op = OP_Nop; prog[i+2].op = OP_Nop;
      i += 2;
    }
  }
  for(int i = 0; i + 1 < prog_len; ++i)
  {
    if(prog[i].op == OP_Constant && prog[i+1].op == OP_Return)
    {
      prog[i].op = SI_ConstReturn;
      prog[i+1].op = OP_Nop;
      ++i;
    }
  }
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
      case OP_Nop: break;
#define SI_ARITH(NAME, EXPR) \
      case NAME: { \
        double l = stack[loc + in.a].num, r = CONSTANTS[in.b]; (void)l; (void)r; \
        stack[sp].num = (EXPR); \
        stack[sp]._refc = nullptr; stack[sp].obj = nullptr; ++sp; \
        break; \
      }
      SI_ARITH(SI_LocalConstAdd, l + r)
      SI_ARITH(SI_LocalConstSub, l - r)
      SI_ARITH(SI_LocalConstMul, l * r)
      SI_ARITH(SI_LocalConstDiv, l / r)
      SI_ARITH(SI_LocalConstMod, std::fmod(l, r))
#undef SI_ARITH

#define SI_CMPJ(NAME, EXPR) \
      case NAME: { \
        double l = stack[loc + in.a].num, r = CONSTANTS[in.b]; (void)l; (void)r; \
        if(!(EXPR)) { pc = in.jt; continue; } \
        break; \
      }
      SI_CMPJ(SI_LocalConstEqJump,  l == r)
      SI_CMPJ(SI_LocalConstLtJump,  l <  r)
      SI_CMPJ(SI_LocalConstLteJump, l <= r)
      SI_CMPJ(SI_LocalConstGtJump,  l >  r)
      SI_CMPJ(SI_LocalConstGteJump, l >= r)
#undef SI_CMPJ
      case SI_ConstReturn: {
        double rv = CONSTANTS[in.a];
        if(dirty) {
          for(int i = loc; i < loc + nlocals; ++i) {
            Val& lv = stack[i];
            if(lv._refc != nullptr) { lv._refc->Release(); lv._refc = nullptr; }
            lv.obj = nullptr;
          }
        }
        stack[loc].num = rv; stack[loc]._refc = nullptr; stack[loc].obj = nullptr;
        sp = loc + 1;
        int ret_pc = frames[fc-1].ret_ip;
        --fc;
        if(fc == 0) return stack[sp-1].num;
        Frame& cf2 = frames[fc-1];
        loc = cf2.locals_offset; nlocals = cf2.locals_vars_num;
        nrets = cf2.return_vars_num; dirty = cf2.dirty;
        pc = ret_pc;
        continue;
      }
      case OP_EqualScalar: { double r = stack[--sp].num; stack[sp-1].num = (r == stack[sp-1].num) ? 1 : 0; break; }
      case OP_Add: { double r = stack[--sp].num; stack[sp-1].num += r; break; }
      case OP_Sub: { double r = stack[--sp].num; stack[sp-1].num -= r; break; }
      case OP_Mul: { double r = stack[--sp].num; stack[sp-1].num *= r; break; }
      case OP_Div: { double r = stack[--sp].num; stack[sp-1].num /= r; break; }
      case OP_Mod: { double r = stack[--sp].num; stack[sp-1].num = std::fmod(stack[sp-1].num, r); break; }
      case OP_LT:  { double r = stack[--sp].num; stack[sp-1].num = (stack[sp-1].num <  r) ? 1 : 0; break; }
      case OP_LTE: { double r = stack[--sp].num; stack[sp-1].num = (stack[sp-1].num <= r) ? 1 : 0; break; }
      case OP_GT:  { double r = stack[--sp].num; stack[sp-1].num = (stack[sp-1].num >  r) ? 1 : 0; break; }
      case OP_GTE: { double r = stack[--sp].num; stack[sp-1].num = (stack[sp-1].num >= r) ? 1 : 0; break; }
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
