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

    Assert.Equal(BHS.RUNNING, s.Run());
    Assert.Equal(1, t1.inits);
    Assert.Equal(1, t1.execs);
    Assert.Equal(0, t1.deinits);
    Assert.Equal(0, t1.defers);
    Assert.Equal(0, t2.inits);
    Assert.Equal(0, t2.execs);
    Assert.Equal(0, t2.deinits);
    Assert.Equal(0, t2.defers);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(BHS.RUNNING, s.Run());
    Assert.Equal(1, t1.inits);
    Assert.Equal(2, t1.execs);
    Assert.Equal(1, t1.deinits);
    Assert.Equal(0, t1.defers);
    Assert.Equal(1, t2.inits);
    Assert.Equal(1, t2.execs);
    Assert.Equal(0, t2.deinits);
    Assert.Equal(0, t2.defers);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.SUCCESS;

    Assert.Equal(BHS.SUCCESS, s.Run());
    Assert.Equal(1, t1.inits);
    Assert.Equal(2, t1.execs);
    Assert.Equal(1, t1.deinits);
    Assert.Equal(1, t1.defers);
    Assert.Equal(1, t2.inits);
    Assert.Equal(2, t2.execs);
    Assert.Equal(1, t2.deinits);
    Assert.Equal(1, t2.defers);
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

    Assert.Equal(BHS.RUNNING, s.Run());
    Assert.Equal(1, t1.inits);
    Assert.Equal(1, t1.execs);
    Assert.Equal(0, t1.deinits);
    Assert.Equal(0, t1.defers);
    Assert.Equal(0, t2.inits);
    Assert.Equal(0, t2.execs);
    Assert.Equal(0, t2.deinits);
    Assert.Equal(0, t2.defers);

    t1.mock_status = BHS.SUCCESS;
    t2.mock_status = BHS.RUNNING;

    Assert.Equal(BHS.RUNNING, s.Run());
    Assert.Equal(1, t1.inits);
    Assert.Equal(2, t1.execs);
    Assert.Equal(1, t1.deinits);
    Assert.Equal(0, t1.defers);
    Assert.Equal(1, t2.inits);
    Assert.Equal(1, t2.execs);
    Assert.Equal(0, t2.deinits);
    Assert.Equal(0, t2.defers);

    s.Stop();
    Assert.Equal(1, t1.inits);
    Assert.Equal(2, t1.execs);
    Assert.Equal(1, t1.deinits);
    Assert.Equal(1, t1.defers);
    Assert.Equal(1, t2.inits);
    Assert.Equal(1, t2.execs);
    Assert.Equal(1, t2.deinits);
    Assert.Equal(1, t2.defers);
  }

  [Fact]
  public void TestDecoratorNode()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;
    var d = new DecoratorTestNode(t);

    Assert.Equal(BHS.RUNNING, d.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(1, t.execs);
    Assert.Equal(0, t.deinits);
    Assert.Equal(0, t.defers);

    t.mock_status = BHS.SUCCESS;
    Assert.Equal(BHS.SUCCESS, d.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(2, t.execs);
    Assert.Equal(1, t.deinits);
    Assert.Equal(0, t.defers);

    d.Defer();
    Assert.Equal(1, t.defers);
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

    Assert.Equal(BHS.RUNNING, t.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(1, t.execs);
    Assert.Equal(0, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.RUNNING, t.last_status);

    t.mock_status = BHS.SUCCESS;
    Assert.Equal(BHS.SUCCESS, t.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(2, t.execs);
    Assert.Equal(1, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.SUCCESS, t.last_status);

    //run again
    Assert.Equal(BHS.SUCCESS, t.Run());
    Assert.Equal(2, t.inits);
    Assert.Equal(3, t.execs);
    Assert.Equal(2, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.SUCCESS, t.last_status);
  }

  [Fact]
  public void TestRunNodeWithFailure()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;

    Assert.Equal(BHS.RUNNING, t.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(1, t.execs);
    Assert.Equal(0, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.RUNNING, t.last_status);

    t.mock_status = BHS.FAILURE;
    Assert.Equal(BHS.FAILURE, t.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(2, t.execs);
    Assert.Equal(1, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.FAILURE, t.last_status);

    //run again
    Assert.Equal(BHS.FAILURE, t.Run());
    Assert.Equal(2, t.inits);
    Assert.Equal(3, t.execs);
    Assert.Equal(2, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.FAILURE, t.last_status);
  }

  [Fact]
  public void TestStopNode()
  {
    var t = new TestNode();
    t.mock_status = BHS.RUNNING;

    Assert.Equal(BHS.RUNNING, t.Run());
    Assert.Equal(1, t.inits);
    Assert.Equal(1, t.execs);
    Assert.Equal(0, t.deinits);
    Assert.Equal(0, t.defers);
    Assert.Equal(BHS.RUNNING, t.last_status);

    t.Stop();
    Assert.Equal(1, t.inits);
    Assert.Equal(1, t.execs);
    Assert.Equal(1, t.deinits);
    Assert.Equal(1, t.defers);
    Assert.Equal(BHS.NONE, t.last_status);
  }
}
