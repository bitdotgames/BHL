CXX      ?= c++
CXXFLAGS ?= -O2 -std=c++17

# every VM variant runs the same real bytecode, so they're directly comparable
VARIANTS = vm vm_table vm_goto vm_pre vm_fast vm_si vm_si2 vm_si3

ITERS ?= 20000
N     ?= 15

.PHONY: all bench langs clean

all: $(VARIANTS) native

# VAL_PAD=1 gives the 64 byte Val the C# VM actually has; without it the value is 32 bytes
$(VARIANTS): %: %.cpp
	$(CXX) $(CXXFLAGS) -DVAL_PAD=1 -o $@ $<

native: native.cpp
	$(CXX) $(CXXFLAGS) -o $@ $<

bench: all
	@for v in $(VARIANTS) native; do printf '%-10s ' $$v; ./$$v $(ITERS) $(N); done

# needs lua/luajit/python3/php on PATH, skips whatever is missing
langs:
	@command -v lua     >/dev/null && { printf '%-10s ' lua;     lua     fib.lua $(ITERS) $(N); } || true
	@command -v luajit  >/dev/null && { printf '%-10s ' luajit;  luajit  fib.lua $(ITERS) $(N); } || true
	@command -v luajit  >/dev/null && { printf '%-10s ' lj-joff; luajit -joff fib.lua $(ITERS) $(N); } || true
	@command -v python3 >/dev/null && { printf '%-10s ' python3; python3 fib.py  $(ITERS) $(N); } || true
	@command -v php     >/dev/null && { printf '%-10s ' php;     php     fib.php $(ITERS) $(N); } || true

clean:
	rm -f $(VARIANTS) native
