using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using CVB = Microsoft.CodeAnalysis.VisualBasic;
using CVBS = Microsoft.CodeAnalysis.VisualBasic.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RefactoringEssentials.CSharp.CodeRefactorings;
using RefactoringEssentials.Converter;
using Microsoft.CodeAnalysis.Text;
using System.Diagnostics.Contracts;

namespace RefactoringEssentials.CSharp.Converter
{
	public partial class VisualBasicConverter
	{
		enum TokenContext
		{
			Global,
			InterfaceOrModule,
			Member,
			VariableOrConst,
			Local
		}

		public static CSharpSyntaxNode Convert(CVB.VisualBasicSyntaxNode input, SemanticModel semanticModel, Document targetDocument)
		{
			return input.Accept(new NodesVisitor(semanticModel, targetDocument));
		}

		static SyntaxTokenList ConvertModifiers(SyntaxTokenList modifiers, TokenContext context = TokenContext.Global)
		{
			return SyntaxFactory.TokenList(ConvertModifiersCore(modifiers, context));
		}

		static IEnumerable<SyntaxToken> ConvertModifiersCore(IEnumerable<SyntaxToken> modifiers, TokenContext context)
		{
			if (context != TokenContext.Local && context != TokenContext.InterfaceOrModule)
			{
				bool visibility = false;
				foreach (var token in modifiers)
				{
					if (IsVisibility(token, context))
					{
						visibility = true;
						break;
					}
				}
				if (!visibility && context == TokenContext.Member)
					yield return CSharpDefaultVisibility(context);
			}
			foreach (var token in modifiers.Where(m => !IgnoreInContext(m, context)))
			{
				var m = ConvertModifier(token); //context
				if (m.HasValue) yield return m.Value;
			}
		}

		static bool IsVisibility(SyntaxToken token, TokenContext context)
		{
			return token.IsKind(CVB.SyntaxKind.PublicKeyword, CVB.SyntaxKind.FriendKeyword, CVB.SyntaxKind.ProtectedKeyword, CVB.SyntaxKind.PrivateKeyword)
				|| (context == TokenContext.VariableOrConst && token.IsKind(CVB.SyntaxKind.ConstKeyword));
		}

		static SyntaxToken CSharpDefaultVisibility(TokenContext context)
		{
			switch (context)
			{
				case TokenContext.Global:
					return SyntaxFactory.Token(SyntaxKind.InternalKeyword);
				case TokenContext.Local:
				case TokenContext.VariableOrConst:
				case TokenContext.Member:
					return SyntaxFactory.Token(SyntaxKind.PrivateKeyword);
			}
			throw new ArgumentOutOfRangeException(nameof(context));
		}

		static bool IgnoreInContext(SyntaxToken m, TokenContext context)
		{
			switch (context)
			{
				case TokenContext.InterfaceOrModule:
					return m.IsKind(CVB.SyntaxKind.PublicKeyword, CVB.SyntaxKind.SharedKeyword);
			}
			return false;
		}

		static SyntaxToken? ConvertModifier(SyntaxToken m) //TokenContext context = TokenContext.Global
		{
			var token = ConvertToken(CVB.VisualBasicExtensions.Kind(m)); //context
			return token == SyntaxKind.None ? null : new SyntaxToken?(SyntaxFactory.Token(token));
		}

