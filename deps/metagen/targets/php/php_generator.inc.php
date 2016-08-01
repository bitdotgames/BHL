<?php
require_once(dirname(__FILE__) . '/php.inc.php');

class mtgPHPGenerator extends mtgGenerator
{
  function makeTargets(mtgMetaInfo $meta)
  {
    $targets = array();

    $codegen = mtg_conf("codegen", null);
    if(!$codegen)
      $codegen = new mtgPHPCodegen();
    $codegen->setMetaInfo($meta);

    $refl = new ReflectionClass($codegen);
    $SHARED_DEPS = array(
      dirname(__FILE__) . '/php.inc.php', 
      dirname(__FILE__) . '/php_tpl.inc.php',
      __FILE__,
      $refl->getFileName()
    );

    mtg_mkdir(mtg_conf('out-dir'));

    $units = array();
    foreach($meta->getUnits() as $unit)
    {
      $out_file = $unit->object->getName() . '.class.php'; 

      $units[] = $unit;
      $file = mtg_conf("out-dir") . "/" . $out_file; 
      $files[] = $file; 
      $targets[] = mtg_new_file($file,
          array_merge($SHARED_DEPS, mtg_get_file_deps($meta, $unit)),
          array('gen_php_struct', $codegen, $unit)
          );
    }

    $bundle = mtg_conf("bundle", null);
    if($bundle && $targets)
      $targets[] = mtg_new_bundle($bundle, $files, array('gen_php_bundle', $units, $meta->getRPCs(), mtg_conf("inc-dir"))); 

    return $targets;
  }
}

function gen_php_struct($OUT, array $DEPS, mtgCodegen $codegen, mtgMetaInfoUnit $unit)
{
  try
  {
    $unit_obj = $unit->object;
    if($unit_obj instanceof mtgMetaPacket)
      return $codegen->genRPC($unit_obj);
    if($unit_obj instanceof mtgMetaEnum)
      return $codegen->genEnum($unit_obj);
    else
      return $codegen->genStruct($unit_obj);
  }
  catch(Exception $e)
  {
    throw new Exception("Error while generating struct '" . $unit_obj->getName() . "': " . $e->getMessage());
  }
}

function gen_php_bundle($OUT, array $DEPS, array $units, array $rpcs, $inc_dir)
{
  $bundle = '';
  $class_map = '';
  $packet_map = '';

  $bundle .= "gme_lazy_load_many(array(\n";

  foreach($DEPS as $i => $file)
  {
    $unit = $units[$i];
    if(!$unit)
      throw new Exception("Indices mismatch");

    $base_name = basename($file);
    $bundle .= "$inc_dir . '/$base_name', '" . current(explode('.', $base_name)) . "', \n";
  }
  $bundle .= "));\n";

  $map = array();
  foreach($units as $unit)
  {
    $class_id = $unit->object->getClassId();
    $class_name = $unit->object->getName();
    if(isset($map[$class_id]))
      throw new Exception("Duplicating class id '$class_id'($class_name vs {$map[$class_id]})");
    $map[$class_id] = $class_name;

    $class_map .= "case " . 1*$class_id . ": return \"$class_name\";\n";
  }

  foreach($rpcs as $rpc)
  {
    $code = $rpc->getCode();
    $name = $rpc->in->getName();

    $packet_map .= "case " . 1*$code . ": return new $name(\$data);\n";
  }

  $templater = new mtg_php_templater();
  $tpl = $templater->tpl_packet_bundle();
  return mtg_fill_template($tpl, array('%bundle%' => $bundle, 
                                       '%class_map%' => $class_map,
                                       '%packet_map%' => $packet_map));
}

