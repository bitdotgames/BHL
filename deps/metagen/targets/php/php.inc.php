<?php

require_once(dirname(__FILE__) . '/../../metagen.inc.php');
require_once(dirname(__FILE__) . '/php_tpl.inc.php');

class mtgPHPCodegen extends mtgCodegen
{
  protected $filter_aliases = array();

  function __construct(array $filter_aliases = array())
  {
    $this->filter_aliases = $filter_aliases;
  }

  function genUnit(mtgMetaInfoUnit $unit)
  {
    $obj = $unit->object;
    if($obj instanceof mtgMetaRPC)
      return $this->genRPC($obj);
    if($obj instanceof mtgMetaEnum)
      return $this->genEnum($obj);
    else if($obj instanceof mtgMetaStruct)
      return $this->genStruct($obj);
    else
    {
      echo "WARN: skipping meta unit '{$obj->getId()}'\n";
      return '';
    }
  }

  function genEnum(mtgMetaEnum $struct)
  {
    $templater = new mtg_php_templater();

    $repl = array();
    $repl['%class%'] = $struct->getName();
    $repl['%class_id%'] = $struct->getClassId();

    $tpl = $templater->tpl_enum();

    $this->fillEnumRepls($struct, $repl);

    return mtg_fill_template($tpl, $repl);
  }

  function fillEnumRepls(mtgMetaEnum $enum, &$repl)
  {
    $repl['%values%'] = '';
    $repl['%vnames_list%'] = '';
    $repl['%values_list%'] = '';

    $vnames = array();
    $values = array();
    $txt_values = array();
    $values_map = array();
    $names_map  = array();
    $default_value = null;

    $enum_name = $enum->getName();
    foreach($enum->getValues() as $vname => $value)
    {
      $txt_values[] = "const $vname = $value;";
      $vnames[] = "'$vname'";
      $values[] = $value;
      $values_map[] = "'$vname' => $value";
      $names_map[] = "$value => '$vname'";
      if(!$default_value)
        $default_value = "$value; // $vname";
    }

    $repl['%values%']      = implode("\n  ", $txt_values);
    $repl['%vnames_list%'] = implode(',', $vnames);
    $repl['%values_list%'] = implode(',', $values);
    $repl['%values_map%']  = implode(',', $values_map);
    $repl['%names_map%']    = implode(',', $names_map);
    $repl['%default_enum_value%'] = $default_value;
  }

  function genRPC(mtgMetaRpc $rpc)
  {
    $req = $this->genRPCPacket($rpc->getReq()); 
    $rsp = $this->genRPCPacket($rpc->getRsp());
    return $req . str_replace('<?php', '', $rsp);
  }

  function genRPCPacket(mtgMetaPacket $packet)
  {
    $templater = new mtg_php_templater();

    $repl = array();
    $repl['%type%'] = $packet->getName();
    $repl['%code%'] = $packet->getNumericCode();
    $repl['%class%'] = $packet->getName();
    $repl['%class_id%'] = $packet->getClassId();
    $repl['%parent_class%'] = 'extends gmeStrictPropsObject';

    $tpl = $templater->tpl_packet();

    $this->fillStructRepls($packet, $repl);

    return mtg_fill_template($tpl, $repl);
  }

  function genStruct(mtgMetaStruct $struct)
  {
    $templater = new mtg_php_templater();

    $repl = array();
    $repl['%class%'] = $struct->getName();
    $repl['%class_id%'] = $struct->getClassId();
    $repl['%parent_class%'] = $struct->getParent() ? "extends " . $struct->getParent() : "extends gmeStrictPropsObject";

    $tpl = $templater->tpl_struct();

    $this->fillStructRepls($struct, $repl);

    return mtg_fill_template($tpl, $repl);
  }

  function preprocessField(mtgMetaStruct $struct, mtgMetaField $field)
  {}

