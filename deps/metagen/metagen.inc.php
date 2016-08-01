<?php

define('METAGEN_LIB', dirname(__FILE__));

$GLOBALS['METAGEN_CONFIG'] = array();

/**
* -e
* -e <value>
* --long-param
* --long-param=<value>
* --long-param <value>
* <value>
*/
function mtg_parse_argv($params, $noopt = array()) 
{
  $result = array();
  reset($params);
  while (list($tmp, $p) = each($params)) 
  {
    if($p{0} == '-') 
    {
      $pname = substr($p, 1);
      $value = true;
      if($pname{0} == '-') 
      {
        // long-opt (--<param>)
        $pname = substr($pname, 1);
        if(strpos($p, '=') !== false) 
        {
          // value specified inline (--<param>=<value>)
          list($pname, $value) = explode('=', substr($p, 2), 2);
        }
      }
      // check if next parameter is a descriptor or a value
      $nextparm = current($params);
      if(!in_array($pname, $noopt) && $value === true && $nextparm !== false && $nextparm{0} != '-') 
        list($tmp, $value) = each($params);
      $result[$pname] = $value;
    } 
    else
      // param doesn't belong to any option
      $result[] = $p;
  }
  //var_dump($result);
  return $result;
}

function mtg_read_from_stdin($timeout_sec = 5)
{
  $read   = array(STDIN);
  $write  = NULL;
  $except = NULL;
  $stdin = '';
  if(false === ($num_changed_streams = stream_select($read, $write, $except, $timeout_sec))) 
    throw new Exception("Unknown stream select error happened");
  elseif ($num_changed_streams > 0) 
    $stdin = stream_get_contents(STDIN);
  else
    throw new Exception("No data in stdin");
  return $stdin;
}

function mtg_extract_files($string)
{
  return mtg_split_and_process($string, '~\s+~', 'realpath');
}

function mtg_split_and_process($string, $regex = '~\s+~', $proc = '')
{
  $items = preg_split($regex, trim($string));
  $result = array();
  foreach($items as $item)
  {
    $item = trim($item);
    if(!$item)
      continue;

    if($proc)
      $item = $proc($item);

    $result[] = $item;
  }
  return $result;
}

function mtg_argv2conf($argv)
{
  global $METAGEN_CONFIG;

  $parsed = mtg_parse_argv($argv);
  foreach($parsed as $key => $value)
  {
    if(!is_string($key))
      continue;

    $METAGEN_CONFIG[preg_replace('~^-+~', '', $key)] = $value;
  }
}

function mtg_conf($key)
{
  global $METAGEN_CONFIG;

  if(!isset($METAGEN_CONFIG[$key]))
  {
    if(func_num_args() == 1)
      throw new Exception("Config option '$key' is not set");
    else //default value
      return func_get_arg(1);
  }

  return $METAGEN_CONFIG[$key];
}

function mtg_conf_set($key, $val)
{
  global $METAGEN_CONFIG;
  $METAGEN_CONFIG[$key] = $val;
}


class mtgMetaInfoUnit
{
  public $file;
  public $object;

  function __construct($f, mtgMetaStruct $o)
  {
    $this->file = $f;
    $this->object = $o;
  }
}

class mtgMetaInfo
{
  public static $SCALAR_TYPES = array('int8', 'int16', 'int32', 'uint8', 'uint16', 'uint32', 'float', 'double', 'uint64');
  public static $STRING_TYPE = 'string';

  private $units = array();
  private $rpcs = array();

  function addUnit(mtgMetaInfoUnit $unit)
  {
    if(isset($this->units[$unit->object->getName()]))
      throw new Exception("Meta info unit '{$unit->object->getName()}' already defined in file '{$this->units[$unit->object->getName()]->file}'");

    $this->units[$unit->object->getName()] = $unit;
  }

  function getUnits()
  {
    return $this->units;
  }

  function findUnit($name, $strict = true)
  {
    if(isset($this->units[$name]))
      return $this->units[$name];
    else if($strict)
      throw new Exception("Unable find unit '$name'. Check that this type is defined.");
  }

  function addRPC(mtgMetaRPC $rpc)
  {
    if(isset($this->rpcs[$rpc->getCode()]))
      throw new Exception("Duplicating RPC id {$rpc->getCode()})");
    $this->rpcs[$rpc->getCode()] = $rpc;
  }

  function getRPCs()
  {
    return $this->rpcs;
  }
}

class mtgMetaStruct
{
  private $id;
  private $name;
  private $fields = array();
  private $parent = null;
  private $tokens = array();

  function __construct($name, $fields = array(), $parent = null, $tokens = array())
  {
    $this->name = $name;
    $this->setFields($fields);
    $this->parent = $parent;
    $this->tokens = $tokens;
  }

  function getName()
  {
    return $this->name;
  }