		static SyntaxKind ConvertToken(CVB.SyntaxKind t) //TokenContext context = TokenContext.Global
		{
			switch (t)
			{
				case CVB.SyntaxKind.None:
					return SyntaxKind.VoidKeyword;
				// built-in types
				case CVB.SyntaxKind.BooleanKeyword:
					return SyntaxKind.BoolKeyword;
				case CVB.SyntaxKind.ByteKeyword:
					return SyntaxKind.ByteKeyword;
				case CVB.SyntaxKind.SByteKeyword:
					return SyntaxKind.SByteKeyword;
				case CVB.SyntaxKind.ShortKeyword:
					return SyntaxKind.ShortKeyword;
				case CVB.SyntaxKind.UShortKeyword:
					return SyntaxKind.UShortKeyword;
				case CVB.SyntaxKind.IntegerKeyword:
					return SyntaxKind.IntKeyword;
				case CVB.SyntaxKind.UIntegerKeyword:
					return SyntaxKind.UIntKeyword;
				case CVB.SyntaxKind.LongKeyword:
					return SyntaxKind.LongKeyword;
				case CVB.SyntaxKind.ULongKeyword:
					return SyntaxKind.ULongKeyword;
				case CVB.SyntaxKind.DoubleKeyword:
					return SyntaxKind.DoubleKeyword;
				case CVB.SyntaxKind.SingleKeyword:
					return SyntaxKind.FloatKeyword;
				case CVB.SyntaxKind.DecimalKeyword:
					return SyntaxKind.DecimalKeyword;
				case CVB.SyntaxKind.StringKeyword:
					return SyntaxKind.StringKeyword;
				case CVB.SyntaxKind.CharKeyword:
					return SyntaxKind.CharKeyword;
				case CVB.SyntaxKind.ObjectKeyword:
					return SyntaxKind.ObjectKeyword;
				// literals
				case CVB.SyntaxKind.NothingKeyword:
					return SyntaxKind.NullKeyword;
				case CVB.SyntaxKind.TrueKeyword:
					return SyntaxKind.TrueKeyword;
				case CVB.SyntaxKind.FalseKeyword:
					return SyntaxKind.FalseKeyword;
				case CVB.SyntaxKind.MeKeyword:
					return SyntaxKind.ThisKeyword;
				case CVB.SyntaxKind.MyBaseKeyword:
					return SyntaxKind.BaseKeyword;
				// modifiers
				case CVB.SyntaxKind.PublicKeyword:
					return SyntaxKind.PublicKeyword;
				case CVB.SyntaxKind.PrivateKeyword:
					return SyntaxKind.PrivateKeyword;
				case CVB.SyntaxKind.FriendKeyword:
					return SyntaxKind.InternalKeyword;
				case CVB.SyntaxKind.ProtectedKeyword:
					return SyntaxKind.ProtectedKeyword;
				case CVB.SyntaxKind.SharedKeyword:
					return SyntaxKind.StaticKeyword;
				case CVB.SyntaxKind.ReadOnlyKeyword:
					return SyntaxKind.ReadOnlyKeyword;
				case CVB.SyntaxKind.NotInheritableKeyword:
					return SyntaxKind.SealedKeyword;
				case CVB.SyntaxKind.NotOverridableKeyword:
					return SyntaxKind.SealedKeyword;
				case CVB.SyntaxKind.ConstKeyword:
					return SyntaxKind.ConstKeyword;
				case CVB.SyntaxKind.OverridesKeyword:
					return SyntaxKind.OverrideKeyword;
				case CVB.SyntaxKind.MustInheritKeyword:
					return SyntaxKind.AbstractKeyword;
				case CVB.SyntaxKind.MustOverrideKeyword:
					return SyntaxKind.AbstractKeyword;				
				case CVB.SyntaxKind.OverridableKeyword:
					return SyntaxKind.VirtualKeyword;
				case CVB.SyntaxKind.ByRefKeyword:
					return SyntaxKind.RefKeyword;				
				case CVB.SyntaxKind.PartialKeyword:
					return SyntaxKind.PartialKeyword;
				case CVB.SyntaxKind.AsyncKeyword:
					return SyntaxKind.AsyncKeyword;
				case CVB.SyntaxKind.ShadowsKeyword:
					return SyntaxKind.NewKeyword;
				case CVB.SyntaxKind.ParamArrayKeyword:
					return SyntaxKind.ParamsKeyword;
				// others
				case CVB.SyntaxKind.AscendingKeyword:
					return SyntaxKind.AscendingKeyword;
				case CVB.SyntaxKind.DescendingKeyword:
					return SyntaxKind.DescendingKeyword;
				case CVB.SyntaxKind.AwaitKeyword:
					return SyntaxKind.AwaitKeyword;
				// expressions
				case CVB.SyntaxKind.AddExpression:
					return SyntaxKind.AddExpression;
				case CVB.SyntaxKind.SubtractExpression:
					return SyntaxKind.SubtractExpression;
				case CVB.SyntaxKind.MultiplyExpression:
					return SyntaxKind.MultiplyExpression;
				case CVB.SyntaxKind.DivideExpression:
					return SyntaxKind.DivideExpression;
				case CVB.SyntaxKind.ModuloExpression:
					return SyntaxKind.ModuloExpression;
				case CVB.SyntaxKind.LeftShiftExpression:
					return SyntaxKind.LeftShiftExpression;
				case CVB.SyntaxKind.RightShiftExpression:
					return SyntaxKind.RightShiftExpression;
				case CVB.SyntaxKind.OrElseExpression:
					return SyntaxKind.LogicalOrExpression;
				case CVB.SyntaxKind.AndAlsoExpression:
					return SyntaxKind.LogicalAndExpression;
				case CVB.SyntaxKind.OrExpression:
					return SyntaxKind.BitwiseOrExpression;
				case CVB.SyntaxKind.AndExpression:
					return SyntaxKind.BitwiseAndExpression;
				case CVB.SyntaxKind.ExclusiveOrExpression:
					return SyntaxKind.ExclusiveOrExpression;
				case CVB.SyntaxKind.EqualsExpression:
					return SyntaxKind.EqualsExpression;
				case CVB.SyntaxKind.NotEqualsExpression:
					return SyntaxKind.NotEqualsExpression;
				case CVB.SyntaxKind.LessThanExpression:
					return SyntaxKind.LessThanExpression;
				case CVB.SyntaxKind.LessThanOrEqualExpression:
					return SyntaxKind.LessThanOrEqualExpression;
				case CVB.SyntaxKind.GreaterThanExpression:
					return SyntaxKind.GreaterThanExpression;
				case CVB.SyntaxKind.GreaterThanOrEqualExpression:
					return SyntaxKind.GreaterThanOrEqualExpression;
				case CVB.SyntaxKind.SimpleAssignmentStatement:
					return SyntaxKind.SimpleAssignmentExpression;
				case CVB.SyntaxKind.AddAssignmentStatement:
					return SyntaxKind.AddAssignmentExpression;
				case CVB.SyntaxKind.SubtractAssignmentStatement:
					return SyntaxKind.SubtractAssignmentExpression;
				case CVB.SyntaxKind.MultiplyAssignmentStatement:
					return SyntaxKind.MultiplyAssignmentExpression;
				case CVB.SyntaxKind.DivideAssignmentStatement:
					return SyntaxKind.DivideAssignmentExpression;
				//case CVB.SyntaxKind.ModuloAssignmentExpression:
				//	return SyntaxKind.ModuloExpression;
				case CVB.SyntaxKind.PlusEqualsToken:
					return SyntaxKind.AndAssignmentExpression;
				//case CVB.SyntaxKind.ExclusiveOrAssignmentExpression:
				//	return SyntaxKind.ExclusiveOrExpression;
				//case CVB.SyntaxKind.OrExpression:
				//	return SyntaxKind.OrAssignmentExpression;				
				case CVB.SyntaxKind.UnaryPlusExpression:
					return SyntaxKind.UnaryPlusExpression;
				case CVB.SyntaxKind.UnaryMinusExpression:
					return SyntaxKind.UnaryMinusExpression;
				case CVB.SyntaxKind.NotExpression:
					return SyntaxKind.BitwiseNotExpression;
				//case CS.SyntaxKind.LogicalNotExpression:
				//	return SyntaxKind.NotExpression;
				//case CVB.SyntaxKind.AddAssignmentStatement:
				//	return SyntaxKind.PreIncrementExpression;
				//case CS.SyntaxKind.PreDecrementExpression:
				//	return SyntaxKind.SubtractAssignmentStatement;
				//case CS.SyntaxKind.PostIncrementExpression:
				//	return SyntaxKind.AddAssignmentStatement;
				//case CS.SyntaxKind.PostDecrementExpression:
				//	return SyntaxKind.SubtractAssignmentStatement;
				//case CS.SyntaxKind.PlusPlusToken:
				//	return SyntaxKind.PlusToken;
				//case CS.SyntaxKind.MinusMinusToken:
				//	return SyntaxKind.MinusToken;
				case CVB.SyntaxKind.ByValKeyword:
					return SyntaxKind.None;
				case CVB.SyntaxKind.DimKeyword:
					return SyntaxKind.None;
			}

			throw new NotSupportedException(t + " is not supported!");
		}

