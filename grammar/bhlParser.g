parser grammar bhlParser;

options {
    tokenVocab=bhlLexer;
    superClass=bhlParserBase;
}

program
  : declOrImport* EOF
  ;

declOrImport
  : decl
  | mimport
  ;

mimport
  : IMPORT NORMALSTRING SEMI*
  ;

decl
  : (nsDecl | classDecl | interfaceDecl | funcDecl | varDeclareOptAssign | enumDecl)
  ;

dotName
 : NAME memberAccess*
 ;

nsName
  : GLOBAL? dotName
  ;

type
  : (ARR | mapType)? (nsName | funcType) 
  ;

mapType
  : OPEN_BRACKET nsName CLOSE_BRACKET
  ;

expList
  : exp (COMMA exp)*
  ;

name
  : GLOBAL? NAME
  ;

chainExp
  : (name | OPEN_PAREN exp CLOSE_PAREN | funcLambda) chainExpItem*
  ;

exp
  : NULL                                      #ExpLiteralNull
  | FALSE                                     #ExpLiteralFalse
  | TRUE                                      #ExpLiteralTrue
  | number                                    #ExpLiteralNum
  | string                                    #ExpLiteralStr
  | OPEN_PAREN type CLOSE_PAREN exp           #ExpTypeCast
  | chainExp                                  #ExpChain
  | funcLambda                                #ExpLambda
  | TYPEOF OPEN_PAREN type CLOSE_PAREN        #ExpTypeof
  | jsonObject                                #ExpJsonObj
  | jsonArray                                 #ExpJsonArr
  | YIELD chainExp                            #ExpYieldCall
  | exp AS type                               #ExpAs
  | exp IS type                               #ExpIs
  | operatorUnary exp                         #ExpUnary
  | exp operatorBitwise exp                   #ExpBitwise
  | exp operatorMulDivMod exp                 #ExpMulDivMod
  | exp operatorAddSub exp                    #ExpAddSub
  | exp operatorComparison exp                #ExpCompare
  | exp LAND exp                              #ExpLogicalAnd
  | exp LOR exp                               #ExpLogicalOr
  | exp ternaryIfExp                          #ExpTernaryIf
  | newExp                                    #ExpNew
  //TODO: move it under ExpChain?
  //| chainExp '.'                              #ExpIncompleteMember
  //| chainExp '(' (callArgsList ','?)?         #ExpIncompleteCall
  ;

ternaryIfExp
  : QUESTION exp COLON exp 
  ;

newExp
  //TODO: notice a quick hack for jsonArray initializer to force it start on the same line as 'new' operator,
  //      currently it's done this way to resolve the following ambiguity:
  //      var tmp = new Foo <--- need 'notLineTerminator' predicate to remove 'new Foo []' ambiguity
  //      []int a = [] 
  : NEW type (jsonObject | ({this.notLineTerminator()}? jsonArray))?
  ;

foreachExp
  : OPEN_PAREN varOrDeclare (COMMA varOrDeclare)* IN exp CLOSE_PAREN
  ;

forPreIter
  : varOrDeclareAssign (COMMA varOrDeclareAssign)*
  ;

forPostIter
  : expModifyOp (COMMA expModifyOp)*
  ;

forExp
  : OPEN_PAREN forPreIter? SEMI exp SEMI forPostIter? CLOSE_PAREN
  ;

//NOTE: statements, order is important
statement
  : SEMI                                                         #StmSeparator
  | varDeclareList ({this.notLineTerminator()}? assignExp)? eos  #StmDeclOptAssign
  //int a, c.r = foo()
  | varDeclaresOrChainExps assignExp eos                         #StmDeclOrExpAssign
  | YIELD OPEN_PAREN CLOSE_PAREN eos                             #StmYield                                                                   
  | YIELD chainExp eos                                           #StmYieldCall
  | YIELD WHILE OPEN_PAREN exp CLOSE_PAREN                       #StmYieldWhile
  //func call or variable/member read/write access
  | chainExp modifyOp? eos                                       #StmChainExp
  | IF OPEN_PAREN exp CLOSE_PAREN block elseIf* else?            #StmIf
  | WHILE OPEN_PAREN exp CLOSE_PAREN block                       #StmWhile
  | DO block WHILE OPEN_PAREN exp CLOSE_PAREN eos                #StmDoWhile
  | FOR forExp block                                             #StmFor
  | FOREACH foreachExp block                                     #StmForeach
  | BREAK eos                                                    #StmBreak
  | CONTINUE eos                                                 #StmContinue
  | RETURN ({this.notLineTerminator()}? expList)? eos            #StmReturn
  | PARAL block                                                  #StmParal
  | PARAL_ALL block                                              #StmParalAll
  | DEFER block                                                  #StmDefer
  | block                                                        #StmBlockNested
  ;

elseIf
  : ELSE IF OPEN_PAREN exp CLOSE_PAREN block
  ;

