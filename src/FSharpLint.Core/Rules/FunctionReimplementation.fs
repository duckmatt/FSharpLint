﻿// FSharpLint, a linter for F#.
// Copyright (C) 2016 Matthew Mcveigh
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace FSharpLint.Rules

/// Checks a lambda function is not simply an 'abbreviation' of another function.
/// For example it will warn when it finds a lambda such as: fun a b -> a * b as it is exactly the same as (*).
module FunctionReimplementation =
    
    open Microsoft.FSharp.Compiler.Ast
    open Microsoft.FSharp.Compiler.PrettyNaming
    open Microsoft.FSharp.Compiler.SourceCodeServices
    open FSharpLint.Framework
    open FSharpLint.Framework.Ast
    open FSharpLint.Framework.Configuration
    open FSharpLint.Framework.ExpressionUtilities

    [<Literal>]
    let AnalyserName = "FunctionReimplementation"
    
    let isRuleEnabled config ruleName =
        isRuleEnabled config AnalyserName ruleName |> Option.isSome 

    let rec simplePatternsLength = function
        | SynSimplePats.SimplePats(patterns, _) -> 
            List.length patterns
        | SynSimplePats.Typed(simplePatterns, _, _) -> 
            simplePatternsLength simplePatterns

    let rec private getLambdaParamIdent = function
        | SynSimplePats.SimplePats(pattern::_, _) -> 
            let rec getIdent = function
                | SynSimplePat.Id(ident, _, _, _, _, _) -> ident
                | SynSimplePat.Typed(simplePattern, _, _)
                | SynSimplePat.Attrib(simplePattern, _, _) ->
                    getIdent simplePattern

            getIdent pattern |> Some
        | SynSimplePats.SimplePats(_) -> None
        | SynSimplePats.Typed(simplePatterns, _, _) -> 
            getLambdaParamIdent simplePatterns

    let validateLambdaCannotBeReplacedWithComposition lambda range visitorInfo =
        let canBeReplacedWithFunctionComposition expression = 
            let getLastElement = List.rev >> List.head

            let rec lambdaArgumentIsLastApplicationInFunctionCalls expression (lambdaArgument:Ident) numFunctionCalls =
                let rec appliedValuesAreConstants appliedValues =
                    match appliedValues with
                    | (SynExpr.Const(_)| SynExpr.Null(_))::rest -> appliedValuesAreConstants rest
                    | [SynExpr.App(_) | SynExpr.Ident(_)] -> true
                    | _ -> false

                match AstNode.Expression expression with
                | FuncApp((SynExpr.Ident(_) | SynExpr.LongIdent(_))::appliedValues, _)
                        when appliedValuesAreConstants appliedValues -> 

                    match getLastElement appliedValues with
                    | SynExpr.Ident(lastArgument) when numFunctionCalls > 1 -> 
                        lastArgument.idText = lambdaArgument.idText
                    | SynExpr.App(_, false, _, _, _) as nextFunction ->
                        lambdaArgumentIsLastApplicationInFunctionCalls nextFunction lambdaArgument (numFunctionCalls + 1)
                    | _ -> false
                | _ -> false

            match lambda.Arguments with
            | [singleParameter] -> 
                getLambdaParamIdent singleParameter
                |> Option.exists (fun paramIdent -> lambdaArgumentIsLastApplicationInFunctionCalls expression paramIdent 1)
            | _ -> false
            
        if canBeReplacedWithFunctionComposition lambda.Body then
            Resources.GetString("RulesCanBeReplacedWithComposition") |> visitorInfo.PostError range 

    let validateLambdaIsNotPointless (checkFile:FSharpCheckFileResults option) lambda range visitorInfo =
        let isConstructor expr =
            let symbol = getSymbolFromIdent checkFile expr

            match symbol with
            | Some(symbol) -> 
                match symbol.Symbol with 
                | s when s.DisplayName = ".ctor" -> true
                | :? FSharpMemberOrFunctionOrValue as v when v.CompiledName = ".ctor" -> true
                | _ -> false
            | Some(_) | None -> false
        
        let rec isFunctionPointless expression = function
            | Some(parameter:Ident) :: parameters ->
                match expression with
                | SynExpr.App(_, _, expression, SynExpr.Ident(identifier), _)
                    when identifier.idText = parameter.idText ->
                        isFunctionPointless expression parameters
                | _ -> None
            | None :: _ -> None
            | [] -> 
                match expression with
                | Identifier(ident, _) -> 
                    if visitorInfo.FSharpVersion.Major >= 4 || 
                       (not << isConstructor) expression then
                        Some(ident)
                    else
                        None
                | _ -> None

        let generateError (identifier:LongIdent) =
            let identifier = 
                identifier 
                    |> List.map (fun x -> DemangleOperatorName x.idText)
                    |> String.concat "."

            let errorFormatString = Resources.GetString("RulesReimplementsFunction")
            let error = System.String.Format(errorFormatString, identifier)
            visitorInfo.PostError range error

        let argumentsAsIdentifiers = lambda.Arguments |> List.map getLambdaParamIdent |> List.rev

        isFunctionPointless lambda.Body argumentsAsIdentifiers
        |> Option.iter generateError

    let analyser visitorInfo checkFile syntaxArray skipArray = 
        let isSuppressed i ruleName = 
            AbstractSyntaxArray.getSuppressMessageAttributes syntaxArray skipArray i 
            |> AbstractSyntaxArray.isRuleSuppressed AnalyserName ruleName

        let isEnabled i ruleName = isRuleEnabled visitorInfo.Config ruleName && not (isSuppressed i ruleName)

        let mutable i = 0
        while i < syntaxArray.Length do
            match syntaxArray.[i].Actual with
            | AstNode.Expression(SynExpr.Lambda(_)) as lambda -> 
                match lambda with
                | Lambda(lambda, range) -> 
                    if (not << List.isEmpty) lambda.Arguments then
                        if isEnabled i "ReimplementsFunction" then
                            validateLambdaIsNotPointless checkFile lambda range visitorInfo
                    
                        if isEnabled i "CanBeReplacedWithComposition" then
                            validateLambdaCannotBeReplacedWithComposition lambda range visitorInfo
                | _ -> ()
            | _ -> ()

            i <- i + 1