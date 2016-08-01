<?php

require_once(dirname(__FILE__) . '/../../metagen.inc.php');
require_once(dirname(__FILE__) . '/go_tpl.inc.php');

class mtgGoCodegen extends mtgCodegen
{
  function genEnum(mtgMetaEnum $struct)
  {
    $templater = new mtg_go_templater();

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
    $values_map = array();
    $default_value = null;
    $consts = array();

    $enum_name = $enum->getName();
    $map = array();
    foreach($enum->getValues() as $vname => $value)
    {
      $map[$vname] = $value;
      $vnames[] = "'$vname'";
      $values[] = $value;
      if(!$default_value)
        $default_value = "$value; // $vname";
      $consts[$value] = "{$enum->getName()}_{$vname} {$enum->getName()} = $value";
    }

    // must be sorted
    sort($values, SORT_NUMERIC);

    $repl['%vnames_list%'] = implode(',', $vnames);
    $repl['%values_list%'] = implode(',', $values);
    $repl['%values_map%']  =  $this->genIntMap($map);
    $repl['%default_enum_value%'] = $default_value;
    $repl['%consts%'] = "\n  " . implode("\n  ", $consts) . "\n";
  }
  
  function preprocessField(mtgMetaStruct $struct, mtgMetaField $field) {}

  function genRPC(mtgMetaPacket $packet)
  {
    $templater = new mtg_go_templater();

    $repl = array();
    $repl['%type%'] = $packet->getName();
    $repl['%code%'] = $packet->getNumericCode();
    $repl['%class%'] = $packet->getName();
    $repl['%class_id%'] = $packet->getClassId();
    $repl['%parent_class%'] = '';
    $repl['%login_ticket%'] = '""';
    
    if(strpos($packet->getName(), '_RSP_') !== false)
      $repl['%class_invert%'] = str_replace('_RSP_', '_REQ_', $packet->getName());
    else
      $repl['%class_invert%'] = str_replace('_REQ_', '_RSP_', $packet->getName());
    
    if(strpos($packet->getName(), '_RSP_') !== false)
    {
      $str  = "  mreq, ok := req.(*".$repl['%class_invert%'].")\n";
      $str .= "  if !ok {\n";
      $str .= "    return fmt.Errorf(\"Req must be ".$repl['%class_invert%']."\")\n";
      $str .= "  }\n";
      $str .= "  return env.(IRPC).".str_replace("RSP_", "", $packet->getName())."(self, mreq)";
      $repl['%rpc_process%'] = $str;
    }
    else
      $repl['%rpc_process%'] = "  return nil";

    $fields = $packet->getFields();
    if(isset($fields["ticket"]))
      $repl['%login_ticket%'] = 'self.Ticket';

    $tpl = $templater->tpl_packet();
    
    if(!$repl['%parent_class%'])
      $repl['%parent_class%'] = 'meta.RpcPacket';

    $this->fillStructRepls($packet, $repl);

    return mtg_fill_template($tpl, $repl);
  }

  function genStruct(mtgMetaStruct $struct)
  {
    $templater = new mtg_go_templater();

    $repl = array();
    $repl['%class%'] = $struct->getName();
    $repl['%class_id%'] = $struct->getClassId();
    $repl['%parent_class%'] = $struct->getParent() ? $struct->getParent() : "";

    $tpl = $templater->tpl_struct();

    $this->fillStructRepls($struct, $repl);

    $result = mtg_fill_template($tpl, $repl);

    if($struct->hasToken('POD') && $struct->hasToken("id") && $struct->hasToken("table") && $struct->hasToken("owner"))
      $result .= "\n" . str_replace(array_keys($repl), array_values($repl), $templater->tpl_collection_item());
    return $result;
  }

