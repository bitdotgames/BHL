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

decls 
  : decl+
  ;

decl
  : (nsDecl | classDecl | interfaceDecl | funcDecl | varDeclareAssign | enumDecl)
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

exps
  : exp (',' exp)*
  ;

returnVal
  //including var decls here for cases when 
  //parser improperly captures the next statement
  //which is not part of return but we need
  //to process that as well
  : (varDeclare | varsDeclareAssign | exps)
  ;

exp
  : 'null'                                   #ExpLiteralNull
  | 'false'                                  #ExpLiteralFalse
  | 'true'                                   #ExpLiteralTrue
  | number                                   #ExpLiteralNum
  | string                                   #ExpLiteralStr
  | name                                     #ExpName
  | 'yield' funcCallExp                      #ExpYieldCall
  | exp chainExpItem* callArgs               #ExpCall
  | exp chainExpItem* memberAccess           #ExpMemberAccess
  //| '(' exp ')' chainExp*                  #ExpParenChain
  //| 'yield' '(' exp ')' chainExp+ callArgs #ExpYieldParen
  | typeof                                   #ExpTypeof
  | jsonObject                               #ExpJsonObj
  | jsonArray                                #ExpJsonArr
  | funcLambda                               #ExpLambda
  | 'yield' funcLambda                       #ExpYieldLambda
  | '(' type ')' exp                         #ExpTypeCast
  | exp 'as' type                            #ExpAs
  | exp 'is' type                            #ExpIs
  | operatorUnary exp                        #ExpUnary
  | exp operatorBitAnd exp                   #ExpBitAnd
  | exp operatorBitOr exp                    #ExpBitOr
  | exp operatorMulDivMod exp                #ExpMulDivMod
  | exp operatorAddSub exp                   #ExpAddSub
  | exp operatorComparison exp               #ExpCompare
  | exp operatorAnd exp                      #ExpAnd
  | exp operatorOr exp                       #ExpOr
  | exp ternaryIfExp                         #ExpTernaryIf
  | newExp                                   #ExpNew
  | '(' exp ')'                              #ExpParen
  ;

ternaryIfExp
  : '?' exp ':' exp 
  ;

newExp
  : 'new' type
  ;

foreachExp
  : '(' varOrDeclares 'in' exp ')' 
  ;

forInsideStmnt
  : varDeclareAssign | varPostIncDec
  ;

forInsideStmnts
  : forInsideStmnt (',' forInsideStmnt)*
  ;

forPreIter
  : forInsideStmnts
  ;

forCond
  : exp
  ;

forPostIter
  : forInsideStmnts
  ;

forExp
  : '(' forPreIter? SEPARATOR forCond SEPARATOR forPostIter? ')' 
  ;

varDeclareAssign
  : varDeclare assignExp?
  ;

varPostIncDec
  : varAccessExp (incrementOperator | decrementOperator)
  ;

incrementOperator
  : '++'
  ;

decrementOperator
  : '--'
  ;

varsDeclareAssign
  : varsDeclares assignExp?
  ;

//statements
statement
  : funcLambda                                 #LambdaCall
  | varsDeclareAssign                          #DeclAssign
  | varAccessExp assignExp                     #VarAccessAssign
  | varAccessExp operatorPostOpAssign exp      #VarPostOpAssign
  | varPostIncDec                              #VarIncDec
  | chainedExp                                 #UselessAccess
  | funcCallExp                                #FuncCall
  | varAccessExp                               #UselessVarAccess
  | mainIf elseIf* else?                       #If
  | 'while' '(' exp ')' block                  #While
  | 'do' block 'while' '(' exp ')'             #DoWhile
  | 'for' forExp block                         #For
  | 'foreach' foreachExp block                 #Foreach
  | 'yield' '(' ')'                            #Yield
  | 'yield' funcCallExp                        #YieldFunc
  | 'yield' funcLambda                         #YieldLambdaCall
  | 'yield' 'while' '(' exp ')'                #YieldWhile
  | 'break'                                    #Break
  | 'continue'                                 #Continue
  | 'return' returnVal?                        #Return
  | 'paral' block                              #Paral
  | 'paral_all' block                          #ParalAll
  | 'defer' block                              #Defer
  | block                                      #BlockNested
  ;

mainIf
  : 'if' '(' exp ')' block 
  ;

elseIf
  : 'else' 'if' '(' exp ')' block
  ;

else
  : 'else' block
  ;
  

//foo()
//bar.foo()
//((bar[0]).foo()).hey
//(((bar[0]).foo()).hey)
chainedExp
  : exp chainExpItem*
  ;

chainExpItem
  : callArgs | memberAccess | arrAccess
  ;

funcCallExp
  : chainedExp callArgs
  ;

name 
  : GLOBAL? NAME
  ;
  
varAccessExp
  : name
  | memberAccessExp
  ;

memberAccessExp
  : chainedExp memberAccess
  ;

typeof 
  : 'typeof' '(' type ')'
  ;

arrAccess
  : ('[' exp ']')
  ;

memberAccess
  : '.' NAME
  ;
  
callArgs
  : '(' callArg? (',' callArg)* ')'
  ;

callArg
  : VARIADIC? (NAME ':')? isRef? exp
  ;

block 
  : '{' (statement SEPARATOR*)* '}'
  | '{}'
  ;

extensions
  : ':' nsName (',' nsName)*
  ;

nsDecl
  : 'namespace' dotName '{' decls '}'
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
  : coroFlag? 'func' retType? '(' funcParams? ')' funcBlock chainExpItem*
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

varsDeclares
  : varDeclare ( ',' varDeclare )*
  ;

varDeclare
  : type NAME
  ;

varOrDeclare
  : NAME | varDeclare
  ;

varOrDeclares
  : varOrDeclare (',' varOrDeclare)*
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

operatorBitOr 
  : '|'
  ;

operatorBitAnd 
  : '&'
  ;

operatorPostOpAssign
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


