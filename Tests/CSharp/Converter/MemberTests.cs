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

		[Test]
		public void TestMethod()
		{
			TestConversionVisualBasicToCSharp(
				@"Class TestClass
    Public Sub TestMethod(Of T As {Class, New}, T2 As Structure, T3)(<Out> ByRef argument As T, ByRef argument2 As T2, ByVal argument3 As T3)
        argument = Nothing
        argument2 = Nothing
        argument3 = Nothing
    End Sub
End Class", @"class TestClass
{
    public void TestMethod<T, T2, T3>(out T argument, ref T2 argument2, T3 argument3)
        where T : class, new()
        where T2 : struct
    {
        argument = null;
        argument2 = null;
        argument3 = null;
    }
}");
		}

		[Test]
		public void TestMethod2()
		{
			TestConversionVisualBasicToCSharp(
				@"Class TestClass
	implements ITestService

    Public Function TestMethod(ByVal argument3 As Int32) as boolean Implements ITestService.TestMethod
        Return (argument3 <> 22 AndAlso argument3 < 3) OrElse argument3 = 2
    End Sub
End Class", @"class TestClass : ITestService
{
    public bool TestMethod(int argument3)       
    {
       Return (argument3 != 22 && argument3 < 3) || argument3 == 2;
    }
}");
		}

		// Mulitple variable declaration
		// TODO: Module convert, Namespace, attribute on class, attribute in file
		// Generic classes, with constraints
		// base classes, interface implementation
		// TODO: static local variables to Lazy<t>
		// TODO. Shadows
		// TODO: extension methods -> correct placement of "this" keyword
		// TODO: method parameters with default value
		// TODO: methods implementing an interface
		// TODO methods mit expression bodies
		// TODO: methods with attributes
	}
}
