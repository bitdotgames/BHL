// Register based counterpart to vm_si2/vm_si3: same Val, same refcounting, same pre-decoded
// dispatch - only the architecture differs, so the delta is the architecture.
//
// CAVEAT the others don't have: the stack variants run the REAL bytecode the C# compiler
// emits. BHL has no register backend, so the program below is hand encoded. It follows what a
// Lua style 'freereg' allocator would produce (locals low, temporaries allocated upward,
// callee window starts at the call's destination register) rather than hand optimized code,
// but it is still the best case - a real allocator would sometimes emit extra moves.
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
  SI_LocalConstArith    = 200,  // GetVarScalar a, Constant b, <arith>
  SI_LocalConstCmpJump  = 201,  // GetVarScalar a, Constant b, <cmp>, JumpZ t
  SI_ConstReturn        = 202,  // Constant a, Return
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


//NOTE: r0 = x (the argument), r1/r2 are expression temporaries. A call's callee window starts
//      at its destination register, so the result lands where the caller already expects it
enum ROp : uint8_t {
  R_Frame = 1,   // a = nlocals
  R_EqKJump,     // if r[a] != K[b] -> jt
  R_RetK,        // return K[a]
  R_SubK,        // r[a] = r[b] - K[c]
  R_Call,        // callee window at r[a], target instr b
  R_Add,         // r[a] = r[b] + r[c]
  R_Ret,         // return r[a]
};

struct RInstr { uint8_t op; int32_t a, b, c, jt; };

static const double CONSTANTS[] = { 0, 1, 2 };

static const RInstr PROG[] = {
  { R_Frame,    3, 0, 0, 0 },
  { R_EqKJump,  0, 0, 0, 3 },
  { R_RetK,     0, 0, 0, 0 },
  { R_EqKJump,  0, 1, 0, 5 },
  { R_RetK,     1, 0, 0, 0 },
  { R_SubK,     1, 0, 1, 0 },
  { R_Call,     1, 0, 0, 0 },
  { R_SubK,     2, 0, 2, 0 },
  { R_Call,     2, 0, 0, 0 },
  { R_Add,      1, 1, 2, 0 },
  { R_Ret,      1, 0, 0, 0 },
};
static const int PROG_LEN = sizeof(PROG) / sizeof(PROG[0]);

struct RFrame {
  int  base;
  int  nlocals;
  int  ret_pc;
  bool dirty;
};

static Val    stack[4096];
static RFrame frames[2048];

static inline void set_num(Val& v, double n)
{
  v.num = n; v._refc = nullptr; v.obj = nullptr;
}

static double run_fib(double arg)
{
  int fc = 0, pc = 0;
  int base = 0, nlocals = 0;
  bool dirty = false;

  set_num(stack[0], arg);
  frames[fc].base = 0; frames[fc].ret_pc = -1;
  ++fc;

  while(pc < PROG_LEN)
  {
    const RInstr& in = PROG[pc];
    switch(in.op)
    {
      case R_Frame:
        nlocals = in.a;
        //NOTE: only the slots past the incoming arguments need clearing
        for(int i = base + 1; i < base + in.a; ++i)
          set_num(stack[i], 0);
        break;

      case R_EqKJump:
        if(stack[base + in.a].num != CONSTANTS[in.b]) { pc = in.jt; continue; }
        break;

      case R_SubK:
        set_num(stack[base + in.a], stack[base + in.b].num - CONSTANTS[in.c]);
        break;

      case R_Add:
        set_num(stack[base + in.a], stack[base + in.b].num + stack[base + in.c].num);
        break;

      case R_Call: {
        RFrame& cf = frames[fc-1];
        cf.base = base; cf.nlocals = nlocals; cf.dirty = dirty;

        RFrame& nf = frames[fc];
        nf.ret_pc = pc + 1;
        ++fc;
        base = base + in.a;   //NOTE: the argument is already sitting in the callee's r0
        dirty = false;
        pc = in.b;
        continue;
      }

      case R_RetK:
      case R_Ret: {
        //NOTE: same discipline as the stack variants - release only if something refcounted
        //      ever landed in this frame
        double rv = (in.op == R_RetK) ? CONSTANTS[in.a] : stack[base + in.a].num;
        if(dirty) {
          for(int i = base; i < base + nlocals; ++i) {
            Val& lv = stack[i];
            if(lv._refc != nullptr) { lv._refc->Release(); lv._refc = nullptr; }
            lv.obj = nullptr;
          }
        }
        set_num(stack[base], rv);

        int ret_pc = frames[fc-1].ret_pc;
        --fc;
        if(fc == 0) return stack[base].num;

        RFrame& cf = frames[fc-1];
        base = cf.base; nlocals = cf.nlocals; dirty = cf.dirty;
        pc = ret_pc;
        continue;
      }
    }
    ++pc;
  }
  return stack[base].num;
}

int main(int argc, char** argv)
{
  int iters = argc > 1 ? atoi(argv[1]) : 2000;
  int n = argc > 2 ? atoi(argv[2]) : 15;

  double check = run_fib(n);
  (void)check;

  auto t0 = std::chrono::high_resolution_clock::now();
  double sink = 0;
  for(int i = 0; i < iters; ++i)
    sink += run_fib(n);
  auto t1 = std::chrono::high_resolution_clock::now();

  double us = std::chrono::duration<double, std::micro>(t1 - t0).count();
  std::printf("Val=%zu bytes  fib(%d)=%.0f  %.2f us/op\n", sizeof(Val), n, sink / iters, us / iters);
  return 0;
}