		static VariableDeclarationSyntax RemodelVariableDeclaration(CVBS.VariableDeclaratorSyntax declaration, NodesVisitor nodesVisitor)
		{

			// SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.v)),
			TypeSyntax type = declaration.AsClause == null ? SyntaxFactory.IdentifierName("var") : GetType(declaration.AsClause, nodesVisitor);

			if (declaration.Names.Count > 1)
				throw new NotSupportedException("Multiple names are not supported");

			EqualsValueClauseSyntax initializer = null;

			if (declaration.Initializer != null)
				initializer = SyntaxFactory.EqualsValueClause((ExpressionSyntax)declaration.Initializer.Value.Accept(nodesVisitor));
			else if (declaration.AsClause != null)
			{
				CVBS.AsNewClauseSyntax newClause = declaration.AsClause as CVBS.AsNewClauseSyntax;
				if (newClause != null)
					initializer = SyntaxFactory.EqualsValueClause((ExpressionSyntax)newClause.NewExpression.Accept(nodesVisitor));
			}

			var declarator = SyntaxFactory.VariableDeclarator(
				SyntaxFactory.Identifier(declaration.Names[0].Identifier.ValueText),
				null,
				initializer
			);			

			return SyntaxFactory.VariableDeclaration(
				type: type,
				variables: SyntaxFactory.SeparatedList(new[] { declarator })
				);    			
		}