  function fillStructRepls(mtgMetaStruct $struct, &$repl)
  {
    foreach($struct->getFields() as $field)
      $this->preprocessField($struct, $field);

    $parent = $struct->getParent();

    $repl['%includes%'] = '';
    $repl['%fields%'] = '';
    $repl['%fields_names%'] = ($parent ? 'array_merge(parent::CLASS_FIELDS(), ' : '') . 'array(';
    $repl['%fields_props%'] = ($parent ? 'array_merge(parent::CLASS_FIELDS_PROPS(), ' : '') . 'array(';
    $repl['%fields_types%'] = ($parent ? 'array_merge(parent::CLASS_FIELDS_TYPES(), ' : '') . 'array(';
    $repl['%fields_init%'] = '';
    $repl['%fill_fields%'] = '';
    $repl['%fill_buffer%'] = '';
    $repl['%total_top_fields%'] = sizeof($struct->getFields());
    $repl["%ext_methods%"] = "";

    $repl['%class_props%'] = str_replace("\n", "", var_export($struct->getTokens(), true));

    $deps = array();
    if($parent)
      $deps[] = $parent.'';
    foreach($struct->getFields() as $field)
    {
      $type = $field->getType();
      if($type instanceof mtgArrType)
        $type = $type->getValue();

      if($type instanceof mtgUserType)
        $deps[] = $type->getName();
    }
    $deps = array_unique($deps);

    foreach($deps as $dep)
      $repl['%includes%'] .= "require_once(dirname(__FILE__) . '/$dep.class.php');\n";

    if($parent)
    {
      $repl['%fields_init%'] .= "parent::__construct();\n";
      $repl['%fill_fields%'] .= "\$IDX = parent::import(\$message, \$assoc, false);\n";
      $repl['%fill_buffer%'] .= "parent::fill(\$message, \$assoc, false);\n";
    }

    $indent = "        ";
    $local_indent = "  ";
    $fill_func_indent = "      ";

    foreach($struct->getFields() as $field)
    {
      $repl['%fields_names%'] .= "'" . $field->getName() . "',";
      $repl['%fields_props%'] .= "'" . $field->getName() . "' => " . str_replace("\n", "", var_export($field->getTokens(), true)) . ",";
      $field_type = $field->getType(); 
      $repl['%fields_types%'] .= "'" . $field->getName() . "' => '" . $field_type . "',";

      $repl['%fields_init%'] .= $this->genFieldInit($field, '$this->') . "\n";
      $repl['%fill_fields%'] .= $local_indent.$this->genFieldFill($field, '$message', true/*check_exist_data*/, '$this->', $indent) . $indent."++\$IDX;\n";
      $repl['%fill_buffer%'] .= $this->genBufFill($field, '$message', '$this->', $fill_func_indent) . "\n";
      $repl['%fields%'] .= $this->genFieldDecl($field) . "\n";
    }

    $repl['%fields_names%'] .= ')' . ($parent ? ')' : '');
    $repl['%fields_props%'] .= ')' . ($parent ? ')' : '');
    $repl['%fields_types%'] .= ')' . ($parent ? ')' : '');

    $this->undoTemp();
  }

  function genFieldInit(mtgMetaField $field, $prefix = "")
  {
    $tokens = $field->getTokens();
    $type = $field->getType();

    if($type instanceof mtgMetaStruct)
      return $prefix . $field->getName() . " = new " . $type . "();";
    else if($type instanceof mtgMetaEnum)
    {
      if(array_key_exists('default', $tokens))
        $default = $this->genDefault($tokens['default']);
      else
        $default = 'DEFAULT_VALUE';

      $default = str_replace('"', '', $default);
      return $prefix . $field->getName() . " = " . $type . "::$default;";
    }
    else if($type instanceof mtgArrType)
      return $prefix . $field->getName() . " = array();";
    else if($type instanceof mtgBuiltinType)
    {
      if($type->isString())
        $init_value = "''";
      else
        $init_value = "0";

      if(array_key_exists('default', $tokens))
        $init_value = $this->genDefault($tokens['default']);

      return $prefix . $field->getName() . " = $init_value;";
    }
    else
      throw new Exception("Unknown type '{$type}'");
  }

  function genFieldFill(mtgMetaField $field, $buf, $check_exist_data, $prefix, $indent = "")
  {
    return $this->genFieldFillEx($field->getName(), $field->getType(), $buf, $check_exist_data, $prefix, $field->getTokens(), false, "", $indent);
  }

