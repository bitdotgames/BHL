<?php
require_once(dirname(__FILE__) . '/cxx.inc.php');

function generate_cxx_packets(mtgMetaInfo $meta)
{
  $targets = array();
  $SHARED_DEPS = array(dirname(__FILE__) . '/cxx.inc.php', 
                       dirname(__FILE__) . '/cxx_tpl.inc.php',
                       dirname(__FILE__) . '/cxx_generator.inc.php'
                       );

  mtg_mkdir(mtg_conf('out-dir'));

  $units = array();
  $all_deps = $SHARED_DEPS;
  foreach($meta->getUnits() as $unit)
  {
    $out_file_h = mtg_cxx_struct_header_file($unit->object); 
    $out_file_cxx = mtg_cxx_struct_impl_file($unit->object); 

    $units[] = $unit;

    $file_deps = array_merge($SHARED_DEPS, mtg_get_file_deps($meta, $unit));
    $all_deps = array_merge($all_deps, $file_deps);

    if($out_file_h)
    {
      $file = mtg_conf("out-dir") . "/" . $out_file_h;
      $files[] = $file; 
      $targets[] = mtg_new_file($file, $file_deps,
          array('gen_cxx_header', $meta, $unit),
          true/*write only if contents differs*/
          );
    }

    if($out_file_cxx)
    {
      $file = mtg_conf("out-dir") . "/" . $out_file_cxx;
      $files[] = $file; 
      $targets[] = mtg_new_file($file, $file_deps,
          array('gen_cxx_impl', $meta, $unit)
          );
    }
  }

  $bundle = mtg_conf("bundle", null);
  $bundle_h = mtg_conf("bundle_h", null);
  if($bundle && $bundle_h && $targets)
  {
    $targets[] = mtg_new_bundle($bundle_h, 
                                array_merge($SHARED_DEPS, $files), 
                                array('gen_cxx_bundle_header', $meta, $units), 
                                true/*write only if contents differs*/); 
    $targets[] = mtg_new_bundle($bundle, 
                                array_merge($SHARED_DEPS, $files), 
                                array('gen_cxx_bundle_impl', 
                                $meta, 
                                $units)); 
  }

  return $targets;
}

function gen_cxx_header($OUT, array $DEPS, mtgMetaInfo $meta, mtgMetaInfoUnit $unit, $in_bundle = false)
{
  if($unit->object instanceof mtgMetaPacket)
    return mtg_cxx_generate_packet_header($meta, $unit->object, $in_bundle);
  else if($unit->object instanceof mtgMetaEnum)
    return mtg_cxx_generate_enum_header($meta, $unit->object, $in_bundle);
  else
    return mtg_cxx_generate_struct_header($meta, $unit->object, $in_bundle);
}

function gen_cxx_impl($OUT, array $DEPS, mtgMetaInfo $meta, mtgMetaInfoUnit $unit, $in_bundle = false)
{
  if($unit->object instanceof mtgMetaPacket)
    return mtg_cxx_generate_packet_impl($meta, $unit->object, $in_bundle);
  else if($unit->object instanceof mtgMetaEnum)
    return '';
  else
    return mtg_cxx_generate_struct_impl($meta, $unit->object, $in_bundle);
}

function gen_cxx_bundle_header($OUT, array $DEPS, mtgMetaInfo $meta, array $units)
{
  $bundle = '';

  $out_dir = mtg_conf("out-dir");
  foreach($units as $unit)
  {
    //$bundle .= gen_cxx_header($OUT, $DEPS, $meta, $unit, true/*in bundle*/);
    //NOTE: headers do take into account dependencies 
    $out_file_h = mtg_cxx_struct_header_file($unit->object); 
    $bundle .= '#include "' . $out_dir . '/' . $out_file_h . '"' . "\n";
  }

  $templater = new mtg_cxx_templater();
  $tpl = $templater->tpl_bundle_header();
  return mtg_fill_template($tpl, array('%bundle%' => $bundle));
}

function gen_cxx_bundle_impl($OUT, array $DEPS, mtgMetaInfo $meta, array $units)
{
  $bundle = '';
  $create_struct_by_name = '';
  $create_struct_by_crc28 = '';
  $create_struct_by_rpcid = '';

  foreach($units as $unit)
  {
    $bundle .= gen_cxx_impl($OUT, $DEPS, $meta, $unit, true/*in bundle*/);

    if($unit->object->hasToken('POD') || 
       $unit->object instanceof mtgMetaEnum)
      continue;

    $create_struct_by_crc28 .= "if(crc == {$unit->object->getClassId()}) return GAME_NEW_EX({$unit->object->getName()}, *allocator)(allocator);\n";

    if($unit->object instanceof mtgMetaPacket)
    {
      if(strpos($unit->object->getName(), '_RSP_') !== false)
      {
        $rpc_name = $unit->object->getName();
        $create_struct_by_rpcid .= "if(id == {$unit->object->getNumericCode()}) return GAME_NEW_EX({$rpc_name}, *allocator)(allocator);\n";
      }
    }
  }

  $templater = new mtg_cxx_templater();
  $tpl = $templater->tpl_bundle_impl();
  return mtg_fill_template($tpl, array('%bundle%' => $bundle, 
                                       '%create_struct_by_name%' => $create_struct_by_name,
                                       '%create_struct_by_crc28%' => $create_struct_by_crc28,
                                       '%create_struct_by_rpcid%' => $create_struct_by_rpcid,
                                       ));
}