  function getClassId()
  {
    //NOTE: using crc28 actually, making sure that crc32 is always an unsigned value,
    return crc32($this->name) & 0xFFFFFFF;
  }

  function getParent()
  {
    return $this->parent;
  }

  function getFields()
  {
    return $this->fields;
  }

  function setFields(array $fields)
  {
    foreach($fields as $field)
      $this->addField($field);
  }

  function addField(mtgMetaField $field)
  {
    if($this->hasField($field->getName()))
      throw new Exception("Struct '{$this->name}' already has field '{$field->getName()}'");

    $this->fields[$field->getName()] = $field;
  }

  function delField(mtgMetaField $field)
  {
    if(isset($this->fields[$field->getName()]))
      unset($this->fields[$field->getName()]);
  }

  function hasField($name)
  {
    return isset($this->fields[$name]);
  }

  function getField($name)
  {
    if(!isset($this->fields[$name]))
      throw new Exception("No such field '$name'");
    return $this->fields[$name];
  }

  function findFieldOwner($name, mtgMetaInfo $meta)
  {
    $tmp = $this;
    while($tmp)
    {
      $fields = $tmp->getFields();
      if(isset($fields[$name]))
        return $tmp;
      $tmp = $tmp->getParent() ? $meta->findUnit($tmp->getParent())->object : null;
    }
    return null;
  }

  function getTokens()
  {
    return $this->tokens;
  }

  function setTokens(array $tokens)
  {
    $this->tokens = $tokens;
  }

  function setToken($name, $val)
  {
    $this->tokens[$name] = $val;
  }

  function getToken($name)
  {
    return $this->hasToken($name) ? $this->tokens[$name] : null; 
  }

  function hasToken($name)
  {
    return array_key_exists($name, $this->tokens);
  }
}

class mtgMetaRPC
{
  public $name;
  public $code;
  public $in;
  public $out;

  function __construct($name, $code, mtgMetaStruct $in, mtgMetaStruct $out)
  {
    $this->name = $name;
    $this->code = $code;
    $this->in = $in;
    $this->out = $out;
  }

  function getName()
  {
    return $this->name;
  }

  function getCode()
  {
    return $this->code;
  }

  function getNumericCode()
  {
    return (int)$this->code;
  }
}

class mtgMetaField
{
  private $name;
  //can be: scalar, string, struct, enum, array
  private $super_type;
  private $type;
  private $tokens = array();

  function __construct($name, $super_type, $type)
  {
    $this->name = $name;
    $this->super_type = $super_type;
    $this->type = $type;
  }

  function setName($name)
  {
    $this->name = $name;
  }

  function getName()
  {
    return $this->name;
  }

  function getSuperType()
  {
    return $this->super_type;
  }

  function getType()
  {
    return $this->type;
  }

  function getTokens()
  {
    return $this->tokens;
  }

  function setTokens(array $tokens)
  {
    $this->tokens = $tokens;
  }

  function setToken($name, $val)
  {
    $this->tokens[$name] = $val;
  }

  function hasToken($name)
  {
    return array_key_exists($name, $this->tokens);
  }
}

class mtgMetaArrayField extends mtgMetaField
{
  private $size;

  //empty size means autosize
  function __construct($name, array $type, $size = '')
  {
    if(!is_array($type) || sizeof($type) < 2)
      throw new Exception("Invalid array type '$type'");

    $this->size = $size;
    parent::__construct($name, 'array', $type);
  }

  //for BC
  function getArrayType()
  {
    return $this->getType();
  }

  function getSize()
  {
    return $this->size;
  }

  function isAutoSize()
  {
    return $this->size === '';
  }
}

class mtgMetaStructField extends mtgMetaField
{
  function __construct($name, $type)
  {
    parent::__construct($name, 'struct', $type);
  }

  //for BC
  function getStruct()
  {
    return $this->getType();
  }
}

class mtgMetaEnumField extends mtgMetaField
{
  function __construct($name, $enum_type)
  {
    parent::__construct($name, 'enum', $enum_type);
  }

  //for BC
  function getStruct()
  {
    return $this->getType();
  }

  function getEnum()
  {
    return $this->getType();
  }
} 


class mtgMetaScalarField extends mtgMetaField
{
  function __construct($name, $scalar_type)
  {
    parent::__construct($name, 'scalar', $scalar_type);
  }

  //for BC
  function getScalarType()
  {
    return $this->getType();
  }
}

class mtgMetaStringField extends mtgMetaField
{
  function __construct($name)
  {
    parent::__construct($name, 'string', 'string');
  }
}

class mtgMetaInfoParser
{
  private $config = array();
  private $current_meta;
  private $parsed_files = array();
  private $file_stack = array();
  private $file = "";
  private $source = "";
  private $cursor = 0;
  private $line = 0;
  private $token = "";
  private $attribute = "";
  private $idltypes = array();
  private $token_strs = array();