		static TypeSyntax GetType(CVBS.AsClauseSyntax asClause, NodesVisitor nodesVisitor)
		{
			CVBS.SimpleAsClauseSyntax simpleClause = asClause as CVBS.SimpleAsClauseSyntax;

			if (simpleClause != null)
				return (TypeSyntax)simpleClause.Type.Accept(nodesVisitor);
			else
			{
				CVBS.AsNewClauseSyntax newClause = asClause as CVBS.AsNewClauseSyntax;

				if (newClause != null)
					return SyntaxFactory.IdentifierName("var");
				else
					return SyntaxFactory.ParseTypeName(asClause.ToString());
			}
		}

		static IdentifierNameSyntax ExtractIdentifier(CVBS.VariableDeclaratorSyntax v)
		{
			if (v.Names.Count > 0)
				throw new NotSupportedException("Multiple names are not supported");

			return SyntaxFactory.IdentifierName(ConvertIdentifier(v.Names[0].Identifier));
		}

		static SyntaxToken ConvertIdentifier(SyntaxToken t)
		{
			var text = t.ValueText;
			if (SyntaxFacts.IsKeywordKind(t.Kind()))
				text = "@" + text;

			return SyntaxFactory.Identifier(text);
		}

		static ExpressionSyntax Literal(object o)
		{
			return ComputeConstantValueCodeRefactoringProvider.GetLiteralExpression(o);
		}
	}
}
