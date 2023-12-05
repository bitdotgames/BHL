parser grammar bhlPreprocParser;

options {
    tokenVocab = bhlPreprocLexer;
}

program
    : text* EOF
    ;

text
    : SHARP directive (NEW_LINE | EOF)
    | code
    ;

code
    : CODE+
    ;

directive
    : 
    //(IMPORT | INCLUDE) directive_text         # preprocImport
      IF preprocessor_expression                # preprocConditional
    //| ELIF preprocessor_expression              # preprocConditional
    | ELSE                                      # preprocConditional
    | ENDIF                                     # preprocConditional
//    | IFDEF CONDITIONAL_SYMBOL                  # preprocDef
//    | IFNDEF CONDITIONAL_SYMBOL                 # preprocDef
//    | UNDEF CONDITIONAL_SYMBOL                  # preprocDef
//    | PRAGMA directive_text                     # preprocPragma
//    | ERROR directive_text                      # preprocError
//    | DEFINE CONDITIONAL_SYMBOL directive_text? # preprocDefine
    ;

//directive_text
//    : TEXT+
//    ;

preprocessor_expression
    : 
    //  TRUE                                                                     # preprocConstant
    //| FALSE                                                                    # preprocConstant
    //| DECIMAL_LITERAL                                                          # preprocConstant
    //| DIRECTIVE_STRING                                                         # preprocConstant
      CONDITIONAL_SYMBOL                                                         # preprocConditionalSymbol
    //| CONDITIONAL_SYMBOL (LPAREN preprocessor_expression RPAREN)?              # preprocConditionalSymbol
    //| LPAREN preprocessor_expression RPAREN                                    # preprocParenthesis
    | BANG preprocessor_expression                                               # preprocNot
    //| preprocessor_expression op = (EQUAL | NOTEQUAL) preprocessor_expression  # preprocBinOp
    //| preprocessor_expression op = AND preprocessor_expression                 # preprocBinOp
    //| preprocessor_expression op = OR preprocessor_expression                  # preprocBinOp
    //| preprocessor_expression op = (LT | GT | LE | GE) preprocessor_expression # preprocBinOp
    //| DEFINED (CONDITIONAL_SYMBOL | LPAREN CONDITIONAL_SYMBOL RPAREN)          # preprocDefined
    ;
