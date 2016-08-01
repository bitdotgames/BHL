<?php
require_once(dirname(__FILE__) . '/go.inc.php');

class mtgGoGenerator extends mtgGenerator
{
  function makeTargets(mtgMetaInfo $meta)
  {
    $targets = array();

    $codegen = mtg_conf("codegen", null);
    if(!$codegen)
      $codegen = new mtgGoCodegen();
    $codegen->setMetaInfo($meta);

    $refl = new ReflectionClass($codegen);
    $SHARED_DEPS = array(
      dirname(__FILE__) . '/go.inc.php', 
      dirname(__FILE__) . '/go_tpl.inc.php',
      __FILE__,
      $refl->getFileName()
    );

    mtg_mkdir(mtg_conf('out-dir'));

    $units = $meta->getUnits();
    $files = array();
    foreach($units as $unit)
    {
      $files = array_merge($files, mtg_get_file_deps($meta, $unit));

      //$out_file = $unit->object->getName() . '.go'; 
      //$file = mtg_conf("out-dir") . "/" . $out_file; 
      //$files[] = $file; 
      //$targets[] = mtg_new_file($file,
      //    array_merge($SHARED_DEPS, mtg_get_file_deps($meta, $unit)),
      //    array('gen_go_struct', $codegen, $unit)
      //    );
    }
    
    $bundle = mtg_conf("bundle", null);
    if($bundle && $units)
    {
      $targets[] = mtg_new_bundle($bundle, 
                                  array_merge($SHARED_DEPS, $files), 
                                  array('gen_go_bundle', $meta, $codegen, $units)); 
    }

    return $targets;
  }
}

function gen_go_struct($OUT, array $DEPS, mtgCodegen $codegen, mtgMetaInfoUnit $unit)
{
  return go_codegen($codegen, $unit);
}

function go_codegen(mtgGoCodegen $codegen, mtgMetaInfoUnit $unit)
{
  $unit_obj = $unit->object;
  if($unit_obj instanceof mtgMetaPacket)
    return $codegen->genRPC($unit_obj);
  if($unit_obj instanceof mtgMetaEnum)
    return $codegen->genEnum($unit_obj);
  else
    return $codegen->genStruct($unit_obj);
}

function gen_go_bundle($OUT, array $DEPS, mtgMetaInfo $meta, mtgGoCodegen $codegen, array $units)
{
  $bundle = '';
  $units_src = '';
  $create_struct_by_crc28 = '';
  $create_struct_by_name = '';
  $create_req_by_id = '';
  $create_rsp_by_id = '';
  $create_rpc = "";

  foreach($units as $unit)
  {
    $units_src .= go_codegen($codegen, $unit);
  }

  foreach($units as $unit)
  {
    if($unit->object->hasToken('POD') || $unit->object instanceof mtgMetaEnum)
      continue;

    $is_req = strpos($unit->object->getName(), "_REQ_");

    $create_struct_by_crc28 .= "\n    case {$unit->object->getClassId()}: { return New{$unit->object->getName()}(), nil }";
    $create_struct_by_name  .= "\n    case \"{$unit->object->getName()}\": { return New{$unit->object->getName()}(), nil }";

    if($unit->object instanceof mtgMetaPacket)
    {
      $str = "\n    case {$unit->object->getCode()}: { return New{$unit->object->getName()}(), nil; }"; 
      if($is_req)
        $create_req_by_id .= $str;
      else
        $create_rsp_by_id .= $str;
      
      $name = $unit->object->getName();
      if(strpos($name, 'RPC_RSP_') !== false)
      {
        $sub_name = str_replace('RPC_RSP_', '', $name);
        $name     = str_replace('RSP_', '', $name);
        $create_rpc .= "  $name(out *RPC_RSP_$sub_name, in *RPC_REQ_$sub_name) error\n";
      }
    }
  }

  $templater = new mtg_go_templater();
  $tpl = $templater->tpl_bundle();
  return mtg_fill_template($tpl, array('%bundle%' => $bundle, 
                                       '%units_src%' => $units_src,
                                       '%create_struct_by_name%' => $create_struct_by_name,
                                       '%create_struct_by_crc28%' => $create_struct_by_crc28,
                                       '%create_req_by_id%' => $create_req_by_id,
                                       '%create_rsp_by_id%' => $create_rsp_by_id,
                                       '%create_rpc%' => $create_rpc,
                                       ));
}
