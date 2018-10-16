<?php
namespace bhl;

require_once(dirname(__FILE__) . '/metagen.inc.php');

function usage()
{
  echo "Usage:\n ./generate.php task [OPTIONS]\n";
}

array_shift($argv);
if(!$argv)
{
  usage();
  exit(1);
}

$task = array_shift($argv);

mtg_argv2conf($argv);

mtg_run_generator(mtg_conf('target'), $task);
