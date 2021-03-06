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

module TestAstInfo

open NUnit.Framework
open FSharpLint.Framework.AstInfo

[<TestFixture>]
type TestAstInfo() =

    [<Test>]
    member __.IsOperator() = 
        Assert.IsTrue(isOperator "op_LeftShift")

        Assert.IsTrue(isOperator "op_TwiddleEqualsDivideComma")

        Assert.IsFalse(isOperator "TwiddleEqualsDivideComma")

        Assert.IsFalse(isOperator "op_fTwiddleEqualsDivideComma")

        Assert.IsFalse(isOperator "op_TwiddleEqualsDivideCommaf")