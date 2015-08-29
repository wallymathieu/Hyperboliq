﻿namespace Hyperboliq.Tests.TokenGeneration

module TokenGeneration_SimpleWhereTests =
    open NUnit.Framework
    open FsUnit
    open Hyperboliq.Tests.TokenGeneration.TestModel
    open Hyperboliq.Tests.TokenGeneration.TestHelpers
    open Hyperboliq
    open Hyperboliq.Domain
    open Hyperboliq.Domain.AST

    [<Test>]
    let ``It should handle a simple where condition`` () =
        let expr = Select.Star<Person>()
                         .From<Person>()
                         .Where(<@ fun (p : Person) -> p.Age > 42 @>)
        let result = expr.ToSqlExpression()
        
        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.StarColumn(StarColumnToken(tref.Reference)) ] }
                From = { Tables = [ tref ]; Joins = [] }
                Where = { Start = { Lhs = ValueNode.Column("Age", typeof<Person>, tref.Reference :> ITableReference)
                                    Rhs = ValueNode.Constant(ConstantNode("42"))
                                    Operation = BinaryOperation.GreaterThan } 
                                  |> ValueNode.BinaryExpression
                          AdditionalClauses = [] } |> Some }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should handle a conditional with and and ors in the expression`` () =
        let expr = Select.Star<Person>()
                         .From<Person>()
                         .Where(<@ fun (p : Person) -> p.Age > 42 || (p.Age < 10 && p.Name = "Karl") @>)
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.StarColumn(StarColumnToken(tref.Reference)) ] }
                From = { Tables = [ tref ]; Joins = [] }
                Where = { Start = { Operation = BinaryOperation.Or
                                    Rhs = { Operation = BinaryOperation.And
                                            Lhs = { Operation = BinaryOperation.LessThan
                                                    Lhs = ValueNode.Column("Age", typeof<Person>, tref.Reference :> ITableReference)
                                                    Rhs = ValueNode.Constant(ConstantNode("10"))
                                                  } |> ValueNode.BinaryExpression
                                            Rhs = { Operation = BinaryOperation.Equal
                                                    Lhs = ValueNode.Column("Name", typeof<Person>, tref.Reference :> ITableReference)
                                                    Rhs = ValueNode.Constant(ConstantNode("'Karl'")) 
                                                  } |> ValueNode.BinaryExpression 
                                          } |> ValueNode.BinaryExpression
                                    Lhs = { Operation = BinaryOperation.GreaterThan
                                            Lhs = ValueNode.Column("Age", typeof<Person>, tref.Reference :> ITableReference)
                                            Rhs = ValueNode.Constant(ConstantNode("42")) 
                                          } |> ValueNode.BinaryExpression
                                  } |> ValueNode.BinaryExpression
                          AdditionalClauses = [] } |> Some }
            |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should handle a where condition with and and ors that are not in the expression`` () =
        let expr = Select.Star<Person>()
                         .From<Person>()
                         .Where(<@ fun (p : Person) -> p.Age < 42 @>)
                         .And(<@ fun (p : Person) -> p.Age > 12 @>)
                         .Or(<@ fun (p : Person) -> p.Name = "Karl" @>)
        let result = expr.ToSqlExpression()

        let tref = TableIdentifier<Person>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.StarColumn(StarColumnToken(tref.Reference)) ] }
                From = { Tables = [ tref ]; Joins = [] }
                Where = { Start = { Operation = BinaryOperation.LessThan
                                    Lhs = ValueNode.Column("Age", typeof<Person>, tref.Reference :> ITableReference)
                                    Rhs = ValueNode.Constant(ConstantNode("42")) } |> ValueNode.BinaryExpression 
                          AdditionalClauses = 
                          [ { Combinator = ExpressionCombinatorType.Or
                              Expression = { Operation = BinaryOperation.Equal
                                             Lhs = ValueNode.Column("Name", typeof<Person>, tref.Reference :> ITableReference)
                                             Rhs = ValueNode.Constant(ConstantNode("'Karl'")) } |> ValueNode.BinaryExpression } 
                            { Combinator = ExpressionCombinatorType.And
                              Expression = { Operation = BinaryOperation.GreaterThan
                                             Lhs = ValueNode.Column("Age", typeof<Person>, tref.Reference :> ITableReference)
                                             Rhs = ValueNode.Constant(ConstantNode("12")) } |> ValueNode.BinaryExpression }
                          ]
                        } |> Some } |> TestHelpers.ToPlainSelect
        result |> should equal expected

    [<Test>]
    let ``It should be possible to make where conditions on joined tables`` () =
        let expr = Select.Star<Person>().Star<Car>()
                         .From<Person>()
                         .InnerJoin<Person, Car>(<@ fun (p : Person) (c : Car) -> p.Id = c.DriverId @>)
                         .Where(<@ fun (p : Person) -> p.Age > 42 @>)
                         .And(<@ fun (c : Car) -> c.Brand = "SAAB" @>)
        let result = expr.ToSqlExpression()

        let ptref = TableIdentifier<Person>()
        let ctref = TableIdentifier<Car>()
        let expected =
            { TestHelpers.EmptySelect with
                Select = { IsDistinct = false
                           Values = [ ValueNode.StarColumn(StarColumnToken(ctref.Reference)); ValueNode.StarColumn(StarColumnToken(ptref.Reference)) ] }
                From = { Tables = [ ptref ]
                         Joins = [ { SourceTables = [ ptref ]
                                     TargetTable = ctref
                                     Type = JoinType.InnerJoin
                                     Condition = { Operation = BinaryOperation.Equal
                                                   Lhs = ValueNode.Column("Id", typeof<Person>, ptref.Reference :> ITableReference)
                                                   Rhs = ValueNode.Column("DriverId", typeof<Car>, ctref.Reference :> ITableReference) } |> ValueNode.BinaryExpression |> Some } ] }
                Where = { Start = { Operation = BinaryOperation.GreaterThan
                                    Lhs = ValueNode.Column("Age", typeof<Person>, ptref.Reference :> ITableReference)
                                    Rhs = ValueNode.Constant(ConstantNode("42")) } |> ValueNode.BinaryExpression
                          AdditionalClauses = [ { Combinator = ExpressionCombinatorType.And
                                                  Expression = { Operation = BinaryOperation.Equal
                                                                 Lhs = ValueNode.Column("Brand", typeof<Car>, ctref.Reference :> ITableReference)
                                                                 Rhs = ValueNode.Constant(ConstantNode("'SAAB'")) } |> ValueNode.BinaryExpression } ] } |> Some
                } |> TestHelpers.ToPlainSelect
        result |> should equal expected