  const T_EOF             = 1001;
  const T_StringConstant  = 1002;
  const T_IntegerConstant = 1003;
  const T_FloatConstant   = 1004;
  const T_Enum            = 1005;
  const T_RPC             = 1006;
  const T_End             = 1007;
  const T_Identifier      = 1008;
  const T_Struct          = 1009;
  const T_Prop            = 1010;
  const T_Extends         = 1011;
  const T_string          = 1020;
  const T_uint32          = 1021;
  const T_int32           = 1022;
  const T_uint16          = 1023;
  const T_int16           = 1024;
  const T_uint8           = 1025;
  const T_int8            = 1026;
  const T_float           = 1027;
  const T_uint64          = 1028;

  function __construct($config = array())
  {
    $this->config = $config;
    if(!isset($this->config['include_path']))
      $this->config['include_path'] = array('.');

    $this->idltypes = array(
      "string" => self::T_string,
      "uint32" => self::T_uint32,
      "int32" => self::T_int32,
      "uint16" => self::T_uint16,
      "int16" => self::T_int16,
      "uint8" => self::T_uint8,
      "int8" => self::T_int8,
      "float" => self::T_float,
      "double" => self::T_float,
      "uint64" => self::T_uint64,
    );
    $this->token_strs = array_flip($this->idltypes);
    $this->token_strs[self::T_EOF] = '<EOF>';
    $this->token_strs[self::T_StringConstant] = '<StringConstant>';
    $this->token_strs[self::T_IntegerConstant] = '<IntegerConstant>';
    $this->token_strs[self::T_FloatConstant] = '<FloatConstant>';
    $this->token_strs[self::T_Enum] = '<enum>';
    $this->token_strs[self::T_RPC] = '<RPC>';
    $this->token_strs[self::T_End] = '<end>';
    $this->token_strs[self::T_Identifier] = '<Identifier>';
    $this->token_strs[self::T_Struct] = '<struct>';
    $this->token_strs[self::T_Prop] = '<@prop>';
    $this->token_strs[self::T_Extends] = '<extends>';
  }

  function parse(mtgMetaInfo $meta, $raw_file)
  {
    $this->current_meta = $meta;

    $file = realpath($raw_file);
    if($file === false)
      throw new Exception("No such file '$raw_file'");

    $this->_parse($file);
  }

  private function _parse($file)
  {
    if(isset($this->parsed_files[$file]))
      return;
    $this->parsed_files[$file] = true;
    $this->file_stack[] = $file;
    $source = file_get_contents($file);
    $is_php = false;

    try
    {
      if($source === false)
        throw new Exception("Could not read file '$file'");

      //PHP include
      if(strpos($source, '<?php') === 0)
      {
        include_once($file);
        $is_php = true;
      }
      else
      {
        self::resolveIncludes($source, $this->config['include_path'], array($this, '_parse'));
      }
    }
    catch(Exception $e)
    {
      throw new Exception(end($this->file_stack) . " : " . $e->getMessage());
    }

    array_pop($this->file_stack);

    if($is_php)
      return;

    $this->file = $file;
    $this->source = $source;
    $this->cursor = 0;
    $this->line = 1;

    try
    {
      $this->_next();
      while($this->token != self::T_EOF)
      {
        //echo "TOKEN : " . $this->token . " " . $this->attribute . " " . $this->line . "\n";

        if($this->token == self::T_Enum)
          $this->_parseEnum();
        else if($this->token == self::T_Struct)
          $this->_parseStruct();
        else if($this->token == self::T_RPC)
          $this->_parseRPC();
        else
          $this->_error("unexpected token ('" . $this->_toStr($this->token) . "' " . $this->attribute . ")");
      }
    }
    catch(Exception $e)
    {
      throw new Exception("$file line {$this->line} : " .  $e->getMessage() . " " . $e->getTraceAsString());
    }
  }

  static function resolveIncludes(&$text, array $include_paths, $callback)
  {
    $result = array();
    $lines = explode("\n", $text);
    foreach($lines as $line)
    {
      if(preg_match('~#include\s+(\S+)~', $line, $m))
      {
        self::processInclude($m[1], $include_paths, $callback);
        $result[] = "";
      }
      else
        $result[] = $line;
    }
    $text = implode("\n", $result);
  }

  static function processInclude($include, array $include_paths, $callback)
  {
    $file = false;
    foreach($include_paths as $include_path)
    {
      $file = realpath($include_path . "/" . $include);
      if($file !== false)
        break;
    }

    if($file === false)
      throw new Exception("#include {$include} can't be resolved(include path is '". implode(':', $include_paths) . "')");

    call_user_func_array($callback, array($file));
  }