  function genFieldFillEx($name, mtgType $type, $buf, $check_exist_data, $prefix = '', $tokens = array(), $as_is = false, $postfix = '', $indent = "")
  {
    $str = '';
    $tmp_val = '$tmp_val__';
    $pname = $prefix.$name;

    $cond_indent = $indent."  ";

    $default_value_arg = array_key_exists('default', $tokens) ? $this->genDefault($tokens['default']) : 'null';
    if($type instanceof mtgMetaStruct)
    {
      $default = array_key_exists('default', $tokens) ? $tokens['default'] : null;
      if($default)
      {
        $default = json_decode($default, true);
        if(!is_array($default))
          throw new Exception("Bad default struct: " . $tokens['default']);
        $default = str_replace("\n", "",  var_export($default, true));
        $default_value_arg = "\$assoc ? $default : array_values($default)";
      }
      else
        $default_value_arg = "null";
    }

    $str .= "\n";
    if($check_exist_data)
    {
      $str .= $indent."if(!\$message)\n";
      $str .= $cond_indent."return \$IDX;\n";
    }

    if($as_is)
      $tmp_val = $buf;
    else
      $str .= $indent."{$tmp_val} = mtg_php_array_extract_val({$buf}, \$assoc, '{$name}', {$default_value_arg});\n";

    $str .= $indent."if({$tmp_val} != 0xDEADCDE)\n".$indent."{\n";

    if($type instanceof mtgBuiltinType)
    {
      $str .= $cond_indent."{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
      $str .= $cond_indent."{$pname} = mtg_php_val_{$type}({$tmp_val});\n";
    }
    else if($type instanceof mtgMetaStruct)
    {
      if(array_key_exists('virtual', $tokens))
      {
        $str .= $cond_indent."{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
        $str .= $cond_indent."\$tmp_sub_arr__ = mtg_php_val_arr({$tmp_val});\n";
        $str .= $cond_indent."\$vclass__ = AutogenBundle::getClassName(mtg_php_val_uint32(mtg_php_array_extract_val(\$tmp_sub_arr__, \$assoc, 'vclass__')));\n";
        $str .= $cond_indent."{$pname} = new \$vclass__(\$tmp_sub_arr__, \$assoc);\n";
        $str .= $cond_indent."ASSERT_IS({$pname}, '{$type}');\n";
      }
      else
      {
        $str .= $cond_indent."{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
        $str .= $cond_indent."\$tmp_sub_arr__ = mtg_php_val_arr({$tmp_val});\n";
        $str .= $cond_indent."{$pname} = new {$type}(\$tmp_sub_arr__, \$assoc);\n";
      }
    }
    else if($type instanceof mtgArrType)
    {
      //TODO: maybe filters should be applied to the whole array as well?
      $str .= $cond_indent."\$tmp_arr__ = mtg_php_val_arr({$tmp_val});\n";
      $str .= $cond_indent."foreach(\$tmp_arr__ as \$tmp_arr_item__)\n";
      $str .= $cond_indent."{\n";
      unset($tokens['default']);
      $str .= $this->genFieldFillEx('$tmp__', $type->getValue(), "\$tmp_arr_item__", false, '', $tokens, true, "{$pname}[] = \$tmp__;", $cond_indent."  ") . "\n";
      $str .= $cond_indent."}\n";
    }
    else if($type instanceof mtgMetaEnum)
    {
      $str .= $cond_indent."{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
      $str .= $cond_indent."{$pname} = mtg_php_val_enum('$type', {$tmp_val});\n";
    }
    else
      throw new Exception("Unknown type '{$type}'");

    if($postfix)
      $str .= $cond_indent.$postfix."\n";
    $str .= $indent."}\n";

    return $str;
  }

  function genApplyFieldFilters($field_name, array $tokens, $val)
  {
    $str = $val;
    foreach($tokens as $token => $args_json)
    {
      if(isset($this->filter_aliases[$token]))
        $token = $this->filter_aliases[$token];

      if(strpos($token, 'flt_') === false)
        continue;

      $filter_func = 'mtg_' . $token; 

      if(function_exists($filter_func))
      {
        $args = array();
        if($args_json)
        {
          $args = json_decode($args_json, true);
          if(!is_array($args))
            throw new Exception("Bad filter args: $args_json");
        }
        $str = "\$assoc ? $filter_func($val, '$field_name', \$this, " . str_replace("\n", "",  var_export($args, true)) . ") : $val";
      }
      else
        throw new Exception("No such filter '$filter_func'");
    }

    return $str;
  }

  function genBufFill(mtgMetaField $field, $buf, $prefix, $indent = "")
  {
    return $this->genBufFillEx($field->getName(), $field->getType(), $buf, $prefix, $field->getTokens(), $indent);
  }

