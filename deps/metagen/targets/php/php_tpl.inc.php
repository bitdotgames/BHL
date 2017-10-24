<?php

class mtg_php_templater
{
  function tpl_struct()
  {
    $TPL = <<<EOD
<?php
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
%includes%

class %class% %parent_class%
{
  const CLASS_ID = %class_id%;

  %fields%

  static function CLASS_PROPS()
  {
    static \$props = %class_props%;
    return \$props;
  }

  static function CLASS_FIELDS()
  {
    static \$flds = null;
    if(\$flds === null)
      \$flds = %fields_names%; 
    return \$flds;
  }

  static function CLASS_FIELDS_TYPES()
  {
    static \$flds = null;
    if(\$flds === null)
      \$flds = %fields_types%; 
    return \$flds;
  }

  function CLASS_FIELDS_PROPS()
  {
    static \$flds = null;
    if(\$flds === null)
      \$flds = %fields_props%; 
    return \$flds;
  }

  function __construct(&\$message = null, \$assoc = false)
  {
    %fields_init%

    if(!is_null(\$message))
      \$this->import(\$message, \$assoc);
  }

  function getClassId()
  {
    return self::CLASS_ID;
  }
  
  %ext_methods%

  function import(&\$message, \$assoc = false, \$root = true)
  {
    \$IDX = 0;
    try
    {
      if(!is_array(\$message))
        throw new Exception("Bad message: \$message");

      try
      {
        %fill_fields%
      }
      catch(Exception \$e)
      {
        \$FIELDS = self::CLASS_FIELDS();
        throw new Exception("Error while filling field '{\$FIELDS[\$IDX]}': " . \$e->getMessage());
      }

      if(\$root && \$assoc && sizeof(\$message) > 0)
        throw new Exception("Junk fields: " . implode(',', array_keys(\$message)));
    }
    catch(Exception \$e)
    {
      throw new Exception("Error while filling fields of '%class%':" . \$e->getMessage());
    }
    return \$IDX;
  }

  function export(\$assoc = false, \$virtual = false)
  {
    \$message = array();
    \$this->fill(\$message, \$assoc, \$virtual);
    return \$message;
  }

  function fill(&\$message, \$assoc = false, \$virtual = false)
  {
    if(\$virtual)
      mtg_php_array_set_value(\$message, \$assoc, 'vclass__', \$this->getClassId());

    try
    {
      %fill_buffer%
    }
    catch(Exception \$e)
    {
      throw new Exception("Error while dumping fields of '%class%':" . \$e->getMessage());
    }
  }
}

EOD;
    return $TPL;
  }

  function tpl_enum()
  {
    $TPL = <<<EOD
<?php
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT! 

class %class%
{
  const CLASS_ID = %class_id%;

  %values%

  const DEFAULT_VALUE = %default_enum_value%;

  function getClassId()
  {
    return self::CLASS_ID;
  }

  static function isValueValid(\$value)
  {
    \$values_list = self::getValuesList();
    return in_array(\$value, self::\$values_list_);
  }

  static private \$values_map_;
  static private \$names_map_;

  static function getValueByName(\$name)
  {
    if(!self::\$values_map_)
    {
      self::\$values_map_ = array(
       %values_map%
      );
    }
    if(!isset(self::\$values_map_[\$name]))
      ASSERT_TRUE(false, "Value with name \$name isn't defined in enum %class%. Accepted: " . implode(',', self::getNamesList()));
    return self::\$values_map_[\$name];
  }
  
  static function getNameByValue(\$value)
  {
    if(!self::\$names_map_)
    {
      self::\$names_map_ = array(
       %names_map%
      );
    }
    if(!isset(self::\$names_map_[\$value]))
      ASSERT_TRUE(false, "Value \$value isn't defined in enum %class%. Accepted: " . implode(',', self::getValuesList()));
    return self::\$names_map_[\$value];
  }

  static function checkValidity(\$value)
  {// throws exception if \$value is not valid numeric enum value
    if(!is_numeric(\$value))
      ASSERT_TRUE(false, "Numeric expected but got \$value");
    if(!self::isValueValid(\$value))
      ASSERT_TRUE(false, "Numeric value \$value isn't value from enum %class%. Accepted numerics are " . implode(',', self::getValuesList()) . " but better to use one of names instead: " . implode(',', self::getNamesList()));
  }

  static private \$values_list_;
  static function getValuesList()
  {
    if(!self::\$values_list_)
    {
      self::\$values_list_ = array(
          %values_list%
          );
    } 
    return self::\$values_list_;
  }

  static private \$names_list_;
  static function getNamesList()
  {
    if(!self::\$names_list_)
    {
      self::\$names_list_ = array(
          %vnames_list%
          );
    } 
    return self::\$names_list_;
  } 
}
 
EOD;
    return $TPL; 
  }

  function tpl_packet()
  {
    $TPL = <<<EOD
<?php
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
%includes%

#PACKET %type% %code%

class %class% %parent_class%
{
  const CLASS_ID = %class_id%;

  #public \$__stamp = 0;
  public \$__seq_id = 0;

  %fields%

  function CLASS_FIELDS()
  {
    static \$flds = %fields_names%; 
    return \$flds;
  }

  function getClassId()
  {
    return self::CLASS_ID;
  }

  function __construct(&\$message = null, \$assoc = false)
  {
    %fields_init%

    if(!is_null(\$message))
      \$this->import(\$message, \$assoc);
  }

  function getCode()
  {
    return %code%;
  }

  function getType()
  {
    return "%type%";
  }

  function import(&\$message, \$assoc = false, \$root = true)
  {
    try
    {
      if(!\$message || !is_array(\$message))
        throw new Exception("Bad message");

      \$IDX = 0;
      try
      {
        %fill_fields%
      }
      catch(Exception \$e)
      {
        \$FIELDS = self::CLASS_FIELDS();
        throw new Exception("Error while filling field '{\$FIELDS[\$IDX]}': " . \$e->getMessage());
      }

      if(\$root && \$assoc && sizeof(\$message) > 0)
        throw new Exception("Junk fields: " . implode(',', array_keys(\$message)));
    }
    catch(Exception \$e)
    {
      throw new Exception("Error while filling fields of '%class%':" . \$e->getMessage());
    }
  }

  function export(\$assoc = false)
  {
    try
    {
      \$message = array();

      %fill_buffer%
      
      return \$message;
    }
    catch(Exception \$e)
    {
      throw new Exception("Error while dumping fields of '%class%':" . \$e->getMessage());
    }
  }
}

EOD;
    return $TPL;
  }

  function tpl_packet_bundle()
  {
    $TPL = <<<EOD
<?php
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
%bundle%

class AutogenBundle
{
  static function getClassName(\$id)
  {
    switch(\$id)
    {
      %class_map%
      default : throw new Exception("Can't find class for id: \$id");
    }
  }

  static function createPacket(\$code, \$data)
  {
    switch(\$code)
    {
      %packet_map%
      default : throw new Exception("Can't find packet for code: \$code");
    }
  }
}

EOD;
    
    return $TPL;
  }

}
