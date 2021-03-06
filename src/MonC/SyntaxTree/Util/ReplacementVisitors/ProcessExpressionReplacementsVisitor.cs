using MonC.SyntaxTree.Nodes;
using MonC.SyntaxTree.Nodes.Expressions;
using MonC.SyntaxTree.Nodes.Expressions.UnaryOperations;

namespace MonC.SyntaxTree.Util.ReplacementVisitors
{
    public class ProcessExpressionReplacementsVisitor : IExpressionVisitor, IUnaryOperationVisitor, IBasicExpressionVisitor
    {
        private readonly ReplacementProcessor _processor;
        public IVisitor<IExpressionNode>? ExtensionVisitor;

        public ProcessExpressionReplacementsVisitor(IReplacementSource replacementSource, IReplacementListener listener)
        {
            _processor = new ReplacementProcessor(replacementSource, listener);
        }

        public void VisitVoid(VoidExpressionNode node)
        {
        }

        public void VisitNumericLiteral(NumericLiteralNode node)
        {
        }

        public void VisitStringLiteral(StringLiteralNode node)
        {
        }

        public void VisitEnumValue(EnumValueNode node)
        {
        }

        public void VisitVariable(VariableNode node)
        {
        }

        public void VisitBasicExpression(IBasicExpression node)
        {
            node.AcceptBasicExpressionVisitor(this);
        }

        public void VisitUnaryOperation(IUnaryOperationNode node)
        {
            node.AcceptUnaryOperationVisitor(this);
            node.RHS = _processor.ProcessReplacement(node.RHS);
        }

        public void VisitNegateUnaryOp(NegateUnaryOpNode node)
        {
        }

        public void VisitLogicalNotUnaryOp(LogicalNotUnaryOpNode node)
        {
        }

        public void VisitCastUnaryOp(CastUnaryOpNode node)
        {
            node.ToType = _processor.ProcessReplacement(node.ToType);
        }

        public void VisitBorrowUnaryOp(BorrowUnaryOpNode node)
        {
        }

        public void VisitDereferenceUnaryOp(DereferenceUnaryOpNode node)
        {
        }

        public void VisitBinaryOperation(IBinaryOperationNode node)
        {
            // TODO: Should this be optional to allow more flexibility with a IBinaryOperationVisitor?
            node.LHS = _processor.ProcessReplacement(node.LHS);
            node.RHS = _processor.ProcessReplacement(node.RHS);
        }

        public void VisitFunctionCall(FunctionCallNode node)
        {
            for (int i = 0, ilen = node.Arguments.Count; i < ilen; ++i) {
                node.Arguments[i] = _processor.ProcessReplacement(node.Arguments[i]);
            }
        }

        public void VisitAssignment(AssignmentNode node)
        {
            node.Rhs = _processor.ProcessReplacement(node.Rhs);
        }

        public void VisitAccess(AccessNode node)
        {
            node.Lhs = _processor.ProcessReplacement(node.Lhs);
            node.Rhs = _processor.ProcessReplacement(node.Rhs);
        }

        public void VisitUnknown(IExpressionNode node)
        {
            ExtensionVisitor?.Visit(node);
        }
    }
}
