<?php

class mtg_go_templater
{
  function tpl_struct()
  {
    $TPL = <<<EOD
//package autogen
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

//import (
//  "gme/data"
//  "gme/meta"
//  "fmt"
//)
//
//var _ = fmt.Printf // can be needed

%includes%

type %class% struct {
  %parent_class%
  %fields%
}

var _%class%_class_props map[string]string = %class_props%
var _%class%_class_fields []string = %fields_names% 
var _%class%_fields_props data.ClassFieldsProps = %fields_props%

func %class%_CLASS_ID() uint32 {
  return %class_id%
}

type I%class% interface {
  meta.IMetaStruct
  I%class%() *%class%
}

func (*%class%) CLASS_ID() uint32 {
  return %class_id%
}

func (*%class%) CLASS_NAME() string {
  return "%class%"
}

func (*%class%) CLASS_PROPS() *map[string]string {
  return &_%class%_class_props
}

func (*%class%) CLASS_FIELDS() []string {
  return _%class%_class_fields
}

func (*%class%) CLASS_FIELDS_PROPS() *data.ClassFieldsProps {
  return &_%class%_fields_props
}

func (self *%class%) I%class%() *%class% {
  return self
}

func New%class%() *%class% {
  item := new (%class%)
  item.Reset()
  return item
}

func (self *%class%) Reset() {
  %fields_reset%
}

func (self *%class%) Read(reader data.Reader, field string) error {
  return meta.ReadStruct(self, reader, &field)
}

func (self *%class%) ReadFields(reader data.Reader) error {
  self.Reset()
  %read_buffer%
  return nil
}

func (self *%class%) Write(writer data.Writer, field string, assoc bool) error { 
  return meta.WriteStruct(self, writer, &field, assoc)
}

func (self *%class%) WriteFields(writer data.Writer, assoc bool) error {
  %write_buffer%
  return nil
}

EOD;
    return $TPL;
  }
  
  function tpl_collection_item()
  {
    $TPL = <<<EOD

func (self *%class%) NewInstance() data.IDataItem {
  return New%class%()
}

func (self *%class%) GetDbTableName() string {
  return "%table_name%"
}

func (self *%class%) GetDbFields() []string {
  return self.CLASS_FIELDS()
}

func (self *%class%) GetOwnerFieldName() string {
  return "%owner%"
}

func (self *%class%) GetIdFieldName() string {
  return "%id_field_name%"
}

func (self *%class%) GetIdValue() uint32 {
  return self.%id_field%
}

func (self *%class%) Import(data interface{}) {
  switch data.(type) {
    case %class%:
    {
      %import_from_mysql%
      break
    }
    default:
      break
  }
}

func (self *%class%) Export(data []interface{}) {
  %export_to_arr%
}

EOD;
    return $TPL;
  }

  function tpl_enum()
  {
    $TPL = <<<EOD
//package autogen
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT! 

//import (
//  "sort"
//  "fmt"
//)

const (%consts%)

type %class% int32

var _%class%_values []int = []int{%values_list%}
var _%class%_map map[string]%class% = map[string]%class%{%values_map%}

func (*%class%) CLASS_ID() uint32 {
  return %class_id%
}

func (*%class%) CLASS_NAME() string {
  return "%class%"
}

func (*%class%) DEFAULT_VALUE() int32 {
  return %default_enum_value%
}

func (self *%class%) IsValid() bool {
  return sort.SearchInts(_%class%_values, int(*self)) != -1
}

func %class%_GetNameByValue(value int) string {
	for name, num := range _%class%_map {
		if value == int(num) {
			return name
		}
	}

	return ""
}

func New%class%ByName(name string) (%class%, error) {
  if v, ok := _%class%_map[name]; ok == true {
    return v, nil
  }
  return 0, fmt.Errorf("Wrong name of %class%: '%s'", name)
}
 
EOD;
    return $TPL; 
  }

  function tpl_packet()
  {
    $TPL = <<<EOD
//package autogen
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!

//import (
//  "gme/data"
//  "gme/meta"
//  "fmt"
//)
//
//var _ = fmt.Printf // can be needed

%includes%

type %class% struct {
  %parent_class%
  %fields%
}

var _%class%_class_props map[string]string = %class_props%
var _%class%_class_fields []string = %fields_names%
var %class%_ func(interface{}, *%class%, *%class_invert%) error
var _%class%_fields_props data.ClassFieldsProps = nil

type I%class% interface {
  meta.IMetaStruct
  I%class%() *%class%
}

func (self *%class%) Process(env interface{}, req meta.IMetaStruct) error {
%rpc_process%
}

func (*%class%) CLASS_ID() uint32 {
  return %class_id%
}

func (*%class%) CLASS_NAME() string {
  return "%class%"
}

func (*%class%) CLASS_FIELDS_PROPS() *data.ClassFieldsProps {
  return &_%class%_fields_props
}

func (*%class%) CLASS_PROPS() *map[string]string {
  return &_%class%_class_props
}

func (*%class%) CLASS_FIELDS() []string {
  return _%class%_class_fields
}

func (self *%class%) I%class%() *%class% {
  return self
}

func (*%class%) GetCode() int32 {
  return %code%
}

func New%class%() *%class% {
  item := new (%class%)
  item.Reset()
  return item
}

func (self *%class%) Reset() {
  %fields_reset%
}

func (self *%class%) Read(reader data.Reader, field string) error {
  return meta.ReadStruct(self, reader, &field)
}

func (self *%class%) ReadFields(reader data.Reader) error {
  self.Reset()
  %read_buffer%
  return nil
}

func (self *%class%) Write(writer data.Writer, field string, assoc bool) error {
  return meta.WriteStruct(self, writer, &field, assoc)
}

func (self *%class%) WriteFields(writer data.Writer, assoc bool) error {
  %write_buffer%
  return nil
}

func (self *%class%) GetLoginTicket() string {
  return %login_ticket%
}

EOD;
    return $TPL;
  }

  function tpl_bundle()
  {
    $TPL = <<<EOD
package autogen
//THIS FILE IS GENERATED AUTOMATICALLY, DON'T TOUCH IT!
%bundle%

import (
  "fmt"
  "sort"
  "gme/meta"
  "gme/data"
)

//supress *imported but not used* warnings
var _ = fmt.Printf
var _ = sort.SearchInts

%units_src%

type IRPC interface {
%create_rpc%
}

func CreateReqPacket(code int32) (meta.IRpcPacket, error) {
  switch code {
%create_req_by_id%
    default : return nil, fmt.Errorf("Can't find packet for code %d", code)
  }
}

func CreateRspPacket(code int32) (meta.IRpcPacket, error) {
  switch code {
%create_rsp_by_id%
    default : return nil, fmt.Errorf("Can't find packet for code %d", code)
  }
}

func init() {

meta.CreateById = func(crc int) (meta.IMetaStruct, error) {
  switch crc {
%create_struct_by_crc28%
    default : return nil, fmt.Errorf("Can't find struct for crc %d", crc)
  }
}

meta.CreateByName = func(name string) (meta.IMetaStruct, error) {
  switch name {
%create_struct_by_name%
    default : return nil, fmt.Errorf("Can't find struct for name %s", name)
  }
}

}


EOD;
    
    return $TPL;
  }
}