  private function _parseEnumOrValues()
  {
    $values = array();
    while(true)
    {
      if($this->token == self::T_Identifier)          
      {
        $values[] = $this->attribute;
        $this->_next();
        if($this->token != ord('|'))
          break;
        else
          $this->_next();
      }
      else
        break;
    }

    return $values;
  }

  private function _parseEnum()
  {
    $this->_next();

    $name = $this->_checkThenNext(self::T_Identifier);

    $enum = new mtgMetaEnum($name);
    $this->current_meta->addUnit(new mtgMetaInfoUnit($this->file, $enum));
    $or_values = array();
    while(true)
    {
      if($this->_nextIf(self::T_End))
        break;
      $key = $this->_checkThenNext(self::T_Identifier);
      $this->_checkThenNext('=');
      if($this->token == self::T_Identifier)
      {
        $or_values[$key] = $this->_parseEnumOrValues();
      }
      else
      {
        $value = $this->_checkThenNext(self::T_IntegerConstant);
        $enum->addValue($key, $value);
      }
    }
    $enum->addOrValues($or_values);
  }

  private function _parseFields($end_token)
  {
    if(is_string($end_token))
      $end_token = ord($end_token);

    $flds = array();

    while(true)
    {
      if($this->_nextIf($end_token))
        break;

      if($this->token == self::T_Identifier)
      {
        $name = $this->attribute;
        $this->_next();
        $this->_checkThenNext(':');

        if($this->token == self::T_Identifier || 
          ($this->token >= self::T_string && $this->token <= self::T_uint64))
        {
          $type = $this->attribute;

          $fld = null;
          if($this->token == self::T_Identifier)
          {
            if($this->current_meta->findUnit($type)->object instanceof mtgMetaEnum)
              $fld = new mtgMetaEnumField($name, $type);
            else
              $fld = new mtgMetaStructField($name, $type);
          }
          else if($this->token == self::T_string)
            $fld = new mtgMetaStringField($name);
          else
            $fld = new mtgMetaScalarField($name, $type);

          $this->_next();

          if($this->token == ord('['))
          {
            $this->_next();
            $this->_checkThenNext(']');
            $fld = new mtgMetaArrayField($name, array($fld->getSuperType(), $type));
          }

          if($this->token == self::T_Prop)
          {
            $props = $this->_parseProps($end_token);
            $fld->setTokens($props);
          }

          $flds[] = $fld;
        }
        else
          $this->_error("type expected");
      }
      else
      {
        $this->_error("unexpected fields token");
      }
    }

    return $flds;
  }

  private function _parseStruct()
  {
    $this->_next();
    $name = $this->_checkThenNext(self::T_Identifier);

    $parent = null;
    if($this->token == self::T_Extends)
    {
      $this->_next();
      $parent = $this->_checkThenNext(self::T_Identifier);
    }

    $s = new mtgMetaStruct($name, array(), $parent);
    $this->current_meta->addUnit(new mtgMetaInfoUnit($this->file, $s));

    if($this->token == self::T_Prop)
    {
      $props = $this->_parseProps(self::T_End);
      $s->setTokens($props);
    }

    $flds = $this->_parseFields(self::T_End);
    foreach($flds as $fld)
      $s->addField($fld);
  }

  private function _parseRPC()
  {
    $this->_next();
    $code = $this->_checkThenNext(self::T_IntegerConstant);
    $name = $this->_checkThenNext(self::T_Identifier);
    $this->_checkThenNext('(');
    $req_fields = $this->_parseFields(')');
    $rsp_fields = $this->_parseFields(self::T_End);

    $req = new mtgMetaPacket($code, "RPC_REQ_$name");
    $req->setFields($req_fields);
    $rsp = new mtgMetaPacket($code, "RPC_RSP_$name");
    $rsp->setFields($rsp_fields);

    $rpc = new mtgMetaRPC("RPC_$name", $code, $req, $rsp);
    $this->current_meta->addRPC($rpc);
    $this->current_meta->addUnit(new mtgMetaInfoUnit($this->file, $rpc->in));
    $this->current_meta->addUnit(new mtgMetaInfoUnit($this->file, $rpc->out));
  }

  private function _parseProps($end_token)
  {
    $props = array();

    while(true)
    {
      if($this->token != self::T_Prop)
        break;

      $name = ltrim($this->attribute, '@');
      $this->_next();

      $value = null;
      if($this->token == ord(':'))
      {
        while(true)
        {
          $this->_next();
          if(($value !== null && $this->token == self::T_Identifier) || $this->token == $end_token || $this->token == self::T_Prop)
            break;

          $tmp = $this->attribute; 
          if($this->token == self::T_StringConstant)
            $tmp = "\"$tmp\"";
          if($value === null)
            $value = '';
          $value .= $tmp;
        }
      }
      if($value && substr($value, 0, 1) == '{')
      {
        $json = json_decode($value);
        if($json === null)
        {
          --$this->line; //hack for more precise reporting
          $this->_error("bad json");
        }
      } 
      $props[$name] = $value;
    }
    return $props;
  }

