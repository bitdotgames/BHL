grammar bhl;

program
  : declOrImport* EOF
  ;

declOrImport
  : decl
  | mimport
  ;

mimport
  : 'import' NORMALSTRING SEPARATOR*
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
  : '[' nsName ']' 
  ;

expList
  : exp (',' exp)*
  ;

returnVal
  //including var decls here for cases when 
  //parser improperly captures the next statement
  //which is not part of return but we need
  //to process that as well
  : (varDeclare | varDeclareAssign | expList)
  ;

name
  : GLOBAL? NAME
  ;

lambdaCall
  : funcLambda callArgs
  ;
  
chainExp
  : (name | '(' exp ')' | lambdaCall) chainExpItem*
  ;

exp
  : 'null'                                    #ExpLiteralNull
  | 'false'                                   #ExpLiteralFalse
  | 'true'                                    #ExpLiteralTrue
  | number                                    #ExpLiteralNum
  | string                                    #ExpLiteralStr
  | '(' type ')' exp                          #ExpTypeCast
  | chainExp                                  #ExpChain
  | funcLambda                                #ExpLambda
  | 'typeof' '(' type ')'                     #ExpTypeof
  | jsonObject                                #ExpJsonObj
  | jsonArray                                 #ExpJsonArr
  | 'yield' chainExp                          #ExpYieldCall
  | exp 'as' type                             #ExpAs
  | exp 'is' type                             #ExpIs
  | operatorUnary exp                         #ExpUnary
  | exp operatorBitwise exp                   #ExpBitwise
  | exp operatorMulDivMod exp                 #ExpMulDivMod
  | exp operatorAddSub exp                    #ExpAddSub
  | exp operatorComparison exp                #ExpCompare
  | exp operatorAnd exp                       #ExpAnd
  | exp operatorOr exp                        #ExpOr
  | exp ternaryIfExp                          #ExpTernaryIf
  | newExp                                    #ExpNew
  //TODO: move it under ExpChain?
  | chainExp '.'                              #ExpIncompleteMember
  | chainExp '(' (callArgsList ','?)?         #ExpIncompleteCall
  ;

ternaryIfExp
  : '?' exp ':' exp 
  ;

newExp
  : 'new' type
  ;

foreachExp
  : '(' varOrDeclare (',' varOrDeclare)* 'in' exp ')' 
  ;

forPreIter
  : varOrDeclareAssign (',' varOrDeclareAssign)*
  ;

forPostIter
  : varPostOp (',' varPostOp)*
  ;

forExp
  : '(' forPreIter? SEPARATOR exp SEPARATOR forPostIter? ')' 
  ;

postOp
  : assignExp | operatorIncDec | (operatorSelfOp exp)
  ;

//NOTE: statements, order is important
statement
  : ';'                                        #StmSeparator
  | chainExp postOp?                           #StmChainExp
  | varDeclare ( ',' varDeclare )* assignExp?  #StmDeclOptAssign
  | 'if' '(' exp ')' block elseIf* else?       #StmIf
  | 'while' '(' exp ')' block                  #StmWhile
  | 'do' block 'while' '(' exp ')'             #StmDoWhile
  | 'for' forExp block                         #StmFor
  | 'foreach' foreachExp block                 #StmForeach
  | 'yield' '(' ')'                            #StmYield                                                                   
  | 'yield' chainExp                           #StmYieldCall
  | 'yield' 'while' '(' exp ')'                #StmYieldWhile
  | 'break'                                    #StmBreak
  | 'continue'                                 #StmContinue
  | 'return' returnVal?                        #StmReturn
  | 'paral' block                              #StmParal
  | 'paral_all' block                          #StmParalAll
  | 'defer' block                              #StmDefer
  | block                                      #StmBlockNested
  ;

elseIf
  : 'else' 'if' '(' exp ')' block
  ;

else
  : 'else' block
  ;
  
chainExpItem
  : callArgs | memberAccess | arrAccess
  ;

arrAccess
  : ('[' exp ']')
  ;

memberAccess
  : '.' NAME
  ;
  
callArgs
  : '(' callArgsList? ')'
  ;

callArgsList
  :  callArg (',' callArg)*
  ;

callArg
  : VARIADIC? (NAME ':')? isRef? exp
  ;

block 
  : '{' statement* '}'
  | '{}'
  ;

extensions
  : ':' nsName (',' nsName)*
  ;

nsDecl
  : 'namespace' dotName '{' decl* '}'
  ;

classDecl
  : 'class' NAME extensions? classBlock
  ;

classBlock
  : '{' classMembers '}'
  | '{}'
  ;

classMembers
  : classMember*
  ;

fldAttribs
  : staticFlag
  ;

