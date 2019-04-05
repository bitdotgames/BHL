using System;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;

public static class BHL
{
  public static void Main(string[] args)
  {
    var tm = new Taskman(typeof(Tasks));
    tm.Run(args);
  }
}

public class Taskman
{
  List<MethodInfo> tasks = new List<MethodInfo>();

  public Taskman(Type tasks_class)
  {
    foreach(var method in tasks_class.GetMethods())
    {
      if(IsTask(method))
        tasks.Add(method);
    }
  }

  static bool IsTask(MemberInfo member)
  {
    foreach(var attribute in member.GetCustomAttributes(true))
    {
      if(attribute is TaskAttribute)
        return true;
    }
    return false;
  }

  public void Run(string[] args)
  {
    if(args.Length == 0)
      return;

    if(tasks.Count == 0)
      return;
    
    var task = FindTask(args[0]);
    if(task == null)
      return;

    task.Invoke(null, new object[] { this });
  }

  public MethodInfo FindTask(string name)
  {
    foreach(var t in tasks)
    {
      if(t.Name == name)
        return t;
    }
    return null;
  }
}

public class TaskAttribute : Attribute
{
}

public class Tasks
{
  [Task()]
  public static void hello(Taskman tm)
  {
    Console.WriteLine("Hello!");
  }
}