  private function _symbol()
  {
    return substr($this->source, $this->cursor, 1);
  }

  private function _next()
  {
    $seen_newline = false;

    while(true)
    {
      $c = $this->_symbol();
      //NOTE: dealing with PHP's types juggling
      if($c === false || $c === '')
        $c = -1;

      $this->token = ord($c);
      ++$this->cursor;
      $this->attribute = $c;

      switch($c)
      {
        case -1: $this->cursor--; $this->token = self::T_EOF; return;
        case ' ': case "\r": case "\t": break;
        case "\n": $this->line++; $seen_newline= true; break;
        case '{': case '}': case '(': case ')': case '[': case ']': case '|': return;
        case ',': case ':': case ';': case '=': return;
        case '.':
          if(!ctype_digit($this->_symbol())) return;
          $this->_error("floating point constant can't start with .");
          break;
        case '"':
          $this->attribute = "";
          while($this->_symbol() != '"') 
          {
            if(ord($this->_symbol()) < ord(' ') && ord($this->_symbol()) >= 0)
              $this->_error("illegal character in string constant");
            if($this->_symbol() == '\\') 
            {
              $this->cursor++;
              switch($this->_symbol()) 
              {
                case 'n':  $this->attribute .= "\n"; $this->cursor++; break;
                case 't':  $this->attribute .= "\t"; $this->cursor++; break;
                case 'r':  $this->attribute .= "\r"; $this->cursor++; break;
                case '"': $this->attribute .= '"'; $this->cursor++; break;
                case '\\': $this->attribute .= '\\'; $this->cursor++; break;
                default: $this->_error("unknown escape code in string constant"); break;
              }
            }
            else // printable chars + UTF-8 bytes
            {
              $this->attribute .= $this->_symbol();
              $this->cursor++;
            }
          }
          $this->token = self::T_StringConstant;
          $this->cursor++;
          return;

        case '/':
          if($this->_symbol() == '/') 
          {
            $this->cursor++;
            while($this->_symbol() !== false && $this->_symbol() != "\n") $this->cursor++;
            break;
          }

        case '#':
          while($this->_symbol() !== false && $this->_symbol() != "\n") $this->cursor++;
          break;

        case '@':
          $start = $this->cursor - 1;
          while(ctype_alnum($this->_symbol()) || $this->_symbol() == '_')
            $this->cursor++;
          $this->token = self::T_Prop;
          $this->attribute = substr($this->source, $start, $this->cursor - $start);
          return;

        //fall thru
        default:

          if(ctype_alpha($c))
          {
            //collect all chars of an identifier
            $start = $this->cursor - 1;
            while(ctype_alnum($this->_symbol()) || $this->_symbol() == '_')
              $this->cursor++;
            $this->attribute = substr($this->source, $start, $this->cursor - $start);

            if(isset($this->idltypes[$this->attribute]))
            {
              $this->token = $this->idltypes[$this->attribute];
              return;
            }

            if($this->attribute == "true" || $this->attribute == "false") 
            {
              $this->attribute = ($this->attribute == "true") ? "1" : "0";
              $this->token = self::T_IntegerConstant;
              return;
            }

            //check for declaration keywords:
            if($this->attribute == "struct")  { $this->token = self::T_Struct; return; }
            if($this->attribute == "enum")    { $this->token = self::T_Enum;   return; }
            if($this->attribute == "RPC")     { $this->token = self::T_RPC;    return; }
            if($this->attribute == "end")     { $this->token = self::T_End;    return; }
            if($this->attribute == "extends") { $this->token = self::T_Extends;    return; }

            //if not it's a user defined identifier
            $this->token = self::T_Identifier;
            return;
          }
          else if(ctype_digit($c) || $c == '-')
          {
            $start = $this->cursor - 1;
            while(ctype_digit($this->_symbol())) $this->cursor++;
            if($this->_symbol() == '.')
            {
              $this->cursor++;
              while(ctype_digit($this->_symbol())) $this->cursor++;
              // see if this float has a scientific notation suffix. Both JSON
              // and C++ (through strtod() we use) have the same format:
              if($this->_symbol() == 'e' || $this->_symbol() == 'E') 
              {
                $this->cursor++;
                if($this->_symbol() == '+' || $this->_symbol() == '-') $this->cursor++;
                while(ctype_digit($this->_symbol())) $this->cursor++;
              }
              $this->token = self::T_FloatConstant;
            }
            else
              $this->token = self::T_IntegerConstant;
            $this->attribute = substr($this->source, $start, $this->cursor - $start);
            return;
          }

          $this->_error("illegal character '$c'");
      }
    }
  }

