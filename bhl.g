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
  | 'yield' funcCallExp                      #ExpYieldCall
  //NOTE: special case for standalone variables which also 
  //      can be the beginning of the expression chain
  | GLOBAL? NAME                             #ExpName
  //NOTE: complexExp 'flattened' to avoid left recursion, we also
  //      require at least one chain item to be present
  | exp chainExpItem+                        #ExpChain
  | 'typeof' '(' type ')'                    #ExpTypeof
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
  : varAccessExp (INC | DEC)
  ;

varsDeclares
  : varDeclare ( ',' varDeclare )*
  ;

varsDeclareAssign
  : varsDeclares assignExp?
  ;

//NOTE: statements, order is important
statement
  : funcLambda                                 #StmLambdaCall
  | varsDeclareAssign                          #StmDeclAssign
  | varAccessExp assignExp                     #StmVarAccessAssign
  | varAccessExp operatorPostOpAssign exp      #StmVarPostOpAssign
  | varPostIncDec                              #StmVarIncDec
  //func/method calls, variable and members access
  | complexExp                                 #StmComplexExp
  | mainIf elseIf* else?                       #StmIf
  | 'while' '(' exp ')' block                  #StmWhile
  | 'do' block 'while' '(' exp ')'             #StmDoWhile
  | 'for' forExp block                         #StmFor
  | 'foreach' foreachExp block                 #StmForeach
  | 'yield' '(' ')'                            #StmYield                                                                   
  | 'yield' funcCallExp                        #StmYieldFunc
  | 'yield' funcLambda                         #StmYieldLambdaCall
  | 'yield' 'while' '(' exp ')'                #StmYieldWhile
  | 'break'                                    #StmBreak
  | 'continue'                                 #StmContinue
  | 'return' returnVal?                        #StmReturn
  | 'paral' block                              #StmParal
  | 'paral_all' block                          #StmParalAll
  | 'defer' block                              #StmDefer
  | block                                      #StmBlockNested
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
  
complexExp
  : exp chainExpItem*
  ;

chainExpItem
  : callArgs | memberAccess | arrAccess
  ;

//NOTE: makes sure it's a func call
funcCallExp
  : complexExp callArgs
  ;

//NOTE: makes sure it's a variable access
varAccessExp
  : complexExp (memberAccess | arrAccess)
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
  
INC
  : '+' '+'
  ;

DEC
  : '-' '-'
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


