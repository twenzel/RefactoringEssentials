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
			#endregion
		}
	}
}
