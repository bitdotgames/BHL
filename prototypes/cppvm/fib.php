<?php
function fib($n) {
  if ($n == 0) return 0;
  elseif ($n == 1) return 1;
  else return fib($n-1) + fib($n-2);
}
$iters = isset($argv[1]) ? (int)$argv[1] : 5000;
$n = isset($argv[2]) ? (int)$argv[2] : 15;
for ($i=0;$i<500;$i++) fib($n);
$t0 = microtime(true);
$sink = 0;
for ($i=0;$i<$iters;$i++) $sink += fib($n);
$t1 = microtime(true);
printf("fib(%d)=%d  %.2f us/op\n", $n, $sink/$iters, ($t1-$t0)*1e6/$iters);
