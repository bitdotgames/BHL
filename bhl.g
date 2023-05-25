grammar bhl;

program
  : declOrImport* EOF
  ;

declOrImport
  : decl
  | mimport
  ;

mimport
  : IMPORT NORMALSTRING SEPARATOR*
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

chainExp
  : (name | '(' exp ')' | funcLambda) chainExpItem*
  ;

exp
  : NULL                                      #ExpLiteralNull
  | FALSE                                     #ExpLiteralFalse
  | TRUE                                      #ExpLiteralTrue
  | number                                    #ExpLiteralNum
  | string                                    #ExpLiteralStr
  | '(' type ')' exp                          #ExpTypeCast
  | chainExp                                  #ExpChain
  | funcLambda                                #ExpLambda
  | TYPEOF '(' type ')'                       #ExpTypeof
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
  | exp operatorLogical exp                   #ExpLogical
  | exp ternaryIfExp                          #ExpTernaryIf
  | newExp                                    #ExpNew
  //TODO: move it under ExpChain?
  //| chainExp '.'                              #ExpIncompleteMember
  //| chainExp '(' (callArgsList ','?)?         #ExpIncompleteCall
  ;

ternaryIfExp
  : '?' exp ':' exp 
  ;

newExp
  : NEW type
  ;

foreachExp
  : '(' varOrDeclare (',' varOrDeclare)* IN exp ')' 
  ;

forPreIter
  : varOrDeclareAssign (',' varOrDeclareAssign)*
  ;

forPostIter
  : expModifyOp (',' expModifyOp)*
  ;

forExp
  : '(' forPreIter? SEPARATOR exp SEPARATOR forPostIter? ')' 
  ;

//NOTE: statements, order is important
statement
  : ';'                                        #StmSeparator
  | varDeclareList assignExp?                  #StmDeclOptAssign
  //int a, c.r = foo()
  | varDeclaresOrChainExps assignExp           #StmDeclOrExpAssign
  //func call or variable/member read/write access
  | chainExp modifyOp?                         #StmChainExp
  | IF '(' exp ')' block elseIf* else?         #StmIf
  | WHILE '(' exp ')' block                    #StmWhile
  | DO block WHILE '(' exp ')'                 #StmDoWhile
  | FOR forExp block                           #StmFor
  | FOREACH foreachExp block                   #StmForeach
  | YIELD '(' ')'                              #StmYield                                                                   
  | YIELD chainExp                             #StmYieldCall
  | YIELD WHILE '(' exp ')'                    #StmYieldWhile
  | BREAK                                      #StmBreak
  | CONTINUE                                   #StmContinue
  | RETURN returnVal?                          #StmReturn
  | PARAL block                                #StmParal
  | PARAL_ALL block                            #StmParalAll
  | DEFER block                                #StmDefer
  | block                                      #StmBlockNested
  ;

elseIf
  : ELSE IF '(' exp ')' block
  ;

else
  : ELSE block
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
  : VARIADIC? (NAME ':')? REF? exp
  ;

block 
  : '{' statement* '}'
  | '{}'
  ;

extensions
  : ':' nsName (',' nsName)*
  ;

nsDecl
  : NAMESPACE dotName '{' decl* '}'
  ;

classDecl
  : CLASS NAME extensions? classBlock
  ;

classBlock
  : '{' classMembers '}'
  | '{}'
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
  : ENUM NAME enumBlock
  ;

enumBlock
  : '{' enumMember+ '}'
  ;

enumMember
  : NAME '=' INT
  ;

funcAttribs
  : (CORO | VIRTUAL | OVERRIDE | STATIC)
  ;

funcDecl
  : funcAttribs* FUNC retType? NAME '(' funcParams? ')' funcBlock
  ;

funcType
  : CORO? FUNC retType? '(' types? ')'
  ;

funcBlock
  : block
  ;

interfaceFuncDecl
  : CORO? FUNC retType? NAME '(' funcParams? ')'
  ;

funcLambda
  : CORO? FUNC retType? '(' funcParams? ')' funcBlock
  ;

refType
  : REF? type
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
  : REF? VARIADIC? type NAME assignExp?
  ;

varDeclare
  : type NAME
  ;

varDeclareList
  : varDeclare ( ',' varDeclare )*
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

varDeclareOrChainExp
  : varDeclare 
  | chainExp
  ;

varDeclaresOrChainExps
  : varDeclareOrChainExp (',' varDeclareOrChainExp)*
  ;

modifyOp
  : assignExp | operatorIncDec | (operatorSelfOp exp)
  ;

expModifyOp
  : chainExp modifyOp
  ;

assignExp
  : '=' exp
  ;
  
operatorLogical
  : LAND | LOR
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
  :   newExp? '{' jsonPair (',' jsonPair)* '}'
  |   newExp? jsonEmptyObj
  ;

jsonEmptyObj
  : '{' '}'
  //TODO: why this special case?
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

IMPORT : 'import' ;
NULL : 'null' ;
FALSE : 'false' ;
TRUE : 'true' ;
IF : 'if' ;
ELSE : 'else' ;
WHILE : 'while' ;
DO : 'do' ;
FOR : 'for' ;
FOREACH : 'foreach' ;
IN : 'in' ;
BREAK : 'break' ;
CONTINUE : 'continue' ;
RETURN : 'return' ;
YIELD : 'yield' ;
AS : 'as' ;
IS : 'is' ;
TYPEOF : 'typeof' ;
NEW : 'new' ;
PARAL : 'paral' ;
PARAL_ALL : 'paral_all' ;
DEFER : 'defer' ;
NAMESPACE : 'namespace' ;  
CLASS : 'class' ;
INTERFACE : 'interface' ;
ENUM : 'enum' ;
VIRTUAL : 'virtual' ;
OVERRIDE : 'override' ;
STATIC : 'static' ;
CORO : 'coro' ;
FUNC : 'func' ;
REF : 'ref' ;
LOR : '||' ;
LAND : '&&' ;
BOR : '|' ;
BAND : '&' ;
INC : '++' ;
DEC : '--' ;
SINC : '+=' ;
SDEC : '-=' ;
SMUL : '*=' ;
SDIV : '/=' ;
LT : '<' ;
LTE : '<=' ;
GT : '>' ;
GTE : '>=' ;
NEQ : '!=' ;
EQ : '==' ;
PLUS : '+' ;
MINUS : '-' ;
MUL : '*' ;
DIV : '/' ;
MOD : '%' ;
NOT : '!' ;

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



