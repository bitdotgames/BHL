grammar bhl;

program
  : progblock* EOF
  ;

progblock
  : imports? decls
  ;

imports 
  : mimport+
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
  : DOT? dotName
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
  : 'null'                                                  #ExpLiteralNull
  | 'false'                                                 #ExpLiteralFalse
  | 'true'                                                  #ExpLiteralTrue
  | number                                                  #ExpLiteralNum
  | string                                                  #ExpLiteralStr
  | 'yield' funcCallExp                                     #ExpYieldCall
  | callExp                                                 #ExpCall
  | typeof                                                  #ExpTypeof
  | jsonObject                                              #ExpJsonObj
  | jsonArray                                               #ExpJsonArr
  | funcLambda                                              #ExpLambda
  | '(' type ')' exp                                        #ExpTypeCast
  | exp 'as' type                                           #ExpAs
  | exp 'is' type                                           #ExpIs
  | operatorUnary exp                                       #ExpUnary
  | '(' exp ')' chainExp*                                   #ExpParen
  | exp operatorBitAnd exp                                  #ExpBitAnd
  | exp operatorBitOr exp                                   #ExpBitOr
  | exp operatorMulDivMod exp                               #ExpMulDivMod
  | exp operatorAddSub exp                                  #ExpAddSub
  | exp operatorComparison exp                              #ExpCompare
  | exp operatorAnd exp                                     #ExpAnd
  | exp operatorOr exp                                      #ExpOr
  | newExp                                                  #ExpNew
  | exp ternaryIfExp                                        #ExpTernaryIf
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

forStmt
  : (varsDeclareOrCallExps assignExp) | callPostIncDec
  ;

forStmts
  : forStmt (',' forStmt)*
  ;

forPre
  : forStmts
  ;

forCond
  : exp
  ;

forPostIter
  : forStmts
  ;

forExp
  : '(' forPre? SEPARATOR forCond SEPARATOR forPostIter? ')' 
  ;

varDeclareAssign
  : varDeclare assignExp?
  ;

callPostIncDec
  : callExp (incrementOperator | decrementOperator)
  ;

incrementOperator
  : '++'
  ;

decrementOperator
  : '--'
  ;

varsDeclareAssign
  : varsDeclareOrCallExps assignExp
  ;

//statements
statement
  : funcLambda                                                  #LambdaCall
  | varsDeclareAssign                                           #DeclAssign
  | varDeclare                                                  #VarDecl
  | callExp operatorPostOpAssign exp                            #VarPostOpAssign
  | callPostIncDec                                              #VarPostIncDec
  | callExp                                                     #SymbCall
  | mainIf elseIf* else?                                        #If
  | 'while' '(' exp ')' block                                   #While
  | 'do' block 'while' '(' exp ')'                              #DoWhile
  | 'for' forExp block                                          #For
  | 'foreach' foreachExp block                                  #Foreach
  | 'yield' '(' ')'                                             #Yield
  | 'yield' funcCallExp                                         #YieldFunc
  | 'yield' funcLambda                                          #YieldLambdaCall
  | 'yield' 'while' '(' exp ')'                                 #YieldWhile
  | 'break'                                                     #Break
  | 'continue'                                                  #Continue
  | 'return' returnVal?                                         #Return
  | 'paral' block                                               #Paral
  | 'paral_all' block                                           #ParalAll
  | 'defer' block                                               #Defer
  | block                                                       #BlockNested
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

//vars && funcs
callExp
  : DOT? NAME chainExp* 
  ;

chainExp
  : callArgs | memberAccess | arrAccess
  ;

funcCallExp
  : callExp callArgs
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

asyncFlag
  : 'async'
  ;

funcAttribs
  : (virtualFlag | overrideFlag | staticFlag)
  ;

funcDecl
  : asyncFlag? 'func' funcAttribs* retType? NAME '(' funcParams? ')' funcBlock
  ;

funcType
  : asyncFlag? 'func' retType? '(' types? ')'
  ;

funcBlock
  : block
  ;

interfaceFuncDecl
  : asyncFlag? 'func' retType? NAME '(' funcParams? ')'
  ;

funcLambda
  : asyncFlag? 'func' retType? '(' funcParams? ')' funcBlock chainExp*
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

varsDeclare
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

varsDeclareOrCallExps
  : varDeclareOrCallExp ( ',' varDeclareOrCallExp )*
  ;

varDeclareOrCallExp
  : varDeclare | callExp
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

DOT
  : '.'
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