  function fillStructRepls(mtgMetaStruct $struct, &$repl)
  {
    foreach($struct->getFields() as $field)
      $this->preprocessField($struct, $field);

    $repl['%includes%'] = '';
    $repl['%fields%'] = '';
    $repl['%fields_names%'] = '[]string{';
    $repl['%fields_props%'] = 'map[string]map[string]string{';
    $repl['%fields_types%'] = 'map[string][]string{';
    $repl['%fields_reset%'] = '';
    $repl['%read_buffer%'] = '';
    $repl['%write_buffer%'] = '';
    $repl['%total_top_fields%'] = sizeof($struct->getFields());

    $repl['%class_props%'] = 'map[string]string{' . $this->genStrMap($struct->getTokens()) . '}';

    $parent = $struct->getParent();
    $all_fields = mtg_get_all_fields($this->info, $struct);

    foreach($all_fields as $field)
    {
      $name = $field->getName();
      $repl['%fields_names%'] .= '"' . $name . '",';
      $repl['%fields_props%'] .= '"' . $this->genFieldName($field) . '" : map[string]string{' . $this->genStrMap($field->getTokens()) . "},";
      $field_type = $field->getType(); 
      $repl['%fields_types%'] .= '"' . $name . '" : []string{"' . $field->getSuperType() . '", "' . (is_array($field_type) ?  ($field_type[0] . '", "' . $field_type[1]) : $field_type) . '"},';
    }

    $repl['%read_buffer%'] .= "\n  data_size, err := reader.GetArraySize()";
    $repl['%read_buffer%'] .= "\n  if err != nil {";
    $repl['%read_buffer%'] .= "\n    return err";
    $repl['%read_buffer%'] .= "\n  }";

    $optional = 0;
    foreach($struct->getFields() as $field)
      if($field->hasToken('optional'))
        $optional++;
    $initial_fields_amount = count($struct->getFields()) - $optional;
    $repl['%read_buffer%'] .= "\n  if data_size < $initial_fields_amount {";
    $repl['%read_buffer%'] .= "\n    data_size = $initial_fields_amount";
    $repl['%read_buffer%'] .= "\n  }";
      
    if($struct->hasToken('POD'))
      $fields = $all_fields;
    else
    {
      if($parent = $struct->getParent())
      {
        $repl['%read_buffer%'] .= "\n  if err := self." . $parent . ".ReadFields(reader); err != nil { return err }";
        $repl['%write_buffer%'] .= "\n  if err := self." . $parent . ".WriteFields(writer, assoc); err != nil { return err }";
      }
      $fields = $struct->getFields();
    }

    foreach($fields as $field)
    {
      $repl['%fields%'] .= "\n " . $this->genFieldDecl($field);
      $repl['%fields_reset%'] .= "\n  " . $this->genFieldReset($field);
      $repl['%read_buffer%'] .= "\n  if data_size <= 0 {";
      $repl['%read_buffer%'] .= "\n    return nil";
      $repl['%read_buffer%'] .= "\n  }";
      $repl['%read_buffer%'] .= "\n  data_size--";
      $repl['%read_buffer%'] .= "\n  " . $this->genBufRead($field->getName(), 'self.'.ucfirst($field->getName()), $field->getSuperType(), $field->getType(), "reader", $field->getTokens());
      $repl['%write_buffer%'] .= "\n  " . $this->genBufWrite($field, "writer");
    }

    $repl['%fields%'] = trim($repl['%fields%'], "\n ");

    $repl['%fields_names%'] .= '}';
    $repl['%fields_props%'] .= '}';
    $repl['%fields_types%'] .= '}';

    $repl['%import_from_mysql%'] = '';
    $repl["%export_to_arr%"] = "";
    if($struct->hasToken('POD') && $struct->hasToken("table"))
    {
      $ind = 0;

      $repl['%import_from_mysql%'] .= "\n      row := data.(".$struct->getName().")";
      foreach($all_fields as $field)
      {
        $name = $this->genFieldName($field);
        $repl['%import_from_mysql%'] .= sprintf("\n      self.%s = row.%s", $name, $name);
        $repl['%export_to_arr%'] .= sprintf("\n  data[%d] = self.%s", $ind, $name);
        $ind++;
      }
    }
    
    $repl["%table_name%"] = $struct->getToken("table");
    $repl["%owner%"] = $struct->getToken("owner");
    $repl["%id_field_name%"] = $struct->getToken("id");
    $repl["%id_field%"] = ucfirst($struct->getToken("id"));
  }

