// +build ignore

package main

import (
  . "./autogen"
  "bit.games/data"
  "fmt"
  "encoding/json"
  "reflect"
  "errors"
)

func checkErr(err error) {
  if err != nil {
    panic(err)
  }
}

func checkEqual(v, n interface{}) {
  if(!reflect.DeepEqual(v, n) || v != n) {
    panic(errors.New(fmt.Sprintf("%v!=%v", v, n)))
  }
}

func testReadPlayer() {
  player0 := new(DataPlayer)
  player0.Id = 10;
  player0.Version = "v1";
  player0.Gold = 255;

  writer := data.NewJsonWriter()
  err := player0.Write(writer, "", true /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  fmt.Printf("%s\n", bytes)

  reader, err := data.NewJsonReader(bytes)
  checkErr(err)

  player := new (DataPlayer)
  err = player.Read(reader, "")
  checkErr(err)

  checkEqual(player.Id, uint32(10))
  checkEqual(player.Version, "v1")
  checkEqual(player.Gold, player0.Gold)
}

func testReadExtends() {
  const json_str = `{"id":11,"title":"Chemist's","floors":1000}`
  reader, err := data.NewJsonReader(json.RawMessage(json_str))
  checkErr(err)

  building := new (ProtoBuilding)
  err = building.Read(reader, "")
  checkErr(err)

  checkEqual(building.Id, uint32(11))
  checkEqual(building.Title, "Chemist's")
  checkEqual(building.Floors, int32(1000))
}

func testReadArray() {
  player0 := new(DataPlayer)
  player0.Id = 1
  player0.Version = "Mike"
  player0.Gold = 5
  writer := data.NewJsonWriter()
  err := player0.Write(writer, "", false /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  if string(bytes) != "[1,\"Mike\",5]" {
    panic(fmt.Errorf("Wrong data %s", bytes))
  }

  reader, err := data.NewJsonReader(bytes)
  checkErr(err)

  player := new (DataPlayer)
  err = player.Read(reader, "")
  checkErr(err)

  checkEqual(player.Id, uint32(1))
  checkEqual(player.Version, "Mike")
  checkEqual(player.Gold, uint8(5))
}

func testReadSubObject() {
  man0 := new (ProtoMan)
  man0.Left_hand.Fingers = 4
  man0.Right_hand.Fingers = 5

  writer := data.NewJsonWriter()
  err := man0.Write(writer, "", false /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  reader, err := data.NewJsonReader(json.RawMessage(bytes))
  checkErr(err)

  man := new (ProtoMan)
  err = man.Read(reader, "")
  checkErr(err)

  checkEqual(man.Left_hand.Fingers, uint32(4))
  checkEqual(man.Right_hand.Fingers, uint32(5))
}

func testReadFieldArray() {
  tags0 := new (ProtoTags)
  tags0.Tags = make([]string, 2)
  tags0.Tags[0] = "tag1"
  tags0.Tags[1] = "tag2"

  tags0.Children = make([]ProtoBase, 2)
  tags0.Children[0].Id = 1
  tags0.Children[1].Title = "Child #2"

  writer := data.NewJsonWriter()
  err := tags0.Write(writer, "", false /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  reader, err := data.NewJsonReader(json.RawMessage(bytes))
  checkErr(err)

  tags := new (ProtoTags)
  err = tags.Read(reader, "")
  checkErr(err)

  checkEqual(len(tags.Tags), 2)
  checkEqual(tags.Tags[0], "tag1")
  checkEqual(tags.Tags[1], "tag2")

  checkEqual(len(tags.Children), 2)
  checkEqual(tags.Children[0].Id, uint32(1))
  checkEqual(tags.Children[1].Title, "Child #2")
}

func testReadVirtual() {
  rsp0 := new(RPC_RSP_GET_ALL_PROTO)
  rsp0.List = make([]IProtoBase, 2)
  rsp0.List[0] = new(ProtoBase)
  rsp0.List[1] = new(ProtoBuilding)
  rsp0.List[1].(*ProtoBuilding).Floors = 5

  writer := data.NewJsonWriter()
  err := rsp0.Write(writer, "", false /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  fmt.Printf("%s\n", bytes)

  reader, err := data.NewJsonReader(json.RawMessage(bytes))
  checkErr(err)

  rsp := new(RPC_RSP_GET_ALL_PROTO)
  err = rsp.Read(reader, "")
  checkErr(err)

  checkEqual(rsp.List[0].CLASS_ID(), ProtoBase_CLASS_ID())
  checkEqual(rsp.List[1].CLASS_ID(), ProtoBuilding_CLASS_ID())
  building := rsp.List[1].(*ProtoBuilding)
  checkEqual(building.Floors, int32(5))
}

func testReadEnum() {
  stock0 := new(ConfStock)
  stock0.Id = EnumStock_XP

  writer := data.NewJsonWriter()
  err := stock0.Write(writer, "", false /* assoc */)
  bytes, err := writer.GetData()
  checkErr(err)

  reader, err := data.NewJsonReader(json.RawMessage(bytes))
  checkErr(err)

  stock := new(ConfStock)
  err = stock.Read(reader, "")
  checkErr(err)

  checkEqual(stock.Id, EnumStock_XP)

  stock_id, err := NewEnumStockByName("GOLD")
  checkErr(err)
  checkEqual(stock_id, EnumStock_GOLD)
}

func main() {
  testReadPlayer()
  testReadExtends()
  testReadArray()
  testReadSubObject()
  testReadFieldArray()
  testReadVirtual()
  testReadEnum()
  fmt.Printf("success\n")
}
