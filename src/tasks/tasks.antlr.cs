namespace bhl {

public static partial class Tasks
{
  [Task(deps: "geng")]
  public static void regen(Taskman tm, string[] args)
  {}

  [Task]
  public static void geng(Taskman tm, string[] args)
  {
    tm.Rm($"{BHL_ROOT}/tmp");
    tm.Mkdir($"{BHL_ROOT}/tmp");

    tm.Copy($"{BHL_ROOT}/grammar/bhlPreprocLexer.g", $"{BHL_ROOT}/tmp/bhlPreprocLexer.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlPreprocParser.g", $"{BHL_ROOT}/tmp/bhlPreprocParser.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlLexer.g", $"{BHL_ROOT}/tmp/bhlLexer.g");
    tm.Copy($"{BHL_ROOT}/grammar/bhlParser.g", $"{BHL_ROOT}/tmp/bhlParser.g");
    tm.Copy($"{BHL_ROOT}/util/g4sharp", $"{BHL_ROOT}/tmp/g4sharp");

    tm.Shell("sh", $"-c 'cd {BHL_ROOT}/tmp && sh g4sharp *.g && cp bhl*.cs ../src/g/' ");
  }
}

}