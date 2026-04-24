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
  : nsDecl | classDecl | interfaceDecl | funcDecl | varDeclareOptAssign | enumDecl
  ;

dotName
  : NAME memberAccess*
  ;

nsName
  : GLOBAL? dotName
  ;

type
  : (arrType | mapType)? (nsName | funcType)
  ;

arrType
  : OPEN_BRACKET CLOSE_BRACKET
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
  | NORMALSTRING                              #ExpLiteralStr
  | OPEN_PAREN type CLOSE_PAREN exp           #ExpTypeCast
  | chainExp                                  #ExpChain
  | TYPEOF OPEN_PAREN type CLOSE_PAREN        #ExpTypeof
  | jsonObject                                #ExpJsonObj
  | jsonArray                                 #ExpJsonArr
  | YIELD chainExp                            #ExpYieldCall
  | newExp                                    #ExpNew
  | operatorUnary exp                         #ExpUnary
  | exp operatorMulDivMod exp                 #ExpMulDivMod
  | exp operatorAddSub exp                    #ExpAddSub
  | exp operatorShift exp                     #ExpShift
  | exp operatorComparison exp                #ExpCompare
  | exp AS type                               #ExpAs
  | exp IS type                               #ExpIs
  | exp BAND exp                              #ExpBitwiseAnd
  | exp BOR exp                               #ExpBitwiseOr
  | exp LAND exp                              #ExpLogicalAnd
  | exp LOR exp                               #ExpLogicalOr
  | exp ternaryIfExp                          #ExpTernaryIf
  ;

ternaryIfExp
  : QUESTION exp COLON exp
  ;

newExp
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

statement
  : SEMI                                                         #StmSeparator
  | varDeclareList ({this.notLineTerminator()}? assignExp)? eos  #StmDeclOptAssign
  | varDeclaresOrChainExps assignExp eos                         #StmDeclOrExpAssign
  | YIELD OPEN_PAREN CLOSE_PAREN eos                             #StmYield
  | YIELD chainExp eos                                           #StmYieldCall
  | YIELD WHILE OPEN_PAREN exp CLOSE_PAREN                       #StmYieldWhile
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
  : callArg (COMMA callArg)*
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

// classBlock + classMembers inlined: -2 rule invocations per class
classDecl
  : CLASS NAME extensions? OPEN_BRACE classMember* CLOSE_BRACE
  ;

fldAttribs
  : STATIC
  ;

fldDeclare
  : fldAttribs* varDeclare
  ;

classMember
  : fldDeclare | funcDecl | classDecl | enumDecl | interfaceDecl
  ;

// interfaceBlock + interfaceMembers + interfaceMember inlined: -3 rule invocations per interface, -1 per method
interfaceDecl
  : INTERFACE NAME extensions? OPEN_BRACE interfaceFuncDecl* CLOSE_BRACE
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
  : CORO | VIRTUAL | OVERRIDE | STATIC
  ;

// funcBlock inlined: -1 rule invocation per function
funcDecl
  : funcAttribs* FUNC retType? NAME OPEN_PAREN funcParams? CLOSE_PAREN block
  ;

funcType
  : CORO? FUNC retType? OPEN_PAREN types? CLOSE_PAREN
  ;

// funcBlock inlined: -1 rule invocation per lambda
funcLambda
  : CORO? FUNC retType? OPEN_PAREN funcParams? CLOSE_PAREN captureList? block
  ;

interfaceFuncDecl
  : CORO? FUNC retType? NAME OPEN_PAREN funcParams? CLOSE_PAREN
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

// operatorBitwise split: shifts placed between +/- and comparisons (C-like precedence);
// BAND and BOR inlined directly into exp with separate precedence tiers
operatorShift
  : SHR | SHL
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
  : NOT | MINUS | BNOT
  ;

number
  : INT | HEX | FLOAT
  ;

// empty case merged in: no backtracking for {} — (jsonPair...)? handles it directly
jsonObject
  : OPEN_BRACE (jsonPair (COMMA jsonPair)*)? CLOSE_BRACE
  ;

jsonPair
  : NAME COLON jsonValue
  ;

// empty case merged in: no backtracking for []
jsonArray
  : OPEN_BRACKET (jsonValue (COMMA jsonValue)*)? CLOSE_BRACKET
  ;

jsonValue
  : exp
  ;

eos
  : SEMI
  | EOF
  | {this.lineTerminator()}?
  | {this.closeBrace()}?
  ;
