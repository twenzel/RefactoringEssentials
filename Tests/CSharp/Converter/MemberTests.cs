using NUnit.Framework;
using RefactoringEssentials.Tests.VB.Converter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefactoringEssentials.Tests.CSharp.Converter
{
	[TestFixture]
	public class MemberTests : ConverterTestBase
	{
		[Test]
		public void TestField()
		{
		
			TestConversionVisualBasicToCSharp(
				 @"Class TestClass
    Const answer As Integer = 42
    Private value As Integer = 10
    Private valueF As Single? = 12.4
    ReadOnly v As Integer = 15
	Dim a As New System.Exception()
End Class",
	@"class TestClass
{
    const int answer = 42;
    private int value = 10;
    private float? valueF = 12.4;
    readonly int v = 15;
    private var a = new System.Exception();
}");
		}

		// Mulitple variable declaration
		// TODO: Module convert, Namespace, attribute on class, attribute in file
		// Generic classes, with constraints
		// base classes, interface implementation
		// TODO: static local variables to Lazy<t>
		// TODO. Shadows
	}
}