fldDeclare
  : fldAttribs* varDeclare
  ;

classMember
  : (fldDeclare | funcDecl | classDecl | enumDecl | interfaceDecl)
  ;

interfaceDecl
  : 'interface' NAME extensions? interfaceBlock
  ;

interfaceBlock
  : '{' interfaceMembers '}'
  | '{}'
  ;

interfaceMembers
  : interfaceMember*
  ;

interfaceMember
  : interfaceFuncDecl
  ;

enumDecl
  : 'enum' NAME enumBlock
  ;

enumBlock
  : '{' enumMember+ '}'
  ;

enumMember
  : NAME '=' INT
  ;

virtualFlag
  : 'virtual'
  ;

overrideFlag
  : 'override'
  ;

staticFlag
  : 'static'
  ;

coroFlag
  : 'coro'
  ;

funcAttribs
  : (coroFlag | virtualFlag | overrideFlag | staticFlag)
  ;

funcDecl
  : funcAttribs* 'func' retType? NAME '(' funcParams? ')' funcBlock
  ;

funcType
  : coroFlag? 'func' retType? '(' types? ')'
  ;

funcBlock
  : block
  ;

interfaceFuncDecl
  : coroFlag? 'func' retType? NAME '(' funcParams? ')'
  ;

funcLambda
  : coroFlag? 'func' retType? '(' funcParams? ')' funcBlock
  ;

refType
  : isRef? type
  ;

retType
  : type (',' type)*
  ;

types
  : refType (',' refType)*
  ;

funcParams
  : funcParamDeclare ( ',' funcParamDeclare )*
  ;

funcParamDeclare
  : isRef? VARIADIC? type NAME assignExp?
  ;

varDeclare
  : type NAME
  ;

varDeclareAssign
  : varDeclare assignExp
  ;

varDeclareOptAssign
  : varDeclare assignExp?
  ;

varOrDeclare
  : varDeclare | NAME
  ;

varOrDeclareAssign
  : varOrDeclare assignExp
  ;

varPostOp
  : chainExp postOp
  ;

assignExp
  : '=' exp
  ;
  
operatorOr 
  : '||'
  ;

operatorAnd 
  : '&&'
  ;

operatorBitwise
  : '|' | '&'
  ;

operatorIncDec
  : '++' | '--'
  ;

operatorSelfOp
  : '+=' | '-=' | '*=' | '/='
  ;

operatorComparison 
  : '<' | '>' | '<=' | '>=' | '!=' | '=='
  ;

operatorAddSub
  : '+' | '-'
  ;

operatorMulDivMod
  : '*' | '/' | '%'
  ;

operatorUnary
  : '!' | '-'
  ;

isRef
  : 'ref'
  ;

number
  : INT | HEX | FLOAT
  ;

string
  : NORMALSTRING
  ;

jsonObject
  :   newExp? '{' jsonPair (',' jsonPair)* '}'
  |   newExp? jsonEmptyObj
  ;

jsonEmptyObj
  : '{' '}'
  | '{}'
  ;

jsonPair
  : NAME ':' jsonValue 
  ;

jsonArray
  :  '[' jsonValue (',' jsonValue)* ']'
  |  jsonEmptyArr
  ;

jsonEmptyArr
  : ARR
  ;

jsonValue
  :  exp
  ;

////////////////////////////// lexer /////////////////////////////

NAME
  : [a-zA-Z_][a-zA-Z_0-9]*
  ;

ARR
  : '[' ']'
  ;

GLOBAL
  : '..'
  ;

VARIADIC
  : '...'
  ;

SEPARATOR
  : ';'
  ;
  
NORMALSTRING
  : '"' ( EscapeSequence | ~('\\'|'"') )* '"'  //" let's keep text editor happy
  ;

INT
  : Digit+
  ;

HEX
  : '0' [xX] HexDigit+
  ;

FLOAT
  : Digit+ '.' Digit* ExponentPart?
  | '.' Digit+ ExponentPart?
  | Digit+ ExponentPart
  ;

fragment
ExponentPart
  : [eE] [+-]? Digit+
  ;

fragment
EscapeSequence
  : '\\' [abfnrtvz"'\\] //" let's keep text editor happy
  | '\\' '\r'? '\n'
  ;

fragment
Digit
  : [0-9]
  ;

fragment
HexDigit
  : [0-9a-fA-F]
  ;

//comments
SINGLE_LINE_COMMENT 
  : '//' ~[\r\n]* -> channel(HIDDEN)
  ;

DELIMITED_COMMENT 
  :   '/*' .*? '*/' -> channel(HIDDEN)
  ;

//white space
WS
  : [ \r\t\u000C\n]+ -> channel(HIDDEN)
  ;



