<?php

require_once(dirname(__FILE__) . '/../../metagen.inc.php');
require_once(dirname(__FILE__) . '/php_tpl.inc.php');

class mtgPHPCodegen extends mtgCodegen
{
  protected $filter_aliases = array();

  function __construct(array $filter_aliases)
  {
    $this->filter_aliases = $filter_aliases;
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

  function genRPC(mtgMetaPacket $packet)
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

    $repl['%class_props%'] = str_replace("\n", "", var_export($struct->getTokens(), true));

    foreach(mtg_extract_deps($struct) as $dep)
      $repl['%includes%'] .= "require_once(dirname(__FILE__) . '/$dep.class.php');\n";

    if($parent)
    {
      $repl['%fields_init%'] .= "parent::__construct();\n";
      $repl['%fill_fields%'] .= "\$IDX = parent::import(\$message, \$assoc, false);\n";
      $repl['%fill_buffer%'] .= "parent::fill(\$message, \$assoc, false);\n";
    }

    foreach($struct->getFields() as $field)
    {
      $repl['%fields_names%'] .= "'" . $field->getName() . "',";
      $repl['%fields_props%'] .= "'" . $field->getName() . "' => " . str_replace("\n", "", var_export($field->getTokens(), true)) . ",";
      $field_type = $field->getType(); 
      $repl['%fields_types%'] .= "'" . $field->getName() . "' => array('" . $field->getSuperType() . "', '" . (is_array($field_type) ?  ($field_type[0] . "', '" . $field_type[1]) : $field_type) . "'),";

      $repl['%fields_init%'] .= $this->genFieldInit($field, '$this->') . "\n";
      $repl['%fill_fields%'] .= $this->genFieldFill($field, '$message', '$this->') . "++\$IDX;\n";
      $repl['%fill_buffer%'] .= $this->genBufFill($field, '$message', '$this->') . "\n";
      $repl['%fields%'] .= $this->genFieldDecl($field) . "\n";
    }

    $repl['%fields_names%'] .= ')' . ($parent ? ')' : '');
    $repl['%fields_props%'] .= ')' . ($parent ? ')' : '');
    $repl['%fields_types%'] .= ')' . ($parent ? ')' : '');

    $this->undoTemp();
  }

  function genFieldInit(mtgMetaField $field, $prefix="")
  {
    $tokens = $field->getTokens();

    if($field->getSuperType() == "struct")
      return $prefix . $field->getName() . " = new " . $field->getType() . "();";
    
    if($field->getSuperType() == "enum")
    {
      if(array_key_exists('default', $tokens))
        $default = $this->genDefault($tokens['default']);
      else
        $default = 'DEFAULT_VALUE';

      $default = str_replace('"', '', $default);
      return $prefix . $field->getName() . " = " . $field->getType() . "::$default;";
    }
    else if($field->getSuperType() == "array")
      return $prefix . $field->getName() . " = array();";
    else
    {
      if($field->getType() == "string")
        $init_value = "''";
      else
        $init_value = "0";

      if(array_key_exists('default', $tokens))
        $init_value = $this->genDefault($tokens['default']);

      return $prefix . $field->getName() . " = $init_value;";
    }
  }

  function genFieldFill(mtgMetaField $field, $buf, $prefix)
  {
    return $this->genFieldFillEx($field->getName(), $field->getSuperType(), $field->getType(), $buf, $prefix, $field->getTokens());
  }

