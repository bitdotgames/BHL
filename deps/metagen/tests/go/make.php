<?php

require_once(__DIR__ . '/../../metagen.inc.php');

mtg_run('go', 'packets', array( 
          "meta-dir" => array(__DIR__ . '/meta/') , 
          "out-dir" => __DIR__ . "/autogen", 
          "bundle" => __DIR__ . "/autogen/bundle.go"
        ));

//mtg_run('php', 'packets', array( 
//          "meta-dir" => array(__DIR__ . '/meta/') , 
//          "out-dir" => __DIR__ . "/autogen", 
//          "bundle" => __DIR__ . "/autogen/bundle.inc.php"
//        ));