  function genBufRead($name, $fname, $super_type, $type, $buf, array $tokens = array())
  {
    $str = '';

    if($super_type == "scalar")
    {
      $str .= $this->genRead($tokens, "{$buf}.Read".$this->genTypePrefix($type)."(&$fname, \"$name\")");
    }
    else if($super_type == "string")
    {
      $str .= $this->genRead($tokens, "{$buf}.ReadString(&$fname, \"$name\")");
    }
    else if($super_type == "struct")
    {
      if(array_key_exists('virtual', $tokens))
        $str .= "if v, err := meta.ReadGeneric(\"{$name}\", $buf); err != nil { return err } else { {$fname} = v.(I{$type}) }";
      else
        $str .= $this->genRead($tokens, "{$fname}.Read($buf, \"$name\")");
    }
    else if($super_type == "enum")
    {
      $str .= $this->genRead($tokens, "{$buf}.ReadI32((*int32)(&{$fname}), \"$name\")");
      $str .= "\n  if !{$fname}.IsValid() { return fmt.Errorf(\"Bad enum value %d for $name\", $fname) }";
    }
    else if($super_type == "array")
    {
      $is_virtual = array_key_exists("virtual", $tokens);
      $native_type = $this->genNativeTypeEx($type[0], $type[1], $tokens);
      $offset = "\n  ";
      $str .= "/*[]{$name}*/";        
      $str .= $offset . $this->genRead($tokens, "{$buf}.BeginArray(\"$name\")");
      $str .= $offset . "{$name}_size, err := {$buf}.GetArraySize()";
      $str .= $offset . "if err != nil { return err }";    
      
      $is_pointer = !$is_virtual && $this->inArrayNeedsPtr($native_type, $tokens);   
      
      $str .= $offset . "for ; {$name}_size > 0; {$name}_size-- {";
      $offset = "\n    ";    
      if(!$is_pointer)
        $str .= $offset . "var tmp_{$name} {$native_type}";  
      else
        $str .= $offset . "tmp_{$name} := new({$native_type})";
        
      $str .= $offset . $this->genBufRead("", "tmp_{$name}", $type[0], $type[1], $buf, $tokens, true);          
      $str .= $offset . "{$fname} = append({$fname}, tmp_{$name})";        
      
      $offset = "\n  ";
      $str .= $offset . "}";
      $str .= $offset .  $this->genRead($tokens, "{$buf}.EndArray()");
      $str .= "\n";    
    }
    else
      throw new Exception("Unknown type {$super_type}");

    return $str;
  }

  function genRead(array $tokens, $op)
  {
    return "if err := $op; err != nil { return err }";
  }

  function genWrite($op)
  {
    return "if err := $op; err != nil { return err }";
  }

  function genBufWrite(mtgMetaField $field, $buf)
  {
    return $this->genBufWriteEx($field->getName(), "self.".ucfirst($field->getName()), $field->getSuperType(), $field->getType(), $buf, $field->getTokens());
  }

  function genBufWriteEx($name, $fname, $super_type, $type, $buf, array $tokens = array())
  {
    $str = '';

    if($super_type == "scalar")
    {
      $str .= $this->genWrite("{$buf}.Write".$this->genTypePrefix($type)."($fname, \"$name\")")."\n";
    }
    else if($super_type == "string")
    {
      $str .= $this->genWrite("{$buf}.WriteString({$fname}, \"$name\")");
    }
    else if($super_type == "struct")
    {
      if(array_key_exists('virtual', $tokens))
        $str .= $this->genWrite("meta.WriteGeneric($fname, $buf, \"$name\", assoc)");
      else
        $str .= $this->genWrite("$fname.Write($buf, \"$name\", assoc)");
    }
    else if($super_type == "enum")
    {
      $str .= $this->genWrite("{$buf}.WriteI32(int32($fname), \"$name\")");
    }
    else if($super_type == "array")
    {
      $str .= "{$buf}.BeginArray(false, \"{$name}\")\n";
      $str .= "  for _, v := range({$fname}) {\n";
      $str .= "    ".$this->genBufWriteEx("", "v", $type[0], $type[1], $buf, $tokens)."\n";
      $str .= "  }\n";
      $str .= "  ".$this->genWrite("{$buf}.EndArray()")."\n"; 
    }
    else
      throw new Exception("Unknown meta type '$super_type'");

    return $str;
  }

