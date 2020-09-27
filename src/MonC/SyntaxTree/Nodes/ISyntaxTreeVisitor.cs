namespace MonC.SyntaxTree.Nodes
{
    public interface ISyntaxTreeVisitor
    {
        void VisitTopLevelStatement(ITopLevelStatementNode node);
        void VisitStatement(IStatementNode node);
        void VisitExpression(IExpressionNode node);
        void VisitSpecifier(ISpecifierNode node);
    }
}