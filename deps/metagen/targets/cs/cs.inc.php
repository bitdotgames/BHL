<?php
namespace bhl;

require_once(dirname(__FILE__) . '/../../metagen.inc.php');
require_once(dirname(__FILE__) . '/cs_tpl.inc.php');

class mtgCSCodegen extends mtgCodegen
{
  public $namespace = 'bhl';

  function genUnit(mtgMetaInfoUnit $unit)
  {
    $obj = $unit->object;
    if($obj instanceof mtgMetaEnum)
      return $this->genEnum($obj);
    else if($obj instanceof mtgMetaStruct)
      return $this->genStruct($obj);
    else if($obj instanceof mtgMetaRPC)
      return $this->genRPC($obj);
    else
    {
      echo "WARN: skipping meta unit '{$obj->getId()}'\n";
      return '';
    }
  }

  function genEnum(mtgMetaEnum $struct)
  {
    $templater = new mtg_cs_templater();

    $repl = array();
    $repl['%namespace%'] = $this->namespace;
    $repl['%class%'] = $struct->getName();
    $repl['%class_id%'] = $struct->getClassId();

    $tpl = $templater->tpl_enum();

    $this->fillEnumRepls($struct, $repl);

    return mtg_fill_template($tpl, $repl);
  }

  function fillEnumRepls(mtgMetaEnum $enum, &$repl)
  {
    $repl['%fields%'] = '';
    $fields =& $repl['%fields%'];
    foreach($enum->getValues() as $vname => $value)
      $fields .= sprintf("\n    %s = %s,", $vname, $value);
  }

  function genRPC(mtgMetaRPC $rpc)
  {
    $templater = new mtg_cs_templater();

    $repl = array();
    $repl['%namespace%'] = $this->namespace;
    $repl['%class%'] = $rpc->getName();
    $repl['%code%'] = $rpc->getCode();
    $repl['%req_class%'] = $rpc->getReq()->getName();
    $repl['%rsp_class%'] = $rpc->getRsp()->getName();
    
    $tpl = $templater->tpl_rpc();

    return 
      mtg_fill_template($tpl, $repl) . 
      $this->genStruct($rpc->getReq()) .
      $this->genStruct($rpc->getRsp());
  }

  function genStruct(mtgMetaStruct $struct)
  {
    $templater = new mtg_cs_templater();

    $repl = array();
    $repl['%namespace%'] = $this->namespace;
    $repl['%class%'] = $struct->getName();
    $repl['%class_id%'] = $struct->getClassId();

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

    $all_fields = mtg_get_all_fields($this->info, $struct);

    $repl['%includes%'] = '';
    $repl['%fields%'] = '';
    $repl['%fields_reset%'] = '';
    
    $repl['%sync_buffer%'] = '';
    $repl['%fields_count%'] = count($all_fields);
    $repl["%ext_methods%"] = "";
  
    $repl['%new_method%'] = '';
    $repl['%virt_method%'] = ' virtual ';
    $repl['%commented_in_child_begin%'] = '';
    $repl['%commented_in_child_end%'] = '';
    $repl['%commented_in_pod_begin%'] = '';
    $repl['%commented_in_pod_end%'] = '';


    if($struct->hasToken('POD'))
    {
      $fields = $all_fields;
      $repl['%type_name%'] = 'struct';
      $repl['%parent%'] = " : IMetaStruct";
      $repl['%virt_method%'] = '';
      $repl['%commented_in_pod_begin%'] = '/*';
      $repl['%commented_in_pod_end%'] = '*/';
    }
    else
    {
      $repl['%type_name%'] = 'class';
      $repl['%parent%'] = " : " . ($struct->getParent() ? $struct->getParent() : " BaseMetaStruct");
      $repl['%sync_buffer%'] .= "\n    base.syncFields(ctx);\n";
      $repl['%commented_in_child_begin%'] = '/*';
      $repl['%commented_in_child_end%'] = '*/';
      $repl['%virt_method%'] = " override ";
      
      if($parent = $struct->getParent())
      {
        $repl['%new_method%'] = " new ";
        $repl['%fields_reset%'] = "base.reset();\n";
      }
      $fields = $struct->getFields();
    }

    $repl['%copy_fields%'] = '';
    if($struct->getParent() && !$struct->hasToken('POD'))
      $repl['%copy_fields%'] .= "\n  base.copyFrom(other);\n";
    
    foreach($fields as $field)
    {
      $repl['%fields%'] .= "\n  " . $this->genFieldDecl($struct, $field);
      $repl['%fields_reset%'] .= "\n  " . $this->genFieldReset($field);
      $repl['%sync_buffer%'] .= "\n  " . $this->genBufSync($field, "ctx", "opts")."\n";
      $repl['%copy_fields%'] .= "\n    " . $this->genFieldCopy($field);
    }

    $repl['%fields%'] = trim($repl['%fields%'], "\n ");

    $this->undoTemp();
  }  

