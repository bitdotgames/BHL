<?php
require_once(dirname(__FILE__) . '/as3.inc.php');

function gen_as3_struct($OUT, array $DEPS, mtgMetaInfo $meta, mtgMetaInfoUnit $unit, $as3_pkg_name)
{
  if($unit->object instanceof mtgMetaPacket)
    return mtg_as3_generate_packet($meta, $unit->object, $as3_pkg_name);
  else
    return mtg_as3_generate_struct($meta, $unit->object, $as3_pkg_name);
}

function gen_as3_map($OUT, array $DEPS, $class, array $units, $as3_pkg_name)
{
  $map = '';
  $imports = array();
  $refs = array();

  foreach($units as $unit)
  {
    if($unit->object instanceof mtgMetaPacket)
      $map .= "case " . $unit->object->getNumericCode() . ": return new " . $unit->object->getName() . "(data);\n";

    $imports[$unit->object->getName()] =  "import ". $as3_pkg_name. ".packet." . $unit->object->getName() . ";";
    $refs[$unit->object->getName()] =  "private static var _" . $unit->object->getName(). " : " . $unit->object->getName() . ";";
  }

  $templater = new mtg_as3_templater();
  $tpl = $templater->tpl_map();
  return mtg_fill_template($tpl, 
      array('%imports%' => implode("\n", $imports), 
        '%references%' => implode("\n", $refs),
        '%map%' => $map, 
        '%class%' => $class, 
        '%package_name%' => $as3_pkg_name
        )
      );
}

function gen_as3_version($OUT, array $DEPS, mtgMetaInfo $meta, $as3_pkg_name)
{
  $v = file_get_contents($meta->getVersionFile());
  if(!$v)
    throw new Exception("Bad version file");

  $txt = <<<EOD
package $as3_pkg_name {
  class Version {
    static var API : String = "$v";
  }
}

EOD;

    return $txt;
}

function generate_as3_packets(mtgMetaInfo $meta)
{
  $targets = array();
  $SHARED_DEPS = array(
      dirname(__FILE__) . '/as3.inc.php', 
      dirname(__FILE__) . '/as3_tpl.inc.php',
      dirname(__FILE__) . '/as3_generator.inc.php'
      );

  mtg_mkdir(mtg_conf('out-dir'));

  $package_name = mtg_conf("out-package");

  $units = array();
  $files = array();
  foreach($meta->getUnits() as $unit)
  {
    $out_file = ($unit->object instanceof mtgMetaPacket) ? 
      mtg_as3_packet_file($unit->object) : mtg_as3_struct_file($unit->object); 

    $units[] = $unit;
    $file = mtg_conf("out-dir") . "/" . $out_file; 
    $files[] = $file; 
    $targets[] = mtg_new_file($file,
        array_merge($SHARED_DEPS, mtg_get_file_deps($meta, $unit)),
        array('gen_as3_struct', $meta, $unit, $package_name)
        );
  }

  //packets map
  $packets_map = mtg_conf("map", null);
  if($packets_map && $units)
  {
    $classname = current(explode('.', basename($packets_map)));
    $targets[] = mtg_new_bundle($packets_map, $files, array('gen_as3_map', $classname, $units, $package_name)); 
  }

  //version
  $version = mtg_conf("version", null);
  if($version)
  {
    $version_dir = dirname($version);
    mtg_mkdir($version_dir);

    $targets[] = mtg_new_file($version,
        array($meta->getVersionFile()),
        array('gen_as3_version', $meta, $package_name)
        );
  }

  return $targets;
}