  private function _nextIf($t)
  {
    if(is_string($t))
      $t = ord($t);
    $yes = $t === $this->token;
    if($yes)
      $this->_next();
    return $yes;
  }

  private function _checkThenNext($t)
  {
    if(is_string($t))
      $t = ord($t);
    if($t !== $this->token)
    {
      $this->_error("Expecting '" . $this->_toStr($t) . "' instead got '" . $this->_toStr($this->token) . "'");
    }

    $attr = $this->attribute;
    $this->_next();
    return $attr;
  }

  private function _toStr($t)
  {
    if($t < 1000)
      return chr($t);
    return $this->token_strs[$t];
  }

  private function _error($msg)
  {
    throw new Exception($msg);
  }
}

function mtg_is_complex($type)
{
  return (!in_array($type, mtgMetaInfo::$SCALAR_TYPES) && $type != mtgMetaInfo::$STRING_TYPE);
}

function mtg_extract_deps(mtgMetaStruct $struct)
{
  $deps = array();
  $parent = $struct->getParent();
  if($parent)
    $deps[] = $parent;
  foreach($struct->getFields() as $field)
  {
    if($field->getSuperType() == "struct")
      $deps[] = $field->getType();
    else if($field->getSuperType() == "enum")
      $deps[] = $field->getType();
    else if($field->getSuperType() == "array")
    {
      $type = $field->getType();
      $sub = end($type);
      if(mtg_is_complex($sub))
        $deps[] = $sub;
    }
  }
  return array_unique($deps);
}

function mtg_get_file_deps(mtgMetaInfo $info, mtgMetaInfoUnit $unit)
{
  $deps = array();
  $deps[] = $unit->file;

  if($unit->object instanceof mtgMetaStruct)
  {
    foreach($unit->object->getFields() as $field)
    {
      if($field->getSuperType() == "struct")
        $deps[] = $info->findUnit($field->getType())->file;
      else if($field->getSuperType() == "enum")
        $deps[] = $info->findUnit($field->getType())->file;
      else if($field->getSuperType() == "array")
      {
        $type = $field->getType();
        $sub = end($type);
        if(mtg_is_complex($sub))
          $deps[] = $info->findUnit($sub)->file;
      }
    }

    $parent = $unit->object->getParent();
    if($parent)
      $deps = array_merge($deps, mtg_get_file_deps($info, $info->findUnit($parent)));
  }
  return $deps;
}

function mtg_get_all_fields(mtgMetaInfo $info, mtgMetaStruct $struct)
{
  $fields = $struct->getFields();
  $parent = $struct->getParent();
  if($parent)
    //NOTE: order is important, parent fields must come first
    $fields = array_merge(mtg_get_all_fields($info, $info->findUnit($parent)->object), $fields);
  return $fields;
}

function mtg_load_dir_meta(mtgMetaInfo $meta, mtgMetaInfoParser $meta_parser, $dir)
{
  $files = mtg_find_meta_files($dir);
  foreach($files as $file)
    $meta_parser->parse($meta, $file);
}

function mtg_find_meta_files($dir)
{
  $items = scandir($dir);
  if($items === false)
    throw new Exception("Directory '$dir' is invalid");

  $files = array();
  foreach($items as $item)
  {
    if($item{0} == '.')
      continue;

    if(strpos($item, ".meta") !== (strlen($item)-5))
      continue;

    $file = $dir . '/' . $item;
    if(is_file($file) && !is_dir($file))
      $files[] = $file;
  }
  return $files;
}

function mtg_is_filtered($name, $filters)
{
  foreach($filters as $filter)
  {
    if(!$filter)
      continue;
    if(strpos($name, $filter) !== false)
      return false;
  }
  return true;
}

function mtg_is_win()
{
  return DIRECTORY_SEPARATOR == '\\';
}

function mtg_which($bin)
{
  if(mtg_is_win())
    $bin .= ".exe";
  $path = exec("which $bin", $out, $ret);
  if($ret != 0)
    throw new Exception("Exection error 'which $bin': " . implode($out));
  return $path;
}

function mtg_ensure_write_to_file($file, $body)
{
  if(file_put_contents($file, $body) === false)
    throw new Exception("Could not write file '$file'");
  echo "> $file\n";
}

function mtg_fill_template($tpl, $replaces)
{
  return str_replace(array_keys($replaces), array_values($replaces), $tpl);
}

function mtg_write_template($tpl, $replaces, $file, $exists_check = false)
{
  if($exists_check && file_exists($file))
  {
    echo "! $file\n";
    return;
  }
  $body = mtg_fill_template($tpl, $replaces);
  mtg_ensure_write_to_file($file, $body);
}