  function genFieldDecl(mtgMetaField $field)
  {
    return $this->genFieldName($field) . " " . $this->genNativeType($field);
  }

  function genFieldName(mtgMetaField $field)
  {
    return ucfirst($field->getName());  
  }

  function genFieldReset(mtgMetaField $field)
  {
    $tokens = $field->getTokens();
    
    $str = '';
    $name = $this->genFieldName($field);

    if($field->getSuperType() == "scalar")
    {
      //NOTE: numeric check is against str2num
      if(array_key_exists('default', $tokens) && is_numeric($tokens['default']))
        $str .= "self.$name = ".$tokens['default'];
      else
        $str .= " self.$name = 0";
    }
    else if($field->getType() == "string")
    {
      if(array_key_exists('default', $tokens))
        $str .= "self.$name = ".$tokens['default'];
      else
        $str .= "self.$name = \"\"";
    }
    else 
    {
      if($field->getSuperType() == "array")
      {
        $str = "if self.$name == nil { \nself.$name = make(".$this->genNativeType($field).",0) \n}\n ";
        $str .= "self.$name = self.{$name}[0:0]";
      }
      else if($field->getSuperType() == "enum")
      {
        if(array_key_exists('default', $tokens))
          $str = "self.$name = ".$this->genNativeType($field)."_".trim($tokens['default'], '"');
        else
          $str = "self.$name = 0";
      }
      else
      {
        $is_virtual = array_key_exists("virtual", $tokens);
        if($is_virtual)
          $str .= "self.$name = New".ltrim($this->genNativeType($field),'I')."() ";
        else
          $str .= "self.$name.Reset()";
      }
    }
    return $str;
  }

  function genNativeType(mtgMetaField $field)
  {
    return $this->genNativeTypeEx($field->getSuperType(), $field->getType(), $field->getTokens());
  }

  function genNativeTypeEx($super_type, $type, array $tokens = array())
  {  
    switch($super_type)
    {
      case "scalar":
        return $this->genType($type);
      case "string":
        return "string";
      case "array":      
        $str = "[]";
        if(array_key_exists("virtual", $tokens))
          $str .= "I";
        else
          $str .= $this->inArrayNeedsPtr($type[0], $tokens) ? "*" : "";
        
        $str .= $this->genType($type[1]);      
              
        return $str;
      case "enum":
        return $type;
      case "struct":
        if(array_key_exists("virtual", $tokens))
          return "I$type";
        else
          return $type;
      default:
        throw new Exception("Unknown super type '{$super_type}'");
    }
  }

  function inArrayNeedsPtr($type, $tokens)
  {   
    if(strpos($type, 'Enum') !== false)
      return false;

    $types = array(
      'float64', 'float32', 'uint32', 'string', 'int32', 'enum', 'scalar',
    ); 
    return !in_array($type, $types);
  }

  function genTypePrefix($type)
  {
    switch($type)
    {
      case "float":
        return "Float";
      case "double":
        return "Double";
      case "uint64":
        return "U64";
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
      default:
        throw new Exception("Unknow type {$type}");
    }
  }

  function genType($type)
  {
    if(in_array($type, mtgMetaInfo::$SCALAR_TYPES))
    {
      if($type == "float")
        return "float32";
      else if($type == "double")
        return "float64";
      else if($type == 'int')
        return 'int32';
      else if($type == 'uint')
        return 'uint32';
      else if($type == 'uint64')
        return 'uint64';
      return $type;
    }
    if($type == mtgMetaInfo::$STRING_TYPE)
      return "string";
    //struct?
    return $type;
  }

  function genStrMap(array $source)
  {
    $str = '';
    foreach($source as $k => $v)
      $str .= '"' . $k . '": "' . ($v ? str_replace('"', '\\"', $v) : "") . '",';
    return $str;
  }

  function genIntMap(array $source)
  {
    $str = '';
    foreach($source as $k => $v)
      $str .= "\"$k\": {$v},";
    return $str;
  }
}
