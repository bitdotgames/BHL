<?php

require_once(dirname(__FILE__) . '/as3_tpl.inc.php');

function mtg_as3_generate_packet(mtgMetaInfo $info, mtgMetaPacket $packet, $as3_package_name)
{
  $templater = new mtg_as3_templater();

  $repl = array();
  $repl['%type%'] = $packet->getName();
  $repl['%code%'] = $packet->getNumericCode();
  $repl['%class%'] = $packet->getName();
  $repl['%class_id%'] = $packet->getClassId();

  $repl['%read_guard_begin%'] = '';
  $repl['%read_guard_end%'] = '';
  $repl['%write_guard_begin%'] = '';
  $repl['%write_guard_end%'] = '';

  $erase_beg = "";
  $erase_end = "";
  //$erase_beg = "#if TEST\n";
  //$erase_end = "#end\n";

  if(false === strpos($packet->getName(), 'CMSG'))
  {
    $repl['%write_guard_begin%'] = $erase_beg;
    $repl['%write_guard_end%'] = $erase_end;
  }
  else
  {
    $repl['%read_guard_begin%'] = $erase_beg;
    $repl['%read_guard_end%'] = $erase_end;
  } 

  $tpl = $templater->tpl_packet();

  _mtg_as3_add_struct_replaces($info, $packet, $repl, $as3_package_name);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function mtg_as3_generate_struct(mtgMetaInfo $info, mtgMetaStruct $struct, $as3_package_name)
{
  $templater = new mtg_as3_templater();

  $repl = array();
  $repl['%type%'] = $struct->getName();
  $repl['%parent_type%'] = $struct->getParent() ? "extends " . $struct->getParent() : "extends NetStructBase";
//  $repl['%parent_construct%'] = $struct->getParent() ? "super();" : "";
  $repl['%parent_override%'] = $struct->getParent() ? "override" : "";
  $repl['%class_id%'] = $struct->getClassId();

  $tpl = $templater->tpl_struct();

  _mtg_as3_add_struct_replaces($info, $struct, $repl, $as3_package_name);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function _mtg_as3_add_struct_replaces(mtgMetaInfo $info, mtgMetaStruct $struct, &$repl, $as3_package_name)
{
  $package_name = $as3_package_name;
  $repl['%package_name%'] = $package_name;
  $repl['%includes%'] = '';
  $repl['%fields%'] = '';
  $repl['%fill_fields%'] = '';
  $repl['%fill_buffer%'] = '';
  $repl['%fields_init%'] = '';
  $repl['%fields_list%'] = '';
  $repl['%fields_desc%'] = '';
  $repl['%fill_dump%'] = '';
  $repl['%private_fields%'] = '';
  $repl['%fill_private_fields%'] = '';
  $repl['%private_fields_init%'] = '';
  $repl['%private_fill_buffer%'] = '';
  $repl['%private_field_getters%'] = '';
  $repl['%private_field_setters%'] = '';
  
  foreach(mtg_extract_deps($struct) as $dep)
    $repl['%includes%'] .= "import ".$package_name.".packet.$dep;\n";

  $fields_desc = array();

  foreach(mtg_get_all_fields($info, $struct) as $field)
  {
    //skip parent fields, render only my own fields
    if($struct->hasField($field->getName()))
    {
      $repl['%fields%'] .= mtg_as3_field_decl($field) . "\n";
      $repl['%private_fields%'] .= mtg_as3_private_field_decl($field) . "\n";
      $repl['%private_field_getters%'] .= mtg_as3_getter_for_private_field($field) . "\n";
      $repl['%private_field_setters%'] .= mtg_as3_setter_for_private_field($field) . "\n";
    }

    $private_name = mtg_as3_field_private_name($field);

    $tokens_keys = array_keys($field->getTokens());
    $tokens_keys_as_str = count($tokens_keys) > 0 ? '"' . implode('","', $tokens_keys) . '"' : "";

    $repl['%fields_list%'] .= '"' . $field->getName() . '",';
    $fields_desc[] = 'new NetStructFieldDesc("' . $field->getName() . '", new <String>['. $tokens_keys_as_str .'])';
    $repl['%fill_fields%'] .= mtg_as3_field_fill_ex($field->getName(), $field->getSuperType(), $field->getType(), "reader", $as3_package_name, $field->getTokens()) . " ++field_num.num;\n";
    $repl['%fill_private_fields%'] .= mtg_as3_field_fill_ex($private_name, $field->getSuperType(), $field->getType(), "reader", $as3_package_name, $field->getTokens()) . " ++field_num.num;\n";
    $repl['%fields_init%'] .= $field->getName() . " = " . mtg_as3_field_init_value($field, $as3_package_name) . ";\n";
    $repl['%private_fields_init%'] .= $private_name . " = " . mtg_as3_field_init_value($field, $as3_package_name) . ";\n";
    $repl['%fill_buffer%'] .= mtg_as3_buffer_fill($field, "writer", "") . " ++field_num.num;\n";
    $repl['%private_fill_buffer%'] .= mtg_as3_buffer_fill($field, "writer", "$") . " ++field_num.num;\n";
  }

  $repl['%fields_desc%'] = implode(",\n         ", $fields_desc);

  if($repl['%fields_list%'])
    $repl['%fields_list%'] = rtrim($repl['%fields_list%'], ',');
}

function mtg_as3_struct_file(mtgMetaStruct $struct)
{
  return $struct->getName() . '.as';
}

function mtg_as3_packet_file(mtgMetaPacket $packet)
{
  return $packet->getName() . '.as';
}

function mtg_as3_handler_file(mtgMetaPacket $packet)
{
  return 'Handler_' . $packet->getName() . '.as';
}

function mtg_as3_field_decl(mtgMetaField $field)
{
  return "public var " . $field->getName() . " : " . mtg_as3_field_native_type($field) . ";";
}

function mtg_as3_private_field_decl(mtgMetaField $field)
{
  $varname = mtg_as3_field_private_name($field);
  return "protected var " . $varname . " : " . mtg_as3_field_native_type($field) . ";";
} 

function mtg_as3_getter_for_private_field(mtgMetaField $field)
{
  $varname = mtg_as3_field_private_name($field);
  return "public function get " . $field->getName() . "() : " . mtg_as3_field_native_type($field) . 
    " { return " . $varname. ";}";
}                                  

function mtg_as3_setter_for_private_field(mtgMetaField $field)
{
  $type = mtg_as3_field_native_type($field);
  $propname = $field->getName();
  $varname = mtg_as3_field_private_name($field);
  return "public function set " . $propname . "(__new_value : " . $type . ") : void " .
    " { if(__new_value !== ". $varname .") { " . $varname. " = __new_value; if(this.on_change !== null) this.on_change(this, \"".$propname."\");}}";
} 

function mtg_as3_field_private_name(mtgMetaField $field)
{
  return "$" . $field->getName();
}

function mtg_as3_field_native_type(mtgMetaField $field)
{
  return mtg_as3_field_native_type_ex($field->getSuperType(), $field->getType());
}

function mtg_as3_field_native_type_ex($super_type, $type)
{
  switch($super_type)
  {
    case "scalar":
      return mtg_as3_type2native($type);
    case "string":
      return "String";
    case "array":
      return "Vector.<" . mtg_as3_type2native($type[1]) . ">";
      //return "Array";
    case "struct":
      return $type;
    default:
      throw new Exception("Unknown super type '{$super_type}'");
  }
}

function mtg_as3_field_type(mtgMetaField $field)
{
  switch($field->getSuperType())
  {
    case "array":
      return end($field->getType()) . "[]";
    default:
      return $field->getType();
  }
}

function mtg_as3_field_init_value(mtgMetaField $field, $as3_package_name)
{
  $tokens = $field->getTokens();
  $super_type = $field->getSuperType();

  if($super_type == "string")
    $init_value = '""';
  else if($super_type == "scalar")
    $init_value = "0";
  else if($super_type == "array")
    $init_value = "new " . mtg_as3_field_native_type($field) . "()";
  else if($super_type == "struct")
    $init_value = "new ".$as3_package_name.".packet." . $field->getType() . "()";
  else
    throw new Exception("Not supported super type '$super_type'");

  if(array_key_exists('default', $tokens) && ($super_type == 'scalar' || $super_type == 'string'))
    $init_value = strval($tokens['default']);

  return $init_value;
}

function mtg_as3_field_fill(mtgMetaField $field, $buf, $as3_package_name)
{
  return mtg_as3_field_fill_ex($field->getName(), $field->getSuperType(), $field->getType(), $buf, $as3_package_name, $field->getTokens());
}

function mtg_as3_field_fill_ex($name, $super_type, $type, $buf, $as3_package_name, $tokens = array())
{
  $str = '';

  if($super_type == "scalar")
  {
    //float ugly special case
    if($type == 'float')
      $str .= "{$name} = $buf.nextAsFloat();";
    else
      $str .= "{$name} = $buf.nextAsInt();";
  }
  else if($super_type == "string")
  {
    $str .= "{$name} = $buf.nextAsString();";
  }
  else if($super_type == "struct")
  {
    $str .= "{$buf}.next();{$buf}.enter();\n";

    if(array_key_exists('virtual', $tokens))
      $str .= "{$name} = ".$as3_package_name.".packet.{$type}.read({$buf}, true/*virtual*/);\n";
    else
      $str .= "{$name} = ".$as3_package_name.".packet.{$type}.read({$buf});\n";

    $str .= "{$buf}.exit();\n";
  }
  else if($super_type == "array")
  {
    unset($tokens['default']);
    $str .= "$buf.extractVector({$name}, function(rref : *) : * { var tmp__ : *;" . 
            mtg_as3_field_fill_ex("tmp__", $type[0], $type[1], $buf, $as3_package_name, $tokens) . " return tmp__;});";
  }
  else
    throw new Exception("Unknown type {$super_type}");

  return $str;
}

function mtg_as3_buffer_fill(mtgMetaField $field, $buf, $name_prefix='')
{
  return mtg_as3_buffer_fill_ex($name_prefix . $field->getName(), $field->getSuperType(), $field->getType(), $buf, $field->getTokens());
}

function mtg_as3_buffer_fill_ex($name, $super_type, $type, $buf, $tokens = array())
{
  $str = '';

  if($super_type == "scalar")
  {
    if($type == 'float')
      $str .= "$buf.addFloat({$name});";
    else
      $str .= "$buf.addInt({$name});";
  }
  else if($super_type == "string")
  {
      $str .= "$buf.addString({$name});";
  }
  else if($super_type == "struct")
  {
    $str .= "{$buf}.beginList();\n";

    if(array_key_exists('virtual', $tokens))
      $str .= "{$name}.writeFields({$buf}, true/*virtual*/);\n";
    else
      $str .= "{$name}.writeFields({$buf});\n";

    $str .= "{$buf}.endList();\n";
  }
  else if($super_type == "array")
  {
    $str .= "$buf.addVector({$name}, function(wref : *, el : *) : void { " . 
            mtg_as3_buffer_fill_ex("el", $type[0], $type[1], $buf, $tokens) . "});";
  }
  else
    throw new Exception("Unknown type {$super_type}");

  return $str;
}

function mtg_as3_type2native($type)
{
  if(in_array($type, mtgMetaInfo::$SCALAR_TYPES))
  {
    if($type == "float")
      return "Number";
    else
    {
      //TODO: add support for uints as well?
      //NOTE: only int types are supported at the moment
      return "int";
    }
  }
  if($type == mtgMetaInfo::$STRING_TYPE)
    return "String";
  //struct?
  return $type;
}