  function genBufFillEx($name, mtgType $type, $buf, $prefix = '', $tokens = array(), $indent = "")
  {
    $str = '';
    $pname = $prefix.$name;

    if($type instanceof mtgBuiltinType)
    {
      if($type->isNumeric())
        $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', 1*{$pname});";
      else if($type->isString())
        $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', ''.{$pname});";
      else if($type->isBool())
        $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', (bool){$pname});";
      else
        throw new Exception("Unknown type '$type'");
    }
    else if($type instanceof mtgMetaStruct)
    {
      if(array_key_exists('virtual', $tokens))
        $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', is_array({$pname}) ? {$pname} : {$pname}->export(\$assoc, true/*virtual*/));";
      else
        $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', is_array({$pname}) ? {$pname} : {$pname}->export(\$assoc));";
    }
    else if($type instanceof mtgMetaEnum)
    {
      $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', 1*{$pname});";
    }
    else if($type instanceof mtgArrType)
    {
      $str .= $indent."\$arr_tmp__ = array();\n";
      //NOTE: adding optimization, checking if the first item is array
      $str .= $indent."if(!\$assoc && {$pname} && is_array(current({$pname})))\n";
      $str .= $indent."{\n";
      //$str .= "  mtg_php_debug('BULK:' . get_class(\$this));\n";
      $str .= $indent."  \$arr_tmp__ = {$pname};\n";
      $str .= $indent."}\n";
      $str .= $indent."else\n";
      $str .= $indent."  foreach({$pname} as \$idx__ => \$arr_tmp_item__)\n";
      $str .= $indent."  {\n";
      $str .= $this->genBufFillEx('$arr_tmp_item__', $type->getValue(), "\$arr_tmp__", "", $tokens, $indent."    ") . "\n";
      $str .= $indent."    if(\$assoc)\n";
      $str .= $indent."    {\n";
      $str .= $indent."      \$arr_tmp__[] =  \$arr_tmp__['\$arr_tmp_item__'];\n";
      $str .= $indent."      unset(\$arr_tmp__['\$arr_tmp_item__']);\n";
      $str .= $indent."    }\n";
      $str .= $indent."  }\n";
      $str .= $indent."mtg_php_array_set_value({$buf}, \$assoc, '$name', \$arr_tmp__);\n";
    }
    else
      throw new Exception("Unknown type '{$type}'");

    return $str;
  }

  function genDefault($default)
  {
    if($default == "[]")
      return 'array()';
    if(strlen($default) > 2 && strpos($default, '[') === 0 && strpos($default, ']') === strlen($default)-1)
    {
      $str = trim($default, '][');
      if(strpos($str, "{") !== false )
        $str = var_export(json_decode(trim($default, ']['), true), true);
      return 'array(' . $str . ')';
    }
    return str_replace('$', '\$', $default);
  }

  function genFieldDecl(mtgMetaField $field)
  {
    $type = $field->getType();

    $type_comment = "/** @var " . $type ." */\n";
    if($type instanceof mtgBuiltinType && $type->isString())
      return $type_comment."public \$" . $field->getName() . " = '';";
    else if($type instanceof mtgMetaStruct)
      return $type_comment."public \$" . $field->getName() . " = null;";
    else if($type instanceof mtgArrType)
      return $type_comment."public \$" . $field->getName() . " = array();";
    else
      return $type_comment."public \$" . $field->getName() . ";";
  }
}

////////////////////////////////////////////////////////////////////////

function mtg_php_array_extract_val(&$arr, $assoc, $name, $default = null)
{
  if(!is_array($arr))
    throw new Exception("$name: Not an array");

  if(!$assoc)
  {
    if(sizeof($arr) == 0)
    {
      if($default !== null)
        return $default;

      throw new Exception("$name: No next array item");
    }
    return array_shift($arr);
  }

  if(!isset($arr[$name]))
  {
    if($default !== null)
      return $default;

    throw new Exception("$name: No array item");
  }

  $val = $arr[$name];
  unset($arr[$name]);
  return $val;
}

function mtg_php_array_set_value(&$arr, $assoc, $name, $value)
{
  if($assoc)
    $arr[$name] = $value;
  else
    $arr[] = $value;
}

function mtg_php_val_string($val)
{
  //special case for empty strings
  if(is_bool($val) && $val === false)
    return '';
  if(!is_string($val))
    throw new Exception("Bad item, not a string(" . serialize($val) . ")");
  return $val;
}

function mtg_php_val_bool($val)
{
  if(!is_bool($val))
    throw new Exception("Bad item, not a bool(" . serialize($val) . ")");
  return $val;
}

function mtg_php_val_float($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  return $val;
}

function mtg_php_val_double($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  return 1*$val;
}

function mtg_php_val_uint64($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if(is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_int64($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if(is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_uint32($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if(($val < 0 && $val < -2147483648) || ($val > 0 && $val > 0xFFFFFFFF) || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_int32($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if($val > 2147483647 || $val < -2147483648 || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_uint16($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if($val > 0xFFFF || $val < 0 || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_int16($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if($val > 32767 || $val < -32768 || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_uint8($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if($val > 0xFF || $val < 0 || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_int8($val)
{
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  $val = 1*$val;
  if($val > 127 || $val < -128 || is_float($val))
    throw new Exception("Value not in range: $val");
  return $val;
}

function mtg_php_val_arr($val)
{
  if(!is_array($val))
    throw new Exception("Bad item, not an array(" . serialize($val) . ")");
  return $val;
}

function mtg_php_val_enum($enum, $val)
{
  if(is_string($val))
    return call_user_func_array(array($enum, "getValueByName"), array($val));

  if(!is_numeric($val))
    throw new Exception("Bad enum value, not a numeric or string(" . serialize($val) . ")");

  call_user_func_array(array($enum, "checkValidity"), array($val));
  return $val;
}