  function genNativePlainType(mtgType $type)
  {
    if($type instanceof mtgBuiltinType)
    {
      switch($type->getName())
      {
        case "int8":
          return "sbyte";
        case "uint8":
          return "byte";
        case "int16":
          return "short";
        case "uint16":
          return "ushort";
        case "int32":
        case "int":
          return "int";
        case "uint32":
        case "uint":
          return "uint";
        case "float":
          return "float";
        case "double":
          return "double";
        case "uint64":
          return "ulong";
        case "int64":
          return "long";
        case "string":
          return "string";
        case "bool":
          return "bool";
      }
      throw new Exception("Unknown type '{$type}'");
    }
    return $type->getName();
  }

  function genTypePrefix(mtgType $type)
  {
    switch($type->getName())
    {
      case "float":
        return "Float";
      case "double":
        return "Double";
      case "uint64":
        return "U64";
      case "int64":
        return "I64";
      case "uint":
      case "uint32":
        return "U32";
      case "uint16":
        return "U16";
      case "uint8":
        return "U8";
      case "int":
      case "int32":
        return "I32";
      case "int16":
        return "I16";
      case "int8":
        return "I8";
      case "string":
        return "String";
      case "bool":
        return "Bool";
      default:
        throw new Exception("Unknown type '{$type}'");
    }
  }

  function genBufSync(mtgMetaField $field, $buf, $opts = "")
  {
    return $this->genBufSyncEx($field->getName(), $field->getType(), $buf, $field->getTokens(), $opts);
  }

  function genBufSyncEx($fname, mtgType $type, $buf, array $tokens = array(), $opts = "")
  {
    $str = '';
    if($type instanceof mtgBuiltinType)
    {
      $str .= "MetaHelper.sync({$buf}, ref {$fname});\n";
    }
    else if($type instanceof mtgMetaStruct)
    {
      if(array_key_exists('virtual', $tokens))
        $str .= "MetaHelper.syncVirtual({$buf}, ref {$fname});\n";
      else
        $str .= "MetaHelper.sync({$buf}, ref {$fname});\n";
    }
    else if($type instanceof mtgMetaEnum)
    {
      $str .= "int __tmp_{$fname} = (int)$fname;\n";
      $str .= "MetaHelper.sync({$buf}, ref __tmp_{$fname});\n";
      $str .= "if($buf.is_read) {$fname} = ({$type->getName()})__tmp_{$fname};\n";
    }
    else if($type instanceof mtgArrType)
    {
      if(array_key_exists('virtual', $tokens))
        $str .= "MetaHelper.syncVirtual({$buf}, ref $fname);\n";
      else
        $str .= "MetaHelper.sync({$buf}, ref $fname);\n";
    }
    else
      throw new Exception("Unknown type '$type'");

    return $str;
  }

  function genConstructArgs($type)
  {
    if($type == "string")  
      return '""';
    return "new {$type}()";
  }

  function isPOD(mtgMetaStruct $struct)
  {
    return $struct->hasToken('POD');
  }

  function genFieldDecl(mtgMetaStruct $struct, mtgMetaField $field)
  {
    $str = "public " . $this->genFieldNativeType($field) . " ";

    if(!$this->isPOD($struct))
      $str .= $this->genFieldDeclInit($field);
    else
      $str .= $this->genFieldName($field) . ";";

    return $str;
  }

  function genFieldCopy(mtgMetaField $field)
  {
    $tokens = $field->getTokens();
    $name =  $this->genFieldName($field);
    $type = $field->getType();
    $str = '';

    if($type instanceof mtgArrType)
    {
      $str .= "for(int i=0;other.{$name} != null && i<other.{$name}.Count;++i) { \n";

      $vtype = $type->getValue();
      if($vtype instanceof mtgMetaStruct)
      {
        if(array_key_exists('virtual', $tokens))
          $str .= "var tmp = ({$vtype->getName()})other.{$name}[i].clone();\n";
        else
          $str .= "var tmp = new {$vtype->getName()}(); tmp.copyFrom(other.{$name}[i]);\n";
        $str .= "{$name}.Add(tmp);\n";
      }
      else
        $str .= "{$name}.Add(other.{$name}[i]);\n";

      $str .= "}\n";
    }
    else if($type instanceof mtgMetaStruct)
    {
      if(array_key_exists('virtual', $tokens))
        $str .= "{$name} = ({$type->getName()})other.{$name}.clone();\n";
      else 
        $str .= "{$name}.copyFrom(other.{$name});\n";
    }
    else
      $str .= "{$name} = other.{$name};\n";

    return $str;
  }

