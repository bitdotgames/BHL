local function fib(n)
  if n == 0 then return 0
  elseif n == 1 then return 1
  else return fib(n-1) + fib(n-2) end
end
local iters = tonumber(arg[1]) or 20000
local n = tonumber(arg[2]) or 15
for i=1,2000 do fib(n) end            -- warmup
local t0 = os.clock()
local sink = 0
for i=1,iters do sink = sink + fib(n) end
local t1 = os.clock()
io.write(string.format("fib(%d)=%d  %.2f us/op\n", n, sink/iters, (t1-t0)*1e6/iters))
