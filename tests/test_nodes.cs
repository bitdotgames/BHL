using System.Collections.Generic;
using bhl;
using Xunit;

public class TestNodes : BHL_TestBase
{
  public class TestNode : BehaviorTreeTerminalNode
  {
    public BHS mock_status = BHS.SUCCESS;
    public int execs;
    public int inits;
    public int deinits;
    public int defers;

    public TestEvents events;

    public override BHS Execute()
    {
      if(events != null)
        events.Add("E", this);
      ++execs;
      return mock_status;
    }

    public override void Init()
    {
      if(events != null)
        events.Add("I", this);
      ++inits;
    }

    public override void Deinit()
    {
      if(events != null)
        events.Add("D", this);
      ++deinits;
    }

    public override void Defer()
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
      SetSlave(t);
    }
  }

  [Fact]
  public void TestNodeSequenceSuccess()
  {
    var t1 = new TestNode();
    var t2 = new TestNode();
    var s = new SequentialNode();
    s.children.Add(t1);
    s.children.Add(t2);

    t1.mock_status = BHS.RUNNING;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(s.Run(), BHS.RUNNING);
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 1);
    Assert.Equal(t1.deinits, 0);
    Assert.Equal(t1.defers, 0);
    Assert.Equal(t2.inits, 0);
    Assert.Equal(t2.execs, 0);
    Assert.Equal(t2.deinits, 0);
    Assert.Equal(t2.defers, 0);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(s.Run(), BHS.RUNNING);
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 2);
    Assert.Equal(t1.deinits, 1);
    Assert.Equal(t1.defers, 0);
    Assert.Equal(t2.inits, 1);
    Assert.Equal(t2.execs, 1);
    Assert.Equal(t2.deinits, 0);
    Assert.Equal(t2.defers, 0);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.SUCCESS;

    Assert.Equal(s.Run(), BHS.SUCCESS);
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 2);
    Assert.Equal(t1.deinits, 1);
    Assert.Equal(t1.defers, 1);
    Assert.Equal(t2.inits, 1);
    Assert.Equal(t2.execs, 2);
    Assert.Equal(t2.deinits, 1);
    Assert.Equal(t2.defers, 1);
  }

  [Fact]
  public void TestNodeSequenceStop()
  {
    var t1 = new TestNode();
    var t2 = new TestNode();
    var s = new SequentialNode();
    s.children.Add(t1);
    s.children.Add(t2);

    t1.mock_status = BHS.RUNNING;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(s.Run(), BHS.RUNNING);
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 1);
    Assert.Equal(t1.deinits, 0);
    Assert.Equal(t1.defers, 0);
    Assert.Equal(t2.inits, 0);
    Assert.Equal(t2.execs, 0);
    Assert.Equal(t2.deinits, 0);
    Assert.Equal(t2.defers, 0);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(s.Run(), BHS.RUNNING);
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 2);
    Assert.Equal(t1.deinits, 1);
    Assert.Equal(t1.defers, 0);
    Assert.Equal(t2.inits, 1);
    Assert.Equal(t2.execs, 1);
    Assert.Equal(t2.deinits, 0);
    Assert.Equal(t2.defers, 0);

    s.Stop();
    Assert.Equal(t1.inits, 1);
    Assert.Equal(t1.execs, 2);
    Assert.Equal(t1.deinits, 1);
    Assert.Equal(t1.defers, 1);
    Assert.Equal(t2.inits, 1);
    Assert.Equal(t2.execs, 1);
    Assert.Equal(t2.deinits, 1);
    Assert.Equal(t2.defers, 1);
  }

  [Fact]
  public void TestDecoratorNode()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;
    var d = new DecoratorTestNode(t);

    Assert.Equal(d.Run(), BHS.RUNNING);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 1);
    Assert.Equal(t.deinits, 0);
    Assert.Equal(t.defers, 0);

    t.mock_status = BHS.SUCCESS;
    Assert.Equal(d.Run(), BHS.SUCCESS);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 2);
    Assert.Equal(t.deinits, 1);
    Assert.Equal(t.defers, 0);

    d.Defer();
    Assert.Equal(t.defers, 1);
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

  [Fact]
  public void TestRunNodeWithSuccess()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;

    Assert.Equal(t.Run(), BHS.RUNNING);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 1);
    Assert.Equal(t.deinits, 0);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.RUNNING);

    t.mock_status = BHS.SUCCESS;
    Assert.Equal(t.Run(), BHS.SUCCESS);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 2);
    Assert.Equal(t.deinits, 1);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.SUCCESS);

    //run again
    Assert.Equal(t.Run(), BHS.SUCCESS);
    Assert.Equal(t.inits, 2);
    Assert.Equal(t.execs, 3);
    Assert.Equal(t.deinits, 2);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.SUCCESS);
  }

  [Fact]
  public void TestRunNodeWithFailure()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;

    Assert.Equal(t.Run(), BHS.RUNNING);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 1);
    Assert.Equal(t.deinits, 0);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.RUNNING);

    t.mock_status = BHS.FAILURE;
    Assert.Equal(t.Run(), BHS.FAILURE);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 2);
    Assert.Equal(t.deinits, 1);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.FAILURE);

    //run again
    Assert.Equal(t.Run(), BHS.FAILURE);
    Assert.Equal(t.inits, 2);
    Assert.Equal(t.execs, 3);
    Assert.Equal(t.deinits, 2);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.FAILURE);
  }

  [Fact]
  public void TestStopNode()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;

    Assert.Equal(t.Run(), BHS.RUNNING);
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 1);
    Assert.Equal(t.deinits, 0);
    Assert.Equal(t.defers, 0);
    Assert.Equal(t.last_status, BHS.RUNNING);

    t.Stop();
    Assert.Equal(t.inits, 1);
    Assert.Equal(t.execs, 1);
    Assert.Equal(t.deinits, 1);
    Assert.Equal(t.defers, 1);
    Assert.Equal(t.last_status, BHS.NONE);
  }
}
