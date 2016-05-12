using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
		class MethodBodyVisitor : CVB.VisualBasicSyntaxVisitor<SyntaxList<StatementSyntax>>
		{
			SemanticModel semanticModel;
			NodesVisitor nodesVisitor;

			public bool IsInterator { get; private set; }

			public MethodBodyVisitor(SemanticModel semanticModel, NodesVisitor nodesVisitor)
			{
				this.semanticModel = semanticModel;
				this.nodesVisitor = nodesVisitor;
			}

			public override SyntaxList<StatementSyntax> DefaultVisit(SyntaxNode node)
			{
				throw new NotImplementedException(node.GetType() + " not implemented!");
			}

			public override SyntaxList<StatementSyntax> VisitYieldStatement(CVBS.YieldStatementSyntax node)
			{
				IsInterator = true;
				StatementSyntax stmt;
				if (node.Expression == null)
					stmt = SyntaxFactory.ReturnStatement();
				else
					stmt = SyntaxFactory.YieldStatement(SyntaxKind.YieldKeyword, (ExpressionSyntax)node.Expression.Accept(nodesVisitor));
				return SyntaxFactory.SingletonList(stmt);
			}

			public override SyntaxList<StatementSyntax> VisitInvocationExpression(CVBS.InvocationExpressionSyntax node)
			{
				return base.VisitInvocationExpression(node);
			}	

			StatementSyntax ConvertSingleExpression(CVBS.ExpressionSyntax node)
			{
				var exprNode = node.Accept(nodesVisitor);
				if (!(exprNode is StatementSyntax))
					exprNode = SyntaxFactory.ExpressionStatement((ExpressionSyntax)exprNode);

				return (StatementSyntax)exprNode;
			}

			public override SyntaxList<StatementSyntax> VisitExpressionStatement(CVBS.ExpressionStatementSyntax node)
			{
				return SyntaxFactory.SingletonList(ConvertSingleExpression(node.Expression));
			}

			public override SyntaxList<StatementSyntax> VisitAssignmentStatement(CVBS.AssignmentStatementSyntax node)
			{
				var statement = SyntaxFactory.ExpressionStatement((ExpressionSyntax)node.Accept(nodesVisitor));
				return SyntaxFactory.SingletonList((StatementSyntax)statement);
			}
		}
	}
}