  function genFieldFillEx($name, $super_type, $type, $buf, $prefix = '', $tokens = array(), $as_is = false, $postfix = '')
  {
    $str = '';
    $tmp_val = '$tmp_val__';
    $pname = $prefix.$name;

    $default_value_arg = array_key_exists('default', $tokens) ? $this->genDefault($tokens['default']) : 'null';
    if($super_type == "struct")
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

    if($as_is)
      $tmp_val = $buf;
    else
      $str .= "{$tmp_val} = mgt_php_array_extract_val({$buf}, \$assoc, '{$name}', {$default_value_arg});\n";

    $str .= "if({$tmp_val} != 0xDEADCDE)\n{\n";

    if($super_type == "scalar")
    {
      $str .= "{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
      $str .= "{$pname} = mgt_php_val_num({$tmp_val});\n";
    }
    else if($super_type == "string")
    {
      $str .= "{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
      $str .= "{$pname} = mgt_php_val_str({$tmp_val});\n";
    }
    else if($super_type == "struct")
    {
      if(array_key_exists('virtual', $tokens))
      {
        $str .= "{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
        $str .= "\$tmp_sub_arr__ = mgt_php_val_arr({$tmp_val});\n";
        $str .= "\$vclass__ = AutogenBundle::getClassName(mgt_php_val_num(mgt_php_array_extract_val(\$tmp_sub_arr__, \$assoc, 'vclass__')));\n";
        $str .= "{$pname} = new \$vclass__(\$tmp_sub_arr__, \$assoc);\n";
        $str .= "ASSERT_IS({$pname}, '{$type}');\n";
      }
      else
      {
        $str .= "{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
        $str .= "\$tmp_sub_arr__ = mgt_php_val_arr({$tmp_val});\n";
        $str .= "{$pname} = new {$type}(\$tmp_sub_arr__, \$assoc);\n";
      }
    }
    else if($super_type == "array")
    {
      //TODO: maybe filters should be applied to the whole array as well?
      $str .= "\$tmp_arr__ = mgt_php_val_arr({$tmp_val});\n";
      $str .= "foreach(\$tmp_arr__ as \$tmp_arr_item__)\n";
      $str .= "{\n";
      unset($tokens['default']);
      $str .= $this->genFieldFillEx('$tmp__', $type[0], $type[1], "\$tmp_arr_item__", '', $tokens, true, "{$pname}[] = \$tmp__;\n") . "\n";
      $str .= "}\n";
    }
    else if($super_type == "enum")
    {
      $str .= "{$tmp_val} = " . $this->genApplyFieldFilters($name, $tokens, "{$tmp_val}"). ";\n";
      $str .= "{$pname} = mgt_php_val_enum('$type', {$tmp_val});\n";
    }
    else
      throw new Exception("Unknown type {$super_type}");

    $str .= $postfix;
    $str .= "}\n";

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

  function genBufFill(mtgMetaField $field, $buf, $prefix)
  {
    return $this->genBufFillEx($field->getName(), $field->getSuperType(), $field->getType(), $buf, $prefix, $field->getTokens());
  }

  function genBufFillEx($name, $super_type, $type, $buf, $prefix = '', $tokens = array())
  {
    $str = '';
    $pname = $prefix.$name;

    if($super_type == "scalar")
    {
      $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', 1*{$pname});";
    }
    else if($super_type == "string")
    {
      $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', ''.{$pname});";
    }
    else if($super_type == "struct")
    {
      if(array_key_exists('virtual', $tokens))
        $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', is_array({$pname}) ? {$pname} : {$pname}->export(\$assoc, true/*virtual*/));";
      else
        $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', is_array({$pname}) ? {$pname} : {$pname}->export(\$assoc));";
    }
    else if($super_type == "array")
    {
      $str .= "\$arr_tmp__ = array();\n";
      //NOTE: adding optimization, checking if the first item is array
      $str .= "if(!\$assoc && {$pname} && is_array(current({$pname})))\n";
      $str .= "{\n";
      //$str .= "  mgt_php_debug('BULK:' . get_class(\$this));\n";
      $str .= "  \$arr_tmp__ = {$pname};\n";
      $str .= "}\n";
      $str .= "else\n";
      $str .= "  foreach({$pname} as \$idx__ => \$arr_tmp_item__)\n";
      $str .= "  {\n";
      $str .= "  " . $this->genBufFillEx('$arr_tmp_item__', $type[0], $type[1], "\$arr_tmp__", "", $tokens) . "\n";
      $str .= "    if(\$assoc) {\n";
      $str .= "      \$arr_tmp__[] =  \$arr_tmp__['\$arr_tmp_item__'];\n";
      $str .= "      unset(\$arr_tmp__['\$arr_tmp_item__']);\n";
      $str .= "    }\n";
      $str .= "  }\n";
      $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', \$arr_tmp__);\n";
    }
    else if($super_type == "enum")
    {
      $str .= "mgt_php_array_set_value({$buf}, \$assoc, '$name', 1*{$pname});";
    }
    else
      throw new Exception("Unknown type {$super_type}");

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
    $type_comment = "/** @var " . $this->genNativeType($field) ." */\n";
    if($field->getSuperType() == "string")
      return $type_comment."public \$" . $field->getName() . " = '';";
    else if($field->getSuperType() == "struct")
      return $type_comment."public \$" . $field->getName() . " = null;";
    else if($field->getSuperType() == "array")
      return $type_comment."public \$" . $field->getName() . " = array();";
    else
      return $type_comment."public \$" . $field->getName() . ";";
  }

  function genNativeType(mtgMetaField $field)
  {
    return $this->genNativeTypeEx($field->getSuperType(), $field->getType());
  }

  function genNativeTypeEx($super_type, $type)
  {
    switch($super_type)
    {
      case "scalar":
        return $this->genType($type);
      case "string":
        return "string";
      case "array":
        return $this->genType($type[1]) . "[]";
      case "enum":
        return $type;
      case "struct":
        return $type;
      default:
        throw new Exception("Unknown super type '{$super_type}'");
    }
  }

  function genType($type)
  {
    if(in_array($type, mtgMetaInfo::$SCALAR_TYPES))
    {
      if($type == "float")
        return "float";
      else if($type == "double")
        return "double";
      else
        return "int";
    }
    if($type == mtgMetaInfo::$STRING_TYPE)
      return "string";
    //struct?
    return $type;
  }
}

////////////////////////////////////////////////////////////////////////

function mgt_php_array_extract_val(&$arr, $assoc, $name, $default = null)
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

function mgt_php_array_set_value(&$arr, $assoc, $name, $value)
{
  if($assoc)
    $arr[$name] = $value;
  else
    $arr[] = $value;
}

function mgt_php_val_str($val)
{
  //special case for arrays (potentionally eval values)
  if(is_array($val))
    return $val;
  //special case for empty strings
  if(is_bool($val) && $val === false)
    return '';
  if(!is_string($val))
    throw new Exception("Bad item, not a string(" . serialize($val) . ")");
  return $val;
}

function mgt_php_val_num($val)
{
  //special case for arrays (potentially eval values)
  if(is_array($val))
    return $val;
  if(!is_numeric($val))
    throw new Exception("Bad item, not a number(" . serialize($val) . ")");
  return 1*$val;
}

function mgt_php_val_arr($val)
{
  if(!is_array($val))
    throw new Exception("Bad item, not an array(" . serialize($val) . ")");
  return $val;
}

function mgt_php_val_enum($enum, $val)
{
  if(is_string($val))
    return call_user_func_array(array($enum, "getValueByName"), array($val));

  if(!is_numeric($val))
    throw new Exception("Bad enum value, not a numeric or string(" . serialize($val) . ")");

  call_user_func_array(array($enum, "checkValidity"), array($val));
  return $val;
}

