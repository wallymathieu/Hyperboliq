﻿using NUnit.Framework;
using Hyperboliq.Domain;
using S = Hyperboliq.Tests.SqlStreamExtensions;

namespace Hyperboliq.Tests.SqlServer
{
    [Schema("HumanResources")]
    public class Employee
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public int Id { get; set; }
    }

    [TestFixture]
    public class SqlServer_ColumnQuotingTests
    {
        [Test]
        public void ItShouldProperlyQuoteIdentifiers()
        {
            var stream =
                S.SelectNode(
                    S.Select(
                        S.Col<Employee>("Name"),
                        S.Col<Employee>("Age"),
                        S.Col<Employee>("Id")),
                    S.From<Employee>());

            var result = SqlGen.SqlifyExpression(Dialects.SqlServer.Dialect, stream);
            Assert.That(result, Is.EqualTo(@"SELECT [EmployeeRef].[Name], [EmployeeRef].[Age], [EmployeeRef].[Id] FROM [HumanResources].[Employee] [EmployeeRef]"));
        }
    }
}
