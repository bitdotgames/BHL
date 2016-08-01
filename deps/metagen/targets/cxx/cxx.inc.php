<?php
require_once(dirname(__FILE__) . '/cxx_tpl.inc.php');

function mtg_cxx_generate_struct_header(mtgMetaInfo $info, mtgMetaStruct $struct, $in_bundle = false)
{
  $templater = new mtg_cxx_templater();

  $repl = array();
  $repl['%classid%'] = $struct->getClassId();
  $repl['%type%'] = $struct->getName();
  $repl['%parent%'] = 'MetaBaseStruct';
  if($parent = $struct->getParent())
    $repl['%parent%'] = $parent;

  _mtg_cxx_add_struct_header_replaces($info, $struct, $repl);

  if($struct->hasToken('POD'))
    $tpl = $templater->tpl_POD_header($in_bundle);
  else
    $tpl = $templater->tpl_struct_header($in_bundle);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function mtg_cxx_generate_struct_impl(mtgMetaInfo $info, mtgMetaStruct $struct, $in_bundle = false)
{
  $templater = new mtg_cxx_templater();

  $repl = array();
  $repl['%classid%'] = $struct->getClassId();
  $repl['%type%'] = $struct->getName();
  $repl['%parent%'] = 'MetaBaseStruct';
  if($parent = $struct->getParent())
    $repl['%parent%'] = $parent;

  _mtg_cxx_add_struct_impl_replaces($info, $struct, $repl);
  if($repl['%fields_init%'])
    $repl['%fields_init%'] = ',' . $repl['%fields_init%'];

  if($struct->hasToken('POD'))
    $tpl = $templater->tpl_POD_impl($in_bundle);
  else
    $tpl = $templater->tpl_struct_impl($in_bundle);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function mtg_cxx_generate_packet_header(mtgMetaInfo $info, mtgMetaPacket $packet, $in_bundle = false)
{
  $templater = new mtg_cxx_templater();

  $repl = array();
  $repl['%classid%'] = $packet->getClassId();
  $repl['%type%'] = $packet->getName();

  _mtg_cxx_add_struct_header_replaces($info, $packet, $repl);

  $tpl = $templater->tpl_packet_header($in_bundle);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function mtg_cxx_generate_packet_impl(mtgMetaInfo $info, mtgMetaPacket $packet, $in_bundle = false)
{
  $templater = new mtg_cxx_templater();

  $repl = array();
  $repl['%classid%'] = $packet->getClassId();
  $repl['%type%'] = $packet->getName();
  $repl['%code%'] = $packet->getNumericCode();

  _mtg_cxx_add_struct_impl_replaces($info, $packet, $repl);
  if($repl['%fields_init%'])
    $repl['%fields_init%'] = ',' . $repl['%fields_init%'];

  $tpl = $templater->tpl_packet_impl($in_bundle);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function mtg_cxx_generate_enum_header(mtgMetaInfo $info, mtgMetaStruct $struct, $in_bundle = false)
{
  $templater = new mtg_cxx_templater();

  $repl = array();
  $repl['%type%'] = $struct->getName();
  $repl['%fields%'] = '';
  $repl['%field_names%'] = '';
  $repl['%field_values%'] = '';
  $repl['%v2str%'] = '';
  $repl['%v2bool%'] = '';

  _mtg_cxx_add_enum_replaces($info, $struct, $repl);

  $tpl = $templater->tpl_enum($in_bundle);

  return str_replace(array_keys($repl), array_values($repl), $tpl);
}

function _mtg_cxx_add_struct_header_replaces(mtgMetaInfo $info, mtgMetaStruct $struct, &$repl)
{
  $repl['%includes%'] = '';
  $repl['%fields%'] = '';
  $all_fields = mtg_get_all_fields($info, $struct); 
  $repl['%fields_count%'] = count($all_fields);

  if($struct->hasToken('POD'))
    $fields = $all_fields;
  else
    $fields = $struct->getFields();

  foreach(mtg_extract_deps($struct) as $dep)
  {
    //protection from self inclusion
    if($dep != $struct->getName())
      $repl['%includes%'] .= "#include \"$dep.h\"\n";
  }

  foreach($fields as $field)
  {
    $repl['%fields%'] .= mtg_cxx_field_decl($field) . "\n";
  }
}

function _mtg_cxx_add_enum_replaces(mtgMetaInfo $info, mtgMetaEnum $enum, &$repl)
{
  $total = 0;
  foreach($enum->getValues() as $k => $v)
  {
    $repl['%fields%'] .= "$k = $v,\n";
    $repl['%field_names%'] .= "\"$k\",";
    $repl['%field_values%'] .= "$k,";
    $repl['%v2str%'] .= "case $v: return \"$k\";\n";
    $repl['%v2bool%'] .= "case $v: return true;\n";
    ++$total;
  }
  $repl['%field_names%'] = rtrim($repl['%field_names%'], ",");
  $repl['%field_values%'] = rtrim($repl['%field_values%'], ",");
  $repl['%fields_count%'] = $total;
}

function _mtg_cxx_add_struct_impl_replaces(mtgMetaInfo $info, mtgMetaStruct $struct, &$repl)
{
  $repl['%fields_init%'] = '';
  $repl['%fields_construct%'] = '';
  $repl['%fields_destruct%'] = '';
  $repl['%read_buffer%'] = '';
  $repl['%write_buffer%'] = '';
  $repl['%optional_set%'] = '';
  $repl['%compare_fields%'] = '';

  $is_POD = false;
  if($struct->hasToken('POD'))
  {
    $is_POD = true;
    $fields = mtg_get_all_fields($info, $struct);
  }
  else
    $fields = $struct->getFields();

  foreach($fields as $field)
  {
    $name = $field->getName();

    if($field->getSuperType() === "array")
    {
      if(!$field->hasToken('arrmax'))
      {
        if($is_POD)
          throw new Exception("Field '{$name}' is not POD in '{$struct->getName()}'");
        $repl['%fields_init%'] .= "$name(allocator_),";
      }
      else
        $repl['%fields_construct%'] .= "$name.clear();\n";
    }
    else if($field->getType() === "string")
    {
      if(!$field->hasToken('strmax'))
      {
        if($is_POD)
          throw new Exception("Field '{$name}' is not POD in '{$struct->getName()}'");
        $repl['%fields_init%'] .= "$name(allocator_),";
      }
      else
        $repl['%fields_construct%'] .= "$name.clear();\n";
    }
    else if($field->getSuperType() === "struct")
    {
      if(!$info->findUnit($field->getType())->object->hasToken('POD'))
      {
        $repl['%fields_init%'] .= "$name(allocator_),";
        if($is_POD)
          throw new Exception("Field '{$name}' is not POD in '{$struct->getName()}'");
      }
      else
        $repl['%fields_construct%'] .= "memset(&$name, 0, sizeof($name));\n";
    }
    else
      $repl['%fields_init%'] .= "$name(),";

    if($field->hasToken('virtual'))
    {
      if($is_POD)
        throw new Exception("Field '{$name}' is not POD in '{$struct->getName()}'");

      if($field->getSuperType() !== "array" || current($field->getType()) !== "struct")
        throw new Exception("Field '{$name}' must be of type BaseStruct[] in order to support @virtual");

      $destr = '';
      $destr .= "for(" . mtg_cxx_field_native_type($field) . "::iterator it__ = {$name}.begin();it__!={$name}.end();++it__){\n";
      $destr .= "GAME_DEL_EX(*it__, *allocator_);\n";
      $destr .= "}\n";

      $repl['%fields_destruct%'] .= $destr;
    }

    $repl['%read_buffer%'] .= mtg_cxx_buf_read($info, $field, "reader", $struct->hasToken('POD')) . "\n";
    $repl['%write_buffer%'] .= mtg_cxx_buf_write($info, $field, "writer", $struct->hasToken('POD')) . "\n";

    if($field->getSuperType() === "struct")
      $field_is_pod = $info->findUnit($field->getType())->object->hasToken('POD');
    else
      $field_is_pod = false;
    $repl['%optional_set%'] .= mtg_cxx_set_optionals_default($info, $field, $field_is_pod);

    $repl['%compare_fields%'] .= "if(!($name == rhs.$name)) return false;\n";
  }
  $repl['%fields_init%'] = rtrim($repl['%fields_init%'], ',');
}

function mtg_cxx_field_native_type(mtgMetaField $field)
{
  return mtg_cxx_field_native_type_ex($field->getSuperType(), $field->getType(), $field->getTokens());
}

function mtg_cxx_field_native_type_ex($super_type, $type, array $tokens = array())
{
  switch($super_type)
  {
    case "scalar":
      return mtg_cxx_type2native($type, $tokens);
    case "string":
      if(array_key_exists('strmax', $tokens))
        return "game::CFixedString<" . $tokens['strmax'] . ">";
      else
        return "game::string";
    case "array":
      if(array_key_exists('arrmax', $tokens))
        return "game::Array< " . mtg_cxx_type2native($type[1], $tokens, $type[0] == "enum") . "," . $tokens['arrmax'] . " >";
      else
        return "game::vector< " . mtg_cxx_type2native($type[1], $tokens, $type[0] == "enum") . " >";
    case "struct":
      return (array_key_exists('virtual', $tokens)) ? "$type*" : $type;
    case "enum":
      return "$type::Value";
    default:
      throw new Exception("Unknown super type '{$super_type}'");
  }
}

function mtg_cxx_field_decl(mtgMetaField $field)
{
  return mtg_cxx_field_native_type($field) . " " . $field->getName() . ";";
}

function mtg_cxx_struct_header_file(mtgMetaStruct $struct)
{
  return $struct->getName() . '.h';
}

function mtg_cxx_struct_impl_file(mtgMetaStruct $struct)
{
  if($struct instanceof mtgMetaEnum)
    return null;

  return $struct->getName() . '.cpp';
}

function mtg_cxx_set_optionals_default(mtgMetaInfo $info, mtgMetaField $field, $is_pod)
{
  return mtg_cxx_set_optionals_default_ex($info, $field->getName(), $field->getSuperType(), $field->getType(), false, $field->getTokens(), $is_pod);
}

function mtg_cxx_set_optionals_default_ex(mtgMetaInfo $info, $name, $super_type, $type, $in_array, array $tokens = array(), $is_pod = false)
{
  $str = '';

  if(!array_key_exists("optional", $tokens))
    return $str;

  if($super_type == "scalar")
  {
    if(array_key_exists("default", $tokens))
      $str .= "$name = {$tokens['default']};\n";
    else
      $str .= "$name = 0;\n";
  }
  else if($super_type == "string")
  {
    if(array_key_exists("default", $tokens))
      $str .= "$name.assign({$tokens['default']});\n";
    else
      $str .= "$name.clear();\n";
  }
  else if($super_type == "struct")
  {
    if(array_key_exists("default", $tokens))
      throw new Exception("Default not supported for $super_type");

    $str .=
    ($is_pod ?
      "$name = ($type){0};\n"
     :
      "$name::$type()\n"
    );
  }
  else if($super_type == "enum")
  {
    if(array_key_exists("default", $tokens))
    {
      $str .= "$name = $type :: " . str_replace("\"", "", $tokens['default']) . ";\n";
    }
  }
  else if($super_type == "array")
  {
    if(array_key_exists("default", $tokens) && $tokens["default"] !== '[]')
      throw new Exception("Default not supported for $super_type");

    $str .= "$name.clear();\n";
  }
  else
    throw new Exception("Unknown type {$super_type}");

  return $str;
}

function mtg_cxx_buf_read(mtgMetaInfo $info, mtgMetaField $field, $buf, $is_pod = false)
{
  return mtg_cxx_buf_read_ex($info, $field->getName(), $field->getSuperType(), $field->getType(), $buf, false, $field->getTokens(), $is_pod);
}

function cxx_read(array $tokens)
{
  return "GAME_CHECK_READ".(array_key_exists('optional', $tokens)?"_OPTIONAL":"");
}

function mtg_cxx_buf_read_ex(mtgMetaInfo $info, $name, $super_type, $type, $buf, $in_array, array $tokens = array(), $is_pod = false)
{
  $str = '';

  //$str .= 'TRACE("Reading '.$name.'");';

  if($super_type == "scalar")
  {
    if($type == 'float')
      $str .= cxx_read($tokens)."({$buf}.readFloat($name), \"$name\");";
    else if (strpos($type, "uint") === false)
      $str .= cxx_read($tokens)."({$buf}.readI32($name), \"$name\");";
    else
      $str .= cxx_read($tokens)."({$buf}.readU32($name), \"$name\");";
    $str .= "\n";
  }
  else if($super_type == "string")
  {
    if(array_key_exists('strmax', $tokens))
    {
      $str .= "{ size_t tmp_size__ = 0; ".cxx_read($tokens)."({$buf}.readString({$name}.data, {$name}.capacity(), &tmp_size__), \"$name\");";
      $str .= "if(tmp_size__ > 0) {$name}.resize(tmp_size__); }";
      //$str .= "TRACE(\"{$name}: %s\", {$name}.data);";
    }
    else
      $str .= cxx_read($tokens)."({$buf}.readString($name), \"$name\");";
  }
  else if($super_type == "struct")
  {
    if(array_key_exists('virtual', $tokens))
      $str .= cxx_read($tokens)."(MetaBaseStruct::readGeneric({$name}, $buf, allocator_), \"$name\");\n";
    else
      $str .= cxx_read($tokens)."({$name}.read($buf), \"$name\");\n";
  }
  else if($super_type == "enum")
  {
    $str .= "i32 {$name}_tmp__;\n";
    $str .= cxx_read($tokens)."({$buf}.readI32(${name}_tmp__), \"$name\");\n";
    $str .= "if(!$type::convert(${name}_tmp__, &{$name})) { GAME_ERROR(\"Bad enum value %d for '$name'\", ${name}_tmp__); return DATA_INVALID_DATA; }\n";
  }
  else if($super_type == "array")
  {
    $str .= "size_t {$name}_size = 0;\n";
    $str .= cxx_read($tokens)."({$buf}.beginArray(&{$name}_size), \"$name\");\n";
    $str .= "{\n";
    $str .= "{$name}.clear();\n";
    $str .= "{$name}.reserve({$name}_size);\n";
    $str .= "for(size_t i=0;i< {$name}_size;++i)\n";
    $str .= "{\n";
    $tmp_name = "{$name}_tmp__";
    $str .= "  " . mtg_cxx_field_native_type_ex($type[0], $type[1], $tokens) . "& {$tmp_name} = {$name}.push_back();\n";
    $str .= "  " . mtg_cxx_buf_read_ex($info, $tmp_name, $type[0], $type[1], $buf, true, $tokens, $is_pod);
    $str .= "}\n";
    $str .= "}\n";
    $str .= cxx_read($tokens)."({$buf}.endArray(), \"$name\");";
  }
  else
    throw new Exception("Unknown type {$super_type}");

  return $str;
}

function mtg_cxx_buf_write(mtgMetaInfo $info, mtgMetaField $field, $buf, $is_pod = false)
{
  return mtg_cxx_buf_write_ex($info, $field->getName(), $field->getSuperType(), $field->getType(), $buf, $field->getTokens(), $is_pod);
}

function mtg_cxx_buf_write_ex(mtgMetaInfo $info, $name, $super_type, $type, $buf, array $tokens = array(), $is_pod = false)
{
  $str = '';

  if($super_type == "scalar")
  {
    if($type == 'float')
      $str .= "GAME_CHECK_WRITE({$buf}.writeFloat($name), \"$name\");";
    else if (strpos($type, "uint") === false)
      $str .= "GAME_CHECK_WRITE({$buf}.writeI32($name), \"$name\");";
    else
      $str .= "GAME_CHECK_WRITE({$buf}.writeU32($name), \"$name\");";
    $str .= "\n";
  }
  else if($super_type == "string")
  {
    $str .= "GAME_CHECK_WRITE({$buf}.writeString({$name}.c_str(), {$name}.length()), \"$name\");\n";
  }
  else if($super_type == "struct")
  {
    if(array_key_exists('virtual', $tokens))
      $str .= "GAME_CHECK_WRITE(($name)->writeGeneric($buf), \"$name\");\n";
    else
      $str .= "GAME_CHECK_WRITE($name.write($buf), \"$name\");\n";
  }
  else if($super_type == "enum")
  {
    $str .= "GAME_CHECK_WRITE({$buf}.writeI32($name), \"$name\");\n";
  }
  else if($super_type == "array")
  {
    $str .= "{\n";
    $str .=   "GAME_CHECK_WRITE({$buf}.beginArray({$name}.size()), \"$name\");\n";
    $str .=   "for(" . mtg_cxx_field_native_type_ex($super_type, $type, $tokens) . "::const_iterator {$name}_it__ = {$name}.begin();{$name}_it__!={$name}.end();++{$name}_it__){\n";
    $str .=     mtg_cxx_buf_write_ex($info, "(*{$name}_it__)", $type[0], $type[1], $buf, $tokens, $is_pod);
    $str .=   "}\n";
    $str .=   "GAME_CHECK_WRITE({$buf}.endArray(), \"$name\");\n";
    $str .= "}\n";
  }
  else
    throw new Exception("Unknown meta type '$super_type'");

  return $str;
}

function mtg_cxx_type2native($type, array $tokens = array(), $is_enum = false)
{
  if(in_array($type, mtgMetaInfo::$SCALAR_TYPES))
  {
    if($type == "float")
      return "float";
    else
    {
      //only 32bit int types are supported
      return (strpos($type, "uint") !== false) ? "u32" : "i32";
    }
  }

  if($type == mtgMetaInfo::$STRING_TYPE)
  {
    if(array_key_exists('strmax', $tokens))
      return "game::CFixedString<".$tokens['strmax'].">";
    else
      return "game::string";
  }

  //struct?
  if(array_key_exists('virtual', $tokens))
    return "$type*";

  return $is_enum ? "$type::Value" : $type;
}

