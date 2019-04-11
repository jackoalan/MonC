using System.Collections.Generic;
using System.Linq;

namespace MonC.SyntaxTree
{
    public class FunctionCallLeaf : IASTLeaf
    {
        public readonly IASTLeaf LHS;
        private readonly IASTLeaf[] _arguments;

        public int ArgumentCount => _arguments.Length;

        public FunctionCallLeaf(IASTLeaf lhs, IEnumerable<IASTLeaf> arguments)
        {
            LHS = lhs;
            _arguments = arguments.ToArray();
        }
        
        public void Accept(IASTLeafVisitor visitor)
        {
            visitor.VisitFunctionCall(this);
        }

        public IASTLeaf GetArgument(int index)
        {
            return _arguments[index];
        }
    }
}