function mtg_write_template_once($tpl, $replaces, $file)
{
  mtg_write_template($tpl, $replaces, $file, true);
}

function mtg_append_to_slot($slot, $replace, &$contents)
{
  $contents = str_replace($slot, $slot . $replace, $contents);
}

function mtg_prepend_to_slot($slot, $replace, &$contents)
{
  $contents = str_replace($slot, $replace . $slot, $contents);
}

function mtg_apply_script($script, $args)
{
  $class = current(explode('.', basename($script)));
  include_once($script);
  $obj = new $class();
  call_user_func_array(array($obj, 'apply'), $args);
}

function mtg_paginate($total, $step)
{
  //pages are returned as an array where each element is in interval [N, Y)
  $pages = array();

  $steps = (int)($total/$step);
  $rest = $total % $step;

  for($i=1;$i<=$steps;++$i)
    $pages[] = array(($i-1)*$step, $i*$step);

  if($rest != 0)
    $pages[]  = array(($i-1)*$step, ($i-1)*$step + $rest);

  return $pages;
}

function mtg_mkdir($dir)
{
  if(!is_dir($dir))
    mkdir($dir, 0777, true);
}

function mtg_rm($path)
{
  if(is_file($path))
  {
    unlink($path);
    return;
  }

  if(!is_dir($path))
    return;

  $path= rtrim($path, '/').'/';
  $handle = opendir($path);
  if($handle === false)
    throw new Exception("Could not open directory '$path' for reading");
  for(;false !== ($file = readdir($handle));)
  {
    if($file != "." and $file != ".." )
    {
      $fullpath= $path.$file;
      if( is_dir($fullpath) )
      {
        mtg_rm($fullpath);
        rmdir($fullpath);
      }
      else
        unlink($fullpath);
    }
  }
  closedir($handle);
}

//returns a name of the temp file to use, temp file IS NOT created
function mtg_get_tmpfile($prefix = '__')
{
  $MAX_TRIES = 1000;
  $tries = 0;
  while($tries < $MAX_TRIES)
  {
    $tmp_file = sys_get_temp_dir() . '/' . $prefix . uniqid();
    if(!is_file($tmp_file))
      break;
    ++$tries;
  }

  if($tries == $MAX_TRIES)
    throw new Exception("Could not make unique temp file name");

  return $tmp_file;
}

function mtg_normalize_path($file)
{
  if(mtg_is_win())
  {
    //strtolower drive letters
    if(strpos($file, ':') === 1)
      $file = strtolower(substr($file, 0, 1)) . substr($file, 1);
  }

  $file = str_replace('\\', '/', $file);
  $file = str_replace('//', '/', $file);
  return $file;
}

class mtgGenTarget
{
  public $file;
  public $deps = array();
  public $func;
  public $args = array();
  public $content_check;

  function __construct($file, array $deps, array $callback, $content_check = true)
  {
    if(!$callback)
      throw new Exception("Bad bind arg");

    if(!is_callable($callback[0]))
      throw new Exception("Callback arg 0, not callable:" . serialize($callback[0]));

    $this->file = $file;
    $this->deps = $deps;
    $this->func = array_shift($callback);
    $this->args = array_merge(array($this->file, $this->deps), $callback);
    $this->content_check = $content_check;
  }

  function execute()
  {
    if(!mtg_need_to_regen($this->file, $this->deps))
      return false;

    $res = call_user_func_array($this->func, $this->args);

    $exists_check = false;
    if(is_array($res))
    {
      $exists_check = $res[0];
      $text = $res[1];
    }
    else if(is_string($res))
      $text = $res;
    else
      throw new Exception("Bad result:" . serialize($res));

    if($exists_check && file_exists($this->file))
    {
      //too verbose
      //echo "! {$this->file}\n";
      return false;
    }

    if($this->content_check)
    {
      if(file_exists($this->file) && file_get_contents($this->file) === $text)
        return false;
    }
    
    mtg_ensure_write_to_file($this->file, $text);
    return true;
  }
}

class mtgGenBundleTarget extends mtgGenTarget
{
  function execute()
  {
    $bundle_info = $this->file . '.bndl';
    $prev_amount = 0;

    if(is_file($bundle_info))
      $prev_amount = 1*file_get_contents($bundle_info);

    //if the amount of bundled files has changed we need to update bundle info
    if($prev_amount != sizeof($this->deps))
      file_put_contents($bundle_info, sizeof($this->deps));

    $this->deps[] = $bundle_info;
    return parent::execute();
  }
}

class msgMetaEnumValue
{
  private $name;
  private $value;

  function __construct($name, $value)
  {
    $this->name = $name;
    $this->value = $value;
  }

  function getName()
  {
    return $this->name;
  }

  function getValue()
  {
    return $this->value;
  } 
} 

class mtgMetaPacket extends mtgMetaStruct
{
  private $code;

