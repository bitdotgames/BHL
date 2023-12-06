lexer grammar bhlPreprocLexer;

channels {
  COMMENTS_CHANNEL
}

SHARP             : '#'                                          -> mode(DIRECTIVE_MODE);
COMMENT           : '/*' .*? '*/'                                -> type(CODE);
LINE_COMMENT      : '//' ~[\r\n]*                                -> type(CODE);
SLASH             : '/'                                          -> type(CODE);
CHARACTER_LITERAL : '\'' (EscapeSequence | ~('\'' | '\\')) '\''  -> type(CODE);
QUOTE_STRING      : '\'' (EscapeSequence | ~('\'' | '\\'))* '\'' -> type(CODE);
STRING            : StringFragment                               -> type(CODE);
CODE              : ~[#'"/]+;

mode DIRECTIVE_MODE;

//IMPORT  : 'import' [ \t]+  -> mode(DIRECTIVE_TEXT);
//INCLUDE : 'include' [ \t]+ -> mode(DIRECTIVE_TEXT);
//PRAGMA  : 'pragma'         -> mode(DIRECTIVE_TEXT);

//DEFINE  : 'define' [ \t]+ -> mode(DIRECTIVE_DEFINE);

IF      : 'if';
ELSE    : 'else';
//ELIF    : 'elif';
ENDIF   : 'endif';
//UNDEF   : 'undef';
//TRUE    : 'true';
//FALSE   : 'false';
//ERROR   : 'error' -> mode(DIRECTIVE_TEXT);

BANG     : '!';
//LPAREN   : '(';
//RPAREN   : ')';
//EQUAL    : '==';
//NOTEQUAL : '!=';
//AND      : '&&';
//OR       : '||';
//LT       : '<';
//GT       : '>';
//LE       : '<=';
//GE       : '>=';

DIRECTIVE_WHITESPACES  : [ \t]+ -> channel(HIDDEN);
DIRECTIVE_STRING       : StringFragment;
CONDITIONAL_SYMBOL     : LETTER (LETTER | [0-9])*;
//DECIMAL_LITERAL        : [0-9]+;
//FLOAT                  : ([0-9]+ '.' [0-9]* | '.' [0-9]+);
NEW_LINE               : '\r'? '\n'      -> mode(DEFAULT_MODE);
DIRECTIVE_COMMENT      : '/*' .*? '*/'   -> channel(COMMENTS_CHANNEL);
//DIRECITVE_LINE_COMMENT : '//' ~[\r\n]*   -> channel(COMMENTS_CHANNEL);
//DIRECITVE_NEW_LINE     : '\\' '\r'? '\n' -> channel(HIDDEN);

//mode DIRECTIVE_DEFINE;

//DIRECTIVE_DEFINE_CONDITIONAL_SYMBOL:
//    LETTER (LETTER | [0-9])* ('(' (LETTER | [0-9,. \t])* ')')? -> type(CONDITIONAL_SYMBOL), mode(DIRECTIVE_TEXT)
//;

//mode DIRECTIVE_TEXT;

//DIRECTIVE_TEXT_NEW_LINE : '\\' '\r'? '\n' -> channel(HIDDEN);
//BACK_SLASH_ESCAPE       : '\\' .          -> type(TEXT);
//TEXT_NEW_LINE           : '\r'? '\n'      -> type(NEW_LINE), mode(DEFAULT_MODE);
//DIRECTIVE_COMMENT       : '/*' .*? '*/'   -> channel(COMMENTS_CHANNEL), type(DIRECITVE_COMMENT);
////DIRECTIVE_LINE_COMMENT:
////    '//' ~[\r\n]* -> channel(COMMENTS_CHANNEL), type(DIRECTIVE_LINE_COMMENT)
////;
//DIRECTIVE_SLASH : '/' -> type(TEXT);
//TEXT            : ~[\r\n\\/]+;

fragment EscapeSequence:
    '\\' ('b' | 't' | 'n' | 'f' | 'r' | '"' | '\'' | '\\')
    | OctalEscape
    | UnicodeEscape
;

fragment OctalEscape: '\\' [0-3] [0-7] [0-7] | '\\' [0-7] [0-7] | '\\' [0-7];

fragment UnicodeEscape: '\\' 'u' HexDigit HexDigit HexDigit HexDigit;

fragment HexDigit: [0-9a-fA-F];

fragment StringFragment: '"' (~('\\' | '"') | '\\' .)* '"';

fragment LETTER:
    [$A-Za-z_]
    | ~[\u0000-\u00FF\uD800-\uDBFF]
    | [\uD800-\uDBFF] [\uDC00-\uDFFF]
    | [\u00E9]
;
