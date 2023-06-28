lexer grammar bhlLexer;

options { 
  superClass=bhlLexerBase; 
}

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

GLOBAL : '..' ;

VARIADIC : '...' ;

ASSIGN : '=' ;
SEMI : ';' ;
COLON : ':' ;
DOT : '.' ;
OPEN_BRACKET : '[';
CLOSE_BRACKET : ']';
OPEN_PAREN : '(';
CLOSE_PAREN : ')';
OPEN_BRACE : '{';
CLOSE_BRACE : '}';
COMMA : ',' ;
QUESTION : '?' ;
  
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

fragment ExponentPart
  : [eE] [+-]? Digit+
  ;

fragment EscapeSequence
  : '\\' [abfnrtvz"'\\] //" let's keep text editor happy
  | '\\' '\r'? '\n'
  ;

fragment Digit : [0-9] ;

fragment HexDigit : [0-9a-fA-F] ;

//comments
SINGLE_LINE_COMMENT : '//' ~[\r\n]* -> channel(HIDDEN) ;

DELIMITED_COMMENT : '/*' .*? '*/' -> channel(HIDDEN) ;

WS: [ \t]+ -> channel(HIDDEN);

NL: [\r\n]+ -> channel(HIDDEN);