else
  : ELSE block
  ;
  
chainExpItem
  : callArgs | memberAccess | arrAccess
  ;

arrAccess
  : ({this.notLineTerminator()}? OPEN_BRACKET exp CLOSE_BRACKET)
  ;

memberAccess
  : DOT NAME
  ;
  
callArgs
  : OPEN_PAREN callArgsList? CLOSE_PAREN
  ;

callArgsList
  :  callArg (COMMA callArg)*
  ;

callArg
  : VARIADIC? (NAME COLON)? REF? exp
  ;

block 
  : OPEN_BRACE statement* CLOSE_BRACE
  ;

extensions
  : COLON nsName (COMMA nsName)*
  ;

nsDecl
  : NAMESPACE dotName OPEN_BRACE decl* CLOSE_BRACE
  ;

classDecl
  : CLASS NAME extensions? classBlock
  ;

classBlock
  : OPEN_BRACE classMembers CLOSE_BRACE
  ;

classMembers
  : classMember*
  ;

fldAttribs
  : STATIC
  ;

fldDeclare
  : fldAttribs* varDeclare
  ;

classMember
  : (fldDeclare | funcDecl | classDecl | enumDecl | interfaceDecl)
  ;

interfaceDecl
  : INTERFACE NAME extensions? interfaceBlock
  ;

interfaceBlock
  : OPEN_BRACE interfaceMembers CLOSE_BRACE
  ;

interfaceMembers
  : interfaceMember*
  ;

interfaceMember
  : interfaceFuncDecl
  ;

enumDecl
  : ENUM NAME enumBlock
  ;

enumBlock
  : OPEN_BRACE enumMember+ CLOSE_BRACE
  ;

enumMember
  : NAME ASSIGN MINUS? INT
  ;

funcAttribs
  : (CORO | VIRTUAL | OVERRIDE | STATIC)
  ;

funcDecl
  : funcAttribs* FUNC retType? NAME OPEN_PAREN funcParams? CLOSE_PAREN funcBlock
  ;

funcType
  : CORO? FUNC retType? OPEN_PAREN types? CLOSE_PAREN
  ;

funcBlock
  : block
  ;

interfaceFuncDecl
  : CORO? FUNC retType? NAME OPEN_PAREN funcParams? CLOSE_PAREN
  ;

funcLambda
  : CORO? FUNC retType? OPEN_PAREN funcParams? CLOSE_PAREN captureList? funcBlock
  ;

refType
  : REF? type
  ;

retType
  : type (COMMA type)*
  ;

captureList
  : OPEN_BRACKET NAME (COMMA NAME)* CLOSE_BRACKET 
  ;

types
  : refType (COMMA refType)*
  ;

funcParams
  : funcParamDeclare (COMMA funcParamDeclare)*
  ;

funcParamDeclare
  : REF? VARIADIC? type NAME assignExp?
  ;

varDeclare
  : type NAME
  ;

varDeclareList
  : varDeclare (COMMA varDeclare)*
  ;

varDeclareOptAssign
  : STATIC? varDeclare assignExp? eos
  ;

varOrDeclare
  : varDeclare | NAME
  ;

varOrDeclareAssign
  : varOrDeclare assignExp
  ;

varDeclareOrChainExp
  : varDeclare 
  | chainExp
  ;

varDeclaresOrChainExps
  : varDeclareOrChainExp (COMMA varDeclareOrChainExp)*
  ;

modifyOp
  : assignExp | operatorIncDec | (operatorSelfOp exp)
  ;

expModifyOp
  : chainExp modifyOp
  ;

assignExp
  : ASSIGN exp
  ;
  
operatorBitwise
  : BOR | BAND
  ;

operatorIncDec
  : INC | DEC
  ;

operatorSelfOp
  : SINC | SDEC | SMUL | SDIV
  ;

operatorComparison 
  : LT | GT | LTE | GTE | NEQ | EQ
  ;

operatorAddSub
  : PLUS | MINUS
  ;

operatorMulDivMod
  : MUL | DIV | MOD
  ;

operatorUnary
  : NOT | MINUS
  ;

number
  : INT | HEX | FLOAT
  ;

string
  : NORMALSTRING
  ;

jsonObject
  :   OPEN_BRACE jsonPair (COMMA jsonPair)* CLOSE_BRACE
  |   jsonEmptyObj
  ;

jsonEmptyObj
  : OPEN_BRACE CLOSE_BRACE
  ;

jsonPair
  : NAME COLON jsonValue 
  ;

jsonArray
  :  OPEN_BRACKET jsonValue (COMMA jsonValue)* CLOSE_BRACKET
  |  jsonEmptyArr
  ;

jsonEmptyArr
  : ARR
  ;

jsonValue
  :  exp
  ;

eos
  : SEMI
  | EOF
  | {this.lineTerminator()}?
  | {this.closeBrace()}?
  ;
