using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactoringEssentials.CSharp.CodeRefactorings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CVB = Microsoft.CodeAnalysis.VisualBasic;
using CVBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace RefactoringEssentials.CSharp.Converter
{
	public partial class VisualBasicConverter
	{
		class NodesVisitor : CVB.VisualBasicSyntaxVisitor<CSharpSyntaxNode>
		{
			SemanticModel semanticModel;
			Document targetDocument;
			CSharpCompilationOptions options;

			public NodesVisitor(SemanticModel semanticModel, Document targetDocument)
			{
				this.semanticModel = semanticModel;
				this.targetDocument = targetDocument;
				this.options = (CSharpCompilationOptions)targetDocument?.Project.CompilationOptions;
			}

			public override CSharpSyntaxNode DefaultVisit(SyntaxNode node)
			{
				throw new NotImplementedException(node.GetType() + " not implemented!");
			}

			public override CSharpSyntaxNode VisitCompilationUnit(CVBS.CompilationUnitSyntax node)
			{		
				var usings = SyntaxFactory.List(node.Imports.Select(a => (UsingDirectiveSyntax)a.Accept(this)));
				var attributes = SyntaxFactory.List(node.Attributes.Select(a => (AttributeListSyntax)a.Accept(this)));
				var members = SyntaxFactory.List(node.Members.Select(m => (MemberDeclarationSyntax)m.Accept(this)));

				return SyntaxFactory.CompilationUnit(
					SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
					usings,
					attributes,
					members
				);
			}

			#region Attributes
			public override CSharpSyntaxNode VisitAttributeList(CVBS.AttributeListSyntax node)
			{
				return SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(node.Attributes.Select(a => (AttributeSyntax)a.Accept(this))));
			}

			public override CSharpSyntaxNode VisitAttribute(CVBS.AttributeSyntax node)
			{				
				return SyntaxFactory.Attribute((NameSyntax)node.Name.Accept(this), (AttributeArgumentListSyntax)node.ArgumentList?.Accept(this));
			}
			#endregion

			#region Namespace Members
			public override CSharpSyntaxNode VisitClassBlock(CVBS.ClassBlockSyntax node)
			{
				var members = node.Members.Select(m => (MemberDeclarationSyntax)m.Accept(this)).ToList();
				var id = ConvertIdentifier(node.BlockStatement.Identifier);


				//List<InheritsStatementSyntax> inherits = new List<InheritsStatementSyntax>();
				//List<ImplementsStatementSyntax> implements = new List<ImplementsStatementSyntax>();
				BaseListSyntax baseList = null;
				List<TypeParameterConstraintClauseSyntax> constraintClauses = new List<TypeParameterConstraintClauseSyntax>();
				//ConvertBaseList(node, inherits, implements);
				//if (node.Modifiers.Any(CS.SyntaxKind.StaticKeyword))
				//{
				//	return SyntaxFactory.ModuleBlock(
				//		SyntaxFactory.ModuleStatement(
				//			SyntaxFactory.List(node.AttributeLists.Select(a => (AttributeListSyntax)a.Accept(this))),
				//			ConvertModifiers(node.Modifiers, TokenContext.InterfaceOrModule),
				//			id, (TypeParameterListSyntax)node.TypeParameterList?.Accept(this)
				//		),
				//		SyntaxFactory.List(inherits),
				//		SyntaxFactory.List(implements),
				//		SyntaxFactory.List(members)
				//	);
				//}
				//else
				//{
				return SyntaxFactory.ClassDeclaration(						
							SyntaxFactory.List(node.ClassStatement.AttributeLists.Select(a => (AttributeListSyntax)a.Accept(this))),
							ConvertModifiers(node.ClassStatement.Modifiers),
							id, 
							(TypeParameterListSyntax)node.ClassStatement.TypeParameterList?.Accept(this),
							baseList,
							SyntaxFactory.List(constraintClauses),
							SyntaxFactory.List(members)						
					);
				//}				
			}
			#endregion

			#region Type Members
			public override CSharpSyntaxNode VisitFieldDeclaration(CVBS.FieldDeclarationSyntax node)
			{
				// At the moment I dont' have a clue how to combine mulitple field declarations into one syntax node
				// Until then only one declaration is supported;

				if (node.Declarators.Count > 1)
					throw new NotSupportedException("Multiple variable declaration are not supported. Please rewrite this declaration into single declaritions.");

				var modifiers = ConvertModifiers(node.Modifiers, TokenContext.VariableOrConst);
				if (modifiers.Count == 0)
					modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

			
				return SyntaxFactory.FieldDeclaration(
					SyntaxFactory.List(node.AttributeLists.Select(a => (AttributeListSyntax)a.Accept(this))),
					modifiers,
					RemodelVariableDeclaration(node.Declarators[0], this)
				);
			}

			public override CSharpSyntaxNode VisitMethodBlock(CVBS.MethodBlockSyntax node)
			{
				BlockSyntax body = null;
				var visitor = new MethodBodyVisitor(semanticModel, this);
				if (node.BlockStatement != null)
				{
					var statements = node.Statements.SelectMany(s => s.Accept(visitor));

					body = SyntaxFactory.Block(statements);
				}

				//if (node.ExpressionBody != null)
				//{
				//	block = SyntaxFactory.SingletonList<StatementSyntax>(
				//		SyntaxFactory.ReturnStatement((ExpressionSyntax)node.ExpressionBody.Expression.Accept(this))
				//	);
				//}

				//if (node.SubOrFunctionStatement.Modifiers.Any(m => m.IsKind(CVB.SyntaxKind.ExternKeyword)))
				//{
				//	block = SyntaxFactory.List<StatementSyntax>();
				//}
				var id = ConvertIdentifier(node.SubOrFunctionStatement.Identifier);
				var methodInfo = semanticModel.GetDeclaredSymbol(node);
				var containingType = methodInfo?.ContainingType;
				var attributes = SyntaxFactory.List(node.BlockStatement.AttributeLists.Select(a => (AttributeListSyntax)a.Accept(this)));
				var parameterList = (ParameterListSyntax)node.BlockStatement.ParameterList?.Accept(this);
				var modifiers = ConvertModifiers(node.BlockStatement.Modifiers, containingType?.IsInterfaceType() == true ? TokenContext.Local : TokenContext.Member);
				var contstraintClauses = node.SubOrFunctionStatement.TypeParameterList.Parameters.Select(p => (TypeParameterConstraintClauseSyntax)p.TypeParameterConstraintClause?.Accept(this));
				contstraintClauses = contstraintClauses.WhereNotNull();

				// extension method?
				//var extensionAttribute = attributes.FirstOrDefault(a => a.)
				//if (node.BlockStatement.ParameterList.Parameters.Count > 0 && node.BlockStatement.ParameterList.Parameters[0].Modifiers.Any(CS.SyntaxKind.ThisKeyword))
				//{
				//	attributes = attributes.Insert(0, SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(null, SyntaxFactory.ParseTypeName("Extension"), SyntaxFactory.ArgumentList()))));
				//	if (!((CS.CSharpSyntaxTree)node.SyntaxTree).HasUsingDirective("System.Runtime.CompilerServices"))
				//		allImports.Add(SyntaxFactory.ImportsStatement(SyntaxFactory.SingletonSeparatedList<ImportsClauseSyntax>(SyntaxFactory.SimpleImportsClause(SyntaxFactory.ParseName("System.Runtime.CompilerServices")))));
				//}

				//if (containingType?.IsStatic == true)
				//{
				//	modifiers = SyntaxFactory.TokenList(modifiers.Where(t => !(t.IsKind(SyntaxKind.SharedKeyword, SyntaxKind.PublicKeyword))));
				//}
				TypeSyntax returnType = SyntaxFactory.ParseTypeName("void");

				//if (node.SubOrFunctionStatement )
				//	returnType = 

				return SyntaxFactory.MethodDeclaration(attributes,
					modifiers,
					returnType,
					null,
					id,
					(TypeParameterListSyntax)node.SubOrFunctionStatement.TypeParameterList?.Accept(this),
					parameterList,
					SyntaxFactory.List(contstraintClauses),
					body, null);				
			}

			public override CSharpSyntaxNode VisitMethodStatement(CVBS.MethodStatementSyntax node)
			{
				return base.VisitMethodStatement(node);
			}

			public override CSharpSyntaxNode VisitParameterList(CVBS.ParameterListSyntax node)
			{
				return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(node.Parameters.Select(p => (ParameterSyntax)p.Accept(this))));
			}

			public override CSharpSyntaxNode VisitParameter(CVBS.ParameterSyntax node)
			{
				var id = ConvertIdentifier(node.Identifier.Identifier);
				var returnType = (TypeSyntax)node.AsClause?.Accept(this);
				EqualsValueClauseSyntax @default = null;
				if (node.Default != null)
				{
					@default = SyntaxFactory.EqualsValueClause((ExpressionSyntax)node.Default?.Value.Accept(this));
				}

				List<AttributeListSyntax> newAttributes = node.AttributeLists.Select(a => (AttributeListSyntax)a.Accept(this)).ToList();

				var modifiers = ConvertModifiers(node.Modifiers, TokenContext.Local);

				if (node.Modifiers.Any(CVB.SyntaxKind.ByRefKeyword))
				{
					var outAttribute = newAttributes.FirstOrDefault(al => al.Attributes.Any(a => a.Name.ToString() == "Out"));

					if (outAttribute != null)
					{
						newAttributes.Remove(outAttribute);
						modifiers = modifiers.Remove(modifiers.First(m => m.Text == "ref"));
						modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.OutKeyword));
					}
					//newAttributes = new[] {
					//	SyntaxFactory.AttributeList(
					//		SyntaxFactory.SingletonSeparatedList(
					//			SyntaxFactory.Attribute(SyntaxFactory.ParseName("ref"))
					//		)
					//	)
					//};
				}
			

				return SyntaxFactory.Parameter(
					SyntaxFactory.List(newAttributes),
					modifiers,
					returnType,
					id,
					@default
				);
			}
			#endregion

			#region Type / Modifiers
			public override CSharpSyntaxNode VisitPredefinedType(CVBS.PredefinedTypeSyntax node)
			{
				return SyntaxFactory.PredefinedType(SyntaxFactory.Token(ConvertToken(CVB.VisualBasicExtensions.Kind(node.Keyword))));
			}

			public override CSharpSyntaxNode VisitNullableType(CVBS.NullableTypeSyntax node)
			{
				return SyntaxFactory.NullableType((TypeSyntax)node.ElementType.Accept(this));
			}

			public override CSharpSyntaxNode VisitOmittedArgument(CVBS.OmittedArgumentSyntax node)
			{
				return SyntaxFactory.ParseTypeName("");
			}

			public override CSharpSyntaxNode VisitSimpleAsClause(CVBS.SimpleAsClauseSyntax node)
			{
				return node.Type.Accept(this);
			}

			public override CSharpSyntaxNode VisitTypeParameterList(CVBS.TypeParameterListSyntax node)
			{
				return SyntaxFactory.TypeParameterList(SyntaxFactory.SeparatedList(node.Parameters.Select(p => (TypeParameterSyntax)p.Accept(this))));
			}

			public override CSharpSyntaxNode VisitTypeParameter(CVBS.TypeParameterSyntax node)
			{
				SyntaxToken variance = default(SyntaxToken);
				var attributes = SyntaxFactory.List<AttributeListSyntax>();
				if (!node.VarianceKeyword.IsKind(CVB.SyntaxKind.None))
				{
					variance = SyntaxFactory.Token(node.VarianceKeyword.IsKind(CVB.SyntaxKind.InKeyword) ? SyntaxKind.InKeyword : SyntaxKind.OutKeyword);
				}
				
				return SyntaxFactory.TypeParameter(attributes, variance, ConvertIdentifier(node.Identifier));
			}

			public override CSharpSyntaxNode VisitTypeParameterSingleConstraintClause(CVBS.TypeParameterSingleConstraintClauseSyntax node)
			{
				var typeParameter = node.Parent as CVBS.TypeParameterSyntax;

				return SyntaxFactory.TypeParameterConstraintClause(SyntaxFactory.IdentifierName(ConvertIdentifier(typeParameter.Identifier)), SyntaxFactory.SeparatedList(new[] { (TypeParameterConstraintSyntax)node.Constraint.Accept(this) }));
			}

			public override CSharpSyntaxNode VisitTypeParameterMultipleConstraintClause(CVBS.TypeParameterMultipleConstraintClauseSyntax node)
			{
				var typeParameter = node.Parent as CVBS.TypeParameterSyntax;

				return SyntaxFactory.TypeParameterConstraintClause(SyntaxFactory.IdentifierName(ConvertIdentifier(typeParameter.Identifier)), SyntaxFactory.SeparatedList(node.Constraints.Select(c => (TypeParameterConstraintSyntax)c.Accept(this))));				
			}

			public override CSharpSyntaxNode VisitSpecialConstraint(CVBS.SpecialConstraintSyntax node)
			{
				if (node.IsKind(CVB.SyntaxKind.ClassConstraint))
					return SyntaxFactory.ClassOrStructConstraint(SyntaxKind.ClassConstraint, SyntaxFactory.Token(SyntaxKind.ClassKeyword));					
				if (node.IsKind(CVB.SyntaxKind.StructureConstraint))
					return SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint, SyntaxFactory.Token(SyntaxKind.StructKeyword));
				if (node.IsKind(CVB.SyntaxKind.NewConstraint))
					return SyntaxFactory.ConstructorConstraint();

				throw new NotSupportedException(String.Format("Constraint {0} is not supported!", node.Kind()));
			}
			#endregion

			#region NameSyntax

			SyntaxToken ConvertIdentifier(SyntaxToken id)
			{
				var keywordKind = SyntaxFacts.GetKeywordKind(id.ValueText);
				if (keywordKind != SyntaxKind.None && !SyntaxFacts.IsPredefinedType(keywordKind))
					return SyntaxFactory.Identifier("[" + id.ValueText + "]");
				return SyntaxFactory.Identifier(id.ValueText);
			}

			public override CSharpSyntaxNode VisitIdentifierName(CVBS.IdentifierNameSyntax node)
			{
				return WrapTypedNameIfNecessary(SyntaxFactory.IdentifierName(ConvertIdentifier(node.Identifier)), node);
			}

			public override CSharpSyntaxNode VisitGenericName(CVBS.GenericNameSyntax node)
			{
				return WrapTypedNameIfNecessary(SyntaxFactory.GenericName(ConvertIdentifier(node.Identifier), (TypeArgumentListSyntax)node.TypeArgumentList.Accept(this)), node);
			}

			public override CSharpSyntaxNode VisitQualifiedName(CVBS.QualifiedNameSyntax node)
			{
				return WrapTypedNameIfNecessary(SyntaxFactory.QualifiedName((NameSyntax)node.Left.Accept(this), (SimpleNameSyntax)node.Right.Accept(this)), node);
			}

			public override CSharpSyntaxNode VisitTypeArgumentList(CVBS.TypeArgumentListSyntax node)
			{
				return SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(node.Arguments.Select(a => (TypeSyntax)a.Accept(this))));
			}

			CSharpSyntaxNode WrapTypedNameIfNecessary(NameSyntax name, CVBS.NameSyntax originalName)
			{
				if (originalName.Parent is CVBS.NameSyntax || originalName.Parent is CVBS.AttributeSyntax)
					return name;

				CVBS.ExpressionSyntax parent = originalName;
				while (parent.Parent is CVBS.MemberAccessExpressionSyntax)
					parent = (CVBS.ExpressionSyntax)parent.Parent;

				if (parent != null && parent.Parent is CVBS.InvocationExpressionSyntax)
					return name;

				var symbol = semanticModel.GetSymbolInfo(originalName).Symbol;
				if (symbol.IsKind(SymbolKind.Method))
					return SyntaxFactory.PointerType(name);

				return name;
			}
			#endregion

			#region Expressions

			public override CSharpSyntaxNode VisitLiteralExpression(CVBS.LiteralExpressionSyntax node)
			{
				// now this looks somehow hacky... is there a better way?
				if (node.IsKind(CVB.SyntaxKind.StringLiteralExpression) && node.Token.Text.StartsWith("@", StringComparison.Ordinal))
				{
					return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralToken,
						SyntaxFactory.Literal(
							node.Token.Text.Substring(1),
							(string)node.Token.Value
						)
					);
				}
				else
				{
					// support for val = default(T3) instead of "val = null"
					//if (node.IsKind(CVB.SyntaxKind.NothingLiteralExpression))
					//{
					//	var assignment = node.Parent as CVBS.AssignmentStatementSyntax;

					//	if (assignment != null)
					//	{
					//		// get type of variable
					//		var info = semanticModel.GetSymbolInfo(assignment.Left);
					//		var type = (info.Symbol as Microsoft.CodeAnalysis.VisualBasic.Symbols.SourceParameterSymbol)?.Type


					//	}
					//}

					return Literal(node.Token.Value);
				}
			}

			public override CSharpSyntaxNode VisitObjectCreationExpression(CVBS.ObjectCreationExpressionSyntax node)
			{
				return SyntaxFactory.ObjectCreationExpression(
					SyntaxFactory.Token(SyntaxKind.NewKeyword),
					(TypeSyntax)node.Type.Accept(this),
					(ArgumentListSyntax)node.ArgumentList?.Accept(this),
					(InitializerExpressionSyntax)node.Initializer?.Accept(this)
				);
			}

			public override CSharpSyntaxNode VisitArgumentList(CVBS.ArgumentListSyntax node)
			{
				return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(node.Arguments.Select(a => (ArgumentSyntax)a.Accept(this))));
			}

			public override CSharpSyntaxNode VisitAssignmentStatement(CVBS.AssignmentStatementSyntax node)
			{
				return MakeAssignmentStatement(node);
				//if (node.Parent is CVBS.ExpressionStatementSyntax)
				//{
				//	if (semanticModel.GetTypeInfo(node.Right).ConvertedType.IsDelegateType())
				//	{
				//		if (node.OperatorToken.IsKind(CVB.SyntaxKind.PlusEqualsToken))
				//		{
				//			return SyntaxFactory.AddHandlerStatement((ExpressionSyntax)node.Left.Accept(this), (ExpressionSyntax)node.Right.Accept(this));
				//		}
				//		if (node.OperatorToken.IsKind(CVB.SyntaxKind.MinusEqualsToken))
				//		{
				//			return SyntaxFactory.RemoveHandlerStatement((ExpressionSyntax)node.Left.Accept(this), (ExpressionSyntax)node.Right.Accept(this));
				//		}
				//	}
				//	return MakeAssignmentStatement(node);
				//}

				//if (node.Parent is CVBS.ForStatementSyntax)
				//{
				//	return MakeAssignmentStatement(node);
				//}

				//if (node.Parent is CVBS.InitializerExpressionSyntax)
				//{
				//	if (node.Left is CVBS.ImplicitElementAccessSyntax)
				//	{
				//		return SyntaxFactory.CollectionInitializer(
				//			SyntaxFactory.SeparatedList(new[] {
				//				(ExpressionSyntax)node.Left.Accept(this),
				//				(ExpressionSyntax)node.Right.Accept(this)
				//			})
				//		);
				//	}
				//	else
				//	{
				//		return SyntaxFactory.NamedFieldInitializer(
				//			(IdentifierNameSyntax)node.Left.Accept(this),
				//			(ExpressionSyntax)node.Right.Accept(this)
				//		);
				//	}
				//}

				//MarkPatchInlineAssignHelper(node);
				//return SyntaxFactory.InvocationExpression(
				//	SyntaxFactory.IdentifierName("__InlineAssignHelper"),
				//	SyntaxFactory.ArgumentList(
				//		SyntaxFactory.SeparatedList(
				//			new ArgumentSyntax[] {
				//				SyntaxFactory.SimpleArgument((ExpressionSyntax)node.Left.Accept(this)),
				//				SyntaxFactory.SimpleArgument((ExpressionSyntax)node.Right.Accept(this))
				//			}
				//		)
				//	)
				//);				
			}

			AssignmentExpressionSyntax MakeAssignmentStatement(CVBS.AssignmentStatementSyntax node)
			{
				var kind = ConvertToken(CVB.VisualBasicExtensions.Kind(node)); //TokenContext.Local
				
				return SyntaxFactory.AssignmentExpression(
					kind,
					(ExpressionSyntax)node.Left.Accept(this),
					SyntaxFactory.Token(CSharpUtil.GetExpressionOperatorTokenKind(kind)),
					(ExpressionSyntax)node.Right.Accept(this)
				);
			}
			#endregion
		}
	}
}