  function __construct($code, $name, array $tokens = array())
  {
    $this->code = $code;

    parent :: __construct($name, array(), null, $tokens);
  }

  function getPrefix()
  {
    list($prefix,) = explode('_', $this->getName());
    return $prefix;
  }

  function getFullName()
  {
    return $this->code . "_" . $this->getName();
  }

  function getCode()
  {
    return $this->code;
  }

  function getNumericCode()
  {
    return (int)$this->code;
  }
}

class mtgMetaEnum extends mtgMetaStruct
{
  private $name;
  private $values = array();

  function __construct($name)
  {
    $this->name = $name;
  }

  function getName()
  {
    return $this->name;
  }

  function getClassId()
  {
    //NOTE: using crc28 actually, making sure that crc32 is always an unsigned value,
    return crc32($this->name) & 0xFFFFFFF;
  }  

  function addValue($value_name, $value)
  {
    if(isset($this->values[$value_name]))
      throw new Exception("Enum '{$this->name}' already has value with name '{$value_name}'");

    if(in_array($value, $this->values))
      throw new Exception("Enum '{$this->name}' already has value '{$value}'");

    $this->values[$value_name] = $value; 
  }

  function calcOrValue($or_keys)
  {
    $res = 0;
    foreach ($or_keys as $value_name)
    {
      if(!isset($this->values[$value_name]))
        throw new Exception("Enum '{$this->name}' has no value '{$value_name}'");
      $res |= $this->values[$value_name];
    }

    return $res;
  }

  function addOrValues($values)
  {
    foreach($values as $value_name => $or_keys)
      $this->addValue($value_name, $this->calcOrValue($or_keys));
  }

  function getValues()
  {
    return $this->values;
  }
} 

abstract class mtgGenerator
{
  abstract function makeTargets(mtgMetaInfo $info);
}

class mtgCodegen
{
  protected $info;
  private $tmp_cmds = array();

  function setMetaInfo(mtgMetaInfo $info)
  {
    $this->info = $info;
  }

  function nextId() 
  {
    static $id = 0;
    $id++;
    return $id;
  }

  function tempRenameField($field, $new_name)
  {
    $prev_name = $field->getName();
    $field->setName($new_name);

    $this->tmp_cmds[] = array(1, $field, $prev_name);
  }

  function addTempField(mtgMetaStruct $struct, mtgMetaField $new_fld)
  {
    $struct->addField($new_fld);

    $this->tmp_cmds[] = array(2, $struct, $new_fld);
  } 

  function undoTemp()
  {
    foreach($this->tmp_cmds as $cmd)
    {
      if($cmd[0] == 1)
      {
        $cmd[1]->setName($cmd[2]);
      }
      else if($cmd[0] == 2)
      {
        $cmd[1]->delField($cmd[2]);
      }
      else 
        throw new Exception("Unknown temp cmd");
    }
  }
}

function mtg_new_file($file, array $deps, array $callback, $content_check = false)
{
  return new mtgGenTarget($file, $deps, $callback, $content_check);
}

function mtg_new_bundle($file, array $deps, array $callback, $content_check = false)
{
  return new mtgGenBundleTarget($file, $deps, $callback, $content_check);
}

function mtg_need_to_regen($file, array $deps)
{
  if(!is_file($file))
    return true;
  $fmtime = filemtime($file);
  foreach($deps as $dep)
  {
    if(is_file($dep) && (filemtime($dep) > $fmtime))
      return true;
  }
  return false;
}

function mtg_parse_meta(array $meta_dirs)
{
  $meta_parser = new mtgMetaInfoParser(array('include_path' => $meta_dirs));
  $meta = new mtgMetaInfo();
  foreach($meta_dirs as $dir)
    mtg_load_dir_meta($meta, $meta_parser, $dir);
  return $meta;
}

function mtg_run(mtgGenerator $gen, array $config)
{
  global $METAGEN_CONFIG;

  $METAGEN_CONFIG = array();
  foreach($config as $k => $v)
    $METAGEN_CONFIG[$k] = $v;

  $meta = mtg_conf('meta', null);
  if(!($meta instanceof mtgMetaInfo)) 
  {
    $meta_dirs = mtg_conf('meta-dir');
    $meta = mtg_parse_meta($meta_dirs);
  }
  $targets = $gen->makeTargets($meta);
  
  foreach($targets as $t)
    $t->execute();
}

function mtg_crc28($what)
{
  return crc32($what) & 0xFFFFFFF;
}

function mtg_static_hash($whar)
{
  $hash = 0;
  for($i = 0; $i < strlen($whar); ++$i)
  {
    $hash = ((65599 * $hash) & 0x0FFFFFFF) + ord($whar[$i]);
  }

  return ($hash ^ ($hash >> 16));
}


