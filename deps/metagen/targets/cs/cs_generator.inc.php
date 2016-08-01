<?php
require_once(dirname(__FILE__) . '/cs.inc.php');

class mtgCsGenerator extends mtgGenerator
{
  function makeTargets(mtgMetaInfo $meta)
  {
    $targets = array();

    mtg_mkdir(mtg_conf('out-dir'));

    $codegen = mtg_conf("codegen", null);
    if(!$codegen)
      $codegen = new mtgCsCodegen();
    $codegen->setMetaInfo($meta);

    $refl = new ReflectionClass($codegen);
    $SHARED_DEPS = array(
      dirname(__FILE__) . '/cs.inc.php', 
      dirname(__FILE__) . '/cs_tpl.inc.php',
      __FILE__,
      $refl->getFileName()
    );

    $units = array();
    $files = array();
    foreach($meta->getUnits() as $unit)
    {
      $units[] = $unit;
      $files = array_merge($files, mtg_get_file_deps($meta, $unit));

      //$out_file = $unit->object->getName() . '.cs'; 
      //$file = mtg_conf("out-dir") . "/" . $out_file; 
      //$files[] = $file; 
      //$targets[] = mtg_new_file($file,
      //    array_merge($SHARED_DEPS, mtg_get_file_deps($meta, $unit)),
      //    array('gen_cs_struct', $codegen, $unit)
      //    );
    }
    
    $bundle = mtg_conf("bundle", null);
    if($bundle && $units)
    {
      $targets[] = mtg_new_bundle($bundle, 
                                  array_merge($SHARED_DEPS, $files), 
                                  array(array($this, 'genBundle'), $meta, $codegen, $units)); 
    }

    return $targets;
  }

  function genBundle($OUT, array $DEPS, mtgMetaInfo $meta, mtgCsCodegen $codegen, array $units)
  {
    $units_src = '';
    $id2type = '';
    $create_struct_by_crc28 = '';
    $create_rpc_by_id = '';

    foreach($meta->getRPCs() as $rpc)
    {
      $units_src .= $codegen->genRPC($rpc);
      $create_rpc_by_id .= "\n    case {$rpc->getCode()}: { return new {$rpc->getName()}(); };"; 
    }

    foreach($units as $unit)
    {
      $units_src .= cs_codegen($codegen, $unit) . "\n";

      if($unit->object->hasToken('POD') || $unit->object instanceof mtgMetaEnum)
        continue;

      $id2type .= "\n    case {$unit->object->getClassId()}: { return typeof({$unit->object->getName()}); };";
      $create_struct_by_crc28 .= "\n    case {$unit->object->getClassId()}: { return new {$unit->object->getName()}(); };";
    }

    $templater = new mtg_cs_templater();
    $tpl = $templater->tpl_bundle();
    return mtg_fill_template($tpl, array( 
      '%namespace%' => $codegen->namespace,
      '%units_src%' => $units_src,
      '%id2type%' => $id2type,
      '%create_struct_by_crc28%' => $create_struct_by_crc28,
      '%create_rpc_by_id%' => $create_rpc_by_id,
    ));
  }
}

function gen_cs_struct($OUT, array $DEPS, mtgCsCodegen $codegen, mtgMetaInfoUnit $unit)
{
  return cs_codegen($codegen, $unit);
}

function cs_codegen(mtgCsCodegen $codegen, mtgMetaInfoUnit $unit)
{
  $unit_obj = $unit->object;
  if($unit_obj instanceof mtgMetaEnum)
    return $codegen->genEnum($unit_obj);
  else
    return $codegen->genStruct($unit_obj);
}

