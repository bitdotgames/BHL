import sys, time
def fib(n):
    if n == 0: return 0
    elif n == 1: return 1
    else: return fib(n-1) + fib(n-2)
iters = int(sys.argv[1]) if len(sys.argv) > 1 else 2000
n = int(sys.argv[2]) if len(sys.argv) > 2 else 15
for _ in range(200): fib(n)
t0 = time.perf_counter()
sink = 0
for _ in range(iters): sink += fib(n)
t1 = time.perf_counter()
print(f"fib({n})={sink//iters}  {(t1-t0)*1e6/iters:.2f} us/op")
