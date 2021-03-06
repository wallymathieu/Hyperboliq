﻿namespace Hyperboliq.Domain

module ExpressionParts =
    open Hyperboliq
    open AST

    let AddPartitionBy window selector tblRef =
        let v = ExpressionVisitor.Visit selector [ tblRef ]
        match v with
        | None -> window
        | Some(ValueList(values)) -> { window with PartitionBy = window.PartitionBy @ values }
        | Some(value) -> { window with PartitionBy = window.PartitionBy @ [ value ]}

    let AddPartitionOrderBy (window : WindowNode) selector tblRef direction nullsOrder =
        let v = ExpressionVisitor.Visit selector [ tblRef ]
        match v with
        | None -> window
        | Some(value) ->
            let clause = { Selector = value; Direction = direction; NullsOrdering = nullsOrder }
            { window with OrderBy = window.OrderBy @ [ clause ] }

    let AddJoinClause fromExpr joinClause =
        { fromExpr with Joins = joinClause :: fromExpr.Joins }

    let CreateJoinClause joinType condition targetTable ([<System.ParamArray>] sourceTables : ITableIdentifier array) =
        let srcList = List.ofArray sourceTables
        let envList = srcList @ [ targetTable ] |> List.map (fun ti -> ti.Reference)
        {
            SourceTables = srcList
            TargetTable = targetTable
            Type = joinType
            Condition = ExpressionVisitor.Visit condition envList
        }

    let NewSelectExpression () =
        { IsDistinct = false; Values = [] }

    let MakeDistinct select =
        { select with IsDistinct = true }

    let SelectAllColumns select (table : ITableIdentifier) =
        { select with SelectValuesExpressionNode.Values = ValueNode.StarColumn(table.Reference) :: select.Values }

    let SelectColumns select expr (table : ITableIdentifier) =
        let stream = ExpressionVisitor.Visit expr [ table.Reference ]
        match stream with
        | None -> select
        | Some(ValueList(v)) -> { select with SelectValuesExpressionNode.Values = v @ select.Values }
        | Some(v) -> { select with Values = select.Values @ [ v ] }

    let SelectColumnWithPartition select expr (table : ITableIdentifier) partition =
        let stream = ExpressionVisitor.Visit expr [ table.Reference ] 
        match stream with
        | Some(Aggregate(a)) ->
            { select with SelectValuesExpressionNode.Values = select.Values @ [ WindowedColumn(a, partition) ] }
        | _ -> select

    let private NewOrderByExpression () = { OrderByExpressionNode.Clauses = [] }

    let private AddOrderingClause tbl direction nullsorder expr orderExpr  =
        let selector = ExpressionVisitor.Visit expr [ tbl ]
        match selector with
        | None -> orderExpr
        | Some(v) -> 
            let clause = { OrderByClauseNode.Direction = direction; NullsOrdering = nullsorder; Selector = v }
            { orderExpr with OrderByExpressionNode.Clauses = clause :: orderExpr.Clauses }

    let AddOrCreateOrderingClause orderExpr tbl direction nullsorder expr =
        match orderExpr with
        | Some(o) -> o
        | None -> NewOrderByExpression ()
        |> AddOrderingClause tbl direction nullsorder expr

    let private NewWhereExpression expr ([<System.ParamArray>] tables : ITableReference array) : WhereExpressionNode =
        let startValue = ExpressionVisitor.Visit expr tables
        match startValue with
        | None -> failwith "Must provide value"
        | Some(v) ->
            { 
                Start = v
                AdditionalClauses = []
            }

    let private CreateWhereClause cmbType whereExpr expr ([<System.ParamArray>] tables : ITableReference array) =
        let startValue = ExpressionVisitor.Visit expr tables
        match startValue with
        | None -> failwith "Must provide value"
        | Some(v) -> 
            let clause = { Combinator = cmbType; Expression = v }
            { whereExpr with AdditionalClauses = clause :: whereExpr.AdditionalClauses }

    let private AddWhereAndClause whereExpr expr ([<System.ParamArray>] tables : ITableReference array) = 
        CreateWhereClause And whereExpr expr tables

    let private AddWhereOrClause whereExpr expr ([<System.ParamArray>] tables : ITableReference array) = 
        CreateWhereClause Or whereExpr expr tables

    let AddOrCreateWhereAndClause whereExpr expr ([<System.ParamArray>] tables : ITableReference array) =
        match whereExpr with
        | Some(w) -> AddWhereAndClause w expr tables
        | None -> NewWhereExpression expr tables

    let AddOrCreateWhereOrClause whereExpr expr ([<System.ParamArray>] tables : ITableReference array) =
        match whereExpr with
        | Some(w) -> AddWhereOrClause w expr tables
        | None -> NewWhereExpression expr tables

    let NewGroupByExpression () = { 
        Clauses = []
        Having = [] 
    }

    let internal AddHavingClause groupByExpr joinType expr ([<System.ParamArray>] tables : ITableReference array) =
        let value = ExpressionVisitor.Visit expr tables 
        match value with
        | None -> failwith "Must provide value"
        | Some(v) ->
            let clause = { WhereClauseNode.Combinator = joinType; Expression = v }
            { groupByExpr with GroupByExpressionNode.Having = clause :: groupByExpr.Having }

    let AddHavingAndClause groupByExpr expr ([<System.ParamArray>] tables : ITableReference array) = 
        match groupByExpr with
        | Some(g) -> AddHavingClause g And expr tables
        | None -> AddHavingClause (NewGroupByExpression ()) And expr tables

    let AddHavingOrClause groupByExpr expr ([<System.ParamArray>] tables : ITableReference array) = 
        match groupByExpr with
        | Some(g) -> AddHavingClause g Or expr tables
        | None -> AddHavingClause (NewGroupByExpression ()) Or expr tables 

    let private AddGroupByClause expr tables groupByExpr = 
        let cols = ExpressionVisitor.Visit expr tables
        match cols with
        | None -> groupByExpr
        | Some(ValueList(v)) ->
            { groupByExpr with GroupByExpressionNode.Clauses = groupByExpr.Clauses @ v }
        | Some(v) -> 
            { groupByExpr with GroupByExpressionNode.Clauses = groupByExpr.Clauses @ [ v ] }

    let AddOrCreateGroupByClause groupByExpr expr ([<System.ParamArray>] tables : ITableReference array) =
        match groupByExpr with
        | Some(g) -> g
        | None -> NewGroupByExpression ()
        |> AddGroupByClause expr tables
