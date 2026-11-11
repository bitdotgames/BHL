#include <cstdio>
#include <chrono>
#include <cstdlib>
__attribute__((noinline)) static double fib(double x) {
  if(x == 0) return 0;
  if(x == 1) return 1;
  return fib(x-1) + fib(x-2);
}
int main(int argc, char** argv) {
  int iters = argc > 1 ? atoi(argv[1]) : 20000;
  volatile int vn = argc > 2 ? atoi(argv[2]) : 15;
  int n = vn;
  auto t0 = std::chrono::high_resolution_clock::now();
  double sink = 0;
  for(int i = 0; i < iters; ++i) { volatile double a = n; sink += fib(a); }
  auto t1 = std::chrono::high_resolution_clock::now();
  std::printf("native C++ fib(%d)=%.0f  %.3f us/op\n", n, sink/iters,
    std::chrono::duration<double,std::micro>(t1-t0).count()/iters);
  return 0;
}