  function genFieldDeclInit(mtgMetaField $field)
  {
    $tokens = $field->getTokens();
    
    $str =  $this->genFieldName($field);
    $type = $field->getType();
    if($type instanceof mtgBuiltinType)
    {
      if($type->isString())
        $str .= ' = ""';
    }
    else
      $str .= " = new ".$this->genFieldNativeType($field)."()";
    $str .= ";";
    return $str;
  }

  function genFieldReset(mtgMetaField $field)
  {
    $tokens = $field->getTokens();
    $default = array_key_exists('default', $tokens) ? $tokens['default'] : null; 

    return $this->genFieldResetEx($this->genFieldName($field), $field->getType(), $default);
  }

  function genFieldResetEx($name, mtgType $type, $default = null)
  {
    $str = '';
    if($type instanceof mtgBuiltinType)
    {
      if($type->isNumeric())
      {
        $str = $name;
        //NOTE: numeric check is against str2num
        if($default && is_numeric($default))
        {
          if($type->isFloat())
            $default .= "f";
          $str .= " = $default;";
        }
        else
          $str .= " = 0;";
      }
      else if($type->isString())
      {
        $str = $name;
        if($default)
        {
          $str .= " = \"".trim($default, '"')."\";";
        }
        else
          $str .= ' = "";';
      }
      else if($type->isBool())
      {
        $str = $name;
        if($default)
        {
          $str .= " = ".trim($default, '"').";";
        }
        else
          $str .= ' = false;';
      }
      else 
        throw new Exception("Unknown type '$type'");
    }
    else if($type instanceof mtgArrType) 
    {
      $str = "if($name == null) $name = new ".$this->genNativeType($type)."(); ";
      $str .= "$name.Clear();";

      if($default)
      {
        $default_arr = is_array($default) ? $default : json_decode($default, true);
        if(!is_array($default_arr))
          throw new Exception("Bad default value for array: '$default'");

        if($default_arr)
        {
          $type_str = $this->genNativeType($type->getValue());
          $str .= "{ $type_str tmp__";
          if($this->isNullableType($type->getValue()))
            $str .= " = null";
          $str .= ";";
          foreach($default_arr as $v)
          {
            $str .= $this->genFieldResetEx("tmp__", $type->getValue(), $v);
            $str .= "$name.Add(tmp__);";
          }
          $str .= "}";
        }
      }
    }
    else if($type instanceof mtgMetaEnum)
    {
      if($default)
        $str = "$name = ".$this->genNativeType($type).".".trim($default,'"')."; ";
      else
        $str = "$name = new ".$this->genNativeType($type)."(); ";
    }
    else if($type instanceof mtgMetaStruct)
    {
      $str = "";
      $is_pod = $type->hasToken('POD');
      if(!$is_pod)
      {
        $str .= "if($name == null) ";
        $str .= "$name = new ".$this->genNativeType($type)."(); ";
      }
      $str .= "$name.reset(); ";

      if($default)
      {
        $default = is_array($default) ? $default : json_decode($default, true);
        if(!is_array($default))
          throw new Exception("Bad default value for struct: $default");

        foreach($default as $k => $v)
        {
          $kf = $type->getField($k); 
          $str .= $this->genFieldResetEx("$name." . $this->genFieldName($kf), $kf->getType(), $v);
        }
      }
    }
    else
      throw new Exception("Bad type '$type'");
    return $str;
  }

  function isNullableType(mtgType $type)
  {
    return $type instanceof mtgArrType || 
           ($type instanceof mtgMetaStruct && !$type->hasToken('POD'));
  }

  function genFieldName(mtgMetaField $field)
  {
    return $field->getName();  
  }

  function genFieldNativeType(mtgMetaField $field)
  {
    return $this->genNativeType($field->getType(), $field->getTokens());
  }

  function genNativeType(mtgType $type, array $tokens = array())
  {
    if($type instanceof mtgBuiltinType || $type instanceof mtgUserType)
      return $this->genNativePlainType($type);
    else if($type instanceof mtgArrType)
      return "List<" . $this->genNativePlainType($type->getValue()) . ">";
    else
      throw new Exception("Unknown type '{$type}'");
  }
}
