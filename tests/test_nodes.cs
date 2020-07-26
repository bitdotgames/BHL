using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using Antlr4.Runtime;
using bhl;

public class BHL_TestNodes : BHL_TestBase
{
  public class TestNode : BehaviorTreeTerminalNode
  {
    public BHS status = BHS.SUCCESS;
    public int execs;
    public int inits;
    public int deinits;
    public int defers;

    public TestEvents events;

    public override BHS execute()
    {
      if(events != null)
        events.Add("E", this);
      ++execs;
      return status;
    }

    public override void init()
    {
      if(events != null)
        events.Add("I", this);
      ++inits;
    }

    public override void deinit()
    {
      if(events != null)
        events.Add("D", this);
      ++deinits;
    }

    public override void defer()
    {
      if(events != null)
        events.Add("F", this);
      ++defers;
    }
  }

  public class DecoratorTestNode : BehaviorTreeDecoratorNode
  {
    public DecoratorTestNode(TestNode t)
    {
      setSlave(t);
    }
  }

  [IsTested()]
  public void TestNodeSequenceSuccess()
  {
    var t1 = new TestNode();
    var t2 = new TestNode();
    var s = new SequentialNode();
    s.children.Add(t1);
    s.children.Add(t2);

    t1.status = BHS.RUNNING;
    t2.status = BHS.RUNNING;

    AssertEqual(s.run(), BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 1);
    AssertEqual(t1.deinits, 0);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 0);
    AssertEqual(t2.execs, 0);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    t1.status = BHS.SUCCESS;
    t2.status = BHS.RUNNING;

    AssertEqual(s.run(), BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 1);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    t1.status = BHS.SUCCESS;
    t2.status = BHS.SUCCESS;

    AssertEqual(s.run(), BHS.SUCCESS);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 1);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 2);
    AssertEqual(t2.deinits, 1);
    AssertEqual(t2.defers, 1);
  }

  [IsTested()]
  public void TestNodeSequenceStop()
  {
    var t1 = new TestNode();
    var t2 = new TestNode();
    var s = new SequentialNode();
    s.children.Add(t1);
    s.children.Add(t2);

    t1.status = BHS.RUNNING;
    t2.status = BHS.RUNNING;

    AssertEqual(s.run(), BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 1);
    AssertEqual(t1.deinits, 0);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 0);
    AssertEqual(t2.execs, 0);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    t1.status = BHS.SUCCESS;
    t2.status = BHS.RUNNING;

    AssertEqual(s.run(), BHS.RUNNING);
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 0);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 1);
    AssertEqual(t2.deinits, 0);
    AssertEqual(t2.defers, 0);

    s.stop();
    AssertEqual(t1.inits, 1);
    AssertEqual(t1.execs, 2);
    AssertEqual(t1.deinits, 1);
    AssertEqual(t1.defers, 1);
    AssertEqual(t2.inits, 1);
    AssertEqual(t2.execs, 1);
    AssertEqual(t2.deinits, 1);
    AssertEqual(t2.defers, 1);
  }

  [IsTested()]
  public void TestDecoratorNode()
  {
    var t = new TestNode();
    t.status = BHS.RUNNING;
    var d = new DecoratorTestNode(t);

    AssertEqual(d.run(), BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);

    t.status = BHS.SUCCESS;
    AssertEqual(d.run(), BHS.SUCCESS);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 2);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 0);

    d.defer();
    AssertEqual(t.defers, 1);
  }

  public class TestEvents
  {
    public class Event
    {
      public string type;
      public BehaviorTreeNode node;
    }
    public List<Event> events = new List<Event>();

    public int Count {
      get {
        return events.Count;
      }
    }

    public Event this[int i]
    {
      get {
        return events[i];
      }
    }

    public void Add(string type, BehaviorTreeNode n)
    {
      var e = new Event();
      e.type = type;
      e.node = n;
      events.Add(e);
    }
  }

  [IsTested()]
  public void TestGroupNode()
  {
    var events = new TestEvents();

    var t = new TestNode();
    t.events = events;
    t.status = BHS.RUNNING;
    var g = new GroupNode();
    g.addChild(t);

    AssertEqual(g.run(), BHS.RUNNING);
    AssertEqual(events.Count, 2);
    AssertEqual(events[0].type, "I");
    AssertEqual(events[1].type, "E");

    t.status = BHS.SUCCESS;
    AssertEqual(g.run(), BHS.SUCCESS);
    AssertEqual(events.Count, 4);
    AssertEqual(events[0].type, "I");
    AssertEqual(events[1].type, "E");
    AssertEqual(events[2].type, "E");
    AssertEqual(events[3].type, "D");

    g.defer();
    AssertEqual(events.Count, 5);
    AssertEqual(events[0].type, "I");
    AssertEqual(events[1].type, "E");
    AssertEqual(events[2].type, "E");
    AssertEqual(events[3].type, "D");
    AssertEqual(events[4].type, "F");
  }

  [IsTested()]
  public void TestRunNodeWithSuccess()
  {
    var t = new TestNode();
    t.status = BHS.RUNNING;

    AssertEqual(t.run(), BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.RUNNING);

    t.status = BHS.SUCCESS;
    AssertEqual(t.run(), BHS.SUCCESS);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 2);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.SUCCESS);

    //run again
    AssertEqual(t.run(), BHS.SUCCESS);
    AssertEqual(t.inits, 2);
    AssertEqual(t.execs, 3);
    AssertEqual(t.deinits, 2);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.SUCCESS);
  }

  [IsTested()]
  public void TestRunNodeWithFailure()
  {
    var t = new TestNode();
    t.status = BHS.RUNNING;

    AssertEqual(t.run(), BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.RUNNING);

    t.status = BHS.FAILURE;
    AssertEqual(t.run(), BHS.FAILURE);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 2);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.FAILURE);

    //run again
    AssertEqual(t.run(), BHS.FAILURE);
    AssertEqual(t.inits, 2);
    AssertEqual(t.execs, 3);
    AssertEqual(t.deinits, 2);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.FAILURE);
  }

  [IsTested()]
  public void TestStopNode()
  {
    var t = new TestNode();
    t.status = BHS.RUNNING;

    AssertEqual(t.run(), BHS.RUNNING);
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 0);
    AssertEqual(t.defers, 0);
    AssertEqual(t.currStatus, BHS.RUNNING);

    t.stop();
    AssertEqual(t.inits, 1);
    AssertEqual(t.execs, 1);
    AssertEqual(t.deinits, 1);
    AssertEqual(t.defers, 1);
    AssertEqual(t.currStatus, BHS.NONE);
  }
}
