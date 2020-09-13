using System.Collections.Generic;
using MonC.Parsing.Scoping;
using MonC.SyntaxTree;
using MonC.SyntaxTree.Leaves;
using MonC.SyntaxTree.Leaves.Statements;
using MonC.SyntaxTree.Util.ChildrenVisitors;

namespace MonC.Parsing.Semantics
{
    public interface IScopeHandler
    {
        Scope CurrentScope { set; }
    }

    public class WalkScopeVisitor : IStatementVisitor
    {
        private readonly IScopeHandler _scopeHandler;
        private readonly IStatementVisitor _statementVisitor;
        private readonly Stack<Scope> _scopes = new Stack<Scope>();
        private readonly ExpressionChildrenVisitor _expressionChildrenVisitor;

        public WalkScopeVisitor(
                IScopeHandler scopeHandler,
                IStatementVisitor statementVisitor,
                IExpressionVisitor expressionVisitor,
                Scope initialScope)
        {
            _scopeHandler = scopeHandler;
            _statementVisitor = statementVisitor;
            _scopes.Push(initialScope);

            // Scope can't change inside of expressions, so just use a children visitor for expressions.
            _expressionChildrenVisitor= new ExpressionChildrenVisitor(expressionVisitor);
        }

        public void VisitDeclaration(DeclarationLeaf leaf)
        {
            VisitStatement(leaf);
            VisitExpression(leaf.Assignment);
            Scope scope = _scopes.Peek();
            scope.Variables.Add(leaf);
        }

        public void VisitBreak(BreakLeaf leaf)
        {
            VisitStatement(leaf);
        }

        public void VisitContinue(ContinueLeaf leaf)
        {
            VisitStatement(leaf);
        }

        public void VisitReturn(ReturnLeaf leaf)
        {
            VisitStatement(leaf);
            VisitExpression(leaf.RHS);
        }

        public void VisitIfElse(IfElseLeaf leaf)
        {
            VisitStatement(leaf);

            VisitExpression(leaf.Condition);
            Scope baseScope = _scopes.Peek();
            _scopes.Push(baseScope.Copy());
            VisitBody(leaf.IfBody);
            _scopes.Pop();
            _scopes.Push(baseScope.Copy());
            VisitBody(leaf.ElseBody);
            _scopes.Pop();
        }

        public void VisitFor(ForLeaf leaf)
        {
            VisitStatement(leaf);

            Scope baseScope = _scopes.Peek();
            _scopes.Push(baseScope.Copy());
            VisitDeclaration(leaf.Declaration);
            VisitExpression(leaf.Condition);
            VisitExpression(leaf.Update);
            VisitBody(leaf.Body);
            _scopes.Pop();
        }

        public void VisitWhile(WhileLeaf leaf)
        {
            VisitStatement(leaf);

            VisitExpression(leaf.Condition);
            _scopes.Push(_scopes.Peek().Copy());
            VisitBody(leaf.Body);
            _scopes.Pop();
        }

        public void VisitExpressionStatement(ExpressionStatementLeaf leaf)
        {
            VisitStatement(leaf);
            VisitExpression(leaf.Expression);
        }

        private void VisitExpression(IExpressionLeaf leaf)
        {
            _scopeHandler.CurrentScope = _scopes.Peek();
            leaf.AcceptExpressionVisitor(_expressionChildrenVisitor);
        }

        private void VisitBody(Body body)
        {
            for (int i = 0, ilen = body.Length; i < ilen; ++i) {
                body.GetStatement(i).AcceptStatementVisitor(this);
            }
        }

        private void VisitStatement(IStatementLeaf leaf)
        {
            Scope scope = _scopes.Peek();
            _scopeHandler.CurrentScope = scope;
            leaf.AcceptStatementVisitor(_statementVisitor);
        }
    }